using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

internal static class WebClientChecks
{
    static void Check(bool value, string name) { if (!value) throw new BehaviorFailure("Web client: " + name); }
    static async Task ProductionBoundary(string root, Func<UniversityDbContext> factory, X509Certificate2 certificate, string directory)
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var certPath = Path.Combine(directory, "production-test.pfx"); var certPassword = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(certPath, certificate.Export(X509ContentType.Pfx, certPassword));
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Path.Combine(root,"bin/Debug/net10.0/UniversityRegistration.dll"), "--contentRoot", directory, "--webroot", Path.Combine(root,"wwwroot"), "--urls", $"https://127.0.0.1:{port}" }) start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production"; start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["Kestrel__Certificates__Default__Path"] = certPath; start.Environment["Kestrel__Certificates__Default__Password"] = certPassword;
        start.Environment["EnrollmentPolicy__MaxCreditsPerTerm"] = "30";
        start.Environment["Accounts__LocalMailboxEnabled"] = "false"; start.Environment["Jwt__Enabled"] = "false"; start.Environment["Jwt__DevTokenEnabled"] = "false";
        AuthAbuseTestSettings.Configure(start);
        await using (var db = factory()) start.Environment["ConnectionStrings__UniversityDatabase"] = db.Database.GetConnectionString();
        using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, ServerCertificateCustomValidationCallback = (_, c, _, _) => c is not null && c.RawData.AsSpan().SequenceEqual(certificate.RawData) }) { BaseAddress = new Uri($"https://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(3) };
            bool started = false;
            for (var i = 0; i < 100 && !process.HasExited; i++)
            { try { using var response = await http.GetAsync("/"); if (response.IsSuccessStatusCode) { started = true; break; } } catch (HttpRequestException) { } await Task.Delay(100); }
            Check(started, "actual Production process serves web root over pinned HTTPS");
            foreach (var path in new[] { "/open", "/closed", "/docs/web34-canary.txt", "/.local/accounts/messages/web34-canary.txt", "/.local/accounts/keys/web34-canary.txt" })
            { using var r = await http.GetAsync(path); Check((int)r.StatusCode == 404, "Production no lab/private routes"); }
            using var noToken = await http.PostAsync("/accounts/dev-token", null); Check((int)noToken.StatusCode == 404, "Production dev-token absent");
            Console.WriteLine("PASS: actual Production executable HTTPS static page; no CORS lab/dev-token/private canary routes");
        }
        finally { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); _ = await stdout; _ = await stderr; }
    }
    public static async Task Run(string root, WebApplication app, Func<UniversityDbContext> factory,
        ApplicationUser user, string password, X509Certificate2 certificate, string directory, bool labOnly = false)
    {
        var hosts = new List<WebApplication>(); Course? course = null; Process? browser = null;
        var counts = new ConcurrentDictionary<string, int>();
        async Task<WebApplication> PageHost()
        {
            var b = WebApplication.CreateBuilder(); b.Logging.ClearProviders();
            b.WebHost.ConfigureKestrel(s => s.Listen(IPAddress.Loopback, 0, l => l.UseHttps(certificate)));
            var h = b.Build(); hosts.Add(h);
            h.MapGet("/", () => Results.Content("<!doctype html><title>Isolated CORS lab</title><p>Local browser test</p>", "text/html"));
            await h.StartAsync(); return h;
        }
        try
        {
            await using (var db = factory())
            {
                course = new Course { Code = "WEB34-" + Guid.NewGuid().ToString("N")[..12], Name = "<img src=x onerror=window.web34Xss=1> Ders", Credits = 3 };
                db.Courses.Add(course); await db.SaveChangesAsync();
            }
            foreach (var relative in new[] { "docs/web34-canary.txt", "scripts/web34-canary.txt", "sql/web34-canary.txt", ".local/accounts/messages/web34-canary.txt", ".local/accounts/keys/web34-canary.txt" })
            { var path = Path.Combine(directory, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, "NOT-A-SECRET-WEB34"); }
            if (!labOnly) await ProductionBoundary(root, factory, certificate, directory);
            var allowed = await PageHost(); var denied = await PageHost();
            var apiBuilder = WebApplication.CreateBuilder(); apiBuilder.Logging.ClearProviders();
            apiBuilder.WebHost.ConfigureKestrel(s => s.Listen(IPAddress.Loopback, 0, l => l.UseHttps(certificate)));
            apiBuilder.Services.AddCors(o => o.AddPolicy("lab", p => p.WithOrigins(allowed.Urls.Single()).WithMethods("GET").WithHeaders("X-Lab")));
            var api = apiBuilder.Build(); hosts.Add(api);
            api.Use(async (context, next) => { counts.AddOrUpdate(context.Request.Method + context.Request.Path, 1, (_, n) => n + 1); await next(context); });
            api.UseRouting(); api.UseCors();
            api.MapGet("/closed", () => new { message = "public lab" });
            api.MapGet("/open", () => new { message = "public lab" }).RequireCors("lab");
            await api.StartAsync();
            using var rsa = certificate.GetRSAPublicKey()!;
            var pin = Convert.ToBase64String(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
            var start = new ProcessStartInfo("node") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "scripts/browser/checks.mjs"));
            browser = Process.Start(start)!;
            // Credentials cross only an anonymous pipe, never arguments, logs, or fixture files.
            await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new { address = app.Urls.Single(), email = user.Email, accountId = user.Id, password, pin,
                allowed = allowed.Urls.Single(), denied = denied.Urls.Single(), api = api.Urls.Single(), labOnly, profile = Path.Combine(directory, "browser-profile"), screenshot = Path.Combine(root, "obj/day34-browser.png") }));
            browser.StandardInput.Close();
            var output = browser.StandardOutput.ReadToEndAsync(); var error = browser.StandardError.ReadToEndAsync();
            await browser.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
            var lines = await output; var errors = await error;
            Check(!lines.Contains(password) && !errors.Contains(password), "browser runner output contains no password");
            Console.Write(lines); foreach (var line in errors.Split('\n').Where(x => x.StartsWith("FAIL: web browser step"))) Console.Error.WriteLine(line); Check(browser.ExitCode == 0, "browser assertions failed (runner prints safe check labels only)");
            Check(counts.GetValueOrDefault("GET/closed") == 1 && counts.GetValueOrDefault("OPTIONS/closed") == 1, "blocked simple GET reached server; rejected preflight did not execute GET");
            Check(counts.GetValueOrDefault("GET/open") == 2 && counts.GetValueOrDefault("OPTIONS/open") == 1, "allowed preflight and actual GET distinct; disallowed simple GET still reaches server");
            await using (var db = factory()) Check(await db.Courses.AsNoTracking().AnyAsync(c => c.Id == course.Id && c.Name == course.Name), "browser did not modify course fixture");
            Console.WriteLine("PASS: CORS lab server counters distinguish simple request/read denial and preflight/actual GET; only test hosts allow one origin; no cross-site assertion");
        }
        finally
        {
            if (browser is not null) { if (!browser.HasExited) { browser.Kill(true); await browser.WaitForExitAsync(); } browser.Dispose(); }
            foreach (var host in hosts.AsEnumerable().Reverse()) { await host.StopAsync(); await host.DisposeAsync(); }
            if (course is not null) { await using var db = factory(); await db.Courses.Where(c => c.Id == course.Id).ExecuteDeleteAsync(); }
            Console.WriteLine("PASS: browser/lab processes stopped; owned course removed; parent fixture removes accounts/keys/canaries");
        }
    }
}
