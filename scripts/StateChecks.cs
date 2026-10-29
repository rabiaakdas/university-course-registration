using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class StateChecks
{
    static void Check(bool ok,string message){if(!ok)throw new BehaviorFailure("State: "+message);}
    private sealed record Subject(Func<EnrollmentStatus> Status,Func<string?> Reason,Func<bool> Submit,Func<bool> Approve,Func<string?,bool> Reject);
    public static void Unit()
    {
        foreach(var experiment in new[]{false,true})
        {
            Subject New()
            {
                if(experiment){var e=new EnrollmentStateExperiment();return new(()=>e.Status,()=>e.RejectionReason,e.TrySubmit,e.TryApprove,e.TryReject);}
                var entity=new Enrollment();return new(()=>entity.Status,()=>entity.RejectionReason,entity.TrySubmit,entity.TryApprove,entity.TryReject);
            }
            foreach(var status in Enum.GetValues<EnrollmentStatus>())foreach(var action in new[]{"submit","approve","reject"})
            {
                var e=New();Check(e.Status()==EnrollmentStatus.Draft && e.Reason() is null,"new Draft");
                if(status!=EnrollmentStatus.Draft)Check(e.Submit(),"arrange Submitted");
                if(status==EnrollmentStatus.Approved)Check(e.Approve(),"arrange Approved");
                if(status==EnrollmentStatus.Rejected)Check(e.Reject(" Original "),"arrange Rejected");
                var oldReason=e.Reason();
                var expected=status==EnrollmentStatus.Draft && action=="submit" || status==EnrollmentStatus.Submitted && action!="submit";
                var actual=action switch{"submit"=>e.Submit(),"approve"=>e.Approve(),_=>e.Reject(" \tReddedildi\u3000 ")};
                Check(actual==expected,$"matrix experiment={experiment} {status}/{action}");
                if(!actual)Check(e.Status()==status && e.Reason()==oldReason,"invalid transition preserves state/reason");
                else Check(e.Status()==(action=="submit"?EnrollmentStatus.Submitted:action=="approve"?EnrollmentStatus.Approved:EnrollmentStatus.Rejected)
                    && e.Reason()==(action=="reject"?"Reddedildi":null),"successful transition/trim");
            }
            foreach(var reason in new string?[]{null,""," \t\r\n\u00a0\u3000",new('x',501)})
            {var e=New();e.Submit();Check(!e.Reject(reason) && e.Status()==EnrollmentStatus.Submitted && e.Reason() is null,"invalid reason preserves Submitted");}
            var boundary=New();boundary.Submit();Check(boundary.Reject(new('x',500)),"500 character reason allowed");
        }
        Check(!typeof(Enrollment).Assembly.GetTypes().Any(t=>t.Name==nameof(EnrollmentStateExperiment)),"State experiment excluded from production");
        Console.WriteLine("PASS: shared enum/State transition matrix, immutable-on-rejection, reason null/whitespace/length/trim; experiment outside web assembly (no DB)");
    }

    // Only fixture setup: a deliberately active pre-existing load, not academic authorization.
    public static Enrollment Active(int student,int offering)
    {var e=new Enrollment{StudentId=student,CourseOfferingId=offering,CreatedAtUtc=DateTimeOffset.UtcNow};Check(e.TrySubmit() && e.TryApprove(),"active fixture transitions");return e;}

    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        var tag="M16_"+Guid.NewGuid().ToString("N")[..10];
        var c=new Course{Code=tag,Name="Migration fixture",Credits=1};
        var t=new AcademicTerm{Name=tag,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)};
        var s=new Student{StudentNumber=tag,FullName="Synthetic migration student"};
        CourseOffering? o=null;Exception? failure=null;
        var stamp=DateTimeOffset.UtcNow;
        try
        {
            await using(var db=factory())
            {
                Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(m=>m.EndsWith("AddEnrollmentLifecycle")),"migration fixture must start before lifecycle; never downgrade");
                await db.GetService<IMigrator>().MigrateAsync("20261029122117_AddCourseOfferingTimeSlots");
                db.AddRange(c,t,s);await db.SaveChangesAsync();
                o=new(){CourseId=c.Id,AcademicTermId=t.Id,SectionCode="A",Capacity=40};db.Add(o);await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.Enrollments(StudentId,CourseOfferingId,CreatedAtUtc) VALUES({s.Id},{o.Id},{stamp})");
            }
            int oldId;
            await using(var db=factory())oldId=await db.Database.SqlQuery<int>($"SELECT Id AS Value FROM dbo.Enrollments WHERE StudentId={s.Id}").SingleAsync();
            await using(var db=factory()){await db.Database.MigrateAsync();await db.Database.MigrateAsync();}
            await using(var db=factory())
            {
                var old=await db.Enrollments.SingleAsync(e=>e.Id==oldId);
                Check(old.Status==EnrollmentStatus.Approved && old.RejectionReason is null && old.StudentId==s.Id && old.CourseOfferingId==o!.Id && old.CreatedAtUtc==stamp,"old row/IDs/time preserved as Approved; private setter loaded");
                Check(!db.Database.HasPendingModelChanges() && !(await db.Database.GetPendingMigrationsAsync()).Any(),"migration replay/model alignment");
            }
            Console.WriteLine("PASS: isolated previous-schema row migrated to Approved preserving Id/FKs/time; replay no-op and snapshot aligned");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                await using var db=factory();
                await db.Enrollments.Where(e=>e.StudentId==s.Id).ExecuteDeleteAsync();
                if(o is not null)await db.CourseOfferings.Where(x=>x.Id==o.Id).ExecuteDeleteAsync();
                await db.Students.Where(x=>x.Id==s.Id).ExecuteDeleteAsync();await db.AcademicTerms.Where(x=>x.Id==t.Id).ExecuteDeleteAsync();await db.Courses.Where(x=>x.Id==c.Id).ExecuteDeleteAsync();
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP lifecycle migration: "+ex.GetType().Name);if(failure is null)throw;}
        }
    }

    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send, Action stop,Func<Task> start)
    {
        var tag="S16_"+Guid.NewGuid().ToString("N")[..8];var courses=new List<Course>();var terms=new List<AcademicTerm>();var offers=new List<CourseOffering>();var students=new List<Student>();
        Exception? failure=null;
        async Task<int> Student(){var s=new Student{StudentNumber=tag+students.Count,FullName="Synthetic lifecycle student"};students.Add(s);await using var db=factory();db.Add(s);await db.SaveChangesAsync();return s.Id;}
        async Task<string[]> Rows(){await using var db=factory();var ids=students.Select(s=>s.Id).ToArray();return (await db.Enrollments.Where(e=>ids.Contains(e.StudentId)).OrderBy(e=>e.Id).ToListAsync()).Select(e=>$"{e.Id}|{e.StudentId}|{e.CourseOfferingId}|{e.CreatedAtUtc:O}|{e.Status}|{e.RejectionReason}").ToArray();}
        void Fields(JsonNode body,string status,string? reason=null)
        {Check(string.Join(',',body.AsObject().Select(p=>p.Key).Order())=="courseOfferingId,createdAtUtc,id,rejectionReason,status,studentId","response fields");Check(body["status"]!.GetValue<string>()==status && body["rejectionReason"]?.GetValue<string>()==reason,"response status/reason");}
        async Task<int> Create(int student,int offering)
        {var r=await send("POST","/enrollments",new{studentId=student,courseOfferingId=offers[offering].Id,status="Approved",rejectionReason="ignored"},null);Check(r.Status==201,"create Draft");Fields(r.Body,"Draft");Check(r.Location==$"/enrollments/{r.Body["id"]}","Location");return r.Body["id"]!.GetValue<int>();}
        async Task<JsonNode> Transition(int id,string action,int status,object? body=null,string? state=null,string? reason=null)
        {
            var before=await Rows();var r=await send("POST",$"/enrollments/{id}/{action}",body,null);
            Check(r.Status==status,$"transition {id}/{action}: expected={status} actual={r.Status}");
            if(status!=200)Check((await Rows()).SequenceEqual(before),"rejected transition preserves all rows/states/reasons");
            else
            {
                Fields(r.Body,state!,reason);await using var db=factory();var e=await db.Enrollments.SingleAsync(e=>e.Id==id);
                Check(e.Status.ToString()==state && e.RejectionReason==reason,"fresh context persisted private setters");
                var detail=await send("GET",$"/enrollments/{id}",null,null);Check(JsonNode.DeepEquals(detail.Body,r.Body),"detail/transition consistency");
            }
            return r.Body;
        }
        async Task<JsonNode> Preview(int student,int offering,bool eligible)
        {var before=await Rows();var r=await send("POST",$"/students/{student}/enrollments/eligibility",new{courseOfferingIds=new[]{offers[offering].Id}},null);Check(r.Status==200 && r.Body["isEligible"]!.GetValue<bool>()==eligible && (await Rows()).SequenceEqual(before),"status-aware read-only preview");return r.Body;}
        void Has(JsonNode body,string code)=>Check(body["failures"]!.AsArray().Any(f=>f!["code"]!.GetValue<string>()==code),"failure "+code);
        try
        {
            foreach(var credit in new[]{24,6,7,1})courses.Add(new(){Code=tag+courses.Count,Name="State fixture",Credits=credit});
            terms.AddRange(Enumerable.Range(0,2).Select(i=>new AcademicTerm{Name=tag+i,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)}));
            await using(var db=factory()){db.AddRange(courses);db.AddRange(terms);await db.SaveChangesAsync();}
            foreach(var (c,t) in new[]{(0,0),(1,0),(2,0),(3,0),(1,1),(1,0),(0,0)})offers.Add(new(){CourseId=courses[c].Id,AcademicTermId=terms[t].Id,SectionCode="S"+offers.Count,Capacity=40});
            await using(var db=factory())
            {db.AddRange(offers);await db.SaveChangesAsync();foreach(var i in new[]{0,1,5})db.CourseOfferingTimeSlots.Add(new(){CourseOfferingId=offers[i].Id,Day=DayOfWeek.Monday,StartsAt=new(9,i==0?0:30),EndsAt=new(10,i==0?0:30)});await db.SaveChangesAsync();}
            var student=await Student();var a=await Create(student,0);var b=await Create(student,1);var creditEnrollment=await Create(student,2);
            await Preview(student,5,true); // All three Drafts excluded from both load and schedule.
            await Preview(student,6,true); // Draft 24 does not contribute another 24-credit load.
            await Transition(a,"approve",409);await Transition(a,"reject",409,new{reason="Valid"});
            await Transition(a,"submit",200,state:"Submitted");await Transition(a,"submit",409);
            Has(await Preview(student,5,false),"schedule_conflict");
            Has(await Preview(student,6,false),"credit_limit_exceeded"); // Submitted contributes load.
            await Transition(a,"approve",200,state:"Approved"); // 24-credit target and own slot excluded once.
            await Transition(a,"approve",409);await Transition(a,"reject",409,new{reason="Valid"});
            Has(await Transition(b,"submit",400),"schedule_conflict");
            Has(await Transition(creditEnrollment,"submit",400),"credit_limit_exceeded");
            var deniedCreate=await send("POST","/enrollments",new{studentId=student,courseOfferingId=offers[5].Id},null);Check(deniedCreate.Status==400,"active load used in create");
            await using(var db=factory())Check(!await db.Enrollments.AnyAsync(e=>e.StudentId==student && e.CourseOfferingId==offers[5].Id),"create academic rejection writes nothing");

            // Submitted candidate must be rechecked against another active row, not just itself.
            var concurrent=await Student();var candidate=await Create(concurrent,0);await Transition(candidate,"submit",200,state:"Submitted");
            await using(var db=factory()){db.Enrollments.Add(Active(concurrent,offers[2].Id));db.Enrollments.Add(Active(concurrent,offers[5].Id));await db.SaveChangesAsync();}
            var approval=await Transition(candidate,"approve",400);Has(approval,"credit_limit_exceeded");Has(approval,"schedule_conflict");

            // Prerequisites can change after creation or submission; both operations re-evaluate.
            var prereq=await Student();var p=await Create(prereq,2);
            await using(var db=factory()){db.CoursePrerequisites.Add(new(){CourseId=courses[2].Id,PrerequisiteCourseId=courses[3].Id});await db.SaveChangesAsync();}
            Has(await Transition(p,"submit",400),"prerequisite_not_satisfied");
            await using(var db=factory()){db.StudentCompletedCourses.Add(new(){StudentId=prereq,CourseId=courses[3].Id});await db.SaveChangesAsync();}
            await Transition(p,"submit",200,state:"Submitted");
            await using(var db=factory())await db.StudentCompletedCourses.Where(x=>x.StudentId==prereq).ExecuteDeleteAsync();
            Has(await Transition(p,"approve",400),"prerequisite_not_satisfied");
            // Reject needs no academic eligibility, even when it would fail now.
            await Transition(p,"reject",200,new{reason="  Eksik ön koşul  "},"Rejected","Eksik ön koşul");

            var rejected=await Student();var r=await Create(rejected,0);await Transition(r,"submit",200,state:"Submitted");
            foreach(var reason in new string?[]{null,""," \t\u00a0",new('x',501)})
            {var invalid=await Transition(r,"reject",400,new{reason});Check(invalid["errors"]?["reason"] is not null,"reason field validation");}
            await Transition(r,"reject",200,new{reason="  Başka seçim \t"},"Rejected","Başka seçim");
            await Preview(rejected,6,true); // Rejected24 is excluded from credit, as well as schedule.
            foreach(var action in new[]{"submit","approve","reject"})await Transition(r,action,409,action=="reject"?new{reason="Other"}:null);
            await Preview(rejected,1,true);var replacement=await Create(rejected,1);await Transition(replacement,"submit",200,state:"Submitted");await Transition(replacement,"approve",200,state:"Approved");
            var duplicate=await send("POST","/enrollments",new{studentId=rejected,courseOfferingId=offers[0].Id},null);Check(duplicate.Status==409,"Rejected still prevents duplicate pair");
            await using(var db=factory())Check(await db.Enrollments.CountAsync(e=>e.StudentId==rejected && e.CourseOfferingId==offers[0].Id)==1,"Rejected duplicate leaves single row");
            foreach(var action in new[]{"submit","approve","reject"})await Transition(int.MaxValue,action,404,action=="reject"?new{reason="Valid"}:null);
            await Transition(int.MaxValue,"reject",400,new{reason=" "}); // Documented validation-before-existence.

            var batchStudent=await Student();var beforeBatch=await Rows();
            var conflict=await send("POST",$"/students/{batchStudent}/enrollments/batch",new{courseOfferingIds=new[]{offers[0].Id,offers[1].Id}},null);
            Check(conflict.Status==400 && (await Rows()).SequenceEqual(beforeBatch),"Draft batch still evaluates own schedule");
            var creditBatch=await send("POST",$"/students/{batchStudent}/enrollments/batch",new{courseOfferingIds=new[]{offers[0].Id,offers[1].Id,offers[5].Id}},null);
            Check(creditBatch.Status==400 && (await Rows()).SequenceEqual(beforeBatch),"Draft batch still evaluates own credit total");Has(creditBatch.Body,"credit_limit_exceeded");
            var batch=await send("POST",$"/students/{batchStudent}/enrollments/batch",new{courseOfferingIds=new[]{offers[3].Id,offers[4].Id}},null);
            Check(batch.Status==201,"valid Draft batch");foreach(var item in batch.Body["items"]!.AsArray())Fields(item!,"Draft");
            stop();await start();
            foreach(var (id,status,reason) in new[]{(a,"Approved",(string?)null),(candidate,"Submitted",(string?)null),(r,"Rejected","Başka seçim"),(b,"Draft",(string?)null)})
            {var detail=await send("GET",$"/enrollments/{id}",null,null);Check(detail.Status==200,"restart detail");Fields(detail.Body,status,reason);}
            var list=await send("GET",$"/students/{rejected}/enrollments",null,null);
            Check(list.Status==200 && list.Body.AsArray().Single(n=>n!["id"]!.GetValue<int>()==r)!["rejectionReason"]!.GetValue<string>()=="Başka seçim" && list.Body.AsArray().All(n=>n!.AsObject().ContainsKey("status")),"student list lifecycle contract");

            // SQL checks protect valid row states, not transition history. Use only this fixture row.
            async Task BadSql(int? status,string? reason,int error,string objectName)
            {
                var before=await Rows();await using var db=factory();
                try{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Enrollments SET Status={status}, RejectionReason={reason} WHERE Id={b}");throw new BehaviorFailure("Expected SQL "+objectName);}
                catch(SqlException ex){Check(ex.Number==error && ex.Message.Contains(objectName,StringComparison.Ordinal),"exact SQL constraint "+objectName);}
                Check((await Rows()).SequenceEqual(before),"failed SQL leaves rows intact");
            }
            foreach(var bad in new[]{-1,4})await BadSql(bad,null,547,"CK_Enrollments_Status");
            await BadSql(null,null,515,"Status");
            foreach(var reason in new string?[]{null,"","   ","\t\r\n","\u00a0\u3000"})await BadSql(3,reason,547,"CK_Enrollments_RejectionReason");
            foreach(var status in new[]{0,1,2})await BadSql(status,"Reason",547,"CK_Enrollments_RejectionReason");
            await using(var db=factory())
            {try{await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Enrollments SET Status=3, RejectionReason={new string('x',501)} WHERE Id={b}");throw new BehaviorFailure("Expected nvarchar length");}catch(SqlException ex){Check(ex.Number is 2628 or 8152,"SQL length class");}}
            await using(var db=factory())await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Enrollments SET Status=2 WHERE Id={b}");
            await using(var db=factory())Check((await db.Enrollments.SingleAsync(e=>e.Id==b)).Status==EnrollmentStatus.Approved,"CHECK permits direct Draft-to-Approved SQL; history not enforced");

            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                await new EnrollmentOperations(db,new(30)).EvaluateBatchAsync(rejected,[offers[4].Id],default);
                var reads=capture.Commands.Where(c=>c.Text.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)).ToArray();
                Check(reads.Length==8 && reads.Where(c=>c.Text.Contains("SUM(") || c.Text.Contains("CourseOfferingTimeSlots") && c.Text.Contains("Enrollments")).All(c=>c.Text.Contains("Status")),"bounded bulk SQL with active status filters");
            }
            Console.WriteLine("PASS: lifecycle HTTP/SQL/private setters/restart, state-aware credits/schedules, submit+approve re-evaluation/self-only exclusion, rejected uniqueness, Draft batch checks and eight bulk reads");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                var sids=students.Select(s=>s.Id).ToArray();var cids=courses.Select(c=>c.Id).ToArray();var tids=terms.Select(t=>t.Id).ToArray();var oids=offers.Select(o=>o.Id).ToArray();await using var db=factory();
                await db.Enrollments.Where(e=>sids.Contains(e.StudentId)).ExecuteDeleteAsync();await db.CourseOfferingTimeSlots.Where(x=>oids.Contains(x.CourseOfferingId)).ExecuteDeleteAsync();
                await db.StudentCompletedCourses.Where(x=>sids.Contains(x.StudentId)).ExecuteDeleteAsync();await db.CoursePrerequisites.Where(x=>cids.Contains(x.CourseId)).ExecuteDeleteAsync();
                await db.Students.Where(x=>sids.Contains(x.Id)).ExecuteDeleteAsync();await db.CourseOfferings.Where(x=>oids.Contains(x.Id)).ExecuteDeleteAsync();await db.AcademicTerms.Where(x=>tids.Contains(x.Id)).ExecuteDeleteAsync();await db.Courses.Where(x=>cids.Contains(x.Id)).ExecuteDeleteAsync();
                Console.WriteLine("PASS: lifecycle fixture cleaned in FK order");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP lifecycle: "+ex.GetType().Name);if(failure is null)throw;}
        }
    }
}
