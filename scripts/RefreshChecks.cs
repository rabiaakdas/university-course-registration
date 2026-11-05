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
            var entry = data.Context!.ChangeTracker.Entries<RefreshSession>().SingleOrDefault(e => e.State == EntityState.Modified);
            if (CollisionHash is { } hash && entry is not null)
            {
                CollisionHash = null; Calls++;
                UncommittedHash = entry.Entity.RefreshTokenHash.ToArray();
                entry.Property(x => x.RefreshTokenHash).CurrentValue = hash.ToArray(); // Real UNIQUE failure, not a fabricated exception.
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
    public static async Task Upgrade(Func<UniversityDbContext> factory)
    {
        var user = new ApplicationUser { UserName = "migration28-" + Guid.NewGuid().ToString("N") };
        var course = new Course { Code = "M28" + Guid.NewGuid().ToString("N")[..10], Name = "Synthetic migration", Credits = 2 };
        var term = new AcademicTerm { Name = "Synthetic migration", StartsOn = new(2026, 1, 1), EndsOn = new(2026, 2, 1) };
        Student? student = null; CourseOffering? offering = null; Enrollment? enrollment = null;
        Exception? failure = null;
        try
        {
            await using (var db = factory())
            {
                Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "upgrade only on new owned DB");
                await db.GetService<IMigrator>().MigrateAsync("20261101104350_AddIdentityAccounts");
                student = new() { StudentNumber = course.Code, FullName = "Synthetic profile" }; Check(student.TryLinkAccount(user.Id), "link");
                db.AddRange(user, course, term, student); await db.SaveChangesAsync();
                offering = new() { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "A", Capacity = 10 }; db.Add(offering); await db.SaveChangesAsync();
                enrollment = StateChecks.Active(student.Id, offering.Id); db.Add(enrollment); await db.SaveChangesAsync();
                await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            }
            await using var verify = factory();
            Check(await verify.Students.AnyAsync(s => s.Id == student.Id && s.UserId == user.Id && s.StudentNumber == course.Code), "profile/identity link preserved");
            Check(await verify.Enrollments.AnyAsync(e => e.Id == enrollment!.Id && e.StudentId == student.Id && e.CourseOfferingId == offering!.Id && e.Status == EnrollmentStatus.Approved), "academic identity/status/FKs preserved");
            Check(await verify.Users.AnyAsync(u => u.Id == user.Id && u.UserName == user.UserName) && !await verify.RefreshSessions.AnyAsync(), "old account preserved/no automatic sessions");
            Check(!verify.Database.HasPendingModelChanges() && !(await verify.Database.GetPendingMigrationsAsync()).Any(), "migration replay/model");
            Console.WriteLine("PASS: previous Identity schema upgrade preserves account/profile/enrollment IDs/status/FKs; repeated update no-op");
        }
        catch (Exception ex) { failure = ex; throw; }
        finally
        {
            try
            {
            await using var db = factory();
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
    public static async Task Run(WebApplication app, Func<HttpClient> clients, Func<UniversityDbContext> factory, string userId,
        JsonObject issued, List<string> secrets, SaveGate gate)
    {
        using var client = clients(); // No authentication cookie or access token.
        var first = issued["refreshToken"]!.GetValue<string>(); secrets.Add(first);
        var access = issued["accessToken"]!.GetValue<string>();
        async Task<RefreshSession> Read(Guid id) { await using var db = factory(); return await db.RefreshSessions.AsNoTracking().SingleAsync(s => s.Id == id); }
        async Task<JsonObject> Post(string? token, int status, string? bearer = null)
        {
            using var r = new HttpRequestMessage(HttpMethod.Post, "/accounts/refresh") { Content = JsonContent.Create(new { refreshToken = token }) };
            if (bearer is not null) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var response = await client.SendAsync(r); var text = await response.Content.ReadAsStringAsync();
            Check((int)response.StatusCode == status && response.Headers.CacheControl?.NoStore == true, "refresh expected=" + status + " actual=" + (int)response.StatusCode);
            var body = JsonNode.Parse(text)!.AsObject();
            if (status == 200) { Check(body.Count == 4, "pair contract"); secrets.Add(body["refreshToken"]!.GetValue<string>()); secrets.Add(body["accessToken"]!.GetValue<string>()); }
            else Check(secrets.All(s => !text.Contains(s)) && !text.Contains("SqlException") && !text.Contains("RefreshTokenHash") && !body.ContainsKey("refreshToken"), "safe error/no pair");
            return body;
        }
        RefreshSession original;
        await using (var db = factory()) original = await db.RefreshSessions.AsNoTracking().SingleAsync(s => s.UserId == userId);
        Check(original.RefreshTokenHash.SequenceEqual(TokenSessions.Hash(first)) && original.RefreshTokenHash.Length == 32
            && original.CreatedAtUtc.Offset == TimeSpan.Zero && original.ExpiresAtUtc - original.CreatedAtUtc == TimeSpan.FromDays(7), "initial server session/hash/fixed seven days");
        Check(typeof(RefreshSession).GetProperties().All(p => p.Name != "RefreshToken" && p.Name != "AccessToken"), "no raw token column model");
        var second = await Post(first, 200); var r2 = second["refreshToken"]!.GetValue<string>();
        Check(r2 != first, "R1 -> R2"); await Post(first, 401);
        var row = await Read(original.Id); Check(row.RefreshTokenHash.SequenceEqual(TokenSessions.Hash(r2)) && row.ExpiresAtUtc == original.ExpiresAtUtc && row.UserId == original.UserId, "same session/user/deadline/new hash");
        // A genuinely expired signed access token is irrelevant to refresh authorization.
        var settings = app.Services.GetRequiredService<UniversityJwtSettings>(); var keys = app.Services.GetRequiredService<UniversityJwtKeys>();
        var expiredAccess = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor { Issuer = settings.Issuer, Audience = settings.Audience,
            Claims = new Dictionary<string, object> { ["sub"] = userId }, IssuedAt = DateTime.UtcNow.AddMinutes(-20), NotBefore = DateTime.UtcNow.AddMinutes(-20), Expires = DateTime.UtcNow.AddMinutes(-10), SigningCredentials = new(keys.SigningKey!, SecurityAlgorithms.RsaSha256) });
        secrets.Add(expiredAccess);
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/accounts/bearer-me"))
        { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredAccess); using var response = await client.SendAsync(request); Check((int)response.StatusCode == 401, "expired access actually rejected over HTTPS"); }
        var third = await Post(r2, 200, expiredAccess); var current = third["refreshToken"]!.GetValue<string>(); await Post(r2, 401);
        var thirdRow = await Read(original.Id);
        Check(thirdRow.RefreshTokenHash.SequenceEqual(TokenSessions.Hash(current)) && thirdRow.ExpiresAtUtc == original.ExpiresAtUtc && thirdRow.UserId == original.UserId, "R3 preserves session identity/deadline");
        await using (var db = factory()) Check(await db.RefreshSessions.CountAsync(s => s.UserId == userId) == 1, "rotation doesn't add sessions");
        using (var r = new HttpRequestMessage(HttpMethod.Get, "/accounts/bearer-me"))
        { r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access); using var result = await client.SendAsync(r); Check((int)result.StatusCode == 200, "old access not revoked by rotation"); }
        foreach (var bad in new string?[] { null, "", " ", current + " ", new('x', 10000), "bad" }) await Post(bad, 400);
        await Post(TokenSessions.NewRefreshToken(), 401);
        var beforeReject = await Read(original.Id);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var u = (await users.FindByIdAsync(userId))!;
            Check((await users.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded, "lockout fixture");
        }
        await Post(current, 401);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); Check((await users.SetLockoutEndDateAsync((await users.FindByIdAsync(userId))!, null)).Succeeded, "restore fixture");
        }
        Check((await Read(original.Id)).RefreshTokenHash.SequenceEqual(beforeReject.RefreshTokenHash), "unsuitable account leaves hash unchanged");
        async Task<(RefreshSession Row, string Token)> Fixture(DateTimeOffset created, DateTimeOffset end, DateTimeOffset? revoked = null)
        {
            var token = TokenSessions.NewRefreshToken(); secrets.Add(token);
            var s = new RefreshSession { UserId = userId, CreatedAtUtc = created, ExpiresAtUtc = end, RevokedAtUtc = revoked, RefreshTokenHash = TokenSessions.Hash(token) };
            await using var db = factory(); db.Add(s); await db.SaveChangesAsync(); return (s, token);
        }
        var now = DateTimeOffset.UtcNow;
        var expired = await Fixture(now.AddDays(-8), now.AddDays(-1)); await Post(expired.Token, 401);
        var revoked = await Fixture(now.AddMinutes(-1), now.AddDays(1), now); await Post(revoked.Token, 401);
        Check((await Read(expired.Row.Id)).RefreshTokenHash.SequenceEqual(expired.Row.RefreshTokenHash) && (await Read(revoked.Row.Id)).RefreshTokenHash.SequenceEqual(revoked.Row.RefreshTokenHash), "expired/revoked unchanged");
        var near = await Fixture(now.AddMinutes(-1), now.AddSeconds(90)); var nearPair = await Post(near.Token, 200);
        var nearJwt = new JsonWebToken(nearPair["accessToken"]!.GetValue<string>());
        Check(nearJwt.ValidTo <= near.Row.ExpiresAtUtc.UtcDateTime && nearPair["expiresIn"]!.GetValue<int>() is > 0 and <= 90
            && nearJwt.ValidTo - nearJwt.IssuedAt == TimeSpan.FromSeconds(nearPair["expiresIn"]!.GetValue<int>()), "access capped to fixed session end");
        gate.CollisionHash = revoked.Row.RefreshTokenHash;
        var beforeFault = await Read(original.Id); await Post(current, 500);
        Check(gate.Calls == 1 && gate.UncommittedHash is not null, "real update unique fault reached");
        await using (var db = factory()) Check(!await db.RefreshSessions.AnyAsync(x => x.RefreshTokenHash == gate.UncommittedHash), "uncommitted replacement hash absent");
        Check((await Read(original.Id)).RefreshTokenHash.SequenceEqual(beforeFault.RefreshTokenHash), "failed save preserves old current hash");
        await Post(current, 200); // Old current token still usable because update failed.
        // Fresh context for each real SQL protection; failed trackers are never reused.
        async Task Sql(Func<UniversityDbContext, Task> action, int number, string name)
        { await using var db = factory(); await Constraint(() => action(db), number, name); }
        await Sql(db => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM AspNetUsers WHERE Id={userId}"), 547, "FK_RefreshSessions_AspNetUsers");
        await Sql(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RefreshSessions SET UserId={"missing-" + Guid.NewGuid()} WHERE Id={original.Id}"), 547, "FK_RefreshSessions_AspNetUsers");
        await Sql(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RefreshSessions SET RefreshTokenHash={revoked.Row.RefreshTokenHash} WHERE Id={original.Id}"), 2601, "UX_RefreshSessions_TokenHash");
        await Sql(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RefreshSessions SET ExpiresAtUtc=CreatedAtUtc WHERE Id={original.Id}"), 547, "CK_RefreshSessions_Expiry");
        await Sql(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RefreshSessions SET RevokedAtUtc=DATEADD(day,-1,CreatedAtUtc) WHERE Id={original.Id}"), 547, "CK_RefreshSessions_Revocation");
        await Sql(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE RefreshSessions SET RefreshTokenHash=NULL WHERE Id={original.Id}"), 515, "RefreshTokenHash");
        await using (var db = factory()) Check(await db.Users.AnyAsync(x => x.Id == userId) && (await db.RefreshSessions.FindAsync(original.Id))!.ExpiresAtUtc == original.ExpiresAtUtc, "constraint failures preserve account/session");
        Console.WriteLine("PASS: refresh HTTPS R1/R2/R3, fixed row/deadline, hash-only, no cookie/access required, expired access ignored, old access valid, rejection preservation, expiry cap; real save UNIQUE500/old token usable/uncommitted hash absent; FK/NO ACTION/UNIQUE/CHECK/NOT NULL");
    }
}
