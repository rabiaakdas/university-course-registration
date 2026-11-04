using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniversityRegistration.Accounts;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

internal static class AccountsChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Accounts: " + label); }
    sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages = new();
        public ILogger CreateLogger(string category) => new CapturedLogger(this);
        public void Dispose() { }
        sealed class CapturedLogger(Logs owner) : ILogger
        {
            public bool IsEnabled(LogLevel level) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
                => owner.Messages.Enqueue(formatter(state, error));
        }
    }
    sealed class Sender : IAccountConfirmationSender
    {
        public Func<CancellationToken, Task>? Failure;
        public ConcurrentQueue<Uri> Links = new();
        public async Task SendAsync(string recipient, Uri link, CancellationToken ct)
        { Links.Enqueue(link); if (Failure is not null) await Failure(ct); }
    }
    sealed class SaveGate : SaveChangesInterceptor
    {
        public Func<DbContext, Task>? BeforeSave;
        public int Calls;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (BeforeSave is { } action) { BeforeSave = null; Calls++; await action(data.Context!); }
            return result;
        }
    }

    public static void Unit(bool probe = false)
    {
        foreach (var item in new[] { ("Production", "https://example.test/confirm"), ("Development", "http://remote.example.test/confirm"), ("Development", "https://example.test/confirm?x=1") })
        {
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = item.Item1 });
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Accounts:LocalMailboxEnabled"] = "true", ["Accounts:ConfirmationPageUrl"] = item.Item2 });
            try { b.Services.AddAccountIdentity(b.Configuration, b.Environment); throw new BehaviorFailure("unsafe mailbox accepted"); }
            catch (InvalidOperationException ex) { Check(ex.Message.Contains("Accounts:") || ex.Message.Contains("Development"), "configuration error is explicit"); }
        }
        Check(!probe, "controlled failure probe");
        Console.WriteLine("PASS: local mailbox rejects Production, remote HTTP and query-bearing configured URLs (no DB)");
    }

    public static async Task Run(string root, Func<UniversityDbContext> factory)
    {
        var tag = "a25" + Guid.NewGuid().ToString("N");
        var directory = Path.GetFullPath(Path.Combine(root, "obj", "accounts-checks", tag));
        Directory.CreateDirectory(directory);
        string connection;
        await using (var db = factory()) connection = db.Database.GetConnectionString()!;
        var sensitive = new List<string>(); var logs = new Logs(); var sender = new Sender(); var gate = new SaveGate();
        // Generated test-only password; never print or write it to the message box.
        var password = "  " + Guid.NewGuid().ToString("N") + "Az9!  "; sensitive.Add(password);
        WebApplication? app = null; Process? process = null; Exception? failure = null;
        var processLog = new ConcurrentQueue<string>();
        async Task<T> WithManager<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
        { await using var scope = app!.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()); }
        async Task<ApplicationUser> Read(string id) { await using var db = factory(); return await db.Users.SingleAsync(x => x.Id == id); }
        async Task<long[]> Academic()
        {
            await using var db = factory();
            return [await db.Students.LongCountAsync(), await db.Enrollments.LongCountAsync(), await db.Courses.LongCountAsync(),
                await db.AcademicTerms.LongCountAsync(), await db.CourseOfferings.LongCountAsync(), await db.StudentCompletedCourses.LongCountAsync()];
        }
        async Task<JsonObject> Post(HttpClient client, string path, object body, int expected)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            var text = await response.Content.ReadAsStringAsync();
            Check((int)response.StatusCode == expected, $"{path}: expected={expected}, actual={(int)response.StatusCode}");
            Check(response.Headers.Location is null && !response.Headers.Contains("Set-Cookie"), "no invented Location/session");
            Check(sensitive.All(secret => !text.Contains(secret, StringComparison.Ordinal)), "no password/hash/token in response");
            return JsonNode.Parse(text)!.AsObject();
        }
        string Email(string suffix) => tag + suffix + "@example.test";
        void StopProcess()
        {
            if (process is null) return;
            if (!process.HasExited) process.Kill(true);
            process.WaitForExit(); process.Dispose(); process = null;
        }
        async Task<HttpClient> StartProcess()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "bin", "Debug", "net10.0", "UniversityRegistration.dll"));
            start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(directory);
            start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["ConnectionStrings__UniversityDatabase"] = connection;
            start.Environment["EnrollmentPolicy__MaxCreditsPerTerm"] = "30";
            start.Environment["Accounts__LocalMailboxEnabled"] = "true";
            start.Environment["Accounts__ConfirmationPageUrl"] = "https://confirmation.example.test/local";
            process = Process.Start(start)!;
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) processLog.Enqueue(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) processLog.Enqueue(e.Data); };
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) };
            var ready = Stopwatch.StartNew();
            while (ready.Elapsed < TimeSpan.FromSeconds(20) && !process.HasExited)
            {
                try { using var r = await client.GetAsync("/courses"); if (r.IsSuccessStatusCode) return client; } catch (HttpRequestException) { }
                await Task.Delay(100); // bounded readiness polling, not a timing assertion
            }
            client.Dispose(); throw new BehaviorFailure("account process readiness failed");
        }
        try
        {
            var before = await Academic();
            var b = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = directory, EnvironmentName = "Development" });
            b.WebHost.UseUrls("http://127.0.0.1:0"); b.Logging.ClearProviders(); b.Logging.AddProvider(logs);
            b.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
            b.Logging.AddFilter("Microsoft.EntityFrameworkCore.Update", LogLevel.None);
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Accounts:LocalMailboxEnabled"] = "true", ["Accounts:ConfirmationPageUrl"] = "https://confirmation.example.test/local" });
            b.Services.AddDbContext<UniversityDbContext>(o => o.UseSqlServer(connection).AddInterceptors(gate));
            b.Services.AddAccountIdentity(b.Configuration, b.Environment);
            b.Services.AddDataProtection().SetApplicationName(tag); // separate test protector purpose and keys
            b.Services.AddSingleton<IAccountConfirmationSender>(sender);
            b.Services.AddProblemDetails(); b.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = false);
            app = b.Build(); app.UseExceptionHandler(h => h.Run(c => Results.Problem(statusCode: 500).ExecuteAsync(c)));
            app.MapAccountEndpoints(); await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            await using (var db = factory())
            {
                var uniqueName = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AspNetUsers') AND name='UserNameIndex' AND is_unique=1 AND has_filter=1").SingleAsync();
                var ordinaryEmail = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AspNetUsers') AND name='EmailIndex' AND is_unique=0").SingleAsync();
                Check(uniqueName == 1 && ordinaryEmail == 1, "real username unique index; email index is not unique");
            }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var disabledEmail = Email("disabled");
                var blocked = await AccountEndpoints.RegisterAsync(new(disabledEmail, password), scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), sender,
                    new(false, null, directory), app.Services.GetRequiredService<ILoggerFactory>(), default);
                Check(((IStatusCodeHttpResult)blocked).StatusCode == 503, "disabled mailbox does not pretend dispatch");
                await using var db = factory(); Check(!await db.Users.AnyAsync(x => x.Email == disabledEmail), "disabled registration writes no account");
            }
            var first = await Post(http, "/accounts/register", new { email = "  " + Email("Mixed.Case+tag") + "  ", password }, 201);
            Check(string.Join(',', first.Select(x => x.Key).Order()) == "confirmationDispatchStatus,emailConfirmationRequired,id" && first["confirmationDispatchStatus"]!.GetValue<string>() == "queued-locally" && first["emailConfirmationRequired"]!.GetValue<bool>(), "register minimal contract");
            var id = first["id"]!.GetValue<string>(); var user = await Read(id); sensitive.Add(user.PasswordHash!);
            Check(!user.EmailConfirmed && user.UserName == Email("Mixed.Case+tag") && user.Email == user.UserName &&
                user.NormalizedEmail == user.Email.ToUpperInvariant() && user.NormalizedUserName == user.NormalizedEmail && user.PasswordHash != password, "unconfirmed; email trim only; normalized by Identity; hash not plaintext");
            Check(await WithManager(m => m.CheckPasswordAsync(user, password)) && !await WithManager(m => m.CheckPasswordAsync(user, password.Trim())) &&
                !await WithManager(m => m.CheckPasswordAsync(user, "wrong")), "exact password preserved, trimmed/wrong rejected");
            var second = await Post(http, "/accounts/register", new { email = Email("second"), password }, 201);
            var secondId = second["id"]!.GetValue<string>(); var user2 = await Read(secondId); sensitive.Add(user2.PasswordHash!);
            Check(user.PasswordHash != user2.PasswordHash, "same password different stored salts/hashes");
            foreach (var email in new[] { user.Email, user.Email!.ToUpperInvariant(), "  " + user.Email + " " })
            { var r = await Post(http, "/accounts/register", new { email, password }, 400); Check(r["code"]!.GetValue<string>() == "account_creation_failed", "duplicate stable error"); }
            foreach (var request in new RegisterAccountRequest[] { new(null, null), new(" ", " "), new("invalid", password), new(Email("short"), "short"), new(Email("long"), new('X',129)), new(new('e',257),password) })
                Check((await Post(http, "/accounts/register", request, 400))["errors"] is JsonObject, "field validation");
            var both = await Post(http, "/accounts/register", new { }, 400);
            Check(both["errors"]!["email"] is not null && both["errors"]!["password"] is not null, "missing independent errors");
            using (var malformed = await http.PostAsync("/accounts/register", new StringContent("{", Encoding.UTF8, "application/json"))) Check((int)malformed.StatusCode == 400, "malformed binding");
            using (var wrongType = await http.PostAsJsonAsync("/accounts/register", new { email = 1, password })) Check((int)wrongType.StatusCode == 400, "wrong JSON type binding");
            await using (var db = factory()) Check(await db.Users.CountAsync(x => x.UserName!.StartsWith(tag)) == 2, "all rejected registrations leave account count unchanged");

            var link = sender.Links.First(); var query = QueryHelpers.ParseQuery(link.Query); var token = query["token"].ToString(); sensitive.Add(token);
            Check(query["userId"] == id && link.Host == "confirmation.example.test", "configured link, not request host");
            var freshRaw = await WithManager(m => m.GenerateEmailConfirmationTokenAsync(user));
            var fresh = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(freshRaw)); sensitive.Add(fresh); sensitive.Add(freshRaw);
            foreach (var request in new[] { new ConfirmEmailRequest(secondId, token), new(Guid.NewGuid().ToString(), token), new(id, token[..^8] + "AAAAAAAA"), new(id, "%%%"), new(id, "A") })
                Check((await Post(http, "/accounts/confirm-email", request, 400))["code"]!.GetValue<string>() == "confirmation_failed", "common invalid confirmation");
            await Post(http, "/accounts/confirm-email", new { }, 400);
            await Post(http, "/accounts/confirm-email", new { userId = id, token }, 200);
            await Post(http, "/accounts/confirm-email", new { userId = id, token }, 200);
            await Post(http, "/accounts/confirm-email", new { userId = id, token = fresh }, 200);
            await Post(http, "/accounts/confirm-email", new { userId = id, token = "arbitrary" }, 400);
            Check((await Read(id)).EmailConfirmed && !(await Read(secondId)).EmailConfirmed, "confirmation persists; wrong account unchanged; repeat token accepted");
            // Provider uses DateTimeOffset.UtcNow, not TimeProvider. Same real provider with expired lifetime, no clock sleeps.
            var options = app.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
            var oldLifetime = options.TokenLifespan;
            options.TokenLifespan = TimeSpan.FromTicks(1);
            try { await Post(http, "/accounts/confirm-email", new { userId = id, token }, 400); }
            finally { options.TokenLifespan = oldLifetime; }
            Check(oldLifetime == TimeSpan.FromHours(2), "production token lifetime two hours");
            var stampChange = await WithManager(async m => await m.UpdateSecurityStampAsync((await m.FindByIdAsync(id))!)); Check(stampChange.Succeeded, "test stamp update");
            await Post(http, "/accounts/confirm-email", new { userId = id, token }, 400);

            var raceEmail = Email("race");
            var responses = await Task.WhenAll(Enumerable.Range(0,4).Select(_ => http.PostAsJsonAsync("/accounts/register", new { email = raceEmail, password })));
            try { Check(responses.Count(x => (int)x.StatusCode == 201) == 1 && responses.Count(x => (int)x.StatusCode == 400) == 3, "parallel same normalized user has one success"); }
            finally { foreach (var response in responses) response.Dispose(); }
            await using (var db = factory()) Check(await db.Users.CountAsync(x => x.Email == raceEmail) == 1, "single race row");
            var faultEmail = Email("savefault");
            gate.BeforeSave = async _ => { var r = await WithManager(m => m.CreateAsync(new ApplicationUser { UserName = faultEmail, Email = faultEmail }, password)); Check(r.Succeeded, "committed competitor at save gate"); };
            Check((await Post(http, "/accounts/register", new { email = faultEmail, password }, 400))["code"]!.GetValue<string>() == "account_creation_failed" && gate.Calls == 1, "real UserNameIndex save-time fault mapped");
            // An unrelated primary-key write error must remain a technical failure.
            gate.BeforeSave = async context =>
            {
                var pendingId = context.ChangeTracker.Entries<ApplicationUser>().Single(x => x.State == EntityState.Added).Entity.Id;
                var r = await WithManager(m => m.CreateAsync(new ApplicationUser { Id = pendingId, UserName = Email("pkcompetitor"), Email = Email("pkcompetitor") }, password));
                Check(r.Succeeded, "different user committed with same PK for narrow classification check");
            };
            await Post(http, "/accounts/register", new { email = Email("infra"), password }, 500);
            sender.Failure = _ => Task.FromException(new IOException("synthetic sensitive message " + password));
            var failed = await Post(http, "/accounts/register", new { email = Email("failed"), password }, 201);
            Check(failed["confirmationDispatchStatus"]!.GetValue<string>() == "failed" && !(await Read(failed["id"]!.GetValue<string>())).EmailConfirmed, "sender failure preserves account and reports failed");
            sender.Failure = _ => Task.FromException(new OperationCanceledException("not caller cancellation"));
            Check((await Post(http, "/accounts/register", new { email = Email("unsolicited"), password }, 201))["confirmationDispatchStatus"]!.GetValue<string>() == "failed", "unsolicited cancellation is dispatch fault");
            using (var cancelled = new CancellationTokenSource())
            {
                sender.Failure = ct => { Check(ct == cancelled.Token, "dispatch token preserved"); cancelled.Cancel(); return Task.FromCanceled(ct); };
                await using var scope = app.Services.CreateAsyncScope();
                var result = await AccountEndpoints.RegisterAsync(new(Email("cancelled"), password), scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), sender,
                    app.Services.GetRequiredService<LocalConfirmationSettings>(), app.Services.GetRequiredService<ILoggerFactory>(), cancelled.Token);
                var value = (RegisteredAccountResponse)((IValueHttpResult)result).Value!;
                Check(value.ConfirmationDispatchStatus == "cancelled" && !(await Read(value.Id)).EmailConfirmed, "postcommit cancellation distinct from failure, account persists");
            }
            sender.Failure = null;
            foreach (var uri in sender.Links) sensitive.Add(QueryHelpers.ParseQuery(uri.Query)["token"].ToString());
            Check(logs.Messages.Any(x => x.Contains("Local confirmation dispatch failed")) && sensitive.All(secret => logs.Messages.All(x => !x.Contains(secret, StringComparison.Ordinal))), "safe controlled application logs");
            await app.StopAsync(); await app.DisposeAsync(); app = null;

            using (var client = await StartProcess())
            {
                var r = await Post(client, "/accounts/register", new { email = Email("disk"), password }, 201);
                id = r["id"]!.GetValue<string>();
            }
            StopProcess();
            var file = Directory.GetFiles(Path.Combine(directory, ".local", "accounts", "messages"), "*.json").Single();
            var messageText = await File.ReadAllTextAsync(file); var message = JsonNode.Parse(messageText)!;
            Check(message["recipient"]!.GetValue<string>() == Email("disk") && message["subject"] is not null && !messageText.Contains(password), "physical mailbox recipient/subject and no password");
            var diskToken = QueryHelpers.ParseQuery(new Uri(message["link"]!.GetValue<string>()).Query)["token"].ToString(); sensitive.Add(diskToken);
            using (var client = await StartProcess()) await Post(client, "/accounts/confirm-email", new { userId = id, token = diskToken }, 200);
            StopProcess();
            Check((await Read(id)).EmailConfirmed, "same isolated keys survive actual process restart");
            Check(sensitive.All(secret => processLog.All(x => !x.Contains(secret, StringComparison.Ordinal))), "process logs contain no password/hash/token");
            var after = await Academic();
            Check(before.SequenceEqual(after), "accounts do not create/change academic rows");
            await using (var db = factory()) Check(!await db.Students.AnyAsync(x => x.UserId != null && db.Users.Where(u => u.UserName!.StartsWith(tag)).Select(u => u.Id).Contains(x.UserId)), "no automatic student linking");
            Console.WriteLine("PASS: account HTTP/password hash/check/salt/normalization/validation; concurrent and real save-stage unique; token wrong-user/tamper/expiry/reuse/regeneration/stamp; failure/cancellation; disk mailbox and process restart; no academic mutation");
        }
        catch (Exception ex)
        {
            failure = ex;
            // Report code location only, never exception messages containing request/provider data.
            Console.Error.WriteLine("ACCOUNT CHECK LOCATIONS: " + string.Join(',', new StackTrace(ex, true).GetFrames()?.Where(f => f.GetFileName()?.EndsWith("AccountsChecks.cs") == true).Select(f => f.GetFileLineNumber()) ?? []));
            throw;
        }
        finally
        {
            try
            {
                StopProcess(); if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
                await using var db = factory(); await db.Users.Where(x => x.UserName!.StartsWith(tag)).ExecuteDeleteAsync();
                Check(!await db.Users.AnyAsync(x => x.UserName!.StartsWith(tag)), "owned accounts cleaned");
                // Only the generated run directory under this repo's obj; never a supplied/user path.
                var allowed = Path.GetFullPath(Path.Combine(root, "obj", "accounts-checks")) + Path.DirectorySeparatorChar;
                Check(directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(directory) == tag, "safe key/mailbox cleanup root");
                Directory.Delete(directory, true);
                Console.WriteLine("PASS: owned accounts, isolated key/mailbox files and processes cleaned");
            }
            catch (Exception ex) { Console.Error.WriteLine("CLEANUP accounts: " + ex.GetType().Name); if (failure is null) throw new CleanupFailure(); }
        }
    }
}
