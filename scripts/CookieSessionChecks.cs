using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using UniversityRegistration.Accounts;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

internal static class CookieSessionChecks
{
    const string CookieName = "__Host-University.Account";
    static void Check(bool ok, string name) { if (!ok) throw new BehaviorFailure("Cookie session: " + name); }
    sealed class Clock : TimeProvider
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now += value;
    }
    sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines = new();
        public ILogger CreateLogger(string name) => new Sink(this);
        public void Dispose() { }
        sealed class Sink(Logs owner) : ILogger
        {
            public IDisposable? BeginScope<T>(T state) where T : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<T>(LogLevel level, EventId id, T state, Exception? error, Func<T, Exception?, string> format) => owner.Lines.Enqueue(format(state, error));
        }
    }
    public static async Task Run(string root, Func<UniversityDbContext> factory, bool probe = false, bool jwt = false, bool abuse = false, bool web = false, bool corsLab = false, bool oidc = false)
    {
        if (probe) throw new BehaviorFailure("Cookie session: controlled runner failure");
        var tag = "c26" + Guid.NewGuid().ToString("N");
        var directory = Path.GetFullPath(Path.Combine(root, "obj", "cookie-checks", tag));
        Directory.CreateDirectory(directory);
        var keyDirectory = Path.Combine(Path.GetTempPath(), "university-jwt-" + tag);
        var clock = new Clock(); var logs = new Logs(); var secrets = new List<string>();
        var abuseObservation = new AuthAbuseChecks.Observation();
        var password = " " + Guid.NewGuid().ToString("N") + "Az9! "; secrets.Add(password);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        using var generatedCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        // Reimport for Windows Schannel; no certificate/private-key file or trust-store mutation.
        using var certificate = X509CertificateLoader.LoadPkcs12(generatedCertificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        await using var oidcChecks = oidc ? new OidcChecks() : null;
        WebApplication? app = null; int? studentId = null; Exception? primary = null;
        try
        {
            if (oidcChecks is not null) await oidcChecks.Start(root, certificate);
            string connection; await using (var db = factory()) connection = db.Database.GetConnectionString()!;
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = directory, WebRootPath = web || oidc ? Path.Combine(root, "wwwroot") : null });
            b.Configuration["Jwt:Enabled"] = "false";
            b.Configuration["Jwt:DevTokenEnabled"] = "false";
            b.Logging.ClearProviders(); b.Logging.AddProvider(logs);
            b.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
            b.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
            b.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, oidcChecks?.Port ?? 0, listen => listen.UseHttps(certificate)));
            b.Services.AddDbContext<UniversityDbContext>(o => o.UseSqlServer(connection));
            var queries = new JwtChecks.SqlCounter();
            var saveGate = new RefreshChecks.SaveGate();
            var familyGate = new RefreshChecks.FamilyGate();
            var resetFault = new PasswordResetChecks.ResetFault();
            var resetSender = new PasswordResetChecks.Sender();
            if (jwt)
            {
                Directory.CreateDirectory(keyDirectory);
                using var signingRsa = RSA.Create(2048);
                File.WriteAllText(Path.Combine(keyDirectory, "private.pem"), signingRsa.ExportPkcs8PrivateKeyPem());
                File.WriteAllText(Path.Combine(keyDirectory, "public.pem"), signingRsa.ExportSubjectPublicKeyInfoPem());
                b.Configuration.AddInMemoryCollection(JwtChecks.Configuration(keyDirectory));
                b.Services.AddDbContext<UniversityDbContext>(o => o.AddInterceptors(queries, saveGate, familyGate, resetFault));
                b.Logging.AddFilter("Microsoft.AspNetCore.Authentication.JwtBearer", LogLevel.None);
                b.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
            }
            if (jwt)
            {
                b.Configuration["Accounts:LocalMailboxEnabled"] = "true";
                b.Configuration["Accounts:ConfirmationPageUrl"] = "https://callback.example.test/confirm";
                b.Configuration["Accounts:PasswordResetPageUrl"] = "https://callback.example.test/reset";
            }
            if (oidcChecks is not null) oidcChecks.SetConfiguration(b);
            b.Services.AddAccountIdentity(b.Configuration, b.Environment);
            if (oidcChecks is not null) { oidcChecks.Configure(b, certificate); b.Logging.AddFilter("Microsoft.AspNetCore.Authentication.OpenIdConnect", LogLevel.None); b.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning); }
            AuthAbuseTestSettings.Configure(b, abuse || web, browser: web);
            if (abuse) AuthAbuseChecks.Configure(b, abuseObservation);
            b.Services.AddScoped<UniversityRegistration.Application.CourseCatalogReader>();
            if (jwt) b.Services.AddSingleton<IAccountConfirmationSender>(sp => { resetSender.Inner = new LocalAccountConfirmationSender(sp.GetRequiredService<LocalConfirmationSettings>()); return resetSender; });
            b.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
            b.Services.AddUniversityJwt(b.Configuration, b.Environment);
            b.Services.AddDataProtection().SetApplicationName(tag);
            b.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, o => o.TimeProvider = clock);
            b.Services.Configure<SecurityStampValidatorOptions>(o => o.TimeProvider = clock);
            if (jwt) RolesChecks.Configure(b);
            ResourceAccessChecks.Configure(b);
            app = b.Build();
            app.UseExceptionHandler(new Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
            {
                SuppressDiagnosticsCallback = _ => true,
                ExceptionHandler = async c => await Results.Problem(statusCode: 500, title: "İstek işlenemedi.").ExecuteAsync(c)
            });
            if (abuse) app.Use(async (context, next) => { context.Connection.RemoteIpAddress = abuseObservation.Address; await next(context); });
            if (web || oidc) app.UseUniversityWebClient();
            if (oidc) OidcChecks.Map(app);
            app.UseRouting(); app.UseAuthentication(); app.UseAuthenticationAbuse(); app.UseAuthorization(); app.MapAccountSessionEndpoints(); app.MapUniversityJwtEndpoints(); app.MapAccountEndpoints(); app.MapCourseEndpoints(); app.MapAcademicTermEndpoints(); app.MapCourseOfferingEndpoints();
            if (jwt) RolesChecks.Map(app);
            app.MapStudentEndpoints(); app.MapEnrollmentEndpoints(); app.MapTermRegistrationSummaryEndpoint();
            await app.StartAsync();
            var address = new Uri(app.Urls.Single());
            HttpClient Client(CookieContainer? jar = null) => new(new HttpClientHandler
            {
                CookieContainer = jar ?? new(), AllowAutoRedirect = false,
                // Only this run's exact certificate is trusted. Never a global TLS bypass.
                ServerCertificateCustomValidationCallback = (_, cert, _, _) => cert is not null && cert.RawData.AsSpan().SequenceEqual(certificate.RawData)
            }) { BaseAddress = address };
            async Task<T> Manager<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
            { await using var scope = app.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()); }
            async Task<ApplicationUser> Create(string suffix, bool confirmed)
            {
                return await Manager(async m =>
                {
                    var u = new ApplicationUser { UserName = tag + suffix + "@example.test", Email = tag + suffix + "@example.test" };
                    Check((await m.CreateAsync(u, password)).Succeeded, "fixture create");
                    if (confirmed) { var token = await m.GenerateEmailConfirmationTokenAsync(u); secrets.Add(token); Check((await m.ConfirmEmailAsync(u, token)).Succeeded, "fixture confirm"); }
                    secrets.Add(u.PasswordHash!); return u;
                });
            }
            var user = await Create("ready", true); var unconfirmed = await Create("pending", false);
            var linked = await Create("linked", true);
            await using (var db = factory())
            {
                var student = new Student { StudentNumber = tag[..25], FullName = "Cookie test profile" };
                Check(student.TryLinkAccount(linked.Id), "fixture link"); db.Add(student); await db.SaveChangesAsync(); studentId = student.Id;
            }
            async Task<string[]> Academic()
            {
                await using var db = factory();
                return [string.Join('|', await db.Students.OrderBy(x => x.Id).Select(x => x.Id + ":" + x.UserId).ToListAsync()),
                    string.Join('|', await db.Enrollments.OrderBy(x => x.Id).Select(x => x.Id + ":" + x.Status).ToListAsync()),
                    (await db.Courses.CountAsync()).ToString(), (await db.CourseOfferings.CountAsync()).ToString()];
            }
            var before = await Academic();
            if (oidcChecks is not null) { await oidcChecks.Run(app, factory, Create, user, unconfirmed); var afterOidc = await Academic(); Check(before.SequenceEqual(afterOidc), "OIDC preserves academics"); Check(logs.Lines.All(line => secrets.Concat(oidcChecks.Secrets).All(secret => !line.Contains(secret, StringComparison.Ordinal))), "OIDC logs contain no fixture password/hash/code/verifier/token/client secret"); return; }
            if (web)
            {
                await WebClientChecks.Run(root, app, factory, user, password, certificate, directory, corsLab);
                var afterWeb = await Academic(); Check(before.SequenceEqual(afterWeb), "browser preserves academic state");
                Check(logs.Lines.All(line => secrets.All(secret => !line.Contains(secret, StringComparison.Ordinal))), "browser host logs contain no fixture credentials/hash");
                return;
            }
            if (abuse)
            {
                await AuthAbuseChecks.Run(app, () => Client(), factory, Create, user, unconfirmed, password, secrets, abuseObservation, queries, resetSender, logs.Lines);
                var afterAbuse = await Academic(); Check(before.SequenceEqual(afterAbuse), "abuse checks preserve academic data");
                return;
            }
            if (!jwt)
            {
                using var disabledClient = Client();
                using var disabled = await disabledClient.PostAsync("/accounts/dev-token", null);
                Check((int)disabled.StatusCode == 404, "disabled dev-token route absent");
            }
            else await JwtChecks.Run(app, () => Client(), user, password, secrets, queries, keyDirectory, certificate, directory, factory, saveGate, familyGate);
            async Task<string> Csrf(HttpClient client)
            {
                using var r = await client.GetAsync("/accounts/antiforgery"); Check(r.StatusCode == HttpStatusCode.OK, "csrf GET");
                Check(r.Headers.CacheControl?.NoStore == true, "csrf not cacheable");
                var token = (JsonNode.Parse(await r.Content.ReadAsStringAsync())!)["requestToken"]!.GetValue<string>(); secrets.Add(token); return token;
            }
            async Task<HttpResponseMessage> Post(HttpClient c, string path, object? body, string? token)
            {
                using var r = new HttpRequestMessage(HttpMethod.Post, path); if (body is not null) r.Content = JsonContent.Create(body);
                if (token is not null) r.Headers.Add("X-CSRF-TOKEN", token);
                return await c.SendAsync(r);
            }
            async Task Me(HttpClient c, int status)
            {
                using var r = await c.GetAsync("/accounts/me"); var text = await r.Content.ReadAsStringAsync();
                Check((int)r.StatusCode == status && r.Headers.Location is null && !text.Contains("<html", StringComparison.OrdinalIgnoreCase), "me status/no redirect/HTML");
                if (status == 200) { var json = JsonNode.Parse(text)!.AsObject(); Check(json.Count == 2 && json["id"]!.GetValue<string>() == user.Id, "me small DTO/account not StudentId"); }
                Check(secrets.All(x => !text.Contains(x, StringComparison.Ordinal)), "no secrets in me");
            }
            var jar = new CookieContainer(); using var client = Client(jar); using var stranger = Client();
            await Me(client, 401); await Me(stranger, 401);
            var anonymousToken = await Csrf(client);
            foreach (var token in new string?[] { null, "invalid" })
            {
                using var r = await Post(client, "/accounts/login", new { email = user.Email, password }, token);
                Check((int)r.StatusCode == 400 && jar.GetCookies(address)[CookieName] is null, "login CSRF denies/no cookie");
            }
            using (var r = await Post(client, "/accounts/login", new { email = " ", password = "" }, anonymousToken)) Check((int)r.StatusCode == 400, "invalid fields");
            using (var r = new HttpRequestMessage(HttpMethod.Post, "/accounts/login") { Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json") })
            { r.Headers.Add("X-CSRF-TOKEN", anonymousToken); using var response = await client.SendAsync(r); Check((int)response.StatusCode == 400, "malformed JSON"); }
            string? denial = null;
            foreach (var pair in new[] { (user.Email, "wrong password"), (tag + "missing@example.test", password), (unconfirmed.Email, password) })
            {
                using var r = await Post(client, "/accounts/login", new { email = pair.Item1, password = pair.Item2 }, anonymousToken);
                var text = await r.Content.ReadAsStringAsync(); denial ??= text;
                Check((int)r.StatusCode == 401 && text == denial && jar.GetCookies(address)[CookieName] is null, "common safe 401/no auth cookie");
            }
            await using (var db = factory()) Check((await db.Users.SingleAsync(x => x.Id == user.Id)).AccessFailedCount == 1, "lockout failure counted");
            using (var r = await Post(client, "/accounts/login", new { email = " " + user.Email!.ToUpperInvariant() + " ", password }, anonymousToken))
            {
                Check((int)r.StatusCode == 200, "confirmed login");
                var header = r.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(CookieName + "="));
                Check(header.Contains("httponly", StringComparison.OrdinalIgnoreCase) && header.Contains("secure", StringComparison.OrdinalIgnoreCase)
                    && header.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) && !header.Contains("expires=", StringComparison.OrdinalIgnoreCase)
                    && !header.Contains("max-age=", StringComparison.OrdinalIgnoreCase), "secure strict session cookie");
                var body = await r.Content.ReadAsStringAsync();
                Check(secrets.All(x => !body.Contains(x)), "no login response secrets");
            }
            var cookie = jar.GetCookies(address)[CookieName]!.Value; secrets.Add(cookie);
            var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
            var ticket = options.TicketDataFormat.Unprotect(cookie)!;
            Check(!ticket.Properties.IsPersistent && ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc == TimeSpan.FromMinutes(20) && !options.SlidingExpiration, "20 minute nonpersistent ticket");
            await Me(client, 200); await Me(stranger, 401);
            foreach (var token in new string?[] { null, "invalid", anonymousToken })
            { using var r = await Post(client, "/accounts/logout", null, token); Check((int)r.StatusCode == 400, "logout CSRF/mismatched identity"); await Me(client, 200); }
            var corruptJar = new CookieContainer(); corruptJar.Add(address, new Cookie(CookieName, "corrupt", "/") { Secure = true }); using var corrupt = Client(corruptJar); await Me(corrupt, 401);
            var missingIdJar = new CookieContainer();
            var missingIdTicket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], IdentityConstants.ApplicationScheme)),
                new AuthenticationProperties { IssuedUtc = clock.GetUtcNow(), ExpiresUtc = clock.GetUtcNow().AddMinutes(20) }, IdentityConstants.ApplicationScheme);
            missingIdJar.Add(address, new Cookie(CookieName, options.TicketDataFormat.Protect(missingIdTicket), "/") { Secure = true }); using var missingId = Client(missingIdJar); await Me(missingId, 401);
            clock.Advance(TimeSpan.FromMinutes(11));
            using (var r = await client.GetAsync("/accounts/me")) Check((int)r.StatusCode == 200 && !r.Headers.Contains("Set-Cookie"), "no sliding renewal beyond halfway");
            var logoutToken = await Csrf(client);
            using (var r = await Post(client, "/accounts/logout", null, logoutToken)) Check((int)r.StatusCode == 204, "logout");
            await Me(client, 401);
            var copiedJar = new CookieContainer(); copiedJar.Add(address, new Cookie(CookieName, cookie, "/") { Secure = true }); using var copied = Client(copiedJar);
            await Me(copied, 200); // Sign-out did not revoke a previously copied ticket.
            clock.Advance(TimeSpan.FromMinutes(10)); await Me(copied, 401);
            var lockoutUser = await Create("locked", true);
            using var lockoutClient = Client(); var lockoutToken = await Csrf(lockoutClient);
            for (var i = 0; i < 5; i++) { using var r = await Post(lockoutClient, "/accounts/login", new { email = lockoutUser.Email, password = "wrong" }, lockoutToken); Check((int)r.StatusCode == 401, "lockout attempt"); }
            using (var r = await Post(lockoutClient, "/accounts/login", new { email = lockoutUser.Email, password }, lockoutToken)) Check((int)r.StatusCode == 401, "locked correct password denied");
            Check(await Manager(async m => await m.IsLockedOutAsync((await m.FindByIdAsync(lockoutUser.Id))!)), "real Identity lockout");
            if (jwt) await RolesChecks.Run(app, () => Client(), factory, Create, password, secrets, linked, clock.Advance);
            if (jwt) await ResourceAccessChecks.Run(app, () => Client(), factory, Create, password, secrets, queries);
            if (jwt) await PasswordResetChecks.Run(app, () => Client(), factory, Create, password, clock.Advance, secrets, familyGate, resetFault, resetSender);
            var after = await Academic(); Check(before.SequenceEqual(after), "academic rows/status/account links preserved");
            Check(logs.Lines.All(line => secrets.All(secret => !line.Contains(secret, StringComparison.Ordinal))), "captured logs contain no password/hash/cookie/token");
            Console.WriteLine("PASS: HTTPS pinned certificate; Identity login/me/logout; common denial/lockout; CSRF identity binding; secure session cookie; controlled 20-minute expiry/no sliding; copied-cookie limit; academic data unchanged");
        }
        catch (Exception ex)
        {
            primary = ex;
            Console.Error.WriteLine("Cookie execution types: " + ex.GetType().Name + "/" + ex.InnerException?.GetType().Name + "/" + ex.InnerException?.InnerException?.GetType().Name);
            Console.Error.WriteLine("Cookie locations: " + string.Join(';', new System.Diagnostics.StackTrace(ex, true).GetFrames().Where(x => x.GetFileName()?.EndsWith("CookieSessionChecks.cs") == true).Select(x => x.GetFileLineNumber())));
            throw;
        }
        finally
        {
            try
            {
                if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
                await using var db = factory();
                await db.RefreshTokenRecords.Where(x => db.RefreshSessions.Any(s => s.Id == x.SessionId && db.Users.Any(u => u.Id == s.UserId && u.UserName!.StartsWith(tag)))).ExecuteDeleteAsync();
                await db.RefreshSessions.Where(x => db.Users.Any(u => u.Id == x.UserId && u.UserName!.StartsWith(tag))).ExecuteDeleteAsync();
                if (studentId is not null) await db.Students.Where(x => x.Id == studentId).ExecuteDeleteAsync();
                await db.UserLogins.Where(x => db.Users.Any(u => u.Id == x.UserId && u.UserName!.StartsWith(tag))).ExecuteDeleteAsync();
                await db.Users.Where(x => x.UserName!.StartsWith(tag)).ExecuteDeleteAsync();
                if (oidcChecks?.OwnedRoleId is { } roleId) await db.Roles.Where(r => r.Id == roleId).ExecuteDeleteAsync();
                var allowed = Path.GetFullPath(Path.Combine(root, "obj", "cookie-checks")) + Path.DirectorySeparatorChar;
                if (!directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(directory) != tag) throw new InvalidOperationException("Unsafe cleanup");
                Directory.Delete(directory, true);
                if (jwt && Directory.Exists(keyDirectory))
                {
                    if (Path.GetFileName(keyDirectory) != "university-jwt-" + tag || Path.GetDirectoryName(Path.GetFullPath(keyDirectory)) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) throw new InvalidOperationException("Unsafe key cleanup");
                    Directory.Delete(keyDirectory, true);
                }
                Console.WriteLine("PASS: cookie test-owned profile/accounts/keys removed; HTTPS host disposed");
            }
            catch (Exception ex) { Console.Error.WriteLine("CLEANUP cookie: " + ex.GetType().Name); if (primary is null) throw new CleanupFailure(); }
        }
    }
}
