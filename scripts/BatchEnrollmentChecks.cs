using UniversityRegistration.Application;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

static class BatchEnrollmentChecks
{
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send,Action stop,Func<Task> start)
    {
        var studentIds=new List<int>();
        var offeringIds=new List<int>();
        async Task<int> Student()
        {
            var response=await send("POST","/students",new{studentNumber="D9_"+studentIds.Count,fullName="Batch test"},null);
            Check(response.Status==201,"Fixture student");
            var id=response.Body["id"]!.GetValue<int>();studentIds.Add(id);return id;
        }
        string Path(int id)=>$"/students/{id}/enrollments/batch";
        async Task<int[]> Read(int student)
        {await using var db=factory();return await db.Enrollments.Where(e=>e.StudentId==student).OrderBy(e=>e.CourseOfferingId).Select(e=>e.CourseOfferingId).ToArrayAsync();}
        try
        {
            var student=await Student();
            int students,offerings;
            await using(var db=factory()){students=await db.Students.CountAsync();offerings=await db.CourseOfferings.CountAsync();}
            var before=DateTimeOffset.UtcNow;
            var response=await send("POST",Path(student),new{courseOfferingIds=new[]{3,1}},null);
            var after=DateTimeOffset.UtcNow;
            Check(response.Status==201 && response.Location is null && response.Body.AsObject().Count==1,"Batch 201 without fictional Location");
            var items=response.Body["items"]!.AsArray();
            Check(items.Count==2 && items.Select(n=>n!["courseOfferingId"]!.GetValue<int>()).SequenceEqual(new[]{3,1}),"Request order");
            Check(items.Select(n=>n!["createdAtUtc"]!.GetValue<DateTimeOffset>()).Distinct().Count()==1,"Batch timestamp");
            foreach(var item in items)
            {
                Check(string.Join(',',item!.AsObject().Select(p=>p.Key).Order())=="courseOfferingId,createdAtUtc,id,studentId","Batch item fields");
                var time=item["createdAtUtc"]!.GetValue<DateTimeOffset>();
                Check(time.Offset==TimeSpan.Zero && time>=before && time<=after && item["studentId"]!.GetValue<int>()==student,"Server time/student");
                var detail=await send("GET","/enrollments/"+item["id"]!.GetValue<int>(),null,null);
                Check(detail.Status==200 && JsonNode.DeepEquals(item,detail.Body),"Batch details");
            }
            Check((await send("GET",$"/students/{student}/enrollments",null,null)).Body.AsArray().Count==2,"Student list batch");
            await using(var db=factory()) Check(await db.Students.CountAsync()==students && await db.CourseOfferings.CountAsync()==offerings,"Batch inserted parents");
            stop();await start();Check((await Read(student)).SequenceEqual(new[]{1,3}),"Batch restart");
            Check((await send("GET","/enrollments/"+items[0]!["id"]!.GetValue<int>(),null,null)).Status==200,"Restart HTTP detail");

            foreach(var ids in new int[]?[]{null,[],[1,1],[0],[-1],Enumerable.Range(1,51).ToArray()})
            {
                var invalid=await send("POST",Path(student),new{courseOfferingIds=ids},null);
                Check(invalid.Status==400 && invalid.Body["errors"]!["courseOfferingIds"] is not null,"Batch input fields");
                Check((await Read(student)).SequenceEqual(new[]{1,3}),"Invalid request changed DB");
            }
            foreach(var raw in new[]{"{}","{\"courseOfferingIds\":\"bad\"}","{\"courseOfferingIds\":[\"1\"]}"})
            {
                Check((await send("POST",Path(student),null,raw)).Status==400,"Batch binding");
                Check((await Read(student)).SequenceEqual(new[]{1,3}),"Binding changed DB");
            }
            Check((await send("POST",Path(int.MaxValue),new{courseOfferingIds=Array.Empty<int>()},null)).Status==400,"Structure before student");
            Check((await send("POST",Path(int.MaxValue),new{courseOfferingIds=new[]{1}},null)).Status==404,"Missing student");
            Check((await Read(int.MaxValue)).Length==0,"Missing student persisted");
            Check((await send("POST",Path(student),new{courseOfferingIds=new[]{2,int.MaxValue}},null)).Status==400,"Missing offering");
            Check((await Read(student)).SequenceEqual(new[]{1,3}),"Missing offering partial write");
            Check((await send("POST",Path(student),new{courseOfferingIds=new[]{2,1}},null)).Status==409,"Existing pair precheck");
            Check((await Read(student)).SequenceEqual(new[]{1,3}),"Precheck must preserve old/exclude new");

            // Boundary and read-count evidence: provider INSERT batch count is not asserted.
            var largeStudent=await Student();
            await using(var db=factory())
            {
                var fixtures=Enumerable.Range(0,50).Select(i=>new CourseOffering{CourseId=1,AcademicTermId=1,SectionCode=$"D9_{i:00}",Capacity=1}).ToArray();
                db.CourseOfferings.AddRange(fixtures);await db.SaveChangesAsync();offeringIds.AddRange(fixtures.Select(o=>o.Id));
            }
            await using(var template=factory())
            {
                var capture=new CatalogSqlCapture();
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
                var result=await BatchEnrollmentEndpoint.CreateAsync(largeStudent,new(offeringIds.ToArray()),new EnrollmentOperations(db),default);
                Check(((IStatusCodeHttpResult)result).StatusCode==201,"50-item boundary");
                Check(capture.Commands.Count(c=>c.Text.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))==3,"Expected three bulk preflight reads, no per-item reads");
                capture.Commands.Clear();
                await BatchEnrollmentEndpoint.CreateAsync(int.MaxValue,new([]),new EnrollmentOperations(db),default);
                Check(capture.Commands.Count==0,"Invalid structure queried DB");
            }
            Check((await Read(largeStudent)).Length==50,"50 batch DB count");
            // Invoke the HTTP-independent operation without duplicating its write scenarios.
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();
                await using var db=factory();
                try
                {
                    await new EnrollmentOperations(db).CreateBatchAsync(largeStudent,[1],cancelled.Token);
                    throw new Exception("Cancellation was converted into an operation result");
                }
                catch(OperationCanceledException) { }
            }
            Check((await Read(largeStudent)).Length==50,"Cancelled operation changed registrations");
            Console.WriteLine("PASS: operation cancellation propagates without a rejection result or write");
            Console.WriteLine("PASS: batch HTTP/ordering/time/details/restart; input 400/student 404/missing offering 400/existing pair 409 are precheck evidence; 50 items and three bulk reads");

            var raceStudent=await Student();
            await using(var template=factory())
            {
                var race=new RegistrationRace(async()=>{
                    await using var other=factory();other.Enrollments.Add(new(){StudentId=raceStudent,CourseOfferingId=2,CreatedAtUtc=DateTimeOffset.UtcNow});await other.SaveChangesAsync();});
                var capture=new CatalogSqlCapture();
                // Test-only command splitting exposes a successful INSERT before a later failure.
                await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>()
                    .UseSqlServer(template.Database.GetConnectionString(),o=>o.MaxBatchSize(1)).AddInterceptors(race,capture).Options);
                var result=await BatchEnrollmentEndpoint.CreateAsync(raceStudent,new([1,2]),new EnrollmentOperations(db),default);
                Check(((IStatusCodeHttpResult)result).StatusCode==409 && race.Classification=="duplicate","Real batch save unique failure");
                Check(capture.Commands.Any(c=>c.Text.Contains("INSERT INTO [Enrollments]",StringComparison.Ordinal)),"No successful INSERT before constraint failure");
                Check(db.ChangeTracker.Entries<Enrollment>().Any(e=>e.State==EntityState.Added),"Failed tracker not cleared");
            }
            Check((await Read(raceStudent)).SequenceEqual(new[]{2}),"Batch partial row survived or competitor lost");
            // The existing classifier also handles FK races after all bulk prechecks.
            foreach(var missing in new[]{"studentId","courseOfferingId"})
            {
                var target=await Student();
                var doomed=offeringIds[missing=="studentId"?0:1];
                await using var template=factory();
                var race=new RegistrationRace(async()=>{
                    await using var other=factory();
                    if(missing=="studentId") await other.Students.Where(s=>s.Id==target).ExecuteDeleteAsync();
                    else {await other.Enrollments.Where(e=>e.CourseOfferingId==doomed && e.StudentId==largeStudent).ExecuteDeleteAsync();await other.CourseOfferings.Where(o=>o.Id==doomed).ExecuteDeleteAsync();}
                });
                await using(var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(race).Options))
                {
                    var result=await BatchEnrollmentEndpoint.CreateAsync(target,new([3,doomed]),new EnrollmentOperations(db),default);
                    Check(((IStatusCodeHttpResult)result).StatusCode==400 && race.Classification==missing,"Batch save FK classification");
                    Check(((ProblemHttpResult)result).ProblemDetails is HttpValidationProblemDetails detail && detail.Errors.ContainsKey(missing),"Batch FK error field");
                }
                Check((await Read(target)).Length==0,"FK failure partial batch");
            }
            Console.WriteLine("PASS: real batch SaveChanges unique fault after successful INSERT rolled back own row, preserved committed competitor; FK save branches roll back entire batch");
        }
        finally
        {
            await using var db=factory();
            await db.Enrollments.Where(e=>studentIds.Contains(e.StudentId)).ExecuteDeleteAsync();
            await db.Students.Where(s=>studentIds.Contains(s.Id)).ExecuteDeleteAsync();
            await db.CourseOfferings.Where(o=>offeringIds.Contains(o.Id)).ExecuteDeleteAsync();
        }
    }
}
