using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using UniversityRegistration.Application;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class DropPlacementChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Drop placement: " + label); }
    sealed class Gate : DbCommandInterceptor
    {
        readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("WITH (UPDLOCK, HOLDLOCK)"))
            {
                if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);
            }
            return result;
        }
    }
    sealed class CancelAfterRelease(CancellationTokenSource stop) : DbCommandInterceptor
    {
        public bool Reached;
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data, int result, CancellationToken ct = default)
        {
            if (result == 1 && command.CommandText.Contains("OccupiedSeats") && command.CommandText.Contains("- 1"))
            { Reached = true; stop.Cancel(); }
            return ValueTask.FromResult(result);
        }
    }
    sealed class Observe(Func<UniversityDbContext> factory) : IEnrollmentApprovedObserver
    {
        public int Count;
        public async Task HandleAsync(EnrollmentApproved e, CancellationToken ct)
        {
            await using var db = factory();
            Check(await db.Enrollments.AnyAsync(x => x.Id == e.EnrollmentId && x.Status == EnrollmentStatus.Approved,ct), "observer sees committed approval");
            Count++;
        }
    }
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        await using var db = factory();
        await db.GetService<IMigrator>().MigrateAsync("20261113123141_AddWaitlistEntries");
        // Full regression also preserves a historical Approved row through the migration chain.
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        Check(!db.Database.HasPendingModelChanges(), "upgrade/model/second update");
    }
    public static async Task Run(Func<UniversityDbContext> factory, CatalogTestIdentity identity)
    {
        var tag = "D43"+Guid.NewGuid().ToString("N")[..10];
        var c = new Course { Code=tag,Name="Placement",Credits=3 };
        var prerequisite = new Course { Code=tag+"P",Name="Required",Credits=1 };
        var term = new AcademicTerm { Name=tag,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1) };
        var students = Enumerable.Range(0,38).Select(i=>new Student{StudentNumber=tag+i,FullName="Placement fixture"}).ToArray();
        CourseOffering? offer=null;
        var observer = new Observe(factory);
        EnrollmentOperations Ops(UniversityDbContext db) => new(db,new(30),DecoratorChecks.TimedEvaluator(),
            new EnrollmentApprovedPublisher([observer],NullLogger<EnrollmentApprovedPublisher>.Instance));
        var rows = new Dictionary<int,int>(); var entries = new Dictionary<int,long>();
        bool trigger=false; Exception? primaryFailure=null; int scenario=0;
        async Task Setup(int holders, int[] waiting, int[] eligible, params (int Student, EnrollmentStatus Status)[] extra)
        {
            Console.WriteLine($"SCENARIO placement {++scenario}: holders={holders}, Waiting={waiting.Length}");
            await using var db=factory();
            await db.WaitlistEntries.Where(w=>w.CourseOfferingId==offer!.Id).ExecuteDeleteAsync();
            await EnrollmentReceiptTestCleanup.DeleteAsync(db,db.Enrollments.Where(e=>e.CourseOfferingId==offer!.Id));
            await db.StudentCompletedCourses.Where(x=>students.Select(s=>s.Id).Contains(x.StudentId)).ExecuteDeleteAsync();
            await db.CourseOfferings.Where(o=>o.Id==offer!.Id).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.Capacity,holders).SetProperty(o=>o.OccupiedSeats,0));
            rows.Clear();entries.Clear();
            foreach(var i in eligible.Concat(Enumerable.Range(0,holders)).Distinct()) db.Add(new StudentCompletedCourse{StudentId=students[i].Id,CourseId=prerequisite.Id});
            foreach(var item in Enumerable.Range(0,holders).Select(i=>(Student:i,Status:EnrollmentStatus.Submitted)).Concat(extra))
            {
                var e=new Enrollment{StudentId=students[item.Student].Id,CourseOfferingId=offer!.Id,CreatedAtUtc=DateTimeOffset.UtcNow};
                if(item.Status!=EnrollmentStatus.Draft) Check(e.TrySubmit(),"fixture submit");
                if(item.Status==EnrollmentStatus.Approved) Check(e.TryApprove(),"fixture approve");
                if(item.Status==EnrollmentStatus.Rejected) Check(e.TryReject("fixture"),"fixture reject");
                if(item.Status==EnrollmentStatus.Dropped) Check(e.TryDrop(),"fixture drop");
                db.Add(e);await db.SaveChangesAsync();rows[item.Student]=e.Id;
            }
            await SeatCapacityChecks.SyncFixtureSeatsAsync(db,offer!.Id);
            foreach(var i in waiting)
            {
                var w=new WaitlistEntry{StudentId=students[i].Id,CourseOfferingId=offer.Id,JoinedAtUtc=DateTimeOffset.UtcNow};
                db.Add(w);await db.SaveChangesAsync();entries[i]=w.Id;
            }
        }
        async Task State(int occupied, params (int Student, WaitlistStatus Status)[] expected)
        {
            await ConcurrencyInvariantChecks.Verify(factory,[offer!.Id],$"placement {scenario}, expected active={occupied}");
            await using var db=factory();
            var count=await db.Enrollments.CountAsync(e=>e.CourseOfferingId==offer!.Id&&(e.Status==EnrollmentStatus.Submitted||e.Status==EnrollmentStatus.Approved));
            Check(count==occupied && await db.CourseOfferings.Where(o=>o.Id==offer!.Id).Select(o=>o.OccupiedSeats).SingleAsync()==count,"counter equals active");
            foreach(var e in expected)
            {
                Check(await db.WaitlistEntries.AnyAsync(w=>w.Id==entries[e.Student]&&w.Status==e.Status),"expected candidate status "+e);
                if(e.Status==WaitlistStatus.Placed)
                    Check(await db.Enrollments.AnyAsync(x=>x.StudentId==students[e.Student].Id&&x.CourseOfferingId==offer!.Id&&x.Status==EnrollmentStatus.Submitted),"new placement Submitted for expected candidate");
            }
        }
        async Task<EnrollmentTransitionResult> Apply(int id,string action)
        {
            await using var db=factory();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            return action switch { "drop"=>await Ops(db).DropAsync(id,stop.Token),"approve"=>await Ops(db).ApproveAsync(id,stop.Token),
                "submit"=>await Ops(db).SubmitAsync(id,stop.Token),_=>await Ops(db).RejectAsync(id,"fixture decision",stop.Token) };
        }
        async Task<EnrollmentTransitionResult[]> Race(int a,string first,int b,string second)
        {
            await using var seed=factory();
            var gate=new Gate();var options=new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(seed.Database.GetConnectionString()).AddInterceptors(gate).Options;
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var sessions=new System.Collections.Concurrent.ConcurrentBag<int>();
            async Task<EnrollmentTransitionResult> Call(int id,string action)
            {
                try { await using var db=new UniversityDbContext(options);
                    await db.Database.OpenConnectionAsync(stop.Token);
                    sessions.Add(await db.Database.SqlQueryRaw<int>("SELECT CAST(@@SPID AS int) AS [Value]").SingleAsync(stop.Token));
                    return action=="drop"?await Ops(db).DropAsync(id,stop.Token):await Ops(db).SubmitAsync(id,stop.Token);
                } catch { stop.Cancel();throw; }
            }
            var result=await Task.WhenAll(Call(a,first),Call(b,second));
            Check(sessions.Distinct().Count()==2,"two independent drop race SQL connections");
            Console.WriteLine($"RESULT drop {first}/{second}: {string.Join(",",result.Select(r=>r.GetType().Name))}; connections=2");
            return result;
        }
        try
        {
            await using(var db=factory())
            {
                db.AddRange(c,prerequisite,term);db.AddRange(students);await db.SaveChangesAsync();
                offer=new CourseOffering{CourseId=c.Id,AcademicTermId=term.Id,SectionCode="A",Capacity=2};
                db.Add(offer);db.Add(new CoursePrerequisite{CourseId=c.Id,PrerequisiteCourseId=prerequisite.Id});await db.SaveChangesAsync();
            }
            var owner=await identity.ForRequestAsync("GET",$"/students/{students[0].Id}",null,null);
            var other=await identity.ForRequestAsync("GET",$"/students/{students[2].Id}",null,null);
            async Task<int> Post(HttpClient client,string path,CancellationToken ct=default) { using var r=await client.PostAsync(path,null,ct);return (int)r.StatusCode; }
            await Setup(1,[],[]);
            using(var anon=identity.AnonymousClient()) Check(await Post(anon,$"/enrollments/{rows[0]}/drop")==401,"anonymous401");
            using(var advisor=await identity.NewRoleClientAsync(UniversityRoles.Advisor)) Check(await Post(advisor,$"/enrollments/{rows[0]}/drop")==403,"wrong role403");
            Check(await Post(other,$"/enrollments/{rows[0]}/drop")==404,"wrong owner404");
            var csrf=owner.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            try{Check(await Post(owner,$"/enrollments/{rows[0]}/drop")==400,"CSRF");}finally{owner.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf);}
            await State(1);
            Check(await Post(owner,$"/enrollments/{rows[0]}/drop")==204,"HTTP no candidate drop");
            Check(await Post(owner,$"/enrollments/{rows[0]}/drop")==204,"HTTP repeated204");await State(0);
            using(var summary=await owner.GetAsync($"/students/{students[0].Id}/terms/{term.Id}/registration-summary"))
            { var json=JsonNode.Parse(await summary.Content.ReadAsStringAsync())!;Check(json["counts"]!["dropped"]!.GetValue<int>()==1&&json["activeCredits"]!.GetValue<int>()==0,"Dropped summary inactive"); }
            await Setup(1,[2],[2],(2,EnrollmentStatus.Draft));
            var draftId=rows[2];Check(await Post(owner,$"/enrollments/{rows[0]}/drop")==204,"HTTP draft placement");
            await State(1,(2,WaitlistStatus.Placed));
            await using(var db=factory())Check(await db.Enrollments.AnyAsync(e=>e.Id==draftId&&e.Status==EnrollmentStatus.Submitted),"Draft reused");
            await Setup(1,[2,3],[3]);
            Check(await Apply(rows[0],"reject") is EnrollmentTransitionResult.Updated,"reject shares placement");
            await State(1,(2,WaitlistStatus.Ineligible),(3,WaitlistStatus.Placed));
            await using(var db=factory())Check(await db.WaitlistEntries.AnyAsync(w=>w.Id==entries[2]&&w.ClosureReason!.Contains("prerequisite_not_satisfied")),"explain academic reason");
            await Setup(1,[2,3],[]);
            await Apply(rows[0],"drop");await State(0,(2,WaitlistStatus.Ineligible),(3,WaitlistStatus.Ineligible));
            await Setup(1,Enumerable.Range(2,35).ToArray(),[36]);
            await Apply(rows[0],"drop");await State(1,(2,WaitlistStatus.Ineligible),(33,WaitlistStatus.Ineligible),(36,WaitlistStatus.Placed));
            await Setup(1,[2,3,4],[2,3,4],(2,EnrollmentStatus.Rejected),(3,EnrollmentStatus.Dropped));
            await Apply(rows[0],"drop");await State(1,(2,WaitlistStatus.Ineligible),(3,WaitlistStatus.Ineligible),(4,WaitlistStatus.Placed));
            // Existing active queue entry: legacy/inconsistent queue fixture, not public join policy.
            await Setup(2,[1,2],[2]);await Apply(rows[0],"drop");await State(2,(1,WaitlistStatus.Ineligible),(2,WaitlistStatus.Placed));
            await Setup(1,[2],[2]);var events=observer.Count;
            var duplicate=await Race(rows[0],"drop",rows[0],"drop");Check(duplicate.All(x=>x is EnrollmentTransitionResult.Updated),"double drop idempotent");await State(1,(2,WaitlistStatus.Placed));
            Check(observer.Count==events,"drop/placement no approval event");
            await Setup(2,[2,3],[2,3]);var twoDrops=await Race(rows[0],"drop",rows[1],"drop");Check(twoDrops.All(r=>r is EnrollmentTransitionResult.Updated),"two distinct drops both succeed");await State(2,(2,WaitlistStatus.Placed),(3,WaitlistStatus.Placed));
            await Setup(1,[2],[2,3],(3,EnrollmentStatus.Draft));
            var cross=await Race(rows[0],"drop",rows[3],"submit");Check(cross[0] is EnrollmentTransitionResult.Updated && cross[1] is EnrollmentTransitionResult.QueuePriority or EnrollmentTransitionResult.CapacityUnavailable,"drop-submit cannot jump queue");await State(1,(2,WaitlistStatus.Placed));
            await Setup(1,[],[]);await Apply(rows[0],"approve");await Apply(rows[0],"drop");await State(0);
            Check(observer.Count==events+1,"only real approve emits");
            await Setup(1,[],[]);await Apply(rows[0],"drop");Check(await Apply(rows[0],"approve") is EnrollmentTransitionResult.InvalidTransition,"drop then approve conflict");await State(0);
            // Real HTTP cancel/placement race. Both outcomes checked in fresh context.
            await Setup(1,[2,3],[2,3]);
            using var httpStop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<int> HttpRace(HttpClient client,string path){try {await start.Task.WaitAsync(TimeSpan.FromSeconds(10),httpStop.Token);return await Post(client,path,httpStop.Token);}catch{httpStop.Cancel();throw;}}
            var drop=HttpRace(owner,$"/enrollments/{rows[0]}/drop");
            var cancel=HttpRace(other,$"/students/{students[2].Id}/waitlist/{entries[2]}/cancel");
            start.SetResult();var responses=await Task.WhenAll(drop,cancel);
            Console.WriteLine($"RESULT HTTP drop/cancel: {responses[0]}/{responses[1]}; all requests settled");
            Check(responses[0]==204&&responses[1] is 204 or 409,"cancel-placement responses");
            await State(1);
            await using(var db=factory())
            {
                var first=await db.WaitlistEntries.SingleAsync(w=>w.Id==entries[2]);
                Check(first.Status==(responses[1]==204?WaitlistStatus.Cancelled:WaitlistStatus.Placed),"Placed never becomes Cancelled");
                var winner=responses[1]==204?students[3].Id:students[2].Id;
                Check(await db.Enrollments.AnyAsync(e=>e.StudentId==winner&&e.CourseOfferingId==offer.Id&&e.Status==EnrollmentStatus.Submitted),"race correct winner");
            }
            await Setup(1,[2],[2]);
            await Apply(rows[0],"drop");
            int placedEnrollment;
            await using(var db=factory()) placedEnrollment=await db.Enrollments.Where(e=>e.StudentId==students[2].Id&&e.CourseOfferingId==offer!.Id).Select(e=>e.Id).SingleAsync();
            Check(await Apply(placedEnrollment,"drop") is EnrollmentTransitionResult.Updated,"placed enrollment may later drop");
            await ConcurrencyInvariantChecks.Verify(factory,[offer!.Id],"historical Placed with Dropped enrollment");
            await using(var db=factory())Check(await db.WaitlistEntries.AnyAsync(w=>w.Id==entries[2]&&w.Status==WaitlistStatus.Placed)
                && await db.Enrollments.AnyAsync(e=>e.Id==placedEnrollment&&e.Status==EnrollmentStatus.Dropped),"historical placement retained");
            await Setup(1,[2,3],[3]);
            await using(var db=factory())
            { await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER dbo.DropPlacementTestFault ON dbo.WaitlistEntries AFTER UPDATE AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Status=2) THROW 51043, 'Controlled placement write failure.', 1; END");trigger=true; }
            Check(await Post(owner,$"/enrollments/{rows[0]}/drop")==500,"real intermediate SQL failure HTTP500");
            try { await Apply(rows[0],"drop");throw new BehaviorFailure("Expected placement SQL fault"); }
            catch(SqlException error) when(error.Number==51043) { Console.WriteLine("REACHED: placement UPDATE trigger error51043 (not auth/CSRF/timeout)"); }
            await State(1,(2,WaitlistStatus.Waiting),(3,WaitlistStatus.Waiting));
            await using(var db=factory())
            {
                Check(await db.Enrollments.CountAsync(e=>e.CourseOfferingId==offer.Id)==1&&await db.Enrollments.AnyAsync(e=>e.Id==rows[0]&&e.Status==EnrollmentStatus.Submitted),"release/new enrollment/Ineligible all rolled back");
                await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.DropPlacementTestFault");trigger=false;
            }
            Check(await Post(owner,$"/enrollments/{rows[0]}/drop")==204,"retry after rollback");await State(1,(2,WaitlistStatus.Ineligible),(3,WaitlistStatus.Placed));
            await Setup(1,[2],[2]);
            await using (var seed=factory())
            {
                using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var cancelAfter=new CancelAfterRelease(stop);
                var options=new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(seed.Database.GetConnectionString()).AddInterceptors(cancelAfter).Options;
                await using var db=new UniversityDbContext(options);
                try { await Ops(db).DropAsync(rows[0],stop.Token);throw new BehaviorFailure("Cancellation expected"); }
                catch(OperationCanceledException) when(stop.IsCancellationRequested) { Check(cancelAfter.Reached,"cancelled after real release"); }
            }
            await State(1,(2,WaitlistStatus.Waiting));
            await using(var db=factory())Check(await db.Enrollments.AnyAsync(e=>e.Id==rows[0]&&e.Status==EnrollmentStatus.Submitted),"cancellation release rollback");
            Console.WriteLine("PASS: drop/reject placement no/first/later/all/page>32 candidates; Draft/new/terminal/active; controlled SQL races; HTTPS ownership/CSRF/cancel race; approval order/event; real intermediate fault full rollback; counters=active");
        }
        catch(Exception error) { primaryFailure=error; throw; }
        finally
        {
            try
            {
                await using var db=factory();
                if(trigger)await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.DropPlacementTestFault");
                var ids=students.Select(s=>s.Id).ToArray();
                await db.WaitlistEntries.Where(w=>ids.Contains(w.StudentId)).ExecuteDeleteAsync();
                await EnrollmentReceiptTestCleanup.DeleteAsync(db,db.Enrollments.Where(e=>ids.Contains(e.StudentId)));
                await db.StudentCompletedCourses.Where(x=>ids.Contains(x.StudentId)).ExecuteDeleteAsync();
                await db.CoursePrerequisites.Where(x=>x.CourseId==c.Id).ExecuteDeleteAsync();
                await db.CourseOfferings.Where(o=>o.CourseId==c.Id).ExecuteDeleteAsync();
                await db.Students.Where(s=>ids.Contains(s.Id)).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(t=>t.Id==term.Id).ExecuteDeleteAsync();
                await db.Courses.Where(x=>x.Id==c.Id||x.Id==prerequisite.Id).ExecuteDeleteAsync();
            }
            catch(Exception cleanup)
            {
                Console.Error.WriteLine("CLEANUP drop-placement: "+cleanup.GetType().Name);
                if(primaryFailure is null) throw new CleanupFailure();
            }
        }
    }
}
