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

// Runs in the existing runner's newly created, isolated database only.
static class RelatedWriteChecks
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    static int Id(JsonNode node) => node["id"]!.GetValue<int>();
    public static async Task Run(Func<UniversityDbContext> dbFactory,
        Func<string, string, object?, string?, Task<(int Status, JsonNode Body, string? Location)>> send,
        Func<string, Task> sql, Func<string, Task> sqlFile, Action stop, Func<Task> start)
    {
        async Task<(int Status, JsonNode Body, string? Location)> Post(string path, object body) => await send("POST",path,body,null);
        async Task<(int Status, JsonNode Body, string? Location)> Get(string path) => await send("GET",path,null,null);
        void Fields(JsonNode node, string expected) => Check(string.Join(',',node.AsObject().Select(p=>p.Key).Order())==expected,"Response fields");
        async Task<(int Courses,int Terms,int Offerings)> Counts()
        { await using var db=dbFactory(); return (await db.Courses.CountAsync(),await db.AcademicTerms.CountAsync(),await db.CourseOfferings.CountAsync()); }
        async Task<int> Term(string name)
        {
            var result=await Post("/academic-terms",new { id=1,name,startsOn="2027-01-01",endsOn="2027-01-01" });
            Check(result.Status==201 && Id(result.Body)>600,"Term 201 / actual max sequence");
            Check(result.Location==$"/academic-terms/{Id(result.Body)}" && (await Get(result.Location!)).Status==200,"Term Location");
            Fields(result.Body,"endsOn,id,name,startsOn");
            Fields((await Get(result.Location!)).Body,"endsOn,id,name,startsOn");
            return Id(result.Body);
        }
        object Offering(int course,int term,string? section,int capacity=40) => new { courseId=course,academicTermId=term,sectionCode=section,capacity };
        var termId=await Term("  D6_İlk Dönem  ");
        Check((await Get($"/academic-terms/{termId}")).Body["name"]!.GetValue<string>()=="D6_İlk Dönem","Trim term");
        var before=await Counts();
        foreach(var body in new object[] {
            new {name=(string?)null,startsOn=(string?)null,endsOn=(string?)null},
            new {name="",startsOn="2027-02-01",endsOn="2027-01-01"},
            new {name="   "}, new {name=new string('x',101),startsOn="2027-01-01",endsOn="2027-01-01"} })
            Check((await Post("/academic-terms",body)).Status==400,"Invalid term");
        var combined=await Post("/academic-terms",new {name=" "});
        Check(combined.Body["errors"]!.AsObject().Count==3,"Combined term fields");
        Check((await send("POST","/academic-terms",null,"{\"name\":\"D6_bad\",\"startsOn\":\"2027-02-30\",\"endsOn\":\"2027-03-01\"}")).Status==400,"Date binding");
        Check(await Counts()==before,"Invalid terms persisted");
        var course=await Post("/courses",new {code="D6_COURSE",name="D6_Ders",credits=3});
        var courseId=Id(course.Body);
        before=await Counts();
        var created=await Post("/course-offerings",Offering(courseId,termId," a "));
        var offeringId=Id(created.Body);
        Check(created.Status==201 && offeringId>700 && created.Body["sectionCode"]!.GetValue<string>()=="A","Offering creation/normalization/sequence");
        Check(await Counts()==(before.Courses,before.Terms,before.Offerings+1),"Offering inserted parents");
        Check(created.Location==$"/course-offerings/{offeringId}" && (await Get(created.Location!)).Status==200,"Offering Location");
        Fields(created.Body,"academicTermId,capacity,courseCode,courseId,courseName,id,sectionCode,termName");
        Fields((await Get(created.Location!)).Body,"academicTermId,capacity,courseCode,courseId,courseName,id,sectionCode,termName");
        Check(created.Body["courseName"]!.GetValue<string>()=="D6_Ders" && created.Body["termName"]!.GetValue<string>()=="D6_İlk Dönem","Related names");
        before=await Counts();
        foreach(var body in new[] {Offering(0,-1,null,0),Offering(courseId,termId,""),Offering(courseId,termId,"  ",-1),Offering(courseId,termId,new string('A',11)),Offering(int.MaxValue,int.MaxValue,"X")})
            Check((await Post("/course-offerings",body)).Status==400,"Invalid offering");
        combined=await Post("/course-offerings",Offering(0,0,null,0));
        Check(combined.Body["errors"]!.AsObject().Count==4,"Combined offering fields");
        Check((await Post("/course-offerings",Offering(courseId,termId,"a"))).Status==409,"Duplicate combination");
        Check(await Counts()==before,"Rejected offering persisted");
        Check((await Post("/course-offerings",Offering(courseId,termId,"B"))).Status==201,"Different section");
        var secondTerm=await Term("D6_İkinci Dönem");
        Check((await Post("/course-offerings",Offering(courseId,secondTerm,"A"))).Status==201,"Different term");
        Check((await Get("/academic-terms/2147483647")).Status==404 && (await Get("/course-offerings/2147483647")).Status==404,"Missing details");

        var parallelTerms=await Task.WhenAll(Enumerable.Range(0,6).Select(i=>Term($"D6_Parallel_{i}")));
        Check(parallelTerms.Distinct().Count()==6,"Parallel term IDs");
        var independent=await Task.WhenAll(Enumerable.Range(0,6).Select(i=>Post("/course-offerings",Offering(courseId,termId,$"P{i}"))));
        Check(independent.All(r=>r.Status==201) && independent.Select(r=>Id(r.Body)).Distinct().Count()==6,"Parallel offering IDs");
        var race=await Task.WhenAll(Enumerable.Range(0,6).Select(i=>Post("/course-offerings",Offering(courseId,termId,i%2==0?" race ":"RACE"))));
        Check(race.Count(r=>r.Status==201)==1 && race.Count(r=>r.Status==409)==5,"Parallel offering conflict");
        await using(var db=dbFactory()) Check(await db.CourseOfferings.CountAsync(o=>o.CourseId==courseId && o.AcademicTermId==termId && o.SectionCode=="RACE")==1,"Race DB count");
        stop(); await start();
        Check((await Get(created.Location!)).Status==200 && (await Get($"/academic-terms/{termId}")).Status==200,"Restart persistence");
        await using(var db=dbFactory())
        {
            await db.Courses.Where(c=>c.Id==courseId).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.Name,"D6_Yeni Ders"));
            await db.AcademicTerms.Where(t=>t.Id==termId).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.Name,"D6_Yeni Dönem"));
        }
        var detail=await Get(created.Location!);
        Check(detail.Body["courseName"]!.GetValue<string>()=="D6_Yeni Ders" && detail.Body["termName"]!.GetValue<string>()=="D6_Yeni Dönem","JOIN uses DB names");
        var list=(await Get("/course-offerings")).Body.AsArray();
        Check(list.Where(n=>n!["academicTermId"]!.GetValue<int>()==termId).All(n=>n!["termName"]!.GetValue<string>()=="D6_Yeni Dönem"),"List JOIN names");
        Check(list.Select(n=>Id(n!)).SequenceEqual(list.Select(n=>Id(n!)).Order()),"Offering sort");
        var terms=(await Get("/academic-terms")).Body.AsArray();
        Check(terms.Any(n=>Id(n!)==termId) && terms.Select(n=>Id(n!)).SequenceEqual(terms.Select(n=>Id(n!)).Order()),"Term list/sort");
        before=await Counts(); await sqlFile("sql/app/seed.sql"); await sqlFile("sql/app/seed.sql");
        Check(await Counts()==before && (await Get(created.Location!)).Body["termName"]!.GetValue<string>()=="D6_Yeni Dönem","Seed preserved user rows");
        Console.WriteLine("PASS: term/offering HTTP, validation, JOIN, restart, parallel IDs/race, seed replay");

        // Deterministic interleavings after endpoint prechecks, only in test contexts.
        async Task SaveRace(string expected, CreateCourseOfferingRequest request, Func<Task> action)
        {
            await using var template=dbFactory();
            var interceptor=new OfferingRace(action);
            var options=new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(interceptor).Options;
            await using var db=new UniversityDbContext(options);
            var result=await CourseOfferingEndpoints.CreateAsync(request,db,default);
            Check(interceptor.Classification==expected,"Real save error classification: "+expected);
            Check(((IStatusCodeHttpResult)result).StatusCode==(expected=="duplicate"?409:400),"Save error status");
            if(expected!="duplicate") Check(((ProblemHttpResult)result).ProblemDetails is HttpValidationProblemDetails validation && validation.Errors.ContainsKey(expected),"FK field");
        }
        await SaveRace("duplicate",new(courseId,termId,"SAVE",30),async()=>{
            await using var db=dbFactory(); db.CourseOfferings.Add(new(){CourseId=courseId,AcademicTermId=termId,SectionCode="SAVE",Capacity=30}); await db.SaveChangesAsync(); });
        var disposableCourse=await Post("/courses",new {code="D6_DELETE",name="D6_Disposable",credits=1});
        var disposableId=Id(disposableCourse.Body);
        await SaveRace("courseId",new(disposableId,termId,"FK",20),async()=>{await using var db=dbFactory();await db.Courses.Where(c=>c.Id==disposableId).ExecuteDeleteAsync();});
        var disposableTerm=await Term("D6_Disposable");
        await SaveRace("academicTermId",new(courseId,disposableTerm,"FK",20),async()=>{await using var db=dbFactory();await db.AcademicTerms.Where(t=>t.Id==disposableTerm).ExecuteDeleteAsync();});
        await using(var db=dbFactory())
        {
            db.CourseOfferings.Add(new(){CourseId=courseId,AcademicTermId=termId,SectionCode="CHECK",Capacity=0});
            try {await db.SaveChangesAsync();throw new Exception("Expected CHECK");}
            catch(DbUpdateException ex) {Check(ex.InnerException is SqlException {Number:547} && OfferingWriteErrors.Classify(ex)==null,"CHECK mislabeled FK");}
        }
        var counter=new CommandCounter();
        await using(var template=dbFactory())
        await using(var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(counter).Options))
        {
            await CourseOfferingEndpoints.CreateAsync(new(0,0,null,0),db,default);
            await AcademicTermEndpoints.CreateAsync(new(null,null,null),db,default);
            Check(counter.Reads==0,"Invalid related POST queried DB");
        }
        Console.WriteLine("PASS: real save-stage unique/FK branches; CHECK 547 not mapped; invalid fields skip DB");
        await Tracking(dbFactory);
    }

    static async Task Tracking(Func<UniversityDbContext> factory)
    {
        int id;
        await using(var db=factory())
        {
            var course=new Course {Code="D6_TRACK",Name="Before",Credits=1};
            Check(db.Entry(course).State==EntityState.Detached,"Detached");
            db.Courses.Add(course); Check(db.Entry(course).State==EntityState.Added,"Added");
            await using(var read=factory()) Check(!await read.Courses.AnyAsync(c=>c.Code=="D6_TRACK"),"Add persisted before SaveChanges");
            await db.SaveChangesAsync(); id=course.Id;
            Check(id>0 && db.Entry(course).State==EntityState.Unchanged,"Saved Unchanged/generated Id");
        }
        await using(var db=factory())
        {
            var course=await db.Courses.SingleAsync(c=>c.Id==id);
            Check(db.Entry(course).State==EntityState.Unchanged,"Query Unchanged");
            course.Name="After"; db.ChangeTracker.DetectChanges();
            Check(db.Entry(course).State==EntityState.Modified,"DetectChanges Modified");
            await using(var read=factory()) Check((await read.Courses.SingleAsync(c=>c.Id==id)).Name=="Before","Before save DB old");
            await db.SaveChangesAsync();
            await using(var read=factory()) Check((await read.Courses.SingleAsync(c=>c.Id==id)).Name=="After","After save DB new");
            db.Courses.Remove(course); Check(db.Entry(course).State==EntityState.Deleted,"Deleted state");
            // Do not save deletion: outer cleanup owns the row.
        }
        await using(var db=factory())
        {
            Check(CourseOfferingEndpoints.Query(db).ToQueryString().Split("INNER JOIN").Length==3,"Expected two SQL JOINs");
            await CourseOfferingEndpoints.Query(db).ToListAsync();
            await db.Courses.Select(c=>new {c.Id,c.Name}).ToListAsync();
            Check(!db.ChangeTracker.Entries().Any(),"Projection tracked entities");
        }
        async Task<DbUpdateException> Fail(UniversityDbContext db)
        {
            DbUpdateException failure;
            try {await db.SaveChangesAsync();throw new Exception("Expected credits CHECK");}
            catch(DbUpdateException ex) {Check(ex.InnerException is SqlException {Number:547} sql && sql.Message.Contains("CK_Courses_Credits_Positive"),"Wrong failure class");failure=ex;}
            Check(db.ChangeTracker.Entries().Any(e=>e.State==EntityState.Added),"Failure unexpectedly cleared tracker");
            return failure;
        }
        await using(var db=factory())
        {
            db.Courses.AddRange(new Course {Code="D6_ATOMIC_OK",Name="Good",Credits=1},new Course {Code="D6_ATOMIC_BAD",Name="Bad",Credits=0});
            await Fail(db);
        }
        await using(var read=factory()) Check(!await read.Courses.AnyAsync(c=>c.Code.StartsWith("D6_ATOMIC_")),"Single SaveChanges partially persisted");
        await using(var db=factory())
        {
            db.Courses.Add(new(){Code="D6_SEPARATE_OK",Name="Good",Credits=1}); await db.SaveChangesAsync();
        }
        // Day22: separate contexts make the independent unit-of-work boundary explicit.
        await using(var db=factory())
        {
            db.Courses.Add(new(){Code="D6_SEPARATE_BAD",Name="Bad",Credits=0}); await Fail(db);
        }
        await using(var read=factory()) Check(await read.Courses.AnyAsync(c=>c.Code=="D6_SEPARATE_OK") && !await read.Courses.AnyAsync(c=>c.Code=="D6_SEPARATE_BAD"),"Separate SaveChanges boundary");
        // Extend the existing A/B experiment with C/D; do not duplicate it elsewhere.
        await using(var db=factory())
        {
            Check(!new SqlConnectionStringBuilder(db.Database.GetConnectionString()).MultipleActiveResultSets,"Expected MARS off");
            Check(!db.Database.CreateExecutionStrategy().RetriesOnFailure,"Unexpected retry strategy");
            await using var transaction=await db.Database.BeginTransactionAsync();
            Check(transaction.SupportsSavepoints,"Expected SQL Server savepoints with MARS off");
            try
            {
                db.Courses.Add(new(){Code="D6_TX_ROLL_OK",Name="First save",Credits=1}); await db.SaveChangesAsync();
                db.Courses.Add(new(){Code="D6_TX_ROLL_BAD",Name="Second save",Credits=0});
                var writeFailure=await Fail(db);
                // Fail checked the real CHECK error. Its savepoint did not undo the first save.
                Check(await db.Courses.AsNoTracking().AnyAsync(c=>c.Code=="D6_TX_ROLL_OK"),"First save should still exist inside transaction");
                try {await transaction.RollbackAsync(CancellationToken.None);}
                catch(Exception cleanup)
                {
                    Console.Error.WriteLine("Rollback failed after expected write failure: "+cleanup.GetType().Name);
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(writeFailure).Throw();
                }
                Check(db.ChangeTracker.Entries().Any(),"Rollback unexpectedly cleared tracker");
            }
            catch
            {
                try {await transaction.RollbackAsync(CancellationToken.None);}
                catch(Exception cleanup) {Console.Error.WriteLine("Rollback cleanup failed: "+cleanup.GetType().Name);}
                throw; // Preserve the primary failure, not the cleanup exception.
            }
        }
        await using(var read=factory()) Check(!await read.Courses.AnyAsync(c=>c.Code.StartsWith("D6_TX_ROLL_")),"Outer rollback left first save");
        await using(var db=factory())
        {
            await using var transaction=await db.Database.BeginTransactionAsync();
            try
            {
                db.Courses.Add(new(){Code="D6_TX_COMMIT_1",Name="First",Credits=1}); await db.SaveChangesAsync();
                db.Courses.Add(new(){Code="D6_TX_COMMIT_2",Name="Second",Credits=1}); await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                try {await transaction.RollbackAsync(CancellationToken.None);}
                catch(Exception cleanup) {Console.Error.WriteLine("Rollback cleanup failed: "+cleanup.GetType().Name);}
                throw;
            }
        }
        await using(var read=factory()) Check(await read.Courses.CountAsync(c=>c.Code.StartsWith("D6_TX_COMMIT_"))==2,"Explicit commit lost rows");
        Console.WriteLine("PASS: transactions A=neither, B=first persists, C=outer rollback neither, D=commit both; savepoint preserves first until full rollback; MARS off/retry off");
        Console.WriteLine("PASS: Detached/Added/Unchanged/Modified/Deleted; independent DB reads; projection empty tracker; single-save rollback vs separate-save persistence; failed tracker retains Added");
    }
}

sealed class OfferingRace(Func<Task> action) : SaveChangesInterceptor
{
    public string? Classification {get;private set;}
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
    {await action();return result;}
    public override Task SaveChangesFailedAsync(DbContextErrorEventData data,CancellationToken ct=default)
    {Classification=data.Exception is DbUpdateException ex ? OfferingWriteErrors.Classify(ex):null;return Task.CompletedTask;}
}
