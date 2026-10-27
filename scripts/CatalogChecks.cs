using System.Data.Common;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

static class CatalogChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task Run(Func<UniversityDbContext> factory,
        Func<string,string,object?,string?,Task<(int Status,JsonNode Body,string? Location)>> send)
    {
        var samples = Enumerable.Range(0,53).Select(i => new Course {
            Code=$"D7C{i:000}", Name=$"D7NameNeedle {i:000}", Credits=i%2==0?11:12 }).ToList();
        foreach(var pair in new[] {("PERCENT","D7 special %"),("UNDER","D7 special _"),("QUOTE","D7 special '"),("ACCENT","D7 café"),("PLAIN","D7 cafe")})
            samples.Add(new() {Code="D7_"+pair.Item1,Name=pair.Item2,Credits=13});
        try
        {
            await using(var db=factory()) {db.Courses.AddRange(samples); await db.SaveChangesAsync();}
            async Task<JsonNode> Catalog(string query="")
            {
                var response=await send("GET","/courses/catalog"+query,null,null);
                Check(response.Status==200,"Catalog status");
                Check(string.Join(',',response.Body.AsObject().Select(p=>p.Key).Order())=="items,page,pageSize,totalCount","Envelope fields");
                foreach(var item in response.Body["items"]!.AsArray())
                    Check(string.Join(',',item!.AsObject().Select(p=>p.Key).Order())=="code,id,name","DTO fields");
                return response.Body;
            }
            int[] Ids(JsonNode node) => node["items"]!.AsArray().Select(n=>n!["id"]!.GetValue<int>()).ToArray();
            var result=await Catalog();
            Check(result["page"]!.GetValue<int>()==1 && result["pageSize"]!.GetValue<int>()==20 && Ids(result).Length==20,"Defaults");
            await using(var db=factory()) Check(result["totalCount"]!.GetValue<int>()==await db.Courses.CountAsync(),"Unfiltered count");
            var code=await Catalog("?search=D7C&pageSize=100");
            Check(Ids(code).SequenceEqual(samples.Take(53).Select(c=>c.Id)),"Code-only search/order");
            Check(Ids(await Catalog("?search=D7NameNeedle&pageSize=100")).SequenceEqual(Ids(code)),"Name-only search");
            Check(Ids(await Catalog("?credits=11&pageSize=100")).SequenceEqual(samples.Where(c=>c.Credits==11).Select(c=>c.Id)),"Credit equality");
            Check((await Catalog("?search=D7C&credits=11&pageSize=100"))["totalCount"]!.GetValue<int>()==27,"AND filters");
            Check((await Catalog("?search=D7C&credits=13"))["totalCount"]!.GetValue<int>()==0,"AND excludes unmatched");
            Check(Ids(await Catalog("?search=%20%20D7C%20&pageSize=100")).SequenceEqual(Ids(code)),"Search trim");
            Check(JsonNode.DeepEquals(result,await Catalog("?search=%20%20")) && JsonNode.DeepEquals(result,await Catalog("?search=")),"Blank search");
            var empty=await Catalog("?search=D7NoSuchCourse");
            Check(Ids(empty).Length==0 && empty["totalCount"]!.GetValue<int>()==0,"No match");
            var all=new List<int>();
            for(var page=1;page<=6;page++)
            {
                var part=await Catalog($"?search=D7C&page={page}&pageSize=10");
                Check(part["totalCount"]!.GetValue<int>()==53 && Ids(part).Length==(page==6?3:10),"Page bounds/count");
                Check(part["page"]!.GetValue<int>()==page && part["pageSize"]!.GetValue<int>()==10,"Page metadata");
                all.AddRange(Ids(part));
            }
            Check(all.SequenceEqual(Ids(code)) && all.Distinct().Count()==53,"Stable pages no omissions/duplicates");
            Check(Ids(await Catalog("?search=D7C&page=7&pageSize=10")).Length==0,"Beyond last page");
            Check(Ids(await Catalog("?page=2147483647&pageSize=1")).Length==0,"Large supported offset");
            foreach(var special in new[] {"%","_","'"})
            {
                var matched=Ids(await Catalog("?search="+Uri.EscapeDataString("D7 special "+special)));
                Check(matched.SequenceEqual(samples.Where(c=>c.Name=="D7 special "+special).Select(c=>c.Id)),"Literal Contains: "+special);
            }
            Check(Ids(await Catalog("?search=d7c&pageSize=100")).SequenceEqual(Ids(code)),"CI collation");
            Check(Ids(await Catalog("?search="+Uri.EscapeDataString("D7 café"))).SequenceEqual(samples.Where(c=>c.Code=="D7_ACCENT").Select(c=>c.Id)),"Accent-sensitive collation");
            foreach(var query in new[] {"page=0","page=-1","pageSize=0","pageSize=-1","pageSize=101","credits=0","credits=-1","page=2147483647&pageSize=100"})
            {
                var response=await send("GET","/courses/catalog?"+query,null,null);
                Check(response.Status==400 && response.Body["errors"] is JsonObject,"Range validation fields");
            }
            var errors=await send("GET","/courses/catalog?page=0&pageSize=101&credits=0",null,null);
            Check(errors.Body["errors"]!.AsObject().Count==3,"Combined query errors");
            foreach(var field in new[] {"page","pageSize","credits"})
                foreach(var value in new[] {"abc","1.5","2147483648","-2147483649"})
                    Check((await send("GET",$"/courses/catalog?{field}={value}",null,null)).Status==400,"Numeric binding");
            Check((await send("GET",$"/courses/{samples[0].Id}",null,null)).Body["credits"]!.GetValue<int>()==11,"Detail route preserved");
            var oldList=await send("GET","/courses",null,null);
            Check(oldList.Body is JsonArray && oldList.Body.AsArray().Count>53 && oldList.Body.AsArray().All(n=>n!.AsObject().Count==3),"Old list unpaged");
            Console.WriteLine("PASS: catalog HTTP defaults/filters/literal search/collation/6 pages/count/overflow/binding/routes/contracts");

            await SqlEvidence(factory);
            await Tracking(factory,samples[0].Id);
        }
        finally
        {
            // IDs belong only to this runner's explicitly created samples; app/lab never used.
            var ids=samples.Where(c=>c.Id>0).Select(c=>c.Id).ToArray();
            await using var db=factory();
            await db.Courses.Where(c=>ids.Contains(c.Id) && c.Code.StartsWith("D7")).ExecuteDeleteAsync();
        }
    }

    static async Task SqlEvidence(Func<UniversityDbContext> factory)
    {
        await using var template=factory();
        var capture=new CatalogSqlCapture();
        await using var db=new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>()
            .UseSqlServer(template.Database.GetConnectionString()).AddInterceptors(capture).Options);
        await CourseCatalogEndpoint.GetAsync(db,default,"D7C",11,2,7);
        Check(capture.Commands.Count==2,"Expected only two executed commands (no per-row queries)");
        foreach(var command in capture.Commands)
        {
            var sql=Regex.Replace(command.Text,@"\s+"," ").ToUpperInvariant();
            var where=sql[(sql.IndexOf("WHERE",StringComparison.Ordinal)+5)..];
            Check(sql.Contains("WHERE") && where.Contains("[CODE]") && where.Contains("[NAME]") && where.Contains("[CREDITS]") && where.Contains(" OR ") && where.Contains(" AND "),"Both filters in executed WHERE");
            Check(!sql.Contains("D7C") && command.Parameters.OfType<string>().Any(s=>s.Contains("D7C")) && command.Parameters.Any(p=>p is int n && n==11),"Parameterized search/credits");
        }
        Check(capture.Commands[0].Text.Contains("COUNT(",StringComparison.OrdinalIgnoreCase),"Separate count first");
        var page=capture.Commands[1].Text;
        Check(Regex.IsMatch(page,@"ORDER\s+BY.*\[Id\]",RegexOptions.IgnoreCase|RegexOptions.Singleline)
            && page.Contains("OFFSET",StringComparison.OrdinalIgnoreCase) && page.Contains("FETCH",StringComparison.OrdinalIgnoreCase),"SQL ordering/pagination");
        var select=page[..page.IndexOf("FROM",StringComparison.OrdinalIgnoreCase)];
        var fields=Regex.Matches(select,@"\[([^\]]+)\]").Select(m=>m.Groups[1].Value).Where(n=>new[]{"Id","Code","Name","Credits"}.Contains(n)).ToArray();
        Check(fields.SequenceEqual(new[]{"Id","Code","Name"}) && !select.Contains('*'),"Only DTO columns selected");
        Check(capture.Commands[1].Parameters.Any(p=>p is int n && n==7),"Pagination parameter");
        Check(!db.ChangeTracker.Entries().Any(),"Catalog tracks no entity");
        capture.Commands.Clear();
        await CourseCatalogEndpoint.GetAsync(db,default,page:0);
        Check(capture.Commands.Count==0,"Invalid catalog queried DB");
        Console.WriteLine("PASS: two real completed SQL commands; filtered COUNT and WHERE/ORDER BY/OFFSET FETCH/3-column page; parameters; no N+1");
    }

    static async Task Tracking(Func<UniversityDbContext> factory,int id)
    {
        string original;
        await using(var db=factory())
        {var entity=await db.Courses.SingleAsync(c=>c.Id==id);original=entity.Name;Check(db.ChangeTracker.Entries<Course>().Count()==1,"Entity query tracking");}
        await using(var db=factory())
        {
            var entity=await db.Courses.AsNoTracking().SingleAsync(c=>c.Id==id);
            Check(!db.ChangeTracker.Entries<Course>().Any(),"AsNoTracking entity");
            entity.Name="D7 unsaved detached change";Check(await db.SaveChangesAsync()==0,"Detached save wrote data");
        }
        await using(var db=factory()) Check((await db.Courses.SingleAsync(c=>c.Id==id)).Name==original,"Detached change reached DB");
        await using(var db=factory())
        {await db.Courses.Where(c=>c.Id==id).Select(c=>new CourseListItemDto(c.Id,c.Code,c.Name)).ToListAsync();Check(!db.ChangeTracker.Entries<Course>().Any(),"DTO projection tracks");}
        await using(var db=factory())
        {await db.Courses.Where(c=>c.Id==id).Select(c=>new {Entity=c,c.Code}).ToListAsync();Check(db.ChangeTracker.Entries<Course>().Count()==1,"Entity projection tracking");}
        Console.WriteLine("PASS: fresh contexts entity=tracked, AsNoTracking=untracked, DTO=untracked, entity projection=tracked; detached edit not saved");
    }
}

sealed class CatalogSqlCapture : DbCommandInterceptor
{
    public List<(string Text,object[] Parameters)> Commands {get;}=[];
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,
        DbDataReader result,CancellationToken cancellationToken=default)
    {
        // Memory only; never print/commit SQL or parameter values.
        Commands.Add((command.CommandText,command.Parameters.Cast<DbParameter>().Select(p=>p.Value!).ToArray()));
        return ValueTask.FromResult(result);
    }
}
