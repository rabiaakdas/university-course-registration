using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

// Day 36: deliberately unsafe algorithms, only in the isolated DB runner.
// Lab occupancy = Submitted/Approved; Draft/Rejected do not occupy a seat here.
// This is NOT EnrollmentOperations or a new production capacity policy.
internal static class ConcurrencyLabChecks
{
    static void Check(bool ok, string label)
    { if (!ok) throw new BehaviorFailure("Concurrency lab: " + label); }

    sealed record Observation(int SessionId, int ReadValue, bool Wrote);

    sealed class BothRead
    {
        readonly TaskCompletionSource<bool> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals;
        public async Task Meet(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult(true);
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
    }

    // Each worker owns its context/connection. Any failure cancels its peer.
    // Await both workers before cleanup; no detached task can write after cleanup.
    static async Task<Observation[]> Pair(Func<int, BothRead, CancellationToken, Task<Observation>> work)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var gate = new BothRead();
        async Task<Observation> Guard(int index)
        {
            try { return await work(index, gate, stop.Token); }
            catch { stop.Cancel(); throw; }
        }
        var first = Guard(0);
        var second = Guard(1);
        return await Task.WhenAll(first, second);
    }

    static Task<int> ActiveCount(UniversityDbContext db, int offeringId, CancellationToken ct) =>
        db.Enrollments.CountAsync(e => e.CourseOfferingId == offeringId &&
            (e.Status == EnrollmentStatus.Submitted || e.Status == EnrollmentStatus.Approved), ct);

    static async Task<Observation> TrySeat(Func<UniversityDbContext> factory, int offeringId,
        int studentId, BothRead? gate, CancellationToken ct)
    {
        await using var db = factory();
        db.Database.SetCommandTimeout(10);
        await db.Database.OpenConnectionAsync(ct); // Held open across the rendezvous.
        // SQL Server @@SPID returns smallint (Int16); SqlQueryRaw<int> requires SQL int.
        var session = await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").SingleAsync(ct);
        var capacity = await db.CourseOfferings.Where(o => o.Id == offeringId).Select(o => o.Capacity).SingleAsync(ct);
        var occupied = await ActiveCount(db, offeringId, ct);
        if (gate is not null) await gate.Meet(ct);
        if (occupied >= capacity) return new(session, occupied, false);

        // Deliberate check-then-insert gap: no coordinating transaction/lock/recheck.
        var enrollment = new Enrollment { StudentId = studentId, CourseOfferingId = offeringId, CreatedAtUtc = DateTimeOffset.UtcNow };
        Check(enrollment.TrySubmit(), "fixture transition to Submitted");
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);
        return new(session, occupied, true);
    }

    public static async Task Run(Func<UniversityDbContext> factory, bool probe = false)
    {
        // Defense in depth: never allow a caller to point this lab at app/lab.
        await using (var source = factory())
        {
            var target = new SqlConnectionStringBuilder(source.Database.GetConnectionString());
            Check(target.DataSource == @"(localdb)\MSSQLLocalDB" &&
                System.Text.RegularExpressions.Regex.IsMatch(target.InitialCatalog,
                    @"^university_registration_tests_day6_[a-f0-9]{32}$"), "isolated test target required");
        }
        var tag = "C36_" + Guid.NewGuid().ToString("N")[..12];
        var course = new Course { Code = tag, Name = "Synthetic concurrency course", Credits = 1 };
        var term = new AcademicTerm { Name = tag, StartsOn = new(2026, 1, 1), EndsOn = new(2026, 6, 1) };
        var students = new[] {
            new Student { StudentNumber = tag + "A", FullName = "Synthetic candidate A" },
            new Student { StudentNumber = tag + "B", FullName = "Synthetic candidate B" }
        };
        var offerings = new List<CourseOffering>();
        // Only this generated/validated identifier is composed into SQL; identifiers cannot be parameters.
        var counterName = "ConcurrencyCounter_" + Guid.NewGuid().ToString("N");
        Check(System.Text.RegularExpressions.Regex.IsMatch(counterName, @"\AConcurrencyCounter_[a-f0-9]{32}\z"),
            "invalid lab counter identifier");
        using var identifiers = new SqlCommandBuilder();
        var counterTable = identifiers.QuoteIdentifier("dbo") + "." + identifiers.QuoteIdentifier(counterName);
        var createCounterSql = "CREATE TABLE " + counterTable + " ([Id] int NOT NULL PRIMARY KEY, [Value] int NOT NULL); " +
            "INSERT INTO " + counterTable + " ([Id], [Value]) VALUES (@id, @initial);";
        var readCounterSql = "SELECT [Value] FROM " + counterTable + " WHERE [Id] = @id";
        var writeCounterSql = "UPDATE " + counterTable + " SET [Value] = @next WHERE [Id] = @id";
        var dropCounterSql = "DROP TABLE IF EXISTS " + counterTable;
        Exception? failure = null;
        try
        {
            await using (var db = factory())
            {
                db.Database.SetCommandTimeout(10);
                db.AddRange(course, term); db.AddRange(students); await db.SaveChangesAsync();
                offerings.Add(new() { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "SEQ", Capacity = 1 });
                offerings.Add(new() { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "RACE", Capacity = 1 });
                db.AddRange(offerings); await db.SaveChangesAsync();
                // No counter exists in the product model. Keep this one entirely outside migrations/model.
                await db.Database.ExecuteSqlRawAsync(createCounterSql,
                    new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = 1 },
                    new SqlParameter("@initial", System.Data.SqlDbType.Int) { Value = 0 });
            }

            using (var sequentialLimit = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var first = await TrySeat(factory, offerings[0].Id, students[0].Id, null, sequentialLimit.Token);
                var second = await TrySeat(factory, offerings[0].Id, students[1].Id, null, sequentialLimit.Token);
                Check(first.ReadValue == 0 && first.Wrote && second.ReadValue == 1 && !second.Wrote,
                    "sequential capacity 1 admits only the first candidate");
                await using var verify = factory();
                var rows = await verify.Enrollments.AsNoTracking().Where(e => e.CourseOfferingId == offerings[0].Id).ToListAsync(sequentialLimit.Token);
                Check(rows.Count == 1 && rows[0].StudentId == students[0].Id && rows[0].Status == EnrollmentStatus.Submitted,
                    "fresh context sees only first candidate");
            }
            Console.WriteLine("PASS: sequential lab capacity=1, occupied reads=0/1, persisted registrations=1");

            var seats = await Pair((index, gate, ct) => TrySeat(factory, offerings[1].Id, students[index].Id, gate, ct));
            Check(seats[0].SessionId != seats[1].SessionId && seats.All(x => x.ReadValue == 0 && x.Wrote),
                "distinct SQL connections both read free seat and both commit");
            await using (var verify = factory())
            {
                var rows = await verify.Enrollments.AsNoTracking().Where(e => e.CourseOfferingId == offerings[1].Id).ToListAsync();
                Check(await verify.CourseOfferings.Where(o => o.Id == offerings[1].Id).Select(o => o.Capacity).SingleAsync() == 1 &&
                    rows.Count == 2 && rows.All(e => e.Status == EnrollmentStatus.Submitted) &&
                    rows.Select(e => e.StudentId).Order().SequenceEqual(students.Select(s => s.Id).Order()),
                    "fresh context proves two distinct active candidates exceed capacity 1");
            }
            Console.WriteLine("PASS (expected defect): both read occupied=0, both commit; registrations=2 > capacity=1");

            var increments = await Pair(async (_, gate, ct) =>
            {
                await using var db = factory();
                db.Database.SetCommandTimeout(10);
                await db.Database.OpenConnectionAsync(ct);
                // Match the SQL smallint session identifier to the requested CLR Int32.
                var session = await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").SingleAsync(ct);
                var value = await db.Database.SqlQueryRaw<int>(readCounterSql, new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = 1 }).SingleAsync(ct);
                await gate.Meet(ct);
                // Deliberately overwrite with the stale client-computed value, NOT SET Value=Value+1.
                var changed = await db.Database.ExecuteSqlRawAsync(writeCounterSql,
                    new object[] { new SqlParameter("@next", System.Data.SqlDbType.Int) { Value = checked(value + 1) },
                        new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = 1 } }, ct);
                Check(changed == 1, "each counter write affects one row");
                return new Observation(session, value, true);
            });
            Check(increments[0].SessionId != increments[1].SessionId && increments.All(x => x.ReadValue == 0 && x.Wrote),
                "distinct SQL connections read the same counter before writes");
            await using (var verify = factory())
                Check(await verify.Database.SqlQueryRaw<int>(readCounterSql, new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = 1 }).SingleAsync() == 1,
                    "fresh context proves counter=1 instead of two increments producing 2");
            Console.WriteLine("PASS (expected defect): counter reads=0/0, writes=1/1, final=1 instead of 2; one increment lost");
            Check(!probe, "controlled runner failure probe");
            Console.WriteLine("Lab only: these observations do not prove production safety and introduce no concurrency solution.");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            // Workers have settled before entry here. Use fresh context and a fresh cleanup deadline.
            var cleanupFailed = false;
            try
            {
                await using var db = factory(); db.Database.SetCommandTimeout(10);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var ids = students.Where(s => s.Id > 0).Select(s => s.Id).ToArray();
                var offeringIds = offerings.Where(o => o.Id > 0).Select(o => o.Id).ToArray();
                await db.Enrollments.Where(e => ids.Contains(e.StudentId) && offeringIds.Contains(e.CourseOfferingId)).ExecuteDeleteAsync(cleanup.Token);
                await db.CourseOfferings.Where(o => offeringIds.Contains(o.Id)).ExecuteDeleteAsync(cleanup.Token);
                await db.Students.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync(cleanup.Token);
                await db.AcademicTerms.Where(t => t.Id == term.Id).ExecuteDeleteAsync(cleanup.Token);
                await db.Courses.Where(c => c.Id == course.Id).ExecuteDeleteAsync(cleanup.Token);
            }
            catch (Exception error) { cleanupFailed = true; Console.Error.WriteLine("CLEANUP concurrency rows: " + error.GetType().Name); }
            // Attempt table cleanup even if academic row cleanup failed.
            try
            {
                await using var db = factory(); db.Database.SetCommandTimeout(10);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await db.Database.ExecuteSqlRawAsync(dropCounterSql, cleanup.Token);
            }
            catch (Exception error) { cleanupFailed = true; Console.Error.WriteLine("CLEANUP concurrency counter: " + error.GetType().Name); }
            if (cleanupFailed && failure is null) throw new CleanupFailure();
            if (!cleanupFailed) Console.WriteLine("PASS: lab-owned rows removed in FK order; counter table dropped; contexts/connections disposed");
        }
    }
}
