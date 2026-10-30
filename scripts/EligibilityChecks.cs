using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;
using UniversityRegistration.Specifications;

internal static class EligibilityChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Eligibility: " + label); }
    static void Throws<T>(Action action) where T : Exception
    { try { action(); throw new BehaviorFailure("Expected " + typeof(T).Name); } catch(T) { } }
    static WeeklyTimeSlot Slot(int start, int end, DayOfWeek day = DayOfWeek.Monday) => new(day,new(start,0),new(end,0));
    public static void Unit()
    {
        var a=Slot(9,10);
        foreach(var (b,expected) in new[]{(Slot(9,10,DayOfWeek.Tuesday),false),(Slot(10,11),false),
            (new WeeklyTimeSlot(DayOfWeek.Monday,new(9,30),new(10,30)),true),(Slot(8,11),true),(Slot(9,10),true)})
            Check(a.Overlaps(b)==expected && b.Overlaps(a)==expected,"symmetric half-open overlap");
        Throws<ArgumentOutOfRangeException>(()=>Slot(10,10));Throws<ArgumentOutOfRangeException>(()=>Slot(11,10));
        Throws<ArgumentOutOfRangeException>(()=>Slot(9,10,(DayOfWeek)7));Throws<ArgumentNullException>(()=>a.Overlaps(null!));
        var mutable=new List<WeeklyTimeSlot>{a}; var schedule=new OfferingSchedule(20,2,mutable);mutable.Clear();
        Check(schedule.Slots.Count==1,"defensive schedule copy");
        var scenario=new EligibilityScenarioBuilder(creditLimit:30)
            .Request(offeringId:20,termId:2,credits:7).WithSlots(20,[a]).Requires(20,[1])
            .Request(offeringId:10,termId:2,credits:7).WithSlots(10,[a,Slot(9,10,DayOfWeek.Wednesday)]).Requires(10,[2])
            .Existing(offeringId:99,termId:2,credits:24,status:EnrollmentStatus.Approved)
            .WithSlots(99,[a,Slot(9,10,DayOfWeek.Wednesday)])
            .Existing(offeringId:98,termId:3,credits:4,status:EnrollmentStatus.Submitted).WithSlots(98,[a])
            .Build();
        var result=new AcademicEligibility().Evaluate(scenario.Requested,scenario.CurrentCredits,scenario.Existing,scenario.CreditLimit);
        Check(!result.IsEligible && result.Failures.SequenceEqual(new AcademicRejection[]{
            new AcademicRejection.PrerequisiteNotSatisfied(20),new AcademicRejection.PrerequisiteNotSatisfied(10),
            new AcademicRejection.CreditLimitExceeded(2),new AcademicRejection.ScheduleConflict(20,99),
            new AcademicRejection.ScheduleConflict(20,10),new AcademicRejection.ScheduleConflict(10,99)}),"all failures, stable order, pair once and cross-term exclusion");
        var failures=new List<AcademicRejection>{new AcademicRejection.CreditLimitExceeded(1)};
        var copy=new AcademicEvaluation(failures); failures.Clear();Check(!copy.IsEligible,"result defensive copy");
        Throws<NotSupportedException>(()=>((IList<AcademicRejection>)copy.Failures).Clear());
        Check(new AcademicEligibility().Evaluate([new(new(1,1,[]),1,new([],[]))],new Dictionary<int,long>(),[],30).IsEligible,"empty known schedule allowed");
        Throws<ArgumentNullException>(()=>new AcademicEvaluation(null!));
        Console.WriteLine("PASS: pure weekly intervals, symmetry/boundaries/invalid inputs, all failure types/order/dedup and immutable result");
    }

    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send)
    {
        var tag="E15_"+Guid.NewGuid().ToString("N")[..8];
        var courses=new List<Course>();var terms=new List<AcademicTerm>();var offers=new List<CourseOffering>();var students=new List<Student>();
        Exception? failure=null;
        async Task<int> Student()
        {var s=new Student{StudentNumber=tag+students.Count,FullName="Synthetic schedule test"};students.Add(s);await using var db=factory();db.Add(s);await db.SaveChangesAsync();return s.Id;}
        async Task<string[]> Rows()
        {await using var db=factory();var ids=students.Select(s=>s.Id).ToArray();return (await db.Enrollments.Where(e=>ids.Contains(e.StudentId)).OrderBy(e=>e.Id).ToListAsync()).Select(e=>$"{e.Id}|{e.StudentId}|{e.CourseOfferingId}|{e.CreatedAtUtc:O}").ToArray();}
        async Task<JsonNode> Request(int student,int[] ids,string kind,int status)
        {
            var before=await Rows();
            var r=await send("POST",kind=="single"?"/enrollments":$"/students/{student}/enrollments/{kind}",
                kind=="single"?(object)new{studentId=student,courseOfferingId=ids.Single()}:new{courseOfferingIds=ids},null);
            Check(r.Status==status,$"{kind} student={student} expected={status} actual={r.Status}");
            if(status!=201 || kind=="eligibility") Check((await Rows()).SequenceEqual(before),"preview/rejection has no new or modified enrollments (fresh context)");
            else
            {
                Check((await Rows()).Length==before.Length+ids.Length,"successful enrollment count");
                var created = kind=="single" ? new[]{r.Body["id"]!.GetValue<int>()} : r.Body["items"]!.AsArray().Select(n=>n!["id"]!.GetValue<int>());
                foreach(var id in created) Check((await send("POST",$"/enrollments/{id}/submit",null,null)).Status==200,"activate schedule fixture");
            }
            return r.Body;
        }
        async Task AddSlots(int index,params (DayOfWeek Day,TimeOnly Start,TimeOnly End)[] values)
        {await using var db=factory();db.CourseOfferingTimeSlots.AddRange(values.Select(v=>new CourseOfferingTimeSlot{CourseOfferingId=offers[index].Id,Day=v.Day,StartsAt=v.Start,EndsAt=v.End}));await db.SaveChangesAsync();}
        void Codes(JsonNode body,params string[] codes) => Check(body["failures"]!.AsArray().Select(n=>n!["code"]!.GetValue<string>()).SequenceEqual(codes),"exact ordered failure codes");
        try
        {
            foreach(var credit in new[]{1,7,24,2}) courses.Add(new(){Code=tag+courses.Count,Name="Schedule fixture",Credits=credit});
            terms.AddRange(Enumerable.Range(0,2).Select(i=>new AcademicTerm{Name=tag+i,StartsOn=new(2026,1,1),EndsOn=new(2026,6,1)}));
            await using(var db=factory()){db.AddRange(courses);db.AddRange(terms);await db.SaveChangesAsync();}
            foreach(var (course,term) in new[]{(2,0),(1,0),(3,0),(3,0),(3,1),(3,0),(3,0)})
                offers.Add(new(){CourseId=courses[course].Id,AcademicTermId=terms[term].Id,SectionCode="S"+offers.Count,Capacity=40});
            await using(var db=factory()){db.AddRange(offers);await db.SaveChangesAsync();db.CoursePrerequisites.Add(new(){CourseId=courses[1].Id,PrerequisiteCourseId=courses[0].Id});await db.SaveChangesAsync();}
            await AddSlots(0,(DayOfWeek.Monday,new(9,0),new(10,0)),(DayOfWeek.Wednesday,new(9,0),new(10,0)));
            await AddSlots(1,(DayOfWeek.Monday,new(9,30),new(10,30)),(DayOfWeek.Wednesday,new(9,30),new(10,30)));
            await AddSlots(2,(DayOfWeek.Monday,new(10,0),new(11,0)));
            await AddSlots(3,(DayOfWeek.Tuesday,new(9,0),new(10,0)));
            await AddSlots(4,(DayOfWeek.Monday,new(9,0),new(10,0)));
            await AddSlots(5,(DayOfWeek.Monday,new(9,45),new(10,15)),(DayOfWeek.Wednesday,new(9,45),new(10,15)));
            var s=await Student();await Request(s,[offers[0].Id],"single",201);
            var preview=await Request(s,[offers[1].Id],"eligibility",200);
            Check(!preview["isEligible"]!.GetValue<bool>(),"ineligible preview");
            Codes(preview,"prerequisite_not_satisfied","credit_limit_exceeded","schedule_conflict");
            var reasons=preview["failures"]!.AsArray();
            Check(reasons[0]!["courseOfferingId"]!.GetValue<int>()==offers[1].Id && reasons[1]!["academicTermId"]!.GetValue<int>()==terms[0].Id &&
                reasons[2]!["courseOfferingId"]!.GetValue<int>()==offers[1].Id && reasons[2]!["conflictingCourseOfferingId"]!.GetValue<int>()==offers[0].Id,"all failure context IDs");
            foreach(var kind in new[]{"single","batch"})
            {var denied=await Request(s,[offers[1].Id],kind,400);Check(denied["title"]!.GetValue<string>()=="Kayıt koşulları sağlanmadı." && JsonNode.DeepEquals(denied["failures"],preview["failures"]),"same evaluation and new Problem Details contract");}
            await Request(s,[offers[2].Id],"single",201); // 10:00 boundary, 26 credits.
            await Request(s,[offers[3].Id,offers[4].Id,offers[6].Id],"batch",201); // other day/term/no schedule; term0 exactly30.

            var fresh=await Student();
            await using(var db=factory()){db.StudentCompletedCourses.Add(new(){StudentId=fresh,CourseId=courses[0].Id});await db.SaveChangesAsync();}
            var pair=await Request(fresh,[offers[5].Id,offers[1].Id],"eligibility",200);
            Codes(pair,"schedule_conflict");Check(pair["failures"]![0]!["courseOfferingId"]!.GetValue<int>()==offers[5].Id,"new-new role follows request");
            await Request(fresh,[offers[5].Id,offers[1].Id],"batch",400);
            var ready=await Request(fresh,[offers[2].Id,offers[6].Id],"eligibility",200);Codes(ready);
            Check(ready["isEligible"]!.GetValue<bool>(),"eligible preview");
            await AddSlots(6,(DayOfWeek.Monday,new(10,15),new(10,45)));
            Codes(await Request(fresh,[offers[2].Id,offers[6].Id],"batch",400),"schedule_conflict");
            await using(var db=factory())await db.CourseOfferingTimeSlots.Where(x=>x.CourseOfferingId==offers[6].Id).ExecuteDeleteAsync();
            await Request(fresh,[offers[2].Id,offers[6].Id],"batch",201);
            await Request(fresh,[offers[2].Id],"eligibility",409);
            var isolated=await Student();
            Check((await Request(isolated,[offers[5].Id],"eligibility",200))["isEligible"]!.GetValue<bool>(),"other student's schedule excluded");
            Check((await Request(isolated,[offers[0].Id,offers[4].Id],"eligibility",200))["isEligible"]!.GetValue<bool>(),"identical weekly times in different terms allowed");
            await using(var db=factory())
            {db.Enrollments.AddRange(StateChecks.Active(isolated,offers[2].Id),StateChecks.Active(isolated,offers[5].Id));await db.SaveChangesAsync();}
            Check((await Request(isolated,[offers[3].Id],"eligibility",200))["isEligible"]!.GetValue<bool>(),"existing-existing conflict is not a new rejection");
            await Request(int.MaxValue,[offers[2].Id],"eligibility",404);
            await Request(fresh,[int.MaxValue],"eligibility",400);
            foreach(var ids in new[]{Array.Empty<int>(),new[]{0},new[]{offers[1].Id,offers[1].Id},Enumerable.Range(1,51).ToArray()})
                await Request(fresh,ids,"eligibility",400);
            foreach(var raw in new[]{"{}","{\"courseOfferingIds\":null}","{\"courseOfferingIds\":\"wrong\"}"})
            {var before=await Rows();var invalid=await send("POST",$"/students/{fresh}/enrollments/eligibility",null,raw);Check(invalid.Status==400 && (await Rows()).SequenceEqual(before),"binding/shape preview rejection");}

            // Native TimeOnly mapping must preserve the least significant tick and day boundary.
            var precise=new TimeOnly(9,0).Add(TimeSpan.FromTicks(1));
            await AddSlots(4,(DayOfWeek.Friday,precise,precise.Add(TimeSpan.FromTicks(1))),
                (DayOfWeek.Sunday,TimeOnly.MinValue,TimeOnly.MaxValue));
            await using(var db=factory())
            {var stored=await db.CourseOfferingTimeSlots.SingleAsync(x=>x.CourseOfferingId==offers[4].Id && x.Day==DayOfWeek.Friday);Check(stored.StartsAt==precise && stored.EndsAt.Ticks==precise.Ticks+1,"time(7) exact 100ns roundtrip");
                var edge=await db.CourseOfferingTimeSlots.SingleAsync(x=>x.CourseOfferingId==offers[4].Id && x.Day==DayOfWeek.Sunday);Check(edge.StartsAt==TimeOnly.MinValue && edge.EndsAt==TimeOnly.MaxValue,"time day limits roundtrip");}
            async Task Constraint(CourseOfferingTimeSlot slot,int number,string name)
            {await using var db=factory();db.Add(slot);try{await db.SaveChangesAsync();throw new BehaviorFailure("Expected constraint "+name);}catch(DbUpdateException ex){Check(ex.InnerException is SqlException sql && sql.Number==number && sql.Message.Contains(name,StringComparison.Ordinal),"SQL number/object "+name);}}
            CourseOfferingTimeSlot S(int offering,DayOfWeek day,int start,int end,int id=0)=>new(){Id=id,CourseOfferingId=offering,Day=day,StartsAt=new(start,0),EndsAt=new(end,0)};
            await Constraint(S(offers[0].Id,(DayOfWeek)7,9,10),547,"CK_CourseOfferingTimeSlots_Day");
            await Constraint(S(offers[0].Id,(DayOfWeek)(-1),9,10),547,"CK_CourseOfferingTimeSlots_Day");
            foreach(var end in new[]{8,9}) await Constraint(S(offers[0].Id,DayOfWeek.Monday,9,end),547,"CK_CourseOfferingTimeSlots_Range");
            await Constraint(S(offers[0].Id,DayOfWeek.Monday,9,10),2601,"UX_CourseOfferingTimeSlots_Offering_Day_Range");
            await Constraint(S(int.MaxValue,DayOfWeek.Monday,9,10),547,"FK_CourseOfferingTimeSlots_CourseOfferings");
            int slotId;await using(var db=factory())slotId=await db.CourseOfferingTimeSlots.Select(x=>x.Id).FirstAsync();
            await Constraint(S(offers[0].Id,DayOfWeek.Saturday,9,10,slotId),2627,"PK_CourseOfferingTimeSlots");
            await using(var db=factory())
            {
                try{await db.CourseOfferings.Where(o=>o.Id==offers[1].Id).ExecuteDeleteAsync();throw new BehaviorFailure("Expected NO ACTION");}
                catch(SqlException ex){Check(ex.Number==547 && ex.Message.Contains("FK_CourseOfferingTimeSlots_CourseOfferings",StringComparison.Ordinal),"time slot NO ACTION delete");}
            }
            await using(var db=factory()) Check(await db.CourseOfferings.AnyAsync(o=>o.Id==offers[1].Id) && await db.CourseOfferingTimeSlots.AnyAsync(x=>x.CourseOfferingId==offers[1].Id),"NO ACTION preserves offering and slots");
            foreach(var column in new[]{"CourseOfferingId","Day","StartsAt","EndsAt"})
            {
                await using var db=factory();
                var sql=$"INSERT dbo.CourseOfferingTimeSlots(CourseOfferingId,Day,StartsAt,EndsAt) VALUES ({(column=="CourseOfferingId"?"NULL":offers[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture))},{(column=="Day"?"NULL":"1")},{(column=="StartsAt"?"NULL":"'09:00'")},{(column=="EndsAt"?"NULL":"'10:00'")})";
                try{await db.Database.ExecuteSqlRawAsync(sql);throw new BehaviorFailure("Expected NOT NULL "+column);}catch(SqlException ex){Check(ex.Number==515 && ex.Message.Contains(column,StringComparison.Ordinal),"NOT NULL "+column);}
            }
            // Unique only stops exact duplicates; overlapping different intervals are allowed in storage.
            await AddSlots(0,(DayOfWeek.Monday,new(9,15),new(9,45)));
            var queryStudent=await Student();
            await using(var db=factory()) {db.Enrollments.Add(StateChecks.Active(queryStudent,offers[0].Id));await db.SaveChangesAsync();}
            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                var result=await new EnrollmentOperations(db,new(30),DecoratorChecks.TimedEvaluator(), ObserverChecks.EmptyPublisher()).EvaluateBatchAsync(queryStudent,[offers[1].Id,offers[5].Id],default);
                var evaluated=((EligibilityResult.Evaluated)result).Evaluation;
                Check(evaluated.Failures.OfType<AcademicRejection.ScheduleConflict>().Count()==3,"one reason per pair despite multiple/overlapping slots");
                Check(!db.ChangeTracker.Entries().Any(),"projection preview has no tracked changes/entities");
                var reads=capture.Commands.Where(c=>c.Text.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)).ToArray();
                Check(reads.Length==8 && capture.Commands.Count==8,"eight bulk SELECTs and no writes in preview");
                var timeReads=reads.Where(c=>c.Text.Contains("CourseOfferingTimeSlots")).ToArray();
                Check(timeReads.Length==2 && timeReads.All(c=>c.Text.Contains("WHERE")) && timeReads.Any(c=>c.Text.Contains("StudentId") && c.Text.Contains("AcademicTermId") && c.Text.Contains("JOIN")),"bounded SQL slot projections, student and term filter");
            }
            Console.WriteLine("PASS: real eligibility HTTP all failures/order/pairs, boundary/day/term/no-slot, re-evaluation/no writes; time(7) tick roundtrip; PK/FK/CHECK/UNIQUE/NOT NULL; eight bounded SELECTs");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                var sids=students.Select(s=>s.Id).ToArray();var cids=courses.Select(c=>c.Id).ToArray();var tids=terms.Select(t=>t.Id).ToArray();var oids=offers.Select(o=>o.Id).ToArray();
                await using var db=factory();
                await db.CourseOfferingTimeSlots.Where(x=>oids.Contains(x.CourseOfferingId)).ExecuteDeleteAsync();
                await db.StudentCompletedCourses.Where(x=>sids.Contains(x.StudentId)).ExecuteDeleteAsync();
                await db.CoursePrerequisites.Where(x=>cids.Contains(x.CourseId)).ExecuteDeleteAsync();
                await db.Enrollments.Where(x=>sids.Contains(x.StudentId)).ExecuteDeleteAsync();
                await db.Students.Where(x=>sids.Contains(x.Id)).ExecuteDeleteAsync();
                await db.CourseOfferings.Where(x=>oids.Contains(x.Id)).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(x=>tids.Contains(x.Id)).ExecuteDeleteAsync();
                await db.Courses.Where(x=>cids.Contains(x.Id)).ExecuteDeleteAsync();
                Console.WriteLine("PASS: eligibility fixture cleanup in FK order");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP eligibility: "+ex.GetType().Name);if(failure is null)throw;}
        }
    }
}
