using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class SeatCapacityChecks
{
    static void Check(bool ok, string message)
    { if (!ok) throw new BehaviorFailure("Seat capacity: " + message); }

    // Fixture setup ONLY, never called after a production transition to hide an incorrect counter.
    // The caller owns the offering IDs. Normal application transitions must use EnrollmentOperations.
    public static async Task SyncFixtureSeatsAsync(UniversityDbContext db, params int[] offeringIds)
    {
        foreach (var id in offeringIds.Distinct())
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE o SET OccupiedSeats = (SELECT COUNT(*) FROM dbo.Enrollments e
                    WHERE e.CourseOfferingId=o.Id AND e.Status IN (1,2))
                FROM dbo.CourseOfferings o WHERE o.Id={id}
                """);
    }

    public static async Task SaveFixtureAsync(UniversityDbContext db)
    {
        var ids = db.ChangeTracker.Entries<Enrollment>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.CourseOfferingId).Distinct().ToArray();
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync() : null;
        await db.SaveChangesAsync();
        await SyncFixtureSeatsAsync(db, ids);
        if (transaction is not null) await transaction.CommitAsync();
    }

    // Historical schemas have no OccupiedSeats column, including INSERT OUTPUT's generated defaults.
    public static async Task<CourseOffering> HistoricalOfferingAsync(UniversityDbContext db, int course, int term, int capacity)
    {
        var ids = await db.Database.SqlQuery<int>($"""
            INSERT dbo.CourseOfferings(CourseId,AcademicTermId,SectionCode,Capacity)
            OUTPUT INSERTED.Id AS [Value] VALUES ({course},{term},{"A"},{capacity})
            """).ToListAsync();
        return new() { Id=ids.Single(), CourseId=course, AcademicTermId=term, SectionCode="A", Capacity=capacity };
    }

    // Run against the runner's fresh, owned database only. Requires the guarded AddOccupiedSeats migration.
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        var tag="S37M_"+Guid.NewGuid().ToString("N")[..8];
        var course=new Course { Code=tag, Name="Seat upgrade fixture", Credits=1 };
        var term=new AcademicTerm { Name=tag, StartsOn=new(2026,1,1), EndsOn=new(2026,6,1) };
        var students=Enumerable.Range(0,4).Select(i=>new Student { StudentNumber=tag+i, FullName="Synthetic seat fixture" }).ToArray();
        CourseOffering? offering=null; Exception? failure=null;
        try
        {
            await using (var db=factory())
            {
                Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "upgrade requires an unused isolated database");
                var migrations=db.GetService<IMigrationsAssembly>().Migrations.Keys.Order().ToArray();
                var seatIndex=Array.FindIndex(migrations,m=>m.EndsWith("_AddOccupiedSeats",StringComparison.Ordinal));
                Check(seatIndex>0,
                    "generate AddOccupiedSeats and insert guarded backfill before running this mode");
                await db.GetService<IMigrator>().MigrateAsync(migrations[seatIndex-1]);
                db.Add(term); db.AddRange(students); await db.SaveChangesAsync(); await CourseVersionChecks.HistoricalCourseAsync(db,course);
                offering=await HistoricalOfferingAsync(db,course.Id,term.Id,1);
                var rows=students.Select(s=>new Enrollment { StudentId=s.Id,CourseOfferingId=offering.Id,CreatedAtUtc=DateTimeOffset.UtcNow }).ToArray();
                Check(rows[1].TrySubmit() && rows[2].TrySubmit() && rows[2].TryApprove()
                    && rows[3].TrySubmit() && rows[3].TryReject("Historical reason"),"historical states");
                db.AddRange(rows); await db.SaveChangesAsync();
                try { await db.Database.MigrateAsync(); throw new BehaviorFailure("Expected over-capacity migration refusal"); }
                catch (SqlException ex) when (ex.Number==51037) { }
            }
            await using (var db=factory())
            {
                Check(await db.Enrollments.CountAsync(e=>e.CourseOfferingId==offering!.Id)==4,"failed migration preserves historical records");
                Check(!((await db.Database.GetAppliedMigrationsAsync()).Any(m=>m.EndsWith("_AddOccupiedSeats"))),"failed backfill not committed");
                Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CourseOfferings') AND name=N'OccupiedSeats'").SingleAsync()==0,"migration column rolled back");
                // Remove only the deliberately invalid test fixture; never repair real data to fit capacity.
                await db.Enrollments.Where(e=>e.CourseOfferingId==offering!.Id).ExecuteDeleteAsync();
                await db.CourseOfferings.Where(o=>o.Id==offering!.Id).ExecuteDeleteAsync();
                offering=await HistoricalOfferingAsync(db,course.Id,term.Id,3);
                var rows=students.Select(s=>new Enrollment { StudentId=s.Id,CourseOfferingId=offering.Id,CreatedAtUtc=DateTimeOffset.UtcNow }).ToArray();
                Check(rows[1].TrySubmit() && rows[2].TrySubmit() && rows[2].TryApprove()
                    && rows[3].TrySubmit() && rows[3].TryReject("Historical reason"),"valid historical states");
                db.AddRange(rows); await db.SaveChangesAsync();
                var expected=rows.OrderBy(e=>e.Id).ToArray();
                var ids=expected.Select(e=>e.Id).ToArray();
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
                await using var verify=factory();
                var persisted=await verify.Enrollments.AsNoTracking().Where(e=>ids.Contains(e.Id)).OrderBy(e=>e.Id).ToArrayAsync();
                Check(persisted.Length==4 && persisted.Select(e=>e.Id).SequenceEqual(ids)
                    && persisted.Select(e=>e.Status).SequenceEqual(expected.Select(e=>e.Status))
                    && persisted.Zip(expected).All(p=>p.First.StudentId==p.Second.StudentId && p.First.CreatedAtUtc==p.Second.CreatedAtUtc
                        && p.First.RejectionReason==p.Second.RejectionReason),"upgrade preserves identities/state/reasons/time");
                Check((await verify.CourseOfferings.SingleAsync(o=>o.Id==offering.Id)).OccupiedSeats==2,"backfill counts Submitted and Approved only");
                Check(!verify.Database.HasPendingModelChanges() && !(await verify.Database.GetPendingMigrationsAsync()).Any(),"snapshot aligned and second update no-op");
            }
            Console.WriteLine("PASS: guarded upgrade rejects overflow; valid historical rows preserved, active backfill=2, replay/model aligned");
        }
        catch(Exception ex) { failure=ex; throw; }
        finally
        {
            try
            {
                await using var db=factory(); db.Database.SetCommandTimeout(10);
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var ids=students.Where(s=>s.Id>0).Select(s=>s.Id).ToArray();
                await db.Enrollments.Where(e=>ids.Contains(e.StudentId)).ExecuteDeleteAsync(stop.Token);
                if(offering is not null) await db.CourseOfferings.Where(o=>o.Id==offering.Id).ExecuteDeleteAsync(stop.Token);
                await db.Students.Where(s=>ids.Contains(s.Id)).ExecuteDeleteAsync(stop.Token);
                await db.AcademicTerms.Where(t=>t.Id==term.Id).ExecuteDeleteAsync(stop.Token);
                await db.Courses.Where(c=>c.Id==course.Id).ExecuteDeleteAsync(stop.Token);
            }
            catch(Exception ex) { Console.Error.WriteLine("CLEANUP seat upgrade: "+ex.GetType().Name); if(failure is null) throw new CleanupFailure(); }
        }
    }

    sealed class Rendezvous : DbCommandInterceptor
    {
        readonly TaskCompletionSource<bool> both=new(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct=default)
        {
            // Meet after both requests have read/evaluated the old state, before either acquires the offering lock.
            if(command.CommandText.Contains("WITH (UPDLOCK, HOLDLOCK)",StringComparison.Ordinal))
            {
                if(Interlocked.Increment(ref arrivals)==2) both.TrySetResult(true);
                await both.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);
            }
            return result;
        }
    }
    sealed class CounterWrite : DbCommandInterceptor
    {
        public int Writes;
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("UPDATE",StringComparison.Ordinal) && command.CommandText.Contains("OccupiedSeats",StringComparison.Ordinal) && result==1) Writes++;
            return ValueTask.FromResult(result);
        }
    }
    sealed class Observer(Func<UniversityDbContext> factory) : IEnrollmentApprovedObserver
    {
        public readonly ConcurrentQueue<(int Id,bool Committed)> Events=new();
        public async Task HandleAsync(EnrollmentApproved notification,CancellationToken ct)
        {
            bool committed=false;
            try
            {
                await using var db=factory();
                committed=await db.Enrollments.AnyAsync(e=>e.Id==notification.EnrollmentId && e.StudentId==notification.StudentId
                    && e.CourseOfferingId==notification.CourseOfferingId && e.Status==EnrollmentStatus.Approved,ct)
                    && await db.CourseOfferings.AnyAsync(o=>o.Id==notification.CourseOfferingId && o.OccupiedSeats==1,ct);
            }
            finally { Events.Enqueue((notification.EnrollmentId,committed)); }
        }
    }

    public static async Task Run(Func<UniversityDbContext> factory,bool probe=false)
    {
        var tag="S37_"+Guid.NewGuid().ToString("N")[..8];
        var course=new Course { Code=tag,Name="Seat race",Credits=1 };
        var term=new AcademicTerm { Name=tag,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1) };
        var students=Enumerable.Range(0,6).Select(i=>new Student { StudentNumber=tag+i,FullName="Synthetic seat race" }).ToArray();
        var offerings=new List<CourseOffering>(); var rows=new List<Enrollment>();
        var observer=new Observer(factory); Exception? failure=null; bool faultConstraint=false;
        var publisher=new EnrollmentApprovedPublisher([observer],NullLogger<EnrollmentApprovedPublisher>.Instance);
        EnrollmentOperations Operations(UniversityDbContext db)=>new(db,new(30),DecoratorChecks.TimedEvaluator(),publisher);
        async Task<EnrollmentTransitionResult> Apply(int id,string action)
        {
            await using var db=factory(); db.Database.SetCommandTimeout(10);
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return action switch { "submit"=>await Operations(db).SubmitAsync(id,stop.Token),
                "approve"=>await Operations(db).ApproveAsync(id,stop.Token), _=>await Operations(db).RejectAsync(id," Reason ",stop.Token) };
        }
        async Task<EnrollmentTransitionResult[]> Race(int first,string a,int second,string b)
        {
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(45)); var gate=new Rendezvous();
            var sessions=new ConcurrentBag<int>();
            async Task<EnrollmentTransitionResult> Worker(int id,string action)
            {
                try
                {
                    await using var template=factory();
                    await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>()
                        .UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(gate).Options);
                    db.Database.SetCommandTimeout(10); await db.Database.OpenConnectionAsync(stop.Token);
                    sessions.Add(await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").SingleAsync(stop.Token));
                    return action switch { "submit"=>await Operations(db).SubmitAsync(id,stop.Token),
                        "approve"=>await Operations(db).ApproveAsync(id,stop.Token), _=>await Operations(db).RejectAsync(id," Reason ",stop.Token) };
                }
                catch { await stop.CancelAsync(); throw; }
            }
            var result=await Task.WhenAll(Worker(first,a),Worker(second,b));
            Check(sessions.Distinct().Count()==2,"two separate open SQL connections");
            return result;
        }
        async Task Invariant(int offeringId,int expected)
        {
            await using var db=factory();
            var seats=await db.CourseOfferings.Where(o=>o.Id==offeringId).Select(o=>o.OccupiedSeats).SingleAsync();
            var active=await db.Enrollments.CountAsync(e=>e.CourseOfferingId==offeringId && (e.Status==EnrollmentStatus.Submitted || e.Status==EnrollmentStatus.Approved));
            Check(seats==expected && seats==active,"fresh-context counter equals Submitted+Approved");
        }
        try
        {
            await using(var db=factory()) { db.AddRange(course,term); db.AddRange(students); await db.SaveChangesAsync(); }
            offerings.AddRange(Enumerable.Range(0,5).Select(i=>new CourseOffering { CourseId=course.Id,AcademicTermId=term.Id,SectionCode="S"+i,Capacity=1 }));
            await using(var db=factory()) { db.AddRange(offerings); await db.SaveChangesAsync(); }
            for(var i=0;i<6;i++) rows.Add(new Enrollment { StudentId=students[i].Id,CourseOfferingId=offerings[Math.Max(0,i-1)].Id,CreatedAtUtc=DateTimeOffset.UtcNow });
            await using(var db=factory()) { db.AddRange(rows); await db.SaveChangesAsync(); }

            foreach(var invalid in new[]{-1,2})
            {
                await using var db=factory();
                try
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.CourseOfferings SET OccupiedSeats={invalid} WHERE Id={offerings[0].Id}");
                    throw new BehaviorFailure("Expected occupied-seat CHECK refusal");
                }
                catch(SqlException ex) when(ex.Number==547 && ex.Message.Contains("CK_CourseOfferings_OccupiedSeats",StringComparison.Ordinal)) { }
            }
            await Invariant(offerings[0].Id,0);

            // All fixture rows are still Draft. A real CHECK rejects the state write AFTER seat increment.
            await using(var db=factory()) await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Enrollments ADD CONSTRAINT CK_Day37_SubmitFault CHECK ([Status] <> 1)");
            faultConstraint=true;
            try
            {
                await using var template=factory(); var capture=new CounterWrite();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                try { await Operations(db).SubmitAsync(rows[0].Id,default); throw new BehaviorFailure("Expected real state write failure"); }
                catch(SqlException ex) when(ex.Number==547 && ex.Message.Contains("CK_Day37_SubmitFault",StringComparison.Ordinal)) { }
                Check(capture.Writes==1,"seat increment executed before failing enrollment write");
                await Invariant(offerings[0].Id,0);
                await using var verify=factory();
                Check(await verify.Enrollments.AnyAsync(e=>e.Id==rows[0].Id && e.Status==EnrollmentStatus.Draft) && observer.Events.IsEmpty,"failed write restores Draft and publishes nothing");
            }
            finally
            {
                await using var db=factory(); await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Enrollments DROP CONSTRAINT CK_Day37_SubmitFault"); faultConstraint=false;
            }
            var last=await Race(rows[0].Id,"submit",rows[1].Id,"submit");
            Check(last.Count(r=>r is EnrollmentTransitionResult.Updated)==1 && last.Count(r=>r is EnrollmentTransitionResult.CapacityUnavailable)==1,"last seat: one success, one capacity conflict");
            await Invariant(offerings[0].Id,1);
            await using(var db=factory()) Check(await db.Enrollments.CountAsync(e=>e.CourseOfferingId==offerings[0].Id && e.Status==EnrollmentStatus.Draft)==1,"full offering retains losing Draft");
            var same=await Race(rows[2].Id,"submit",rows[2].Id,"submit");
            Check(same.Count(r=>r is EnrollmentTransitionResult.Updated)==1 && same.Count(r=>r is EnrollmentTransitionResult.InvalidTransition)==1,"same Draft: exactly one transition");
            await Invariant(offerings[1].Id,1);
            Check(await Apply(rows[3].Id,"submit") is EnrollmentTransitionResult.Updated,"arrange Submitted");
            var reject=await Race(rows[3].Id,"reject",rows[3].Id,"reject");
            Check(reject.Count(r=>r is EnrollmentTransitionResult.Updated)==1 && reject.Count(r=>r is EnrollmentTransitionResult.InvalidTransition)==1,"double reject: one release");
            await Invariant(offerings[2].Id,0);
            await using(var db=factory()) Check((await db.Enrollments.SingleAsync(e=>e.Id==rows[3].Id)).RejectionReason=="Reason","rejection reason remains trimmed");
            Check(await Apply(rows[4].Id,"submit") is EnrollmentTransitionResult.Updated,"arrange approval race");
            var terminal=await Race(rows[4].Id,"approve",rows[4].Id,"reject");
            Check(terminal.Count(r=>r is EnrollmentTransitionResult.Updated)==1 && terminal.Count(r=>r is EnrollmentTransitionResult.InvalidTransition)==1,"approve/reject: exactly one terminal transition");
            await using(var db=factory())
            {
                var status=await db.Enrollments.Where(e=>e.Id==rows[4].Id).Select(e=>e.Status).SingleAsync();
                Check(status is EnrollmentStatus.Approved or EnrollmentStatus.Rejected,"terminal state");
                await Invariant(offerings[3].Id,status==EnrollmentStatus.Approved?1:0);
                Check(observer.Events.Count==(status==EnrollmentStatus.Approved?1:0) && observer.Events.All(e=>e.Committed),"only committed winning approval publishes");
            }
            // Also prove the approval publication path regardless of the nondeterministic race winner.
            Check(await Apply(rows[5].Id,"submit") is EnrollmentTransitionResult.Updated,"arrange deterministic approval");
            Check(await Apply(rows[5].Id,"approve") is EnrollmentTransitionResult.Updated,"approve retains seat");
            await Invariant(offerings[4].Id,1);
            Check(observer.Events.Count(e=>e.Id==rows[5].Id)==1 && observer.Events.All(e=>e.Committed),"observer reads committed Approved through independent context");
            Check(!probe,"controlled failure propagation");
            Console.WriteLine("PASS: last seat, same Draft, double reject, approve/reject; real post-increment failure rollback; fresh-context seat invariants and postcommit events");
        }
        catch(Exception ex) { failure=ex; throw; }
        finally
        {
            var cleanupFailed=false;
            if(faultConstraint)
            {
                try { await using var db=factory(); db.Database.SetCommandTimeout(10); await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Enrollments DROP CONSTRAINT CK_Day37_SubmitFault"); }
                catch(Exception ex) { cleanupFailed=true; Console.Error.WriteLine("CLEANUP seat constraint: "+ex.GetType().Name); }
            }
            try
            {
                await using var db=factory(); db.Database.SetCommandTimeout(10);
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var ids=students.Where(s=>s.Id>0).Select(s=>s.Id).ToArray(); var oids=offerings.Where(o=>o.Id>0).Select(o=>o.Id).ToArray();
                await db.Enrollments.Where(e=>ids.Contains(e.StudentId)).ExecuteDeleteAsync(stop.Token);
                await db.CourseOfferings.Where(o=>oids.Contains(o.Id)).ExecuteDeleteAsync(stop.Token);
                await db.Students.Where(s=>ids.Contains(s.Id)).ExecuteDeleteAsync(stop.Token);
                await db.AcademicTerms.Where(t=>t.Id==term.Id).ExecuteDeleteAsync(stop.Token);
                await db.Courses.Where(c=>c.Id==course.Id).ExecuteDeleteAsync(stop.Token);
            }
            catch(Exception ex) { cleanupFailed=true; Console.Error.WriteLine("CLEANUP seat rows: "+ex.GetType().Name); }
            if(cleanupFailed && failure is null) throw new CleanupFailure();
            if(!cleanupFailed) Console.WriteLine("PASS: owned seat fixtures removed; workers settled and connections disposed");
        }
    }
}
