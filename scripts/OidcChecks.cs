using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal sealed class OidcChecks : IAsyncDisposable
{
    Process? process; Task<string>? stderr; HttpClient? backchannel; string current = "startup";
    public ConcurrentBag<string> Secrets { get; } = new();
    readonly ConcurrentDictionary<string, string> failures = new();
    readonly ConcurrentDictionary<string, bool> validated = new();
    public string? OwnedRoleId { get; private set; }
    public int Port { get; }
    public string Authority { get; private set; } = "";
    readonly string secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    static void Check(bool ok, string name) { if (!ok) throw new BehaviorFailure("OIDC: " + name); }
    public OidcChecks()
    { using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; }
    public async Task Start(string root, X509Certificate2 cert)
    {
        ConfigurationChecks();
        var start = new ProcessStartInfo("node") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(root, "scripts/browser/oidc-checks.mjs"));
        process = Process.Start(start)!; stderr = process.StandardError.ReadToEndAsync();
        using var key = cert.GetRSAPrivateKey()!;
        await Send(new { address = $"https://127.0.0.1:{Port}", secret, cert = cert.ExportCertificatePem(), key = key.ExportPkcs8PrivateKeyPem(), pin = Convert.ToBase64String(SHA256.HashData(key.ExportSubjectPublicKeyInfo())) });
        var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Check(line is not null && line.Contains("ready"), "real provider started"); Authority = JsonDocument.Parse(line![7..]).RootElement.GetProperty("ready").GetString()!;
    }
    static void ConfigurationChecks()
    {
        foreach (var variant in new[] { "production", "http", "secret", "authority", "client" })
        {
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = variant == "production" ? "Production" : "Development" });
            b.Configuration["Oidc:Enabled"] = "true"; b.Configuration["Oidc:Authority"] = variant == "http" ? "http://localhost" : variant == "authority" ? "" : "https://localhost:5555";
            b.Configuration["Oidc:ClientId"] = variant == "client" ? "" : "test"; b.Configuration["Oidc:ClientSecret"] = variant == "secret" ? "" : "not-a-real-secret";
            bool rejected = false; try { b.Services.AddUniversityOidc(b.Configuration, b.Environment); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "invalid/Production configuration " + variant);
        }
        Console.WriteLine("PASS: enabled OIDC rejects Production and incomplete/non-HTTPS configuration");
    }
    Task Send(object value) => process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value));
    public void SetConfiguration(WebApplicationBuilder b)
    {
        b.Configuration["Oidc:Enabled"] = "true"; b.Configuration["Oidc:Authority"] = Authority; b.Configuration["Oidc:ClientId"] = "university-test"; b.Configuration["Oidc:ClientSecret"] = secret;
        }
    public void Configure(WebApplicationBuilder b, X509Certificate2 certificate)
    {
        b.Services.Configure<OpenIdConnectOptions>(UniversityOidc.Scheme, o =>
        {
            o.Backchannel = backchannel = new HttpClient(new Backchannel(this, new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && cert.RawData.AsSpan().SequenceEqual(certificate.RawData) }));
            var failure = o.Events.OnRemoteFailure;
            o.Events.OnRemoteFailure = async c => { var names = new List<string>(); for (Exception? e = c.Failure; e is not null; e = e.InnerException) names.Add(e.GetType().Name); failures[current] = string.Join('/', names); Console.WriteLine("OIDC control: " + current + ": " + failures[current]); await failure(c); };
            var token = o.Events.OnTokenValidated;
            o.Events.OnTokenValidated = async c => { validated[current] = true; await token(c); };
        });
    }
    sealed class Backchannel(OidcChecks owner, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath != "/token") return await base.SendAsync(request, ct);
            var text = await request.Content!.ReadAsStringAsync(ct);
            foreach (var value in Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(text).Values) if (value.ToString().Length > 10) owner.Secrets.Add(value.ToString());
            if (owner.current == "verifier")
            { var fields = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(text).ToDictionary(x => x.Key, x => x.Value.ToString()); fields["code_verifier"] = new string('x', 43); request.Content = new FormUrlEncodedContent(fields); }
            var response = await base.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) { using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); foreach (var key in new[] { "id_token", "access_token" }) if (document.RootElement.TryGetProperty(key, out var value)) owner.Secrets.Add(value.GetString()!); }
            if (owner.current == "replay")
            {
                Check(response.IsSuccessStatusCode, "first code exchange succeeded before replay"); response.Dispose();
                using var second = new HttpRequestMessage(HttpMethod.Post, request.RequestUri) { Content = new StringContent(text, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded") };
                var repeated = await base.SendAsync(second, ct); Check((int)repeated.StatusCode == 400, "real provider rejected redeemed code"); return repeated;
            }
            if (owner.current == "verifier") Check((int)response.StatusCode == 400, "real provider rejected wrong verifier");
            return response;
        }
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/test/oidc-local", async (HttpContext c) => {
            var result = await c.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            return Results.Ok(new { instructor = c.User.IsInRole(UniversityRoles.Instructor), studentAffairs = c.User.IsInRole(UniversityRoles.StudentAffairs), savedTokens = result.Properties!.GetTokens().Any() });
        }).RequireAuthorization();
    }
    public async Task Run(WebApplication app, Func<UniversityDbContext> factory, Func<string, bool, Task<ApplicationUser>> create, ApplicationUser ready, ApplicationUser pending)
    {
        var locked = await create("oidclock", true); var mfa = await create("oidcmfa", true);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(UniversityRoles.Instructor)) { var role = new IdentityRole(UniversityRoles.Instructor); Check((await roles.CreateAsync(role)).Succeeded, "fixture role"); OwnedRoleId = role.Id; }
            Check((await users.AddToRoleAsync((await users.FindByIdAsync(ready.Id))!, UniversityRoles.Instructor)).Succeeded, "local role");
            Check((await users.SetLockoutEndDateAsync((await users.FindByIdAsync(locked.Id))!, DateTimeOffset.UtcNow.AddHours(1))).Succeeded, "lockout fixture");
            Check((await users.SetTwoFactorEnabledAsync((await users.FindByIdAsync(mfa.Id))!, true)).Succeeded, "MFA fixture");
        }
        foreach (var pair in new[] { (ready.Id,"linked"), (pending.Id,"pending"), (locked.Id,"locked"), (mfa.Id,"mfa") })
            Check(await ExternalLoginMaintenance.RunAsync(app.Services, app.Environment, ["link",pair.Id,pair.Item2]) == 0, "manager link");
        Check(await ExternalLoginMaintenance.RunAsync(app.Services, app.Environment, ["link",ready.Id,"linked"]) == 0, "idempotent link");
        Check(await ExternalLoginMaintenance.RunAsync(app.Services, app.Environment, ["link",pending.Id,"linked"]) == 1, "duplicate external key denied");
        Check(new UniversityOidcSettings(true, Authority).LoginProvider != new UniversityOidcSettings(true, "https://other.example.test").LoginProvider, "issuer bound login provider");
        int accounts; await using (var db = factory()) accounts = await db.Users.CountAsync();
        await Send(new { run = true, accountId = ready.Id, email = ready.Email });
        bool done = false;
        while (await process!.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromMinutes(2)) is { } line)
        {
            if (!line.StartsWith("OIDC35:")) continue;
            using var json = JsonDocument.Parse(line[7..]); var r = json.RootElement;
            if (r.TryGetProperty("test", out var test)) { current = test.GetString()!; await Send(new { ack = true }); }
            if (r.TryGetProperty("pass", out var pass)) Console.WriteLine("PASS: real OIDC browser " + pass.GetString());
            if (r.TryGetProperty("failure", out var failure)) throw new BehaviorFailure("OIDC browser failed at " + failure.GetString());
            if (r.TryGetProperty("done", out _)) { done = true; break; }
        }
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); Check(done && process.ExitCode == 0, "browser completed");
        foreach (var name in new[] { "nonce", "missing-nonce" }) Check(validated.ContainsKey(name) && failures.GetValueOrDefault(name, "").Contains("Nonce"), name + " reached nonce validation after signature/issuer/audience");
        foreach (var pair in new[] { ("issuer","Issuer"), ("audience","Audience"), ("signature","Signature"), ("expired","Expired") })
            Check(failures.GetValueOrDefault(pair.Item1, "").Contains(pair.Item2), pair.Item1 + " actual rejection point");
        foreach (var name in new[] { "sub-empty", "sub-multiple" }) Check(failures.ContainsKey(name), "invalid subject rejected");
        foreach (var name in new[] { "state", "correlation", "verifier", "replay", "unavailable" }) Check(failures.ContainsKey(name) && !validated.ContainsKey(name), name + " rejected before ID token acceptance");
        await using (var db = factory()) Check(await db.Users.CountAsync() == accounts && !await db.Students.AnyAsync(s => s.UserId == ready.Id), "no automatic account/profile creation");
        await using (var db = factory()) Check(await db.Users.Where(u => u.Id == ready.Id || u.Id == locked.Id || u.Id == pending.Id || u.Id == mfa.Id).AllAsync(u => u.AccessFailedCount == 0), "external attempts do not increment password failure count");
        Console.WriteLine("PASS: actual protocol rejection points, no automatic account/profile, local roles only; token endpoint is server-side");
    }
    public async ValueTask DisposeAsync()
    { if (process is not null) { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); if (stderr is not null) _ = await stderr; process.Dispose(); } backchannel?.Dispose(); }
}
