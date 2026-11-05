using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniversityRegistration.Accounts;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

internal static class JwtChecks
{
    const string Issuer = "https://issuer.example.test";
    const string Audience = "university-api-test";
    static void Check(bool ok, string name) { if (!ok) throw new BehaviorFailure("JWT: " + name); }
    public sealed class SqlCounter : DbCommandInterceptor
    {
        public int Reads;
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command, CommandEventData data, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken ct = default)
        { Interlocked.Increment(ref Reads); return ValueTask.FromResult(result); }
    }
    public static Dictionary<string, string?> Configuration(string directory) => new()
    {
        ["Jwt:Enabled"] = "true", ["Jwt:DevTokenEnabled"] = "true", ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience,
        ["Jwt:AccessTokenSeconds"] = "600", ["Jwt:RefreshSessionDays"] = "7",
        ["Jwt:PublicKeyPath"] = Path.Combine(directory, "public.pem"), ["Jwt:PrivateKeyPath"] = Path.Combine(directory, "private.pem")
    };
    public static void Unit(string root)
    {
        foreach (var kind in new[] { "production", "disabled", "issuer", "audience", "paths", "access-duration", "session-duration" })
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = kind == "production" ? "Production" : "Development", ContentRootPath = root });
            var values = Configuration(Path.Combine(Path.GetTempPath(), "unused-jwt"));
            if (kind == "disabled") values["Jwt:Enabled"] = "false";
            if (kind == "issuer") values["Jwt:Issuer"] = "not absolute";
            if (kind == "audience") values["Jwt:Audience"] = " ";
            if (kind == "paths") values["Jwt:PrivateKeyPath"] = "relative.pem";
            if (kind == "access-duration") values["Jwt:AccessTokenSeconds"] = "0";
            if (kind == "session-duration") values["Jwt:RefreshSessionDays"] = "-1";
            builder.Configuration.AddInMemoryCollection(values);
            var rejected = false;
            try { builder.Services.AddUniversityJwt(builder.Configuration, builder.Environment); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "startup rejects " + kind);
        }
        Console.WriteLine("PASS: JWT Production minting/disabled/issuer/audience/path configuration guards (no DB)");
    }
    public static async Task Run(WebApplication app, Func<HttpClient> clientFactory, ApplicationUser user, string password,
        List<string> secrets, SqlCounter counter, string keyDirectory, X509Certificate2 certificate, string contentRoot,
        Func<UniversityRegistration.Data.UniversityDbContext> factory, RefreshChecks.SaveGate saveGate, RefreshChecks.FamilyGate familyGate)
    {
        var privatePem = File.ReadAllText(Path.Combine(keyDirectory, "private.pem")); secrets.Add(privatePem);
        using var signer = RSA.Create(); signer.ImportFromPem(privatePem);
        using var wrongKey = RSA.Create(2048);
        var keys = app.Services.GetRequiredService<UniversityJwtKeys>();
        var privateExportRejected = false;
        try { _ = keys.VerificationKey.Rsa.ExportParameters(true); }
        catch (CryptographicException) { privateExportRejected = true; }
        Check(privateExportRejected && keys.VerificationKey.Rsa != keys.SigningKey!.Rsa, "verification object public only");
        foreach (var path in new[] { Path.Combine(keyDirectory, "absent.pem"), Path.Combine(keyDirectory, "broken.pem") })
        {
            if (path.EndsWith("broken.pem")) File.WriteAllText(path, "not a key");
            bool rejected = false; try { using var unused = UniversityJwtKeys.Load(path, null); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "missing/malformed key rejected safely");
        }
        using var cookie = clientFactory(); using var bearer = clientFactory();
        async Task<string> Csrf(HttpClient c)
        {
            using var r = await c.GetAsync("/accounts/antiforgery"); Check((int)r.StatusCode == 200, "csrf");
            var token = JsonNode.Parse(await r.Content.ReadAsStringAsync())!["requestToken"]!.GetValue<string>(); secrets.Add(token); return token;
        }
        async Task<HttpResponseMessage> Post(HttpClient c, string path, string? csrf, object? body = null)
        {
            using var r = new HttpRequestMessage(HttpMethod.Post, path); if (csrf is not null) r.Headers.Add("X-CSRF-TOKEN", csrf);
            if (body is not null) r.Content = JsonContent.Create(body); return await c.SendAsync(r);
        }
        async Task Bearer(string? token, int expected, HttpClient? c = null, string path = "/accounts/bearer-me")
        {
            using var r = new HttpRequestMessage(HttpMethod.Get, path);
            if (token is not null) { secrets.Add(token); r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); }
            var before = counter.Reads; using var response = await (c ?? bearer).SendAsync(r);
            var text = await response.Content.ReadAsStringAsync();
            Check((int)response.StatusCode == expected, "bearer expected=" + expected + " actual=" + (int)response.StatusCode);
            Check(response.Headers.Location is null && !text.Contains("<html", StringComparison.OrdinalIgnoreCase), "no redirect/HTML");
            Check(counter.Reads == before, "bearer endpoint has no DB reads");
            Check(secrets.All(x => !text.Contains(x, StringComparison.Ordinal)), "safe bearer response");
            if (expected == 200) { var json = JsonNode.Parse(text)!.AsObject(); Check(json.Count == 1 && json["accountId"]!.GetValue<string>() == user.Id, "server validated accountId"); }
            if (expected == 401) Check(!response.Headers.WwwAuthenticate.ToString().Contains("error_description"), "no authentication exception detail");
        }
        var anonymous = await Csrf(cookie);
        using (var r = await Post(cookie, "/accounts/login", anonymous, new { email = user.Email, password })) Check((int)r.StatusCode == 200, "existing cookie login");
        var csrf = await Csrf(cookie);
        Check(counter.Reads > 0, "SQL interceptor observes login account reads");
        foreach (var token in new string?[] { null, "invalid", anonymous })
        { using var r = await Post(cookie, "/accounts/dev-token", token); Check((int)r.StatusCode == 400, "mint CSRF denies"); }
        string valid;
        using (var r = await Post(cookie, "/accounts/dev-token", csrf, new { userId = "attacker-choice", studentId = 999, role = "admin", expiresIn = 999999 }))
        {
            Check((int)r.StatusCode == 200 && r.Headers.CacheControl?.NoStore == true, "mint/no-store");
            var body = JsonNode.Parse(await r.Content.ReadAsStringAsync())!.AsObject(); Check(body.Count == 4 && body["tokenType"]!.GetValue<string>() == "Bearer" && body["expiresIn"]!.GetValue<int>() == 600, "token response contract");
            valid = body["accessToken"]!.GetValue<string>(); secrets.Add(valid);
            // Inspection only for fixture assertions; production identity comes from validation.
            var jwt = new JsonWebToken(valid);
            Check(jwt.Subject == user.Id && jwt.Alg == "RS256" && jwt.Issuer == Issuer && jwt.Audiences.Single() == Audience
                && jwt.ValidTo - jwt.ValidFrom == TimeSpan.FromMinutes(10) && jwt.IssuedAt == jwt.ValidFrom
                && !string.IsNullOrWhiteSpace(jwt.Id) && !jwt.Claims.Any(x => x.Type is "role" or "email" or "studentId"), "server claims/time/no client override");
            await RefreshChecks.Run(app, clientFactory, factory, user.Id, body, secrets, saveGate, familyGate, cookie, csrf, password);
        }
        int sessionsBefore;
        await using (var db = factory()) sessionsBefore = await db.RefreshSessions.CountAsync(s => s.UserId == user.Id);
        using (var another = await Post(cookie, "/accounts/dev-token", csrf))
        {
            Check((int)another.StatusCode == 200, "second explicit issue");
            var pair = JsonNode.Parse(await another.Content.ReadAsStringAsync())!;
            secrets.Add(pair["refreshToken"]!.GetValue<string>()); secrets.Add(pair["accessToken"]!.GetValue<string>());
        }
        await using (var db = factory()) Check(await db.RefreshSessions.CountAsync(s => s.UserId == user.Id) == sessionsBefore + 1, "each initial issue creates a session");
        await Bearer(null, 401); await Bearer(null, 401, cookie); await Bearer(valid, 200); await Bearer(valid, 200);
        await Bearer("invalid", 401, cookie);
        using (var r = new HttpRequestMessage(HttpMethod.Get, "/accounts/me"))
        { r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", valid); using var response = await bearer.SendAsync(r); Check((int)response.StatusCode == 401, "bearer cannot use cookie me"); }
        using (var r = new HttpRequestMessage(HttpMethod.Post, "/accounts/dev-token"))
        { r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", valid); using var response = await bearer.SendAsync(r); Check((int)response.StatusCode == 401, "bearer cannot mint"); }
        await Bearer(null, 401, path: "/accounts/bearer-me?access_token=" + Uri.EscapeDataString(valid));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        JsonObject Payload() => new() { ["sub"] = user.Id, ["jti"] = Guid.NewGuid().ToString("N"), ["iss"] = Issuer, ["aud"] = Audience, ["iat"] = now, ["nbf"] = now - 60, ["exp"] = now + 600 };
        string Fixture(JsonObject payload, string alg = "RS256", RSA? key = null) => new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(payload.ToJsonString(), new SigningCredentials(new RsaSecurityKey(key ?? signer), alg));
        var cases = new Dictionary<string, string>();
        var pieces = valid.Split('.'); var changed = JsonNode.Parse(Base64UrlEncoder.Decode(pieces[1]))!.AsObject(); changed["sub"] = "altered";
        cases["tampered payload"] = pieces[0] + "." + Base64UrlEncoder.Encode(changed.ToJsonString()) + "." + pieces[2];
        cases["wrong key"] = Fixture(Payload(), key: wrongKey);
        foreach (var field in new[] { "iss", "aud" }) { var p = Payload(); p[field] = "wrong"; cases[field] = Fixture(p); }
        var expired = Payload(); expired["exp"] = now - 120; expired["nbf"] = now - 600; cases["expired beyond skew"] = Fixture(expired);
        var future = Payload(); future["nbf"] = now + 120; cases["future beyond skew"] = Fixture(future);
        var noExpiry = Payload(); noExpiry.Remove("exp"); cases["missing exp"] = Fixture(noExpiry);
        cases["unsigned"] = Base64UrlEncoder.Encode("{\"alg\":\"none\"}") + "." + Base64UrlEncoder.Encode(Payload().ToJsonString()) + ".";
        cases["RS512 not allowed"] = Fixture(Payload(), SecurityAlgorithms.RsaSha512);
        var missingSub = Payload(); missingSub.Remove("sub"); cases["missing sub"] = Fixture(missingSub);
        var blankSub = Payload(); blankSub["sub"] = " "; cases["blank sub"] = Fixture(blankSub);
        var manySub = Payload(); manySub["sub"] = new JsonArray(user.Id, "other"); cases["multiple sub"] = Fixture(manySub);
        var duplicateJson = Payload().ToJsonString().TrimEnd('}') + ",\"sub\":\"other\"}";
        cases["duplicate JSON sub property"] = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }
            .CreateToken(duplicateJson, new SigningCredentials(new RsaSecurityKey(signer), SecurityAlgorithms.RsaSha256));
        cases["malformed"] = "not-a-jwt";
        foreach (var item in cases) { await Bearer(item.Value, 401); Console.WriteLine("PASS: JWT HTTP rejects " + item.Key); }
        // Large margins inside/outside the 30s allowance; no sleeping on a boundary.
        var within = Payload(); within["exp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 2; within["nbf"] = now - 600; await Bearer(Fixture(within), 200);
        within = Payload(); within["nbf"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10; await Bearer(Fixture(within), 200);
        using (var r = await Post(cookie, "/accounts/logout", csrf)) Check((int)r.StatusCode == 204, "cookie logout unchanged");
        await Bearer(valid, 200);
        // Production is a public-key-only validator, never a development minting endpoint.
        var prod = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production", ContentRootPath = contentRoot });
        var config = Configuration(keyDirectory); config["Jwt:DevTokenEnabled"] = "false"; config.Remove("Jwt:PrivateKeyPath");
        prod.Configuration.AddInMemoryCollection(config); prod.Logging.ClearProviders();
        prod.WebHost.ConfigureKestrel(s => s.Listen(System.Net.IPAddress.Loopback, 0, l => l.UseHttps(certificate)));
        prod.Services.AddUniversityJwt(prod.Configuration, prod.Environment);
        await using (var host = prod.Build())
        {
            host.UseAuthentication(); host.UseAuthorization(); host.MapUniversityJwtEndpoints(); await host.StartAsync();
            using var c = clientFactory(); c.BaseAddress = new Uri(host.Urls.Single());
            using var absent = await c.PostAsync("/accounts/dev-token", null); Check((int)absent.StatusCode == 404, "production route absent");
            await Bearer(valid, 200, c); await host.StopAsync();
        }
        Console.WriteLine("PASS: JWT mint/CSRF/server claims, scheme isolation, no DB reads, replay/logout boundary, skew margins, public-only Production validation");
    }
}
