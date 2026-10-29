using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;
using UniversityRegistration.Specifications;

internal static class SpecificationChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Specification: " + label); }
    static void Throws<T>(Action action, string? parameter = null) where T : ArgumentException
    {
        try { action(); throw new BehaviorFailure("Expected " + typeof(T).Name); }
        catch (T ex) { if (parameter is not null) Check(ex.ParamName == parameter, "exception parameter " + parameter); }
    }
    public static void Unit()
    {
        var prerequisites = new PrerequisitesSatisfiedSpecification();
        Check(prerequisites.IsSatisfiedBy(new([],[])), "empty prerequisites");
        Check(prerequisites.IsSatisfiedBy(new([1],[1])), "one completed");
        Check(!prerequisites.IsSatisfiedBy(new([1,2],[1])), "one missing");
        Check(prerequisites.IsSatisfiedBy(new([1,2],[1,2,3])), "all and extra completed");
        var required = new List<int>{1}; var completed = new List<int>{1};
        var copy = new PrerequisiteInput(required,completed); required.Add(2); completed.Clear();
        Check(prerequisites.IsSatisfiedBy(copy), "defensive copies");
        Throws<ArgumentNullException>(()=>new PrerequisiteInput(null!,[]),"requiredCourseIds");
        Throws<ArgumentNullException>(()=>new PrerequisiteInput([],null!),"completedCourseIds");
        Throws<ArgumentNullException>(()=>prerequisites.IsSatisfiedBy(null!),"input");
        Throws<ArgumentOutOfRangeException>(()=>new PrerequisiteInput([0],[]),"requiredCourseIds");
        Throws<ArgumentOutOfRangeException>(()=>new PrerequisiteInput([],[-1]),"completedCourseIds");
        var credit = new CreditLimitSpecification();
        foreach(var (current,requested,limit,expected) in new (long,long,long,bool)[] {
            (24,5,30,true),(24,6,30,true),(24,7,30,false),(31,0,30,false),(0,0,30,true),
            (long.MaxValue-1,1,long.MaxValue,true),(long.MaxValue,1,long.MaxValue,false),
            (1,long.MaxValue,long.MaxValue,false),(0,long.MaxValue,long.MaxValue,true) })
            Check(credit.IsSatisfiedBy(new(current,requested,limit))==expected,"credit boundary");
        Throws<ArgumentOutOfRangeException>(()=>new CreditLoad(-1,0,30),"currentCredits");
        Throws<ArgumentOutOfRangeException>(()=>new CreditLoad(0,-1,30),"requestedCredits");
        foreach(var limit in new long[]{0,-1}) Throws<ArgumentOutOfRangeException>(()=>new CreditLoad(0,0,limit),"creditLimit");
        Throws<ArgumentNullException>(()=>credit.IsSatisfiedBy(null!),"input");
        Console.WriteLine("PASS: pure prerequisites/copies/null/positive IDs and credit boundaries/Int64 overflow safety");
    }

    public static async Task Configuration(string root)
    {
        var emptyRoot = Path.Combine(root,"obj","day14-empty-config");
        Directory.CreateDirectory(emptyRoot);
        foreach(var setting in new string?[]{null,"","wrong","0","-1","9223372036854775808"})
        {
            var info = new ProcessStartInfo("dotnet") { WorkingDirectory=root, UseShellExecute=false,
                CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden, RedirectStandardOutput=true, RedirectStandardError=true };
            info.ArgumentList.Add(Path.Combine(root,"bin/Debug/net10.0/UniversityRegistration.dll"));
            info.ArgumentList.Add("--contentRoot"); info.ArgumentList.Add(emptyRoot);
            info.Environment.Remove("EnrollmentPolicy__MaxCreditsPerTerm");
            if(setting is not null) info.Environment["EnrollmentPolicy__MaxCreditsPerTerm"] = setting;
            using var process=Process.Start(info)!;
            var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
            try
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await process.WaitForExitAsync(timeout.Token);
                var text=await output + await error;
                Check(process.ExitCode!=0 && text.Contains("EnrollmentPolicy:MaxCreditsPerTerm") && text.Contains("pozitif"),"invalid/missing configuration must fail clearly");
            }
            finally { if(!process.HasExited) {process.Kill(true);await process.WaitForExitAsync();} }
        }
        Console.WriteLine("PASS: missing/empty/malformed/zero/negative/overflow policy fails startup; processes closed");
    }

    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send)
    {
        var tag="S14_"+Guid.NewGuid().ToString("N")[..8];
        var courses=new List<Course>();var terms=new List<AcademicTerm>();var students=new List<Student>();var offers=new List<CourseOffering>();
        Exception? failure=null;
        async Task<int> Student()
        {
            var s=new Student{StudentNumber=tag+"_"+students.Count,FullName="Synthetic academic test"};students.Add(s);
            await using var db=factory();db.Students.Add(s);await db.SaveChangesAsync();return s.Id;
        }
        async Task Completed(int student,int course)
        {await using var db=factory();db.StudentCompletedCourses.Add(new(){StudentId=student,CourseId=course});await db.SaveChangesAsync();}
        async Task<string[]> Rows(int student)
        {await using var db=factory();return (await db.Enrollments.Where(e=>e.StudentId==student).OrderBy(e=>e.Id).ToListAsync()).Select(e=>$"{e.Id}|{e.StudentId}|{e.CourseOfferingId}|{e.CreatedAtUtc:O}").ToArray();}
        async Task Request(int student,int[] ids,bool batch,int status,string? code=null,int? contextId=null)
        {
            var before=await Rows(student);
            var response=await send("POST",batch?$"/students/{student}/enrollments/batch":"/enrollments",
                batch?(object)new{courseOfferingIds=ids}:new{studentId=student,courseOfferingId=ids.Single()},null);
            Check(response.Status==status,$"HTTP expected={status} actual={response.Status} student={student} ids={string.Join(',',ids)}");
            if(status==400)
            {
                Check(response.Body["failures"]![0]!["code"]?.GetValue<string>()==code,"academic code");
                var key=code=="prerequisite_not_satisfied"?"courseOfferingId":"academicTermId";
                Check(response.Body["failures"]![0]![key]?.GetValue<int>()==contextId,"first ordered failure context ID");
                Check((await Rows(student)).SequenceEqual(before),"rejection preserves all previous DB records; no partial write");
            }
            else
            {
                Check((await Rows(student)).Length==before.Length+ids.Length,"success DB count");
                if(!batch)
                {
                    Check(response.Location==$"/enrollments/{response.Body["id"]}","single Location");
                    var detail=await send("GET",response.Location!,null,null);Check(detail.Status==200 && JsonNode.DeepEquals(detail.Body,response.Body),"single detail");
                }
                else Check(response.Body["items"]!.AsArray().Select(n=>n!["courseOfferingId"]!.GetValue<int>()).SequenceEqual(ids),"batch response order");
            }
        }
        try
        {
            foreach(var credits in new[]{1,4,4,24,6,7,1,int.MaxValue}) courses.Add(new(){Code=tag+"_"+courses.Count,Name="Academic fixture",Credits=credits});
            terms.AddRange(Enumerable.Range(0,2).Select(i=>new AcademicTerm{Name=tag+"_T"+i,StartsOn=new(2026,1,1),EndsOn=new(2026,1,2)}));
            await using(var db=factory()) {db.AddRange(courses);db.AddRange(terms);await db.SaveChangesAsync();}
            foreach(var t in terms) foreach(var c in courses) offers.Add(new(){CourseId=c.Id,AcademicTermId=t.Id,SectionCode="A",Capacity=40});
            offers.Add(new(){CourseId=courses[2].Id,AcademicTermId=terms[0].Id,SectionCode="B",Capacity=30});
            await using(var db=factory())
            {
                db.AddRange(offers);await db.SaveChangesAsync();
                db.CoursePrerequisites.Add(new(){CourseId=courses[1].Id,PrerequisiteCourseId=courses[0].Id});await db.SaveChangesAsync();
            }
            int O(int course,int term=0)=>offers.Single(o=>o.CourseId==courses[course].Id && o.AcademicTermId==terms[term].Id && o.SectionCode=="A").Id;
            var s=await Student();var other=await Student();
            await Request(s,[O(0)],false,201); // Enrollment is not completion.
            await Request(s,[O(1)],false,400,"prerequisite_not_satisfied",O(1));
            await Completed(other,courses[0].Id);
            await Request(s,[O(1)],false,400,"prerequisite_not_satisfied",O(1));
            await Completed(s,courses[0].Id);await Request(s,[O(1)],false,201);
            var batchStudent=await Student();
            await Request(batchStudent,[O(0),O(1)],true,400,"prerequisite_not_satisfied",O(1));
            await using(var db=factory()) {db.CoursePrerequisites.Add(new(){CourseId=courses[2].Id,PrerequisiteCourseId=courses[0].Id});await db.SaveChangesAsync();}
            await Request(batchStudent,[O(2),O(1)],true,400,"prerequisite_not_satisfied",O(2));
            await Request(batchStudent,[O(1),O(2)],true,400,"prerequisite_not_satisfied",O(1));
            await using(var db=factory()) await db.CoursePrerequisites.Where(p=>p.CourseId==courses[2].Id).ExecuteDeleteAsync();
            var loadStudent=await Student();await Request(loadStudent,[O(3)],false,201);
            await Request(loadStudent,[O(3,1)],false,201); // Other term's 24 must not count.
            await Request(loadStudent,[O(5)],false,400,"credit_limit_exceeded",terms[0].Id);
            await Request(loadStudent,[O(2),offers.Last().Id],true,400,"credit_limit_exceeded",terms[0].Id); // 24+4+4
            await Request(loadStudent,[O(6,1),O(5)],true,400,"credit_limit_exceeded",terms[0].Id); // one valid term + one invalid
            await Request(loadStudent,[O(5,1),O(5)],true,400,"credit_limit_exceeded",terms[1].Id); // request term order
            await Request(loadStudent,[O(4),O(4,1)],true,201); // exactly30 separately
            await Request(loadStudent,[O(1)],false,400,"prerequisite_not_satisfied",O(1)); // both rules fail: prerequisite comes first
            await Request(loadStudent,[O(6)],false,400,"credit_limit_exceeded",terms[0].Id);
            var sectionStudent=await Student();await Request(sectionStudent,[O(2),offers.Last().Id],true,201);
            await Request(sectionStudent,[O(3)],false,400,"credit_limit_exceeded",terms[0].Id); // two sections count twice:8+24

            // SQL SUM must use bigint: prepare a legitimate pre-existing load above Int32.
            var wide=await Student();
            await using(var db=factory())
            {
                var bigB=new CourseOffering{CourseId=courses[7].Id,AcademicTermId=terms[0].Id,SectionCode="B",Capacity=1};offers.Add(bigB);db.Add(bigB);await db.SaveChangesAsync();
                db.Enrollments.AddRange(new Enrollment{StudentId=wide,CourseOfferingId=O(7),CreatedAtUtc=DateTimeOffset.UtcNow},
                    new Enrollment{StudentId=wide,CourseOfferingId=bigB.Id,CreatedAtUtc=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
            }
            await Request(wide,[O(6)],false,400,"credit_limit_exceeded",terms[0].Id);

            var sqlStudent=await Student();await Completed(sqlStudent,courses[0].Id);
            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                var result=await new EnrollmentOperations(db,new EnrollmentPolicy(30)).CreateBatchAsync(sqlStudent,[O(1),O(2)],default);
                Check(result is BatchEnrollmentResult.Created,"direct operation succeeds");
                var reads=capture.Commands.Where(c=>c.Text.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)).ToArray();
                Check(reads.Length==8,"eight bulk queries independent of selection count");
                Check(reads.Any(c=>c.Text.Contains("GROUP BY") && c.Text.Contains("SUM(") && c.Text.Contains("bigint",StringComparison.OrdinalIgnoreCase) && c.Text.Contains("WHERE") && c.Text.Contains("JOIN")),"actual SQL filtered JOIN/GROUP BY/SUM(bigint)");
                Check(reads.Where(c=>c.Text.Contains("StudentCompletedCourses") || c.Text.Contains("CoursePrerequisites")).All(c=>c.Text.Contains("WHERE")),"bounded academic reads");
            }

            async Task Constraint(Action<UniversityDbContext> change,int number,string name)
            {
                await using var db=factory();change(db);
                try {await db.SaveChangesAsync();throw new BehaviorFailure("Expected SQL constraint "+name);}
                catch(DbUpdateException ex) {Check(ex.InnerException is SqlException sql && sql.Number==number && sql.Message.Contains(name,StringComparison.Ordinal),"exact constraint "+name);}
            }
            await Constraint(db=>db.CoursePrerequisites.Add(new(){CourseId=courses[1].Id,PrerequisiteCourseId=courses[0].Id}),2627,"PK_CoursePrerequisites");
            await Constraint(db=>db.CoursePrerequisites.Add(new(){CourseId=courses[0].Id,PrerequisiteCourseId=courses[0].Id}),547,"CK_CoursePrerequisites_NotSelf");
            await Constraint(db=>db.CoursePrerequisites.Add(new(){CourseId=int.MaxValue,PrerequisiteCourseId=courses[0].Id}),547,"FK_CoursePrerequisites_Courses");
            await Constraint(db=>db.CoursePrerequisites.Add(new(){CourseId=courses[0].Id,PrerequisiteCourseId=int.MaxValue}),547,"FK_CoursePrerequisites_PrerequisiteCourses");
            await Constraint(db=>db.StudentCompletedCourses.Add(new(){StudentId=s,CourseId=courses[0].Id}),2627,"PK_StudentCompletedCourses");
            await Constraint(db=>db.StudentCompletedCourses.Add(new(){StudentId=int.MaxValue,CourseId=courses[0].Id}),547,"FK_StudentCompletedCourses_Students");
            await Constraint(db=>db.StudentCompletedCourses.Add(new(){StudentId=s,CourseId=int.MaxValue}),547,"FK_StudentCompletedCourses_Courses");
            // This is deliberately allowed: a self CHECK does not prohibit an indirect cycle.
            await using(var db=factory()) {db.CoursePrerequisites.Add(new(){CourseId=courses[0].Id,PrerequisiteCourseId=courses[1].Id});await db.SaveChangesAsync();}
            await using(var db=factory()) Check(await db.CoursePrerequisites.AnyAsync(p=>p.CourseId==courses[0].Id && p.PrerequisiteCourseId==courses[1].Id),"indirect cycle limitation observed");
            Console.WriteLine("PASS: academic HTTP ordered failures/term order/completion isolation/exact-limit/multi-term batch/no partial writes; actual eight bounded SQL reads and bigint SUM; PK/FK/self-CHECK and cycle boundary");
        }
        catch(Exception ex){failure=ex;throw;}
        finally
        {
            try
            {
                var studentIds=students.Select(s=>s.Id).ToArray();var courseIds=courses.Select(c=>c.Id).ToArray();var termIds=terms.Select(t=>t.Id).ToArray();
                await using var db=factory();
                await db.StudentCompletedCourses.Where(c=>studentIds.Contains(c.StudentId)).ExecuteDeleteAsync();
                await db.CoursePrerequisites.Where(p=>courseIds.Contains(p.CourseId)).ExecuteDeleteAsync();
                await db.Enrollments.Where(e=>studentIds.Contains(e.StudentId)).ExecuteDeleteAsync();
                await db.CourseOfferings.Where(o=>courseIds.Contains(o.CourseId)).ExecuteDeleteAsync();
                await db.Students.Where(s=>studentIds.Contains(s.Id)).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(t=>termIds.Contains(t.Id)).ExecuteDeleteAsync();
                await db.Courses.Where(c=>courseIds.Contains(c.Id)).ExecuteDeleteAsync();
                Console.WriteLine("PASS: academic fixture relations and parents cleaned in FK order");
            }
            catch(Exception ex){Console.Error.WriteLine("CLEANUP academic: "+ex.GetType().Name);if(failure is null)throw;}
        }
    }
}
