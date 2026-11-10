using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class RefreshChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new BehaviorFailure("Refresh: " + name); }
    public sealed class SaveGate : SaveChangesInterceptor
    {
        public byte[]? CollisionHash;
        public byte[]? UncommittedHash;
        public int Calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            var entry = data.Context!.ChangeTracker.Entries<RefreshTokenRecord>().SingleOrDefault(e => e.State == EntityState.Added);
            if (CollisionHash is { } hash && entry is not null)
            {
                CollisionHash = null; Calls++;
                UncommittedHash = entry.Entity.TokenHash.ToArray();
                entry.Property(x => x.TokenHash).CurrentValue = hash.ToArray(); // Real UNIQUE failure, not a fabricated exception.
            }
            return ValueTask.FromResult(result);
        }
    }
    public static void Unit()
    {
        var token = TokenSessions.NewRefreshToken();
        Check(TokenSessions.IsValidFormat(token) && TokenSessions.Hash(token).Length == 32, "random canonical 43-character token/hash");
        foreach (var bad in new string?[] { null, "", " ", token + " ", " " + token, token + "=", new('a', 10000), "!" + token[1..] })
            Check(!TokenSessions.IsValidFormat(bad), "invalid format is not normalized");
        Check(TokenSessions.NewRefreshToken() != token, "fresh token");
        Console.WriteLine("PASS: refresh canonical input/no normalization and 32-byte hash (no DB)");
    }
    static async Task Constraint(Func<Task> action, int number, string name)
    {
        try { await action(); }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException) is { } sql && sql.Errors.Cast<SqlError>().Any(e => e.Number == number && e.Message.Contains(name, StringComparison.Ordinal))) { return; }
        throw new BehaviorFailure("Refresh constraint: expected " + number + "/" + name);
    }
    static string? migratedToken;
    public static async Task Upgrade(Func<UniversityDbContext> factory, Func<Task>? verifyHttp = null)
    {
        var user = new ApplicationUser { UserName = "migration29-" + Guid.NewGuid().ToString("N"), EmailConfirmed = true };
        var course = new Course { Code = "M28" + Guid.NewGuid().ToString("N")[..10], Name = "Synthetic migration", Credits = 2 };
        var term = new AcademicTerm { Name = "Synthetic migration", StartsOn = new(2026, 1, 1), EndsOn = new(2026, 2, 1) };
        Student? student = null; CourseOffering? offering = null; Enrollment? enrollment = null;
        Exception? failure = null;
        var revokedId = Guid.NewGuid(); var expiredId = Guid.NewGuid();
        var sessionId = Guid.NewGuid(); var token = TokenSessions.NewRefreshToken(); var hash = TokenSessions.Hash(token); var created = DateTimeOffset.UtcNow; var expires = created.AddDays(7);
        try
        {
            await using (var db = factory())
            {
                Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "upgrade only on new owned DB");
                await db.GetService<IMigrator>().MigrateAsync("20261105101325_AddRefreshSessions");
                db.AddRange(user, term); await db.SaveChangesAsync(); await CourseVersionChecks.HistoricalCourseAsync(db,course);
                var studentKeys = await db.Database.SqlQuery<int>($"INSERT INTO Students(StudentNumber,FullName,UserId) OUTPUT INSERTED.Id AS [Value] VALUES ({course.Code},{"Synthetic profile"},{user.Id})").ToListAsync();
                student = new() { Id=studentKeys.Single(),StudentNumber=course.Code,FullName="Synthetic profile" }; Check(student.TryLinkAccount(user.Id),"link");
                offering = await SeatCapacityChecks.HistoricalOfferingAsync(db,course.Id,term.Id,10);
                enrollment = StateChecks.Active(student.Id, offering.Id); db.Add(enrollment); await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO RefreshSessions (Id,UserId,RefreshTokenHash,CreatedAtUtc,ExpiresAtUtc) VALUES ({sessionId},{user.Id},{hash},{created},{expires})");
                var secondHash = TokenSessions.Hash(TokenSessions.NewRefreshToken()); var thirdHash = TokenSessions.Hash(TokenSessions.NewRefreshToken());
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO RefreshSessions (Id,UserId,RefreshTokenHash,CreatedAtUtc,ExpiresAtUtc,RevokedAtUtc) VALUES ({revokedId},{user.Id},{secondHash},{created},{expires},{created})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO RefreshSessions (Id,UserId,RefreshTokenHash,CreatedAtUtc,ExpiresAtUtc) VALUES ({expiredId},{user.Id},{thirdHash},{created.AddDays(-2)},{created.AddDays(-1)})");
                // Day31's last schema: verify the existing account/profile/enrollment before adding the advisor column.
                await db.GetService<IMigrator>().MigrateAsync("20261105103704_AddRefreshTokenHistory");
                Check((await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM Students WHERE Id={student.Id} AND UserId={user.Id}").SingleAsync())==1,"pre-advisor link exists");
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            await using var verify = factory();
            Check(await verify.Students.AnyAsync(s => s.Id == student.Id && s.UserId == user.Id && s.StudentNumber == course.Code && s.AdvisorUserId == null), "profile/identity link preserved");
            Check(await verify.Enrollments.AnyAsync(e => e.Id == enrollment!.Id && e.StudentId == student.Id && e.CourseOfferingId == offering!.Id && e.Status == EnrollmentStatus.Approved), "academic identity/status/FKs preserved");
            Check(await verify.Users.AnyAsync(u => u.Id == user.Id && u.UserName == user.UserName), "old account preserved");
            Check(!verify.Database.HasPendingModelChanges() && !(await verify.Database.GetPendingMigrationsAsync()).Any(), "migration replay/model");
            var family = await verify.RefreshSessions.AsNoTracking().SingleAsync(x => x.Id == sessionId);
            var migrated = await verify.RefreshTokenRecords.AsNoTracking().SingleAsync(x => x.SessionId == sessionId);
            Check(family.UserId == user.Id && family.CreatedAtUtc == created && family.ExpiresAtUtc == expires && family.RevokedAtUtc is null
                && migrated.TokenHash.SequenceEqual(hash) && migrated.UsedAtUtc is null, "day28 current token/history migration");
            Check(await verify.RefreshSessions.AnyAsync(x => x.Id == revokedId && x.RevokedAtUtc == created && x.ExpiresAtUtc == expires)
                && await verify.RefreshSessions.AnyAsync(x => x.Id == expiredId && x.ExpiresAtUtc == created.AddDays(-1)), "revoked/expired families preserved");
            migratedToken = token;
            if (verifyHttp is not null) await verifyHttp();
            Console.WriteLine("PASS: day28 upgrade preserves account/profile/enrollment and session identity/deadline/current hash; migrated token refreshed over HTTPS; replay/model aligned");
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            try
            {
            await using var db = factory();
            migratedToken = null;
            await db.RefreshTokenRecords.Where(x => x.SessionId == sessionId || x.SessionId == revokedId || x.SessionId == expiredId).ExecuteDeleteAsync();
            await db.RefreshSessions.Where(x => x.Id == sessionId || x.Id == revokedId || x.Id == expiredId).ExecuteDeleteAsync();
            if (enrollment?.Id > 0) await db.Enrollments.Where(x => x.Id == enrollment.Id).ExecuteDeleteAsync();
            if (offering?.Id > 0) await db.CourseOfferings.Where(x => x.Id == offering.Id).ExecuteDeleteAsync();
            if (student?.Id > 0) await db.Students.Where(x => x.Id == student.Id).ExecuteDeleteAsync();
            if (course.Id > 0) await db.Courses.Where(x => x.Id == course.Id).ExecuteDeleteAsync();
            if (term.Id > 0) await db.AcademicTerms.Where(x => x.Id == term.Id).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == user.Id).ExecuteDeleteAsync();
            }
            catch (Exception ex) { Console.Error.WriteLine("CLEANUP refresh upgrade: " + ex.GetType().Name); if (failure is null) throw new CleanupFailure(); }
        }
    }

    // Test-only barrier before SQL execution: two independent HTTP contexts reach the family lock.
    public sealed class FamilyGate : DbCommandInterceptor
    {
        public TaskCompletionSource Ready = NewSignal(), Release = NewSignal();
        public TaskCompletionSource SecondReady = NewSignal();
        int remaining;
        bool ordered;
        int entrants, holdFirst;
        public HashSet<System.Data.Common.DbConnection> Connections = [];
        static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() { ordered = false; Ready = NewSignal(); Release = NewSignal(); Connections.Clear(); remaining = 2; }
        public void ArmOrdered() { ordered = true; entrants = 0; holdFirst = 1; Ready = NewSignal(); SecondReady = NewSignal(); Release = NewSignal(); }
        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command, CommandEventData data, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken ct = default)
        {
            if ((command.CommandText.Contains("UPDLOCK") && command.CommandText.Contains("AspNetUsers")) && ordered)
            {
                if (Interlocked.Increment(ref entrants) == 2) SecondReady.TrySetResult();
                return result;
            }
            if ((command.CommandText.Contains("UPDLOCK") && command.CommandText.Contains("AspNetUsers")) && Volatile.Read(ref remaining) > 0)
            {
                lock (Connections) Connections.Add(command.Connection!);
                if (Interlocked.Decrement(ref remaining) == 0) Ready.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
        public override async ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(System.Data.Common.DbCommand command, CommandExecutedEventData data, System.Data.Common.DbDataReader result, CancellationToken ct = default)
        {
            if (ordered && (command.CommandText.Contains("UPDLOCK") && command.CommandText.Contains("AspNetUsers")) && Interlocked.Exchange(ref holdFirst, 0) == 1)
            {
                Ready.TrySetResult(); // SQL has acquired the first transaction's account lock.
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
    }
    public static async Task Run(WebApplication app, Func<HttpClient> clients, Func<UniversityDbContext> factory, string userId,
        JsonObject issued, List<string> secrets, SaveGate gate, FamilyGate familyGate, HttpClient cookie, string csrf, string password)
    {
        using var client = clients();
        var first = issued["refreshToken"]!.GetValue<string>(); secrets.Add(first);
        var access = issued["accessToken"]!.GetValue<string>();
        async Task<RefreshSession> Read(Guid id) { await using var db = factory(); return await db.RefreshSessions.AsNoTracking().SingleAsync(s => s.Id == id); }
        async Task<RefreshTokenRecord[]> Records(Guid id) { await using var db = factory(); return await db.RefreshTokenRecords.AsNoTracking().Where(s => s.SessionId == id).ToArrayAsync(); }
        async Task<(int Status, JsonObject Body)> Send(string? token, HttpClient? sender = null, string? bearer = null)
        {
            using var r = new HttpRequestMessage(HttpMethod.Post, "/accounts/refresh") { Content = JsonContent.Create(new { refreshToken = token }) };
            if (bearer is not null) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var response = await (sender ?? client).SendAsync(r); var text = await response.Content.ReadAsStringAsync();
            Check(response.Headers.CacheControl?.NoStore == true, "refresh no-store");
            var body = JsonNode.Parse(text)!.AsObject();
            if ((int)response.StatusCode == 200) { Check(body.Count == 4, "pair contract"); lock (secrets) { secrets.Add(body["refreshToken"]!.GetValue<string>()); secrets.Add(body["accessToken"]!.GetValue<string>()); } }
            else Check(!body.ContainsKey("refreshToken") && !text.Contains("SqlException") && !text.Contains("TokenHash"), "safe error/no pair");
            return ((int)response.StatusCode, body);
        }
        async Task<JsonObject> Post(string? token, int expected, string? bearer = null)
        { var r = await Send(token, bearer: bearer); Check(r.Status == expected, "refresh expected=" + expected + " actual=" + r.Status); return r.Body; }
        async Task<(RefreshSession Row, string Token)> Fixture(DateTimeOffset? end = null, bool revoked = false)
        {
            var now = DateTimeOffset.UtcNow; var token = TokenSessions.NewRefreshToken(); secrets.Add(token);
            var s = new RefreshSession { UserId = userId, CreatedAtUtc = now.AddDays(-2), ExpiresAtUtc = end ?? now.AddDays(1), RevokedAtUtc = revoked ? now : null };
            await using var db = factory(); db.Add(s); db.Add(new RefreshTokenRecord { SessionId = s.Id, TokenHash = TokenSessions.Hash(token), CreatedAtUtc = s.CreatedAtUtc }); await db.SaveChangesAsync(); return (s, token);
        }
        async Task<int> Revoke(HttpClient sender, Guid id, string? csrfToken)
        {
            using var r = new HttpRequestMessage(HttpMethod.Post, $"/accounts/sessions/{id}/revoke");
            if (csrfToken is not null) r.Headers.Add("X-CSRF-TOKEN", csrfToken);
            using var response = await sender.SendAsync(r); return (int)response.StatusCode;
        }
        if (migratedToken is not null) await Post(migratedToken, 200);
        RefreshSession original;
        await using (var db = factory()) original = await db.RefreshSessions.AsNoTracking().SingleAsync(s => s.UserId == userId);
        var initial = (await Records(original.Id)).Single();
        Check(initial.TokenHash.SequenceEqual(TokenSessions.Hash(first)) && initial.UsedAtUtc is null && original.ExpiresAtUtc-original.CreatedAtUtc == TimeSpan.FromDays(7), "initial hash-only family");
        var r2 = (await Post(first, 200))["refreshToken"]!.GetValue<string>();
        var r3 = (await Post(r2, 200))["refreshToken"]!.GetValue<string>();
        var chain = await Records(original.Id); var second = chain.Single(x => x.TokenHash.SequenceEqual(TokenSessions.Hash(r2))); var third = chain.Single(x => x.TokenHash.SequenceEqual(TokenSessions.Hash(r3)));
        Check(chain.Length == 3 && chain.Single(x => x.Id == initial.Id).ReplacedById == second.Id && second.ReplacedById == third.Id
            && chain.Count(x => x.UsedAtUtc is not null) == 2 && third.UsedAtUtc is null && (await Read(original.Id)).ExpiresAtUtc == original.ExpiresAtUtc, "R1/R2/R3 history links/fixed end");
        var independent = await Fixture();
        await Post(first, 401); Check((await Read(original.Id)).RevokedAtUtc is not null, "reuse rejection commits revocation"); await Post(r3, 401);
        await Post(independent.Token, 200); Check((await Read(independent.Row.Id)).RevokedAtUtc is null, "second family unaffected");
        await Post(TokenSessions.NewRefreshToken(), 401); Check((await Read(independent.Row.Id)).RevokedAtUtc is null, "unknown token cannot revoke family");
        foreach (var bad in new string?[] { null, "", " ", r3 + " ", new('x',10000), "bad" }) await Post(bad,400);
        var expired = await Fixture(DateTimeOffset.UtcNow.AddDays(-1)); await Post(expired.Token,401);
        var revoked = await Fixture(revoked:true); await Post(revoked.Token,401);
        var near = await Fixture(DateTimeOffset.UtcNow.AddSeconds(90)); var nearPair=await Post(near.Token,200); var jwt=new JsonWebToken(nearPair["accessToken"]!.GetValue<string>());
        Check(jwt.ValidTo<=near.Row.ExpiresAtUtc.UtcDateTime && nearPair["expiresIn"]!.GetValue<int>() is >0 and <=90, "remaining session bounds access");
        var settings=app.Services.GetRequiredService<UniversityJwtSettings>(); var keys=app.Services.GetRequiredService<UniversityJwtKeys>();
        var expiredAccess=new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer=settings.Issuer,Audience=settings.Audience,Claims=new Dictionary<string,object>{{"sub",userId}},IssuedAt=DateTime.UtcNow.AddMinutes(-20),NotBefore=DateTime.UtcNow.AddMinutes(-20),Expires=DateTime.UtcNow.AddMinutes(-10),SigningCredentials=new(keys.SigningKey!,SecurityAlgorithms.RsaSha256)});
        secrets.Add(expiredAccess); var withExpired=await Fixture(); await Post(withExpired.Token,200,expiredAccess);
        var unsuitable=await Fixture();
        await using(var scope=app.Services.CreateAsyncScope()) { var m=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); Check((await m.SetLockoutEndDateAsync((await m.FindByIdAsync(userId))!,DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded,"lock fixture"); }
        await Post(unsuitable.Token,401); Check((await Records(unsuitable.Row.Id)).Single().UsedAtUtc is null,"unsuitable doesn't consume");
        await using(var scope=app.Services.CreateAsyncScope()) { var m=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); Check((await m.SetLockoutEndDateAsync((await m.FindByIdAsync(userId))!,null)).Succeeded,"unlock fixture"); }
        // Coordinated two-connection refresh race, no timing guesses.
        var race=await Fixture(); using var other=clients(); familyGate.Arm();
        var one=Send(race.Token); var two=Send(race.Token,other);
        try { await familyGate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20)); Check(familyGate.Connections.Count==2,"independent connections"); }
        finally { familyGate.Release.TrySetResult(); }
        var responses=await Task.WhenAll(one,two);
        Check(responses.Count(x=>x.Status==200)==1 && responses.Count(x=>x.Status==401)==1,"race exactly200+401");
        var raceRecords=await Records(race.Row.Id);
        Check(raceRecords.Length==2 && raceRecords.Count(x=>x.UsedAtUtc is not null)==1 && (await Read(race.Row.Id)).RevokedAtUtc is not null,"single successor and strict reuse revokes");
        await Post(responses.Single(x=>x.Status==200).Body["refreshToken"]!.GetValue<string>(),401);
        // Real insertion UNIQUE fault after conditional consumption; whole explicit transaction rolls back.
        var fault=await Fixture(); var old=(await Records(fault.Row.Id)).Single(); gate.CollisionHash=initial.TokenHash;
        await Post(fault.Token,500); var after=await Records(fault.Row.Id);
        Check(gate.Calls==1 && after.Length==1 && after[0].UsedAtUtc is null && after[0].ReplacedById is null,"consumption and successor rolled back");
        await using(var db=factory()) Check(!await db.RefreshTokenRecords.AnyAsync(x=>x.TokenHash==gate.UncommittedHash),"uncommitted hash absent");
        await Post(fault.Token,200);
        // Ownership and CSRF: create no new account; use this host's other confirmed test account.
        ApplicationUser b; await using(var db=factory()) b=await db.Users.SingleAsync(x=>x.UserName!.StartsWith("c26") && x.UserName.EndsWith("linked@example.test"));
        using var bClient=clients();
        async Task<string> Csrf(HttpClient c) { using var r=await c.GetAsync("/accounts/antiforgery"); var value=JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>(); secrets.Add(value); return value; }
        var bCsrf=await Csrf(bClient);
        using(var login=new HttpRequestMessage(HttpMethod.Post,"/accounts/login") {Content=JsonContent.Create(new {email=b.Email,password})}) { login.Headers.Add("X-CSRF-TOKEN",bCsrf); using var response=await bClient.SendAsync(login); Check((int)response.StatusCode==200,"B login"); }
        bCsrf=await Csrf(bClient);
        var managed=await Fixture();
        Check(await Revoke(bClient,managed.Row.Id,bCsrf)==404 && await Revoke(bClient,Guid.NewGuid(),bCsrf)==404,"foreign and missing404");
        Check((await Read(managed.Row.Id)).RevokedAtUtc is null,"foreign attempt no mutation");
        Check(await Revoke(cookie,managed.Row.Id,null)==400 && await Revoke(cookie,managed.Row.Id,"invalid")==400 && (await Read(managed.Row.Id)).RevokedAtUtc is null,"CSRF rejects without mutation");
        using(var response=await bClient.GetAsync("/accounts/sessions")) { var body=JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray(); Check((int)response.StatusCode==200 && body.Count==0,"B cannot list A"); }
        using(var response=await cookie.GetAsync("/accounts/sessions")) {
            var body=JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
            await using var db=factory(); var ids=await db.RefreshSessions.Where(x=>x.UserId==userId).OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Select(x=>x.Id).ToArrayAsync();
            Check(response.Headers.CacheControl?.NoStore==true && body.Select(x=>x!["sessionId"]!.GetValue<Guid>()).SequenceEqual(ids) && body.All(x=>x!.AsObject().Count==4),"A deterministic DTO/no secrets"); }
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/accounts/sessions")) { request.Headers.Authorization=new("Bearer",access); using var response=await client.SendAsync(request); Check((int)response.StatusCode==401,"bearer cannot list"); }
        using(var request=new HttpRequestMessage(HttpMethod.Post,$"/accounts/sessions/{managed.Row.Id}/revoke")) { request.Headers.Authorization=new("Bearer",access); request.Headers.Add("X-CSRF-TOKEN",csrf); using var response=await client.SendAsync(request); Check((int)response.StatusCode==401,"bearer cannot revoke"); }
        Check(await Revoke(cookie,managed.Row.Id,csrf)==204,"own revoke"); var revokedAt=(await Read(managed.Row.Id)).RevokedAtUtc;
        Check(await Revoke(cookie,managed.Row.Id,csrf)==204 && (await Read(managed.Row.Id)).RevokedAtUtc==revokedAt,"idempotent revoke timestamp"); await Post(managed.Token,401);
        var refreshFirst=await Fixture(); var successor=(await Post(refreshFirst.Token,200))["refreshToken"]!.GetValue<string>(); Check(await Revoke(cookie,refreshFirst.Row.Id,csrf)==204,"refresh first then revoke"); await Post(successor,401);
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/accounts/bearer-me")) {request.Headers.Authorization=new("Bearer",access);using var response=await client.SendAsync(request);Check((int)response.StatusCode==200,"revocation doesn't revoke access");}
        // Force each lock acquisition order while both HTTP operations overlap.
        foreach (var revokeFirst in new[] { true, false })
        {
            var orderedFamily = await Fixture(); familyGate.ArmOrdered();
            Task<int>? revokeTask = null; Task<(int Status, JsonObject Body)>? refreshTask = null;
            if (revokeFirst) revokeTask = Revoke(cookie, orderedFamily.Row.Id, csrf);
            else refreshTask = Send(orderedFamily.Token);
            try
            {
                await familyGate.Ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
                if (revokeFirst) refreshTask = Send(orderedFamily.Token);
                else revokeTask = Revoke(cookie, orderedFamily.Row.Id, csrf);
                await familyGate.SecondReady.Task.WaitAsync(TimeSpan.FromSeconds(20));
            }
            finally { familyGate.Release.TrySetResult(); }
            Check(await revokeTask! == 204, "ordered revoke completed");
            var refreshed = await refreshTask!;
            Check(refreshed.Status == (revokeFirst ? 401 : 200), "ordered refresh/revoke outcomes");
            Check((await Read(orderedFamily.Row.Id)).RevokedAtUtc is not null && (await Records(orderedFamily.Row.Id)).Length == (revokeFirst ? 1 : 2), "ordered race persistent family state");
            if (!revokeFirst) await Post(refreshed.Body["refreshToken"]!.GetValue<string>(),401);
        }
        // Actual relational constraints, not arbitrary exception acceptance.
        async Task Sql(FormattableString sql,int number,string name) {await using var db=factory();await Constraint(()=>db.Database.ExecuteSqlInterpolatedAsync(sql),number,name);}
        await Sql($"UPDATE RefreshTokenRecords SET TokenHash={initial.TokenHash} WHERE Id={old.Id}",2601,"UX_RefreshTokens_Hash");
        await Sql($"UPDATE RefreshTokenRecords SET UsedAtUtc=DATEADD(day,-1,CreatedAtUtc) WHERE Id={old.Id}",547,"CK_RefreshTokens_Used");
        await Sql($"UPDATE RefreshTokenRecords SET ReplacedById={third.Id} WHERE Id={old.Id}",547,"FK_RefreshTokenRecords_RefreshTokenRecords");
        await Sql($"UPDATE RefreshTokenRecords SET ReplacedById=Id WHERE Id={old.Id}",547,"CK_RefreshTokens_Replacement");
        await Sql($"DELETE FROM RefreshSessions WHERE Id={original.Id}",547,"FK_RefreshTokenRecords_RefreshSessions");
        await Sql($"DELETE FROM AspNetUsers WHERE Id={userId}",547,"FK_RefreshSessions_AspNetUsers");
        Console.WriteLine("PASS: refresh families HTTPS history/R1-R2-R3/reuse committed401/current denied/other family unchanged; two connections200+401/one successor; real post-consumption INSERT fault rollback; ownership/CSRF/cookie-only/idempotent revoke; fixed expiry/access boundary; SQL constraints");
    }
}
