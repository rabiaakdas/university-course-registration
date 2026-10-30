using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

var root = args[0];
var mode = args.Length > 1 ? args[1] : "full";
var reverse = args.Length > 2 && bool.Parse(args[2]);
var probe = args.Length > 3 && bool.Parse(args[3]);
if (mode is "builder" or "unit" or "full")
{
    try { BuilderChecks.Run(mode == "builder" && probe); }
    catch (BehaviorFailure ex) { Console.Error.WriteLine("ASSERTION: " + ex.Message); Environment.ExitCode = 1; return; }
    if (mode == "builder") { EligibilityChecks.Unit(); return; }
}
if (mode is "adapter" or "unit" or "full")
{
    try { await AdapterChecks.Run(mode == "adapter" && probe); }
    catch (BehaviorFailure ex) { Console.Error.WriteLine("ASSERTION: " + ex.Message); Environment.ExitCode = 1; return; }
    if (mode == "adapter") return;
}
if (mode is "unit" or "decorator" or "full") await DecoratorChecks.Run();
if (mode is "state" or "full") StateChecks.Unit();
if (mode is "eligibility" or "full") EligibilityChecks.Unit();
if (mode is "specifications" or "full")
{
    SpecificationChecks.Unit();
    await SpecificationChecks.Configuration(root);
}
if (mode is "factories" or "full")
{
    try { FactoryExperiments.Run(probe); }
    catch (BehaviorFailure ex) { Console.Error.WriteLine("ASSERTION: " + ex.Message); Environment.ExitCode = 1; return; }
    if (mode == "factories") return;
}
if (mode is "unit" or "core")
{
    try { CoreBehaviorChecks.Unit(probe); }
    catch (BehaviorFailure ex) { Console.Error.WriteLine("ASSERTION: " + ex.Message); Environment.ExitCode = 1; return; }
    if (mode == "unit") return;
}
if (mode is "preview" or "full")
{
    try { PriorityChecks.Unit(); await PriorityChecks.Http(root); }
    catch (BehaviorFailure ex) { Console.Error.WriteLine("ASSERTION: " + ex.Message); Environment.ExitCode = 1; return; }
    catch (Exception ex) { Console.Error.WriteLine("ENVIRONMENT/EXECUTION preview: " + ex.GetType().Name); Environment.ExitCode = 2; return; }
    if (mode == "preview") return;
}
var database = (mode == "core" ? "university_registration_tests_day10_" : "university_registration_tests_day6_") + Guid.NewGuid().ToString("N");
var connection = new SqlConnectionStringBuilder
{
    DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = database,
    IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true
}.ConnectionString;
var options = new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(connection).Options;
UniversityDbContext Db() => new(options);
void Check(bool condition, string label) { if (!condition) throw new BehaviorFailure(label); }
void Pass(string label) => Console.WriteLine("PASS: " + label);
async Task Sql(string sql) { await using var db = Db(); await db.Database.ExecuteSqlRawAsync(sql); }
async Task<int> Count() { await using var db = Db(); return await db.Courses.CountAsync(); }
async Task<Course> Read(int id) { await using var db = Db(); return await db.Courses.SingleAsync(c => c.Id == id); }
Process? api = null;
var evaluationLogs = new EvaluationLogObserver();
var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(15) };

async Task Run(string command, params string[] arguments)
{
    var info = new ProcessStartInfo(command) { WorkingDirectory = root, UseShellExecute = false,
        CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    info.Environment["ConnectionStrings__UniversityDatabase"] = connection;
    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var text = await output; await error; // Never echo connection or raw error details.
    Check(process.ExitCode == 0, $"Child command failed: {command} {arguments.LastOrDefault()}");
    foreach (var line in text.Split('\n').Where(l => l.StartsWith("PASS"))) Console.WriteLine(line.Trim());
}
Task SqlFile(string file) => Run("sqlcmd", "-S", @"(localdb)\MSSQLLocalDB", "-E", "-d", database, "-b", "-f", "65001", "-i", file);
void StopApi() { if (api is not null) { if (!api.HasExited) { api.Kill(true); api.WaitForExit(); } api.Dispose(); api = null; } }
async Task StartApi()
{
    var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false,
        CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
    info.ArgumentList.Add(Path.Combine(root, "bin/Debug/net10.0/UniversityRegistration.dll"));
    info.ArgumentList.Add("--urls"); info.ArgumentList.Add(client.BaseAddress!.ToString());
    info.Environment["ConnectionStrings__UniversityDatabase"] = connection;
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; // Errors must be safe even here.
    info.Environment["DOTNET_ENVIRONMENT"] = "Development";
    if (mode == "decorator") { info.Environment["Logging__Console__FormatterName"] = "json"; info.Environment["Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics"] = "Information"; }
    api = Process.Start(info)!;
    api.OutputDataReceived += (_, e) => { if (mode == "decorator") evaluationLogs.Receive(e.Data); }; api.ErrorDataReceived += (_, _) => { };
    api.BeginOutputReadLine(); api.BeginErrorReadLine();
    for (var i = 0; i < 50; i++)
    {
        if (api.HasExited) throw new InvalidOperationException("API exited before readiness");
        try { using var response = await client.GetAsync("/courses"); if (response.IsSuccessStatusCode) return; }
        catch (HttpRequestException) { }
        await Task.Delay(200);
    }
    throw new Exception("API readiness timeout");
}
async Task<(int Status, JsonNode Body, string? Location)> Send(string method, string path, object? body = null, string? raw = null)
{
    var logBefore = evaluationLogs.Before(path);
    using var request = new HttpRequestMessage(new HttpMethod(method), path);
    if (raw is not null) request.Content = new StringContent(raw, Encoding.UTF8, "application/json");
    else if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await client.SendAsync(request);
    var status = (int)response.StatusCode;
    var text = await response.Content.ReadAsStringAsync();
    var json = JsonNode.Parse(text)!;
    if (status >= 400)
    {
        Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Problem Details content type");
        Check(json["status"]!.GetValue<int>() == status, "Problem Details status");
        foreach (var forbidden in new[] { "SqlException", "DbUpdateException", "ConnectionString", "stackTrace", "C:\\", "NEXT VALUE", "CK_", "UX_", "INSERT INTO" })
            Check(!text.Contains(forbidden, StringComparison.OrdinalIgnoreCase), "Sensitive error response");
    }
    else Check(response.Content.Headers.ContentType?.MediaType == "application/json", "Success content type");
    if (mode == "decorator") await evaluationLogs.Verify(method, path, status, json, logBefore);
    return (status, json, response.Headers.Location?.ToString());
}
void DetailFields(JsonNode node) => Check(string.Join(',', node.AsObject().Select(p => p.Key).Order()) == "code,credits,id,name", "Detail field contract");

// Refuse to reuse any existing database. The generated name is not user input.
var master = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
try
{
    Check(master.DataSource == @"(localdb)\MSSQLLocalDB" &&
        System.Text.RegularExpressions.Regex.IsMatch(database, @"^university_registration_tests_day(6|10)_[a-f0-9]{32}$"), "Unsafe test target");
    await using var checkConnection = new SqlConnection(master.ConnectionString);
    await checkConnection.OpenAsync();
    using var check = checkConnection.CreateCommand(); check.CommandText = "SELECT DB_ID(@name)";
    check.Parameters.AddWithValue("@name", database);
    Check(await check.ExecuteScalarAsync() is DBNull, "Test DB name already exists");
}
catch (Exception ex)
{
    Console.Error.WriteLine("ENVIRONMENT: LocalDB prerequisite/unused test target; type=" + ex.GetType().Name);
    Environment.ExitCode = 2;
    return;
}
Console.WriteLine("Test DB: " + database);
if (mode is "core" or "specifications" or "eligibility" or "state" or "decorator" or "summary")
{
    try
    {
        if (mode == "state") await StateChecks.Upgrade(Db);
        else { await using var db = Db(); await db.Database.MigrateAsync(); }
        await StartApi();
        if (mode == "core") await CoreBehaviorChecks.Run(Db, Send, reverse);
        else if (mode == "specifications") await SpecificationChecks.Run(Db, Send);
        else if (mode == "eligibility") await EligibilityChecks.Run(Db, Send);
        else if (mode == "summary") await SummaryChecks.Run(Db, Send, probe);
        else if (mode == "decorator") { await EligibilityChecks.Run(Db, Send); await StateChecks.Run(Db, Send, StopApi, StartApi); }
        else await StateChecks.Run(Db, Send, StopApi, StartApi);
        await using var verify = Db();
        Check(await verify.Enrollments.CountAsync() == 0 && await verify.Students.CountAsync() == 0
            && await verify.CourseOfferings.CountAsync() == 0 && await verify.AcademicTerms.CountAsync() == 0
            && await verify.Courses.CountAsync() == 0 && await verify.CoursePrerequisites.CountAsync() == 0
            && await verify.StudentCompletedCourses.CountAsync() == 0 && await verify.CourseOfferingTimeSlots.CountAsync() == 0, "Core fixture residue");
        Check(!verify.Database.HasPendingModelChanges(), "Model/snapshot mismatch");
        Check(!(await verify.Database.GetPendingMigrationsAsync()).Any(), "Pending migration");
        Console.WriteLine("PASS: empty test tables; migration/model alignment");
        if (mode == "decorator") Console.WriteLine($"PASS: real DI/HTTP structured timing logs; {evaluationLogs.Audited} evaluations; early rejections have no timing; no timing JSON fields");
    }
    catch (BehaviorFailure ex) { Console.Error.WriteLine("ASSERTION: " + ex.Message); Environment.ExitCode = 1; }
    catch (CleanupFailure) { Environment.ExitCode = 3; }
    catch (Exception ex) { Console.Error.WriteLine("ENVIRONMENT/EXECUTION: " + ex.GetType().Name); Environment.ExitCode = 2; }
    finally
    {
        try { StopApi(); Console.WriteLine("PASS: API process stopped"); }
        catch (Exception ex) { Console.Error.WriteLine("CLEANUP process: " + ex.GetType().Name); Environment.ExitCode = Environment.ExitCode == 0 ? 3 : Environment.ExitCode; }
    }
    return;
}
try
{
    await using (var db = Db())
        await db.GetService<IMigrator>().MigrateAsync("20261027110911_InitialUniversitySchema");
    await Sql("EXEC sys.sp_addextendedproperty @name=N'UniversityRegistrationTests', @value=N'day6';");
    await Sql("INSERT dbo.Courses (Id,Code,Name,Credits) VALUES (500,N'D5_HIGH',N'Migration sentinel',3);");
    await Sql("INSERT dbo.AcademicTerms (Id,Name,StartsOn,EndsOn) VALUES (600,N'D6_HIGH','20260101','20260101'); INSERT dbo.CourseOfferings (Id,CourseId,AcademicTermId,SectionCode,Capacity) VALUES (700,500,600,N'HIGH',1);");
    await StateChecks.Upgrade(Db);
    await using (var db = Db()) Check(await db.CourseOfferings.AnyAsync(o => o.Id==700 && o.CourseId==500 && o.AcademicTermId==600), "Migration lost FK/IDs");
    await Sql("DELETE dbo.CourseOfferings WHERE Id=700; DELETE dbo.AcademicTerms WHERE Id=600;");
    Check((await Read(500)).Code == "D5_HIGH", "Migration lost preexisting row");
    await using (var db = Db()) Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Pending migration");
    await Sql("DELETE dbo.Courses WHERE Id=500 AND Code=N'D5_HIGH';");
    await SqlFile("sql/app/seed.sql"); await SqlFile("sql/app/seed.sql");
    Check(await Count() == 2, "Seed replay duplicates");
    await SqlFile("sql/app/verify.sql"); await SqlFile("sql/app/verify-constraints.sql");
    Pass("Migration preserved Id 500; seed replay and real schema/constraint checks");

    await StartApi();
    var created = await Send("POST", "/courses", new { id = 1, code = "  d5_first  ", name = "  Türkçe Ders  ", credits = 3 });
    Check(created.Status == 201, "POST 201"); DetailFields(created.Body);
    var id = created.Body["id"]!.GetValue<int>();
    Check(id > 500 && created.Location == $"/courses/{id}", "Sequence from real max / Location / client Id ignored");
    Check(created.Body["code"]!.GetValue<string>() == "D5_FIRST" && created.Body["name"]!.GetValue<string>() == "Türkçe Ders", "Normalization");
    Check((await Read(id)).Name == "Türkçe Ders", "POST database row");
    var detail = await Send("GET", created.Location!); Check(detail.Status == 200, "Location GET"); DetailFields(detail.Body);
    var list = await Send("GET", "/courses");
    Check(list.Body.AsArray().Any(c => c!["id"]!.GetValue<int>() == id), "New row in list");
    await Run("pwsh", "-File", "scripts/verify-http.ps1", "-Port", (port + 1).ToString());
    StopApi(); await StartApi(); Check((await Send("GET", created.Location!)).Body["name"]!.GetValue<string>() == "Türkçe Ders", "Restart persistence");
    Pass("POST, Location/detail, normalization, extra-row GET contracts, restart persistence");

    var count = await Count();
    var updated = await Send("PUT", created.Location!, new { code = " d5_first ", name = " Güncel Ders ", credits = 5 });
    Check(updated.Status == 200 && updated.Body["id"]!.GetValue<int>() == id && await Count() == count, "PUT same identity/no insert");
    DetailFields(updated.Body); Check((await Read(id)).Name == "Güncel Ders" && (await Read(id)).Credits == 5, "Tracked update");
    var conflict = await Send("PUT", created.Location!, new { code = " cse201 ", name = "Must not persist", credits = 9 });
    Check(conflict.Status == 409 && (await Read(id)).Code == "D5_FIRST" && (await Read(id)).Name == "Güncel Ders", "PUT conflict preserved values");
    var duplicate = await Send("POST", "/courses", new { code = " d5_first ", name = "Duplicate", credits = 3 });
    Check(duplicate.Status == 409 && await Count() == count, "POST duplicate count");
    Check(duplicate.Body["title"]!.GetValue<string>() == conflict.Body["title"]!.GetValue<string>(), "Consistent conflicts");
    Pass("PUT self-code update, same Id, conflict preservation, duplicate POST");

    var invalidInputs = new object[] {
        new { code=(string?)null, name=(string?)null, credits=0 },
        new { code="", name="", credits=-1 }, new { code=" \t ", name=" \t ", credits=0 },
        new { code=new string('X',21), name=new string('Y',201), credits=-2 }
    };
    foreach (var input in invalidInputs)
    {
        foreach (var verb in new[] { "POST", "PUT" })
        {
            var invalid = await Send(verb, verb == "POST" ? "/courses" : created.Location!, input);
            Check(invalid.Status == 400 && invalid.Body["errors"]!.AsObject().Count == 3, "Aggregated field validation");
        }
    }
    foreach (var raw in new[] { "{", "{\"code\":\"D5_BAD\",\"name\":\"Bad\",\"credits\":\"oops\"}", "{\"code\":\"D5_BAD\",\"name\":\"Bad\",\"credits\":\"3\"}" })
        Check((await Send("POST", "/courses", raw: raw)).Status == 400, "Malformed JSON / type binding");
    Check(await Count() == count && (await Read(id)).Name == "Güncel Ders", "Invalid input changed database");
    Check((await Send("GET", "/courses/2147483647")).Status == 404, "Missing detail");
    Check((await Send("PUT", "/courses/2147483647", invalidInputs[0])).Status == 404, "PUT existence before validation");
    var commands = new CommandCounter();
    await using (var db = new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(connection).AddInterceptors(commands).Options))
    {
        var result = await CourseEndpoints.CreateAsync(new(null,null,0), db, default);
        Check(((IStatusCodeHttpResult)result).StatusCode == 400 && commands.Reads == 0, "Invalid POST queried DB");
    }
    Pass("Null/empty/long/credit validation, combined errors, binding 400, missing 404, no invalid POST query");

    var distinct = await Task.WhenAll(Enumerable.Range(0,8).Select(i => Send("POST", "/courses", new { code=$"D5_PAR_{i}", name="Parallel", credits=2 })));
    Check(distinct.All(r => r.Status == 201) && distinct.Select(r => r.Body["id"]!.GetValue<int>()).Distinct().Count() == 8, "Parallel generated IDs collide");
    var same = await Task.WhenAll(Enumerable.Range(0,8).Select(i => Send("POST", "/courses", new { code=i%2==0 ? " d5_race " : "D5_RACE", name="Race", credits=2 })));
    Check(same.Count(r => r.Status == 201) == 1 && same.Count(r => r.Status == 409) == 7, "Same code race status");
    await using (var db = Db()) Check(await db.Courses.CountAsync(c => c.Code == "D5_RACE") == 1, "Race row count");
    Pass("Eight parallel distinct IDs; same normalized code gives one 201 and seven 409");

    // Deterministic real race after the endpoint precheck, without production hooks.
    var race = new SaveRace(async () => {
        await using var competitor = Db(); competitor.Courses.Add(new() { Code="D5_SAVE_RACE", Name="Winner", Credits=2 });
        await competitor.SaveChangesAsync();
    });
    await using (var db = new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().UseSqlServer(connection).AddInterceptors(race).Options))
    {
        var result = await CourseEndpoints.CreateAsync(new("D5_SAVE_RACE","Loser",2), db, default);
        Check(((IStatusCodeHttpResult)result).StatusCode == 409 && race.SawDuplicate, "Save-stage classifier branch not exercised");
    }
    await using (var db = Db())
    {
        db.Courses.Add(new() { Id=1, Code="D5_PK", Name="PK collision", Credits=2 });
        try { await db.SaveChangesAsync(); throw new Exception("Expected PK failure"); }
        catch (DbUpdateException ex) { Check(ex.InnerException is SqlException { Number:2627 } && !CourseWriteErrors.IsDuplicateCode(ex), "PK mislabeled as code conflict"); }
    }
    await using (var db = Db())
    {
        db.CourseOfferings.Add(new() { Id=90, CourseId=1, AcademicTermId=1, SectionCode="A", Capacity=5 });
        try { await db.SaveChangesAsync(); throw new Exception("Expected unrelated unique failure"); }
        catch (DbUpdateException ex) { Check(ex.InnerException is SqlException { Number:2601 } && !CourseWriteErrors.IsDuplicateCode(ex), "Unrelated unique index mislabeled"); }
    }
    Pass("Real SaveChanges code-race branch =409; real PK and other unique index not classified as code conflict");

    // Only this freshly-created test DB gets this temporary constraint.
    await Sql("ALTER TABLE dbo.Courses ADD CONSTRAINT CK_Day5_TestOnly CHECK (Credits <> 777);");
    try
    {
        var unexpected = await Send("POST", "/courses", new { code="D5_CHECK", name="Do not leak", credits=777 });
        Check(unexpected.Status == 500, "Unexpected CHECK error was mislabeled as 409/400");
    }
    finally { await Sql("ALTER TABLE dbo.Courses DROP CONSTRAINT CK_Day5_TestOnly;"); }
    Pass("Unexpected DB error = sanitized 500 Problem Details even in Development");
    await RelatedWriteChecks.Run(Db, Send, Sql, SqlFile, StopApi, StartApi);
    await CatalogChecks.Run(Db, Send);
    await EnrollmentChecks.Run(Db, Send, StopApi, StartApi, SqlFile);
    await BatchEnrollmentChecks.Run(Db, Send, StopApi, StartApi);
    await SpecificationChecks.Run(Db, Send);
    await EligibilityChecks.Run(Db, Send);
    await StateChecks.Run(Db, Send, StopApi, StartApi);
    await SummaryChecks.Run(Db, Send);
    await SqlFile("sql/app/academic-example.sql");
    await SqlFile("sql/app/time-slot-example.sql");
    await SqlFile("sql/app/time-slot-example.sql");
    await Run("pwsh", "-NoProfile", "-File", "scripts/verify-http.ps1", "-Port", (port+1).ToString());
}
finally
{
    StopApi();
    // Never touch app/lab; remove only this runner's prefixed rows in its unique DB.
    await Sql("IF DB_NAME() NOT LIKE N'university[_]registration[_]tests[_]day6[_]%' THROW 51000,'Wrong cleanup target',1; IF OBJECT_ID(N'dbo.CourseOfferings') IS NOT NULL DELETE dbo.CourseOfferings WHERE Id>=700; IF OBJECT_ID(N'dbo.AcademicTerms') IS NOT NULL DELETE dbo.AcademicTerms WHERE Name LIKE N'D6[_]%'; IF OBJECT_ID(N'dbo.Courses') IS NOT NULL DELETE dbo.Courses WHERE Code LIKE N'D5[_]%' OR Code LIKE N'D6[_]%';");
    Console.WriteLine("Cleanup: API stopped; owned test rows removed; test DB retained with seed. " + database);
}

sealed class SaveRace(Func<Task> competitor) : SaveChangesInterceptor
{
    public bool SawDuplicate { get; private set; }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    { await competitor(); return result; }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken ct = default)
    { SawDuplicate = data.Exception is DbUpdateException ex && CourseWriteErrors.IsDuplicateCode(ex); return Task.CompletedTask; }
}
sealed class CommandCounter : DbCommandInterceptor
{
    public int Reads { get; private set; }
    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command, CommandEventData data, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken ct = default)
    { Reads++; return ValueTask.FromResult(result); }
}
