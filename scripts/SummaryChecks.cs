using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

internal static class SummaryChecks
{
    static void Check(bool value, string label) { if (!value) throw new BehaviorFailure("Summary: " + label); }
    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send, bool probe = false, Func<int,Task>? prepareIdentity = null, CatalogTestIdentity? identity = null)
    {
        var tag="S19_"+Guid.NewGuid().ToString("N")[..8];
        var students=new List<Student>();var terms=new List<AcademicTerm>();var courses=new List<Course>();
        var offerings=new List<CourseOffering>();var enrollments=new List<Enrollment>();var slots=new List<CourseOfferingTimeSlot>();
        Exception? failure=null;
        Enrollment NewEnrollment(int student,int offering,EnrollmentStatus status)
        {
            var e=new Enrollment{StudentId=student,CourseOfferingId=offering,CreatedAtUtc=DateTimeOffset.UtcNow};
            if(status!=EnrollmentStatus.Draft)Check(e.TrySubmit(),"fixture submit");
            if(status==EnrollmentStatus.Approved)Check(e.TryApprove(),"fixture approve");
            if(status==EnrollmentStatus.Rejected)Check(e.TryReject(" Synthetic reason "),"fixture reject");
            if(status==EnrollmentStatus.Dropped)Check(e.TryDrop(),"fixture drop");
            enrollments.Add(e);return e;
        }
        void Slot(int offering,DayOfWeek day,TimeOnly start,TimeOnly end)=>slots.Add(new(){CourseOfferingId=offering,Day=day,StartsAt=start,EndsAt=end});
        string Path(int student,int term)=>$"/students/{student}/terms/{term}/registration-summary";
        async Task<JsonNode> Get(int student,int term,int expected=200)
        {var r=await send("GET",Path(student,term),null,null);Check(r.Status==expected,$"HTTP expected {expected}, actual {r.Status}");return r.Body;}
        async Task<string> Snapshot()
        {
            await using var db=factory();var sids=students.Select(s=>s.Id).ToArray();var tids=terms.Select(t=>t.Id).ToArray();var cids=courses.Select(c=>c.Id).ToArray();var oids=offerings.Select(o=>o.Id).ToArray();
            return JsonSerializer.Serialize(new {
                Students=await db.Students.AsNoTracking().Where(s=>sids.Contains(s.Id)).OrderBy(s=>s.Id).ToListAsync(),
                Terms=await db.AcademicTerms.AsNoTracking().Where(t=>tids.Contains(t.Id)).OrderBy(t=>t.Id).ToListAsync(),
                Courses=await db.Courses.AsNoTracking().Where(c=>cids.Contains(c.Id)).OrderBy(c=>c.Id).ToListAsync(),
                Offerings=await db.CourseOfferings.AsNoTracking().Where(o=>oids.Contains(o.Id)).OrderBy(o=>o.Id).ToListAsync(),
                Enrollments=await db.Enrollments.AsNoTracking().Where(e=>sids.Contains(e.StudentId)).OrderBy(e=>e.Id).ToListAsync(),
                Slots=await db.CourseOfferingTimeSlots.AsNoTracking().Where(s=>oids.Contains(s.CourseOfferingId)).OrderBy(s=>s.Id).ToListAsync() });
        }
        async Task InspectSql(int student,int term,int expectedReads)
        {
            await using var template=factory();var capture=new CatalogSqlCapture();
            await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
            var result=await new TermRegistrationSummaryFacade(db).GetAsync(student,term,default);
            Check(result is TermRegistrationSummaryResult.Found,"facade summary");
            Check(!db.ChangeTracker.Entries().Any(),"scalar projection tracks no entity");
            Check(capture.Commands.Count==expectedReads && capture.Commands.All(c=>c.Text.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)),"bounded read-only SQL");
            var query=capture.Commands.Single(c=>c.Text.Contains("JOIN")&&c.Text.Contains("Courses")&&!c.Text.Contains("SUM("));
            Check(query.Text.Contains("WHERE")&&query.Text.Contains("StudentId")&&query.Text.Contains("AcademicTermId")&&query.Text.Contains("ORDER BY")&&!query.Text.Contains("CreatedAtUtc"),"filtered ordered enrollment projection");
            Check(!query.Text.Contains("CourseOfferingTimeSlots") && !query.Text.Contains("RowVersion")
                && !query.Text.Contains("Capacity") && !query.Text.Contains("OccupiedSeats")
                && !query.Text.Contains("FullName") && !query.Text.Contains("UserId"),"enrollment projection avoids entity/private/unused fields and slot fan-out");
            Check(capture.Commands.Single(c=>c.Text==query.Text).Parameters.Any(p=>p is int n && n==student)
                && capture.Commands.Single(c=>c.Text==query.Text).Parameters.Any(p=>p is int n && n==term),"student/term SQL parameters");
            var sum=capture.Commands.Single(c=>c.Text.Contains("SUM(")).Text;
            Check(sum.Contains("bigint") && sum.Contains("WHERE") && sum.Contains("Status") && !sum.Contains("CourseOfferingTimeSlots"),"active SQL credit aggregate without slot multiplication");
            if(expectedReads==5)
            {
                var schedule=capture.Commands.Single(c=>c.Text.Contains("CourseOfferingTimeSlots"));
                Check(schedule.Text.Contains("WHERE")&&schedule.Text.Contains("ORDER BY")&&schedule.Text.Contains("%"),"filtered SQL Monday-first slot order");
                var projection=schedule.Text[..schedule.Text.IndexOf("FROM",StringComparison.OrdinalIgnoreCase)];
                Check(new[]{"CourseOfferingId","Day","StartsAt","EndsAt"}.All(projection.Contains)
                    && !projection.Contains("JOIN") && !projection.Contains("Status") && !projection.Contains("StudentId")
                    && !projection.Contains("Name") && !projection.Contains("].[Id]"),"only four slot DTO columns selected (Id only orders)");
            }
            var data=((TermRegistrationSummaryResult.Found)result).Summary;
            Console.WriteLine($"MEASURE summary: enrollments={data.Enrollments.Count}; schedule={data.ActiveSchedule.Count}; commands={capture.Commands.Count}; tracked={db.ChangeTracker.Entries().Count()}");
        }
        try
        {
            students.AddRange(Enumerable.Range(0,3).Select(i=>new Student{StudentNumber=tag+i,FullName="Synthetic summary student"}));
            terms.AddRange(Enumerable.Range(0,2).Select(i=>new AcademicTerm{Name=tag+i,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)}));
            courses.AddRange(new[]{3,4,5,6}.Select((credit,i)=>new Course{Code=tag+i,Name="Özet dersi "+i,Credits=credit}));
            await using(var db=factory()){db.AddRange(students);db.AddRange(terms);db.AddRange(courses);await db.SaveChangesAsync();}
            for(var i=0;i<4;i++)offerings.Add(new(){CourseId=courses[i].Id,AcademicTermId=terms[0].Id,SectionCode="A",Capacity=40});
            offerings.Add(new(){CourseId=courses[1].Id,AcademicTermId=terms[1].Id,SectionCode="A",Capacity=40});
            await using(var db=factory()){db.AddRange(offerings);await db.SaveChangesAsync();}
            for(var i=0;i<4;i++)NewEnrollment(students[0].Id,offerings[i].Id,(EnrollmentStatus)i);
            NewEnrollment(students[1].Id,offerings[1].Id,EnrollmentStatus.Approved);
            NewEnrollment(students[0].Id,offerings[4].Id,EnrollmentStatus.Approved);
            var precise=new TimeOnly(9,0).Add(TimeSpan.FromTicks(1234567));
            Slot(offerings[1].Id,DayOfWeek.Sunday,new(8,0),new(9,0));
            Slot(offerings[1].Id,DayOfWeek.Monday,precise,new(10,0));
            Slot(offerings[1].Id,DayOfWeek.Monday,new(8,0),new(9,0));
            Slot(offerings[1].Id,DayOfWeek.Monday,precise,new(9,30));
            Slot(offerings[0].Id,DayOfWeek.Monday,new(6,0),new(7,0));
            Slot(offerings[3].Id,DayOfWeek.Tuesday,new(6,0),new(7,0));
            Slot(offerings[4].Id,DayOfWeek.Wednesday,new(6,0),new(7,0));
            await using(var db=factory()){db.AddRange(enrollments);db.AddRange(slots);await SeatCapacityChecks.SaveFixtureAsync(db);}
            if (prepareIdentity is not null) foreach (var student in students) await prepareIdentity(student.Id);
            HttpClient? advisor=null;
            if(identity is not null)
                advisor=await identity.ForRequestAsync("POST",$"/enrollments/{enrollments[0].Id}/approve",null,null);
            var before=await Snapshot();var summary=await Get(students[0].Id,terms[0].Id);
            if(identity is not null)
            {
                async Task Access(HttpClient client,int student,int expected)
                {
                    using var response=await client.GetAsync(Path(student,terms[0].Id));
                    Check((int)response.StatusCode==expected,"summary access boundary "+expected);
                    if(expected==200)Check(response.Headers.CacheControl?.NoStore==true,"summary no-store");
                }
                var owner=await identity.ForRequestAsync("GET",Path(students[0].Id,terms[0].Id),null,null);
                using var anonymous=identity.AnonymousClient();
                await Access(anonymous,students[0].Id,401);await Access(identity.Client,students[0].Id,403);
                await Access(owner,students[0].Id,200);await Access(owner,students[1].Id,404);
                await Access(advisor!,students[0].Id,200);await Access(advisor!,students[1].Id,404);
            }
            Check(summary["studentId"]!.GetValue<int>()==students[0].Id&&summary["academicTermId"]!.GetValue<int>()==terms[0].Id,"identity");
            Check(summary["activeCredits"]!.GetValue<long>()==9,"multi-slot course counted once, active policy");
            Check(summary["counts"]!.AsObject().Count==5&&summary["counts"]!["dropped"]!.GetValue<int>()==0&&summary["counts"]!.AsObject().Where(p=>p.Key!="dropped").All(p=>p.Value!.GetValue<int>()==1),"four exact status counts");
            var rows=summary["enrollments"]!.AsArray();Check(rows.Count==4,"other student/term excluded");
            Check(rows.Select(n=>n!["id"]!.GetValue<int>()).SequenceEqual(enrollments.Take(4).Select(e=>e.Id).Order()),"enrollment ID order");
            foreach(var row in rows)
            {
                var e=enrollments.Single(e=>e.Id==row!["id"]!.GetValue<int>());var o=offerings.Single(o=>o.Id==e.CourseOfferingId);var c=courses.Single(c=>c.Id==o.CourseId);
                Check(row!.AsObject().Select(p=>p.Key).Order().SequenceEqual(new[]{"id","courseOfferingId","courseCode","courseName","credits","status","rejectionReason"}.Order()),"exact enrollment fields");
                Check(row["courseOfferingId"]!.GetValue<int>()==o.Id&&row["courseCode"]!.GetValue<string>()==c.Code&&row["courseName"]!.GetValue<string>()==c.Name&&row["credits"]!.GetValue<int>()==c.Credits&&row["status"]!.GetValue<string>()==e.Status.ToString()&&row["rejectionReason"]?.GetValue<string>()==e.RejectionReason,"all row data");
            }
            var schedule=summary["activeSchedule"]!.AsArray();var expectedSlots=slots.Where(s=>s.CourseOfferingId==offerings[1].Id).OrderBy(s=>((int)s.Day+6)%7).ThenBy(s=>s.StartsAt).ThenBy(s=>s.EndsAt).ThenBy(s=>s.CourseOfferingId).ThenBy(s=>s.Id).ToArray();
            Check(schedule.Count==4,"only active target schedule");
            for(var i=0;i<schedule.Count;i++)Check(schedule[i]!["day"]!.GetValue<string>()==expectedSlots[i].Day.ToString()&&schedule[i]!["startsAt"]!.Deserialize<TimeOnly>()==expectedSlots[i].StartsAt&&schedule[i]!["endsAt"]!.Deserialize<TimeOnly>()==expectedSlots[i].EndsAt&&schedule[i]!["courseOfferingId"]!.GetValue<int>()==expectedSlots[i].CourseOfferingId,"Monday/time/end order and tick-exact JSON");
            Check(summary["unscheduledActiveOfferingIds"]!.AsArray().Select(n=>n!.GetValue<int>()).SequenceEqual(new[]{offerings[2].Id}),"active missing schedule");
            Check(JsonNode.DeepEquals(summary,await Get(students[0].Id,terms[0].Id)),"repeat deterministic");
            var empty=await Get(students[2].Id,terms[0].Id);Check(empty["activeCredits"]!.GetValue<long>()==0&&empty["counts"]!.AsObject().All(p=>p.Value!.GetValue<int>()==0)&&new[]{"enrollments","activeSchedule","unscheduledActiveOfferingIds"}.All(k=>empty[k]!.AsArray().Count==0),"existing resources empty summary");
            await InspectSql(students[0].Id,terms[0].Id,5);await InspectSql(students[2].Id,terms[0].Id,4);
            Check(before==await Snapshot(),"HTTP and fresh-context facade did not change any fixture row/state");
            await using(var db=factory())Check(!await db.Students.AnyAsync(s=>s.Id==int.MaxValue)&&!await db.AcademicTerms.AnyAsync(t=>t.Id==int.MaxValue),"missing IDs verified");
            await Get(int.MaxValue,terms[0].Id,404);await Get(students[0].Id,int.MaxValue,404);
            await Get(0,-1,404); var invalid=await Get(students[0].Id,-1,400);Check(invalid["errors"]!["termId"] is not null,"authorized term field validation");
            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                var facade=new TermRegistrationSummaryFacade(db);
                Check(await facade.GetAsync(int.MaxValue,int.MaxValue,default) is TermRegistrationSummaryResult.StudentMissing&&capture.Commands.Count==1,"student missing wins, no term query");
                capture.Commands.Clear();Check(await facade.GetAsync(students[0].Id,int.MaxValue,default) is TermRegistrationSummaryResult.TermMissing&&capture.Commands.Count==2,"term missing second");
                capture.Commands.Clear();var bad=await TermRegistrationSummaryEndpoint.GetAsync(0,0,facade,default);Check(((IStatusCodeHttpResult)bad).StatusCode==400&&capture.Commands.Count==0,"invalid IDs avoid DB");
            }
            // Inactive-only path still returns enrollment data but does not query slots.
            await using(var db=factory()){db.Add(NewEnrollment(students[2].Id,offerings[0].Id,EnrollmentStatus.Draft));await db.SaveChangesAsync();}
            var inactiveBefore=await Snapshot();await InspectSql(students[2].Id,terms[0].Id,4);
            var inactive=await Get(students[2].Id,terms[0].Id);
            Check(inactive["counts"]!["draft"]!.GetValue<int>()==1&&inactive["activeCredits"]!.GetValue<long>()==0&&inactive["activeSchedule"]!.AsArray().Count==0&&inactive["unscheduledActiveOfferingIds"]!.AsArray().Count==0,"inactive-only with known slots excludes schedule");
            Check(inactiveBefore==await Snapshot(),"inactive read unchanged");
            // Active enrollment with no slots still performs ONE empty slot query.
            await using(var db=factory()){db.Add(NewEnrollment(students[2].Id,offerings[2].Id,EnrollmentStatus.Approved));await SeatCapacityChecks.SaveFixtureAsync(db);}
            var noSlotsBefore=await Snapshot();await InspectSql(students[2].Id,terms[0].Id,5);
            var noSlots=await Get(students[2].Id,terms[0].Id);
            Check(noSlots["activeCredits"]!.GetValue<long>()==5 && noSlots["activeSchedule"]!.AsArray().Count==0
                && noSlots["unscheduledActiveOfferingIds"]!.AsArray().Single()!.GetValue<int>()==offerings[2].Id,"active but unscheduled contract");
            Check(noSlotsBefore==await Snapshot(),"unscheduled read unchanged");

            // More rows, including two sections of the same course and totals beyond Int32.
            var large=new Course{Code=tag+"L",Name="Wide credit fixture",Credits=int.MaxValue};courses.Add(large);
            await using(var db=factory()){db.Add(large);await db.SaveChangesAsync();}
            var extra=Enumerable.Range(0,20).Select(i=>new CourseOffering{CourseId=i<2?large.Id:courses[1].Id,AcademicTermId=terms[0].Id,SectionCode="X"+i,Capacity=40}).ToArray();offerings.AddRange(extra);
            await using(var db=factory()){db.AddRange(extra);await db.SaveChangesAsync();db.AddRange(extra.Select(o=>NewEnrollment(students[0].Id,o.Id,EnrollmentStatus.Approved)).ToArray());await SeatCapacityChecks.SaveFixtureAsync(db);}
            var tieStart=slots.Count;
            Slot(extra[1].Id,DayOfWeek.Monday,new(8,0),new(9,0));
            Slot(extra[0].Id,DayOfWeek.Monday,new(8,0),new(9,0));
            await using(var db=factory()){db.AddRange(slots.Skip(tieStart));await db.SaveChangesAsync();}
            var expandedBefore=await Snapshot();await InspectSql(students[0].Id,terms[0].Id,5);
            var expanded=await Get(students[0].Id,terms[0].Id);Check(expanded["enrollments"]!.AsArray().Count==24&&expanded["activeCredits"]!.GetValue<long>()==9+2L*int.MaxValue+18L*4,"same course sections each count, checked long total");
            var unscheduled=expanded["unscheduledActiveOfferingIds"]!.AsArray().Select(n=>n!.GetValue<int>()).ToArray();Check(unscheduled.Length==19&&unscheduled.SequenceEqual(unscheduled.Order()),"sorted unscheduled IDs");
            Check(expanded["activeSchedule"]!.AsArray().Take(3).Select(n=>n!["courseOfferingId"]!.GetValue<int>()).SequenceEqual(new[]{offerings[1].Id,extra[0].Id,extra[1].Id}.Order()),"equal weekday/start/end ordered by offering ID");
            var expectedExpanded=expectedSlots.Concat(slots.Skip(tieStart)).OrderBy(s=>((int)s.Day+6)%7)
                .ThenBy(s=>s.StartsAt).ThenBy(s=>s.EndsAt).ThenBy(s=>s.CourseOfferingId).ThenBy(s=>s.Id).ToArray();
            var expandedSlots=expanded["activeSchedule"]!.AsArray();
            Check(expandedSlots.Count==6,"all six expanded slots, no duplication or omission");
            for(var i=0;i<expectedExpanded.Length;i++)
                Check(expandedSlots[i]!["courseOfferingId"]!.GetValue<int>()==expectedExpanded[i].CourseOfferingId
                    && expandedSlots[i]!["day"]!.GetValue<string>()==expectedExpanded[i].Day.ToString()
                    && expandedSlots[i]!["startsAt"]!.Deserialize<TimeOnly>()==expectedExpanded[i].StartsAt
                    && expandedSlots[i]!["endsAt"]!.Deserialize<TimeOnly>()==expectedExpanded[i].EndsAt,"expanded exact slot identity/time");
            Check(expandedBefore==await Snapshot(),"expanded read unchanged in fresh context");
            var droppedOffering=new CourseOffering{CourseId=courses[0].Id,AcademicTermId=terms[0].Id,SectionCode="DROP",Capacity=40};
            offerings.Add(droppedOffering);
            await using(var db=factory()){db.Add(droppedOffering);await db.SaveChangesAsync();db.Add(NewEnrollment(students[0].Id,droppedOffering.Id,EnrollmentStatus.Dropped));await db.SaveChangesAsync();}
            Slot(droppedOffering.Id,DayOfWeek.Friday,new(14,0),new(15,0));
            await using(var db=factory()){db.Add(slots.Last());await db.SaveChangesAsync();}
            var droppedBefore=await Snapshot();await InspectSql(students[0].Id,terms[0].Id,5);
            var dropped=await Get(students[0].Id,terms[0].Id);
            Check(dropped["enrollments"]!.AsArray().Count==25 && dropped["counts"]!["dropped"]!.GetValue<int>()==1
                && dropped["activeCredits"]!.GetValue<long>()==9+2L*int.MaxValue+18L*4
                && dropped["activeSchedule"]!.AsArray().Count==6
                && !dropped["activeSchedule"]!.AsArray().Any(n=>n!["courseOfferingId"]!.GetValue<int>()==droppedOffering.Id)
                && !dropped["unscheduledActiveOfferingIds"]!.AsArray().Any(n=>n!.GetValue<int>()==droppedOffering.Id),"Dropped listed but consumes no schedule or load");
            Check(droppedBefore==await Snapshot(),"Dropped read unchanged");
            if(probe)Check(false,"controlled failure propagation");
            Console.WriteLine("PASS: summary real HTTP/SQL identities/statuses/9 credits/no multiplication; active-only schedule and unscheduled IDs; Monday/tick precision; 404/400/empty/repeat; five reads at 4 and 24 rows, four when empty; projection untracked; fresh-context no-write and Int64 total");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                await using var db=factory();var sids=students.Select(s=>s.Id).ToArray();var oids=offerings.Select(o=>o.Id).ToArray();var tids=terms.Select(t=>t.Id).ToArray();var cids=courses.Select(c=>c.Id).ToArray();
                await db.Enrollments.Where(e=>sids.Contains(e.StudentId)).ExecuteDeleteAsync();await db.CourseOfferingTimeSlots.Where(s=>oids.Contains(s.CourseOfferingId)).ExecuteDeleteAsync();
                await db.Students.Where(s=>sids.Contains(s.Id)).ExecuteDeleteAsync();await db.CourseOfferings.Where(o=>oids.Contains(o.Id)).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(t=>tids.Contains(t.Id)).ExecuteDeleteAsync();await db.Courses.Where(c=>cids.Contains(c.Id)).ExecuteDeleteAsync();
                Console.WriteLine("PASS: summary fixture cleanup in FK order");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP summary: "+ex.GetType().Name);if(failure is null)throw;}
        }
    }
}
