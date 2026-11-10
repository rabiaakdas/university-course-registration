using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class CourseVersionChecks
{
    static void Check(bool ok,string message){if(!ok)throw new BehaviorFailure("Course version: "+message);}
    public static async Task<JsonObject> WithCurrentVersion(HttpClient client,string path,object body)
    {
        var json=JsonSerializer.SerializeToNode(body)!.AsObject();
        if(!json.ContainsKey("rowVersion"))
        {
            using var response=await client.GetAsync(path);
            if(response.IsSuccessStatusCode)json["rowVersion"]=JsonNode.Parse(await response.Content.ReadAsStringAsync())!["rowVersion"]!.DeepClone();
        }
        return json;
    }
    // Historical schemas lack RowVersion. Explicit columns avoid INSERT OUTPUT of a nonexistent column.
    public static async Task HistoricalCourseAsync(UniversityDbContext db,Course course)
    {
        var ids=await db.Database.SqlQuery<int>($"INSERT dbo.Courses(Code,Name,Credits) OUTPUT INSERTED.Id AS [Value] VALUES({course.Code},{course.Name},{course.Credits})").ToListAsync();
        db.Entry(course).Property(c=>c.Id).CurrentValue=ids.Single();
        db.Entry(course).State=EntityState.Detached;
    }
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        var c=new Course{Code="V38M"+Guid.NewGuid().ToString("N")[..8],Name="Historical course",Credits=4};
        try
        {
            await using(var db=factory())
            {
                Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(),"upgrade only on fresh test DB");
                await db.GetService<IMigrator>().MigrateAsync("20261110110730_AddOccupiedSeats");
                await HistoricalCourseAsync(db,c);
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            await using var verify=factory();var row=await verify.Courses.SingleAsync(x=>x.Id==c.Id);
            Check(row.Code==c.Code && row.Name==c.Name && row.Credits==c.Credits && row.RowVersion.Length==8,"upgrade preserves old fields and generates version");
            Check(!verify.Database.HasPendingModelChanges() && !(await verify.Database.GetPendingMigrationsAsync()).Any(),"model/replay aligned");
            Console.WriteLine("PASS: course rowversion upgrade preserves old row; generated 8-byte version; second update/model aligned");
        }
        finally {await using var db=factory();await db.Courses.Where(x=>x.Id==c.Id).ExecuteDeleteAsync();}
    }
    public static async Task Run(Func<UniversityDbContext> factory,CatalogTestIdentity identity)
    {
        var tag="V38"+Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var c=new Course{Code=tag,Name="Before",Credits=3};
        var other=new Course{Code=tag+"B",Name="Unique target",Credits=2};
        using var first=await identity.NewRoleClientAsync(UniversityRoles.StudentAffairs);
        using var second=await identity.NewRoleClientAsync(UniversityRoles.StudentAffairs);
        using var student=await identity.NewRoleClientAsync(UniversityRoles.Student);
        using var anonymous=identity.AnonymousClient();
        string path="";
        async Task<JsonObject> Detail(HttpClient client)
        {using var r=await client.GetAsync(path);Check((int)r.StatusCode==200,"detail200");return JsonNode.Parse(await r.Content.ReadAsStringAsync())!.AsObject();}
        async Task<(int Status,JsonNode Body)> Put(HttpClient client,object body,string? target=null)
        {using var r=await client.PutAsJsonAsync(target??path,body);Check(r.Content.Headers.ContentType?.MediaType is "application/json" or "application/problem+json","JSON not redirect");return ((int)r.StatusCode,JsonNode.Parse(await r.Content.ReadAsStringAsync())!);}
        try
        {
            await using(var db=factory()){db.AddRange(c,other);await db.SaveChangesAsync();} path="/courses/"+c.Id;
            var a=await Detail(first);var b=await Detail(second);var v=a["rowVersion"]!.GetValue<string>();
            Check(Convert.FromBase64String(v).Length==8 && b["rowVersion"]!.GetValue<string>()==v,"independent HTTPS clients read same DB version");
            var success=await Put(first,new{code=" "+tag.ToLowerInvariant()+" ",name=" First writer ",credits=4,rowVersion=v});
            Check(success.Status==200 && success.Body["name"]!.GetValue<string>()=="First writer" && success.Body["code"]!.GetValue<string>()==tag,"first write and normalization");
            var next=success.Body["rowVersion"]!.GetValue<string>();Check(next!=v,"success returns new version");
            var stale=await Put(second,new{code=tag,name="Lost update",credits=9,rowVersion=v});
            Check(stale.Status==409 && stale.Body["code"]!.GetValue<string>()=="course_version_conflict","stale writer409");
            await using(var db=factory()){var saved=await db.Courses.SingleAsync(x=>x.Id==c.Id);Check(saved.Name=="First writer" && saved.Credits==4 && Convert.ToBase64String(saved.RowVersion)==next,"fresh context retains winner");}
            Check((await Put(second,new{code=tag,name="First writer",credits=4,rowVersion=v})).Status==409,"stale identical payload also checked");
            var fresh=await Detail(second);var current=fresh["rowVersion"]!.GetValue<string>();
            var retry=await Put(second,new{code=tag,name="Reviewed edit",credits=5,rowVersion=current});
            Check(retry.Status==200 && retry.Body["rowVersion"]!.GetValue<string>()!=current,"explicit fresh-version edit succeeds");
            current=retry.Body["rowVersion"]!.GetValue<string>();
            foreach(var invalid in new string?[]{null,"","bad",Convert.ToBase64String(new byte[7]),Convert.ToBase64String(new byte[9])," "+current})
            {var r=await Put(first,new{code=tag,name="Invalid",credits=3,rowVersion=invalid});Check(r.Status==400 && r.Body["errors"]?["rowVersion"] is not null,"invalid version field400");}
            Check((await Put(first,new{code=tag,name="Missing",credits=3})).Status==400,"missing version400");
            Check((await Put(first,new{code=tag,name="Bad credits",credits=-1,rowVersion=current})).Status==400,"credit validation preserved");
            var duplicate=await Put(first,new{code=other.Code,name="Duplicate",credits=1,rowVersion=current});
            Check(duplicate.Status==409 && duplicate.Body["code"] is null,"code uniqueness distinct from concurrency");
            Check((await Put(first,new{code=tag,name="Missing",credits=1},"/courses/2147483647")).Status==404,"initially missing404 before field validation");
            var valid=new{code=tag,name="Denied",credits=2,rowVersion=current};
            Check((await Put(anonymous,valid)).Status==401 && (await Put(student,valid)).Status==403,"auth/role rejection");
            var csrf=first.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single(); first.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            try{Check((await Put(first,valid)).Status==400,"CSRF rejection");}finally{first.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf);}
            await using(var db=factory()){var saved=await db.Courses.SingleAsync(x=>x.Id==c.Id);Check(saved.Name=="Reviewed edit" && saved.Credits==5 && Convert.ToBase64String(saved.RowVersion)==current,"all denials leave version and business data unchanged");}
            foreach(var url in new[]{"/courses","/courses/catalog"})
            {using var r=await first.GetAsync(url);var json=JsonNode.Parse(await r.Content.ReadAsStringAsync())!;var items=url.EndsWith("catalog")?json["items"]!.AsArray():json.AsArray();Check(items.All(x=>x!.AsObject().Select(p=>p.Key).Order().SequenceEqual(new[]{"code","id","name"})),"list DTO unchanged");}
            Console.WriteLine("PASS: rowversion two HTTPS clients/first200/stale409/fresh200; identical stale payload; Base64/400/404; auth/CSRF/normalization/credit/unique; fresh-context winner and unchanged lists");
        }
        finally {await using var db=factory();await db.Courses.Where(x=>x.Id==c.Id || x.Id==other.Id).ExecuteDeleteAsync();}
    }
}
