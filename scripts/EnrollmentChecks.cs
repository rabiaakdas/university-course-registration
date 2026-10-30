using UniversityRegistration.Application;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

static class EnrollmentChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static int Id(JsonNode node) => node["id"]!.GetValue<int>();
    static void Fields(JsonNode node, string fields) => Check(string.Join(',',node.AsObject().Select(p=>p.Key).Order())==fields,"Registration response fields");

    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send,
        Action stop, Func<Task> start, Func<string,Task> sqlFile)
    {
        async Task<(int Status,JsonNode Body,string? Location)> Post(string path,object body) => await send("POST",path,body,null);
        async Task<(int Status,JsonNode Body,string? Location)> Get(string path) => await send("GET",path,null,null);
        var ownedOfferings = new List<int>();
        try
        {
            var student=await Post("/students",new {id=99999,studentNumber=" 0008Ab ",fullName=" Rabia Nur Akdaş "});
            Check(student.Status==201 && Id(student.Body)!=99999,"Student 201 / server ID");
            Fields(student.Body,"fullName,id,studentNumber");
            Check(student.Body["studentNumber"]!.GetValue<string>()=="0008Ab" && student.Body["fullName"]!.GetValue<string>()=="Rabia Nur Akdaş","Trim/leading zeros/case");
            var first=Id(student.Body);
            Check(student.Location==$"/students/{first}" && JsonNode.DeepEquals((await Get(student.Location!)).Body,student.Body),"Student Location/detail");
            var secondResponse=await Post("/students",new {studentNumber="D8_SECOND",fullName="Rabia Nur Akdaş"});
            Check(secondResponse.Status==201,"Same FullName allowed");
            var second=Id(secondResponse.Body);
            await using(var db=factory()) Check(await db.Students.CountAsync()==2,"Student count");
            Check((await Post("/students",new {studentNumber="0008ab",fullName="Other"})).Status==409,"Collation number duplicate");
            foreach(var value in new string?[] {null,"","   ",new string('x',201)})
            {
                var response=await Post("/students",new {studentNumber=value,fullName=value});
                Check(response.Status==400 && response.Body["errors"]!.AsObject().Count==2,"Student aggregate errors");
            }
            Check((await Post("/students",new {studentNumber=new string('x',31),fullName="Valid"})).Status==400,"Student number max30");
            await using(var db=factory()) Check(await db.Students.CountAsync()==2,"Invalid/duplicate student persisted");
            Check((await Get("/students/2147483647")).Status==404,"Missing student detail");
            var empty=await Get($"/students/{first}/enrollments");
            Check(empty.Status==200 && empty.Body.AsArray().Count==0,"Existing student empty enrollments");
            Check((await Get("/students/2147483647/enrollments")).Status==404,"Missing student's enrollments");

            async Task<int> Enroll(int sid,int oid)
            {
                var before=DateTimeOffset.UtcNow;
                var response=await Post("/enrollments",new {id=99999,studentId=sid,courseOfferingId=oid,createdAtUtc="2000-01-01T00:00:00Z"});
                var after=DateTimeOffset.UtcNow;
                Check(response.Status==201 && Id(response.Body)!=99999,"Enrollment 201 / generated ID");
                Fields(response.Body,"courseOfferingId,createdAtUtc,id,rejectionReason,status,studentId");
                Check(response.Body["status"]!.GetValue<string>()=="Draft" && response.Body["rejectionReason"] is null,"New enrollment Draft/null reason");
                var created=response.Body["createdAtUtc"]!.GetValue<DateTimeOffset>();
                Check(created.Offset==TimeSpan.Zero && created>=before && created<=after,"Server UTC time bounds");
                Check(response.Location==$"/enrollments/{Id(response.Body)}" && JsonNode.DeepEquals((await Get(response.Location!)).Body,response.Body),"Enrollment Location/detail");
                return Id(response.Body);
            }
            int parentStudents,parentOfferings;
            await using(var db=factory()) {parentStudents=await db.Students.CountAsync();parentOfferings=await db.CourseOfferings.CountAsync();}
            var enrollmentId=await Enroll(first,1);
            await Enroll(first,2); // Same course/term, distinct A/B offerings allowed.
            await Enroll(second,1);
            await using(var db=factory()) Check(await db.Students.CountAsync()==parentStudents && await db.CourseOfferings.CountAsync()==parentOfferings && await db.Enrollments.CountAsync()==3,"Only Enrollment inserted");
            Check((await Post("/enrollments",new {studentId=first,courseOfferingId=1})).Status==409,"Duplicate pair");
            foreach(var pair in new[] {(0,0),(-1,-2),(int.MaxValue,int.MaxValue)})
            {
                var response=await Post("/enrollments",new {studentId=pair.Item1,courseOfferingId=pair.Item2});
                Check(response.Status==400 && response.Body["errors"]!.AsObject().ContainsKey("studentId") && response.Body["errors"]!.AsObject().ContainsKey("courseOfferingId"),"Enrollment field errors");
            }
            await using(var db=factory()) Check(await db.Enrollments.CountAsync()==3,"Rejected enrollment persisted");
            Check((await Get("/enrollments/2147483647")).Status==404,"Missing enrollment detail");
            var list=(await Get($"/students/{first}/enrollments")).Body.AsArray();
            Check(list.Count==2 && list.Select(n=>Id(n!)).SequenceEqual(list.Select(n=>Id(n!)).Order()),"Student enrollment list/order");
            foreach(var item in list)
            {
                Fields(item!,"courseCode,courseName,courseOfferingId,createdAtUtc,id,rejectionReason,sectionCode,status,termName");
                Check(item!["courseCode"]!.GetValue<string>()=="CSE201" && item["courseName"]!.GetValue<string>()=="Veri Yapıları" && item["termName"]!.GetValue<string>()=="2026-2027 Güz","List JOIN content");
            }
            Check(list.Select(n=>n!["sectionCode"]!.GetValue<string>()).SequenceEqual(new[]{"A","B"}),"Different sections accepted");
            var race=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>Post("/enrollments",new {studentId=second,courseOfferingId=2})));
            Check(race.Count(r=>r.Status==201)==1 && race.Count(r=>r.Status==409)==5,"Concurrent pair race");
            await using(var db=factory()) Check(await db.Enrollments.CountAsync(e=>e.StudentId==second && e.CourseOfferingId==2)==1,"Race DB row count");
            stop(); await start();
            Check((await Get($"/students/{first}")).Status==200 && (await Get($"/enrollments/{enrollmentId}")).Status==200,"Registration restart persistence");

            async Task DirectFailure(Action<UniversityDbContext> add,string? expected,int number)
            {
                await using var db=factory(); add(db);
                try {await db.SaveChangesAsync();throw new Exception("Expected database constraint failure");}
                catch(DbUpdateException ex) {Check(ex.InnerException is SqlException sql && sql.Number==number && RegistrationWriteErrors.Classify(ex)==expected,"Direct constraint classification");}
            }
            Enrollment NewEnrollment(int sid,int oid) => new(){StudentId=sid,CourseOfferingId=oid,CreatedAtUtc=DateTimeOffset.UtcNow};
            await DirectFailure(db=>db.Enrollments.Add(NewEnrollment(first,1)),"duplicate",2601);
            await DirectFailure(db=>db.Enrollments.Add(NewEnrollment(int.MaxValue,1)),"studentId",547);
            await DirectFailure(db=>db.Enrollments.Add(NewEnrollment(first,int.MaxValue)),"courseOfferingId",547);
            await DirectFailure(db=>db.Courses.Add(new(){Code="D8_CHECK",Name="Test",Credits=0}),null,547);
            await DirectFailure(db=>db.Courses.Add(new(){Code="CSE201",Name="Test",Credits=1}),null,2601);
            await DirectFailure(db=>db.Students.Add(new(){Id=first,StudentNumber="D8_PK",FullName="Test"}),null,2627);

            async Task SaveRace(string expected,Func<UniversityDbContext,Task<IResult>> action,Func<Task> competitor)
            {
                await using var template=factory();
                var observer=new RegistrationRace(competitor);
                var capture=new CatalogSqlCapture();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(observer,capture).Options);
                var result=await action(db);
                if(expected != "studentNumber")
                    Check(capture.Commands.Count(c=>c.Text.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))==8,"Single registration keeps eight bounded preflight reads");
                Check(observer.Classification==expected,"Real save branch: "+expected);
                Check(((IStatusCodeHttpResult)result).StatusCode==(expected is "studentNumber" or "duplicate"?409:400),"Save branch status");
                if(expected is "studentId" or "courseOfferingId") Check(((ProblemHttpResult)result).ProblemDetails is HttpValidationProblemDetails validation && validation.Errors.ContainsKey(expected),"Save branch field");
            }
            await SaveRace("studentNumber",db=>StudentEndpoints.CreateAsync(new("D8_SAVE","Test"),db,default),async()=>{
                await using var db=factory();db.Students.Add(new(){StudentNumber="D8_SAVE",FullName="Winner"});await db.SaveChangesAsync();});
            await SaveRace("duplicate",db=>EnrollmentEndpoints.CreateAsync(new(first,3),new EnrollmentOperations(db, new EnrollmentPolicy(30), DecoratorChecks.TimedEvaluator(), ObserverChecks.EmptyPublisher()),default),async()=>{
                await using var db=factory();db.Enrollments.Add(NewEnrollment(first,3));await db.SaveChangesAsync();});
            var disposable=Id((await Post("/students",new {studentNumber="D8_DELETE",fullName="Test"})).Body);
            await SaveRace("studentId",db=>EnrollmentEndpoints.CreateAsync(new(disposable,1),new EnrollmentOperations(db, new EnrollmentPolicy(30), DecoratorChecks.TimedEvaluator(), ObserverChecks.EmptyPublisher()),default),async()=>{
                await using var db=factory();await db.Students.Where(s=>s.Id==disposable).ExecuteDeleteAsync();});
            int offeringId;
            await using(var db=factory()) {var o=new CourseOffering{CourseId=1,AcademicTermId=1,SectionCode="D8_FK",Capacity=1};db.CourseOfferings.Add(o);await db.SaveChangesAsync();offeringId=o.Id;ownedOfferings.Add(o.Id);}
            await SaveRace("courseOfferingId",db=>EnrollmentEndpoints.CreateAsync(new(first,offeringId),new EnrollmentOperations(db, new EnrollmentPolicy(30), DecoratorChecks.TimedEvaluator(), ObserverChecks.EmptyPublisher()),default),async()=>{
                await using var db=factory();await db.CourseOfferings.Where(o=>o.Id==offeringId).ExecuteDeleteAsync();});

            async Task BlockDelete(Func<UniversityDbContext,Task<int>> delete,string constraint)
            {
                await using var db=factory();
                try {await delete(db);throw new Exception("Expected NO ACTION rejection");}
                catch(SqlException ex) {Check(ex.Number==547 && ex.Message.Contains(constraint),"Delete constraint");}
            }
            await BlockDelete(db=>db.Students.Where(s=>s.Id==first).ExecuteDeleteAsync(),"FK_Enrollments_Students");
            await BlockDelete(db=>db.CourseOfferings.Where(o=>o.Id==1).ExecuteDeleteAsync(),"FK_Enrollments_CourseOfferings");
            await using(var db=factory()) Check(await db.Students.AnyAsync(s=>s.Id==first) && await db.CourseOfferings.AnyAsync(o=>o.Id==1) && await db.Enrollments.AnyAsync(e=>e.Id==enrollmentId),"NO ACTION preserved rows");

            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                await StudentEndpoints.ListEnrollmentsAsync(first,db,default);
                Check(capture.Commands.Count==2 && capture.Commands[1].Text.Split("INNER JOIN").Length==4 && capture.Commands[1].Text.Contains("ORDER BY") && capture.Commands[1].Text.Contains("WHERE"),"Fixed existence + joined list queries");
                Check(!db.ChangeTracker.Entries().Any(),"Enrollment DTO tracks entity");
                capture.Commands.Clear();
                await StudentEndpoints.CreateAsync(new(null,null),db,default);
                await EnrollmentEndpoints.CreateAsync(new(0,0),new EnrollmentOperations(db, new EnrollmentPolicy(30), DecoratorChecks.TimedEvaluator(), ObserverChecks.EmptyPublisher()),default);
                Check(capture.Commands.Count==0,"Invalid registration queried DB");
            }
            int count;
            await using(var db=factory()) count=await db.Enrollments.CountAsync();
            await sqlFile("sql/app/seed.sql");await sqlFile("sql/app/seed.sql");
            await using(var db=factory()) Check(await db.Enrollments.CountAsync()==count && await db.Students.AnyAsync(s=>s.Id==first),"Seed preserved registration");
            Console.WriteLine("PASS: students/leading zeros/CI uniqueness/validation; many-to-many HTTP/UTC/restart; actual unique/FK save races; direct constraints/NO ACTION; two SQL reads/no N+1; seed replay");
        }
        finally
        {
            await using var db=factory();
            var students=db.Students.Where(s=>s.StudentNumber=="0008Ab" || s.StudentNumber.StartsWith("D8_"));
            await db.Enrollments.Where(e=>students.Select(s=>s.Id).Contains(e.StudentId)).ExecuteDeleteAsync();
            await students.ExecuteDeleteAsync();
            await db.CourseOfferings.Where(o=>ownedOfferings.Contains(o.Id)).ExecuteDeleteAsync();
        }
    }
}

sealed class RegistrationRace(Func<Task> competitor) : SaveChangesInterceptor
{
    public string? Classification {get;private set;}
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    {await competitor();return result;}
    public override Task SaveChangesFailedAsync(DbContextErrorEventData data,CancellationToken ct=default)
    {Classification=data.Exception is DbUpdateException ex?RegistrationWriteErrors.Classify(ex):null;return Task.CompletedTask;}
}
