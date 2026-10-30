using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class RepositoryChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Repository: " + label); }
    // One behavioral harness; no mock DbSet and no duplicated direct/repository tests.
    sealed record Access(Func<int, CancellationToken, Task<Enrollment?>> Find, Action<Enrollment> Add);
    static Access Open(UniversityDbContext db, bool repository)
    {
        if (!repository) return new((id, ct) => db.Enrollments.SingleOrDefaultAsync(e => e.Id == id, ct), e => db.Enrollments.Add(e));
        var alternative = new DemoEnrollmentRepository(db);
        return new(alternative.FindAsync, alternative.Add);
    }

    public static async Task Run(Func<UniversityDbContext> factory, bool probe = false)
    {
        var tag = "R22_" + Guid.NewGuid().ToString("N")[..10];
        var course = new Course { Code = tag, Name = "Synthetic repository course", Credits = 1 };
        var term = new AcademicTerm { Name = tag, StartsOn = new(2026,1,1), EndsOn = new(2026,6,1) };
        var student = new Student { StudentNumber = tag, FullName = "Synthetic repository student" };
        var extra = new Student { StudentNumber = tag + "_UOW", FullName = "Synthetic shared context student" };
        var offerings = new List<CourseOffering>();
        Exception? failure = null;
        Enrollment New(int offeringId) => new() { StudentId = student.Id, CourseOfferingId = offeringId, CreatedAtUtc = DateTimeOffset.UtcNow };
        try
        {
            await using (var db = factory())
            {
                db.AddRange(course, term, student); await db.SaveChangesAsync();
                for (var i = 0; i < 6; i++) offerings.Add(new() { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "R" + i, Capacity = 40 });
                db.AddRange(offerings); await db.SaveChangesAsync();
                Check(!await db.CourseOfferings.AnyAsync(o => o.Id == int.MaxValue), "missing FK fixture really absent");
                Check(!await db.Enrollments.AnyAsync(e => e.Id == int.MaxValue), "missing enrollment really absent");
            }
            foreach (var repository in new[] { false, true })
            {
                var offering = offerings[repository ? 1 : 0].Id;
                var entity = New(offering);
                await using (var db = factory())
                {
                    var access = Open(db, repository); access.Add(entity);
                    Check(db.Entry(entity).State == EntityState.Added, "Add is Added, no implicit save");
                    await using (var independent = factory())
                        Check(!await independent.Enrollments.AnyAsync(e => e.StudentId == student.Id && e.CourseOfferingId == offering), "no row before save");
                    await db.SaveChangesAsync();
                    Check(db.Entry(entity).State == EntityState.Unchanged && entity.Id > 0, "caller save/generated ID");
                }
                await using (var independent = factory())
                    Check(await independent.Enrollments.AnyAsync(e => e.Id == entity.Id && e.CourseOfferingId == offering), "row after save in new context");
                var capture = new CatalogSqlCapture();
                await using (var source = factory())
                await using (var db = new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>()
                    .UseSqlServer(source.Database.GetConnectionString()).AddInterceptors(capture).Options))
                {
                    var access = Open(db, repository);
                    var found = await access.Find(entity.Id, default);
                    Check(found is not null && found.StudentId == student.Id && db.Entry(found).State == EntityState.Unchanged
                        && db.ChangeTracker.Entries<Enrollment>().Count() == 1, "Find uses expected context tracking");
                    Check(await access.Find(int.MaxValue, default) is null, "missing is null");
                    Check(capture.Commands.Count == 2 && capture.Commands.All(c => c.Text.Contains("WHERE", StringComparison.OrdinalIgnoreCase)
                        && !c.Text.Contains("JOIN", StringComparison.OrdinalIgnoreCase) && c.Parameters.Length > 0), "executed parameterized filtered Find, no includes/all-table read");
                    capture.Commands.Clear();
                    var projection = await db.Enrollments.Where(e => e.StudentId == student.Id && e.CourseOfferingId == offering)
                        .Select(e => new { e.Id, e.Status }).ToListAsync();
                    var sql = capture.Commands.Single().Text;
                    Check(projection.Count == 1 && sql.Contains("WHERE") && sql.Contains("Status")
                        && !sql.Contains("CreatedAtUtc") && !sql.Contains("RejectionReason"), "direct scalar projection stays in executed SQL");
                    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                    try { await access.Find(entity.Id, cancelled.Token); throw new BehaviorFailure("Cancellation became success/null"); }
                    catch (OperationCanceledException) { }
                }
                // The same shared harness verifies the real FK failure, not any exception.
                var validOffering = offerings[repository ? 3 : 2].Id;
                await using (var db = factory())
                {
                    var access = Open(db, repository);
                    access.Add(New(validOffering)); access.Add(New(int.MaxValue));
                    try { await db.SaveChangesAsync(); throw new BehaviorFailure("Expected real FK rejection"); }
                    catch (DbUpdateException error)
                    { Check(RegistrationWriteErrors.Classify(error) == "courseOfferingId", "specific offering FK failure"); }
                    Check(db.ChangeTracker.Entries<Enrollment>().Count(e => e.State == EntityState.Added) == 2, "failed tracker not reset");
                }
                await using (var independent = factory())
                    Check(!await independent.Enrollments.AnyAsync(e => e.StudentId == student.Id
                        && (e.CourseOfferingId == validOffering || e.CourseOfferingId == int.MaxValue)), "single save neither row persists");
                // Disposed query is a lifetime error, never a not-found result.
                Access expired;
                IQueryable<int> deferred;
                await using (var db = factory()) { expired = Open(db, repository); deferred = db.Enrollments.Where(e => e.Id == entity.Id).Select(e => e.Id); }
                try { await expired.Find(entity.Id, default); throw new BehaviorFailure("Disposed context converted to null"); }
                catch (ObjectDisposedException) { }
                try { await deferred.ToListAsync(); throw new BehaviorFailure("Query escaped context lifetime"); }
                catch (ObjectDisposedException) { }
                Console.WriteLine($"PASS: {(repository ? "repository" : "direct EF")} find/null/tracking/Add/save visibility/real FK atomicity/token/lifetime; executed SQL filter/projection");
            }
            var shared = New(offerings[4].Id);
            await using (var db = factory())
            {
                new DemoEnrollmentRepository(db).Add(shared);
                new DemoStudentWriter(db).Add(extra);
                Check(db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added) == 2, "two components one tracker");
                await using (var read = factory()) Check(!await read.Students.AnyAsync(s => s.StudentNumber == extra.StudentNumber)
                    && !await read.Enrollments.AnyAsync(e => e.StudentId == student.Id && e.CourseOfferingId == shared.CourseOfferingId), "shared pending changes not persisted");
                await db.SaveChangesAsync();
            }
            await using (var read = factory()) Check(await read.Students.AnyAsync(s => s.Id == extra.Id)
                && await read.Enrollments.AnyAsync(e => e.Id == shared.Id), "one caller SaveChanges covers both components");
            Check(!typeof(Enrollment).Assembly.GetTypes().Any(t => t.Name == nameof(DemoEnrollmentRepository)), "experiment excluded from web assembly");
            Check(!probe, "controlled runner failure probe");
            Console.WriteLine("PASS: two narrow components share pending changes/one SaveChanges; no repository commit/dispose; experiment outside web assembly");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                await using var db = factory();
                await db.Enrollments.Where(e => e.StudentId == student.Id).ExecuteDeleteAsync();
                var ids = offerings.Select(o => o.Id).ToArray();
                await db.CourseOfferings.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync();
                await db.Students.Where(s => s.Id == student.Id || s.Id == extra.Id).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(t => t.Id == term.Id).ExecuteDeleteAsync();
                await db.Courses.Where(c => c.Id == course.Id).ExecuteDeleteAsync();
                Console.WriteLine("PASS: repository fixture cleaned in FK order");
            }
            catch (Exception error) { Console.Error.WriteLine("CLEANUP repository: " + error.GetType().Name); if (failure is null) throw new CleanupFailure(); }
        }
    }
}
