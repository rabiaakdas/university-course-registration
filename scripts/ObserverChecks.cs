using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;
using UniversityRegistration.Models;

internal static class ObserverChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("Observer: " + label); }
    public static EnrollmentApprovedPublisher EmptyPublisher() => new([], NullLogger<EnrollmentApprovedPublisher>.Instance);
    sealed class Observer(Func<EnrollmentApproved, CancellationToken, Task> action) : IEnrollmentApprovedObserver
    {
        public int Calls;
        public Task HandleAsync(EnrollmentApproved notification, CancellationToken token)
        { Calls++; return action(notification, token); }
    }
    sealed class CapturedLog<T> : ILogger<T>
    {
        public readonly List<(EventId Id, Dictionary<string, object?> Fields)> Entries = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Check(exception is null, "raw exception must not be logged");
            Entries.Add((id, ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(p => p.Key, p => p.Value)));
        }
    }

    public static async Task Unit(bool probe = false)
    {
        var notification = new EnrollmentApproved(Guid.NewGuid(), 9, 7, 8, DateTimeOffset.UtcNow);
        Check(await EmptyPublisher().PublishAsync(notification, default) == ApprovalPublication.Completed, "zero observers");
        try { await EmptyPublisher().PublishAsync(null!, default); throw new BehaviorFailure("Null event accepted"); }
        catch (ArgumentNullException error) { Check(error.ParamName == "notification", "null parameter"); }
        var log = new CapturedLog<ApprovalLogObserver>();
        var production = new ApprovalLogObserver(log);
        await production.HandleAsync(notification, default);
        Check(log.Entries.Single().Id == ApprovalLogObserver.ApprovalObserved
            && Equals(log.Entries[0].Fields["EventId"], notification.EventId), "production structured event");
        try { await production.HandleAsync(null!, default); throw new BehaviorFailure("Null observer event accepted"); }
        catch (ArgumentNullException) { }
        // A with expression produces a separate immutable event, leaving the original untouched.
        var copy = notification with { EnrollmentId = 99 };
        Check(notification.EnrollmentId == 9 && copy.EnrollmentId == 99, "immutable event value");
        using var cancellation = new CancellationTokenSource();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();
        var first = new Observer(async (e, ct) => {
            Check(ReferenceEquals(e, notification) && ct == cancellation.Token, "event/token preserved");
            order.Add(1); await gate.Task; order.Add(2);
        });
        var second = new Observer((e, ct) => {
            Check(ReferenceEquals(e, notification) && ct == cancellation.Token, "second event/token");
            order.Add(3); return Task.CompletedTask;
        });
        var publisher = new EnrollmentApprovedPublisher([first, second], NullLogger<EnrollmentApprovedPublisher>.Instance);
        var pending = publisher.PublishAsync(notification, cancellation.Token);
        Check(!pending.IsCompleted && second.Calls == 0, "sequential awaited dispatch");
        gate.SetResult();
        Check(await pending == ApprovalPublication.Completed && order.SequenceEqual(new[] { 1, 2, 3 })
            && first.Calls == 1 && second.Calls == 1, "one call per registration, ordered completion");
        foreach (var error in new Exception[] { new InvalidOperationException("secret must not appear"), new OperationCanceledException() })
        {
            var errors = new CapturedLog<EnrollmentApprovedPublisher>();
            var failed = new Observer((_, _) => Task.FromException(error));
            var next = new Observer((_, _) => Task.CompletedTask);
            Check(await new EnrollmentApprovedPublisher([failed, next], errors).PublishAsync(notification, default)
                == ApprovalPublication.Completed && next.Calls == 1, "fault including unsolicited OCE continues");
            var entry = errors.Entries.Single();
            Check(entry.Id == EnrollmentApprovedPublisher.ObserverFailed
                && Equals(entry.Fields["EventId"], notification.EventId)
                && Equals(entry.Fields["EnrollmentId"], notification.EnrollmentId)
                && Equals(entry.Fields["ObserverType"], failed.GetType().FullName)
                && Equals(entry.Fields["ExceptionType"], error.GetType().FullName), "safe failure identity/type log");
        }
        var skipped = new Observer((_, _) => Task.CompletedTask);
        var canceller = new Observer((_, ct) => { cancellation.Cancel(); return Task.FromCanceled(ct); });
        var cancelledLog = new CapturedLog<EnrollmentApprovedPublisher>();
        Check(await new EnrollmentApprovedPublisher([canceller, skipped], cancelledLog).PublishAsync(notification, cancellation.Token)
            == ApprovalPublication.Cancelled && skipped.Calls == 0 && cancelledLog.Entries.Count == 0, "caller cancellation is not observer failure");
        Check(await new EnrollmentApprovedPublisher([skipped], cancelledLog).PublishAsync(notification, cancellation.Token)
            == ApprovalPublication.Cancelled && skipped.Calls == 0, "already cancelled starts no observer");
        Check(!probe, "controlled runner failure probe");
        Console.WriteLine("PASS: observer zero/null/immutable event, sequential await, same token, safe fault continuation and caller/unsolicited cancellation (no DB)");
    }

    public static async Task Run(Func<UniversityDbContext> factory)
    {
        var tag = "O21_" + Guid.NewGuid().ToString("N")[..10];
        var course = new Course { Code = tag, Name = "Synthetic observer course", Credits = 1 };
        var term = new AcademicTerm { Name = tag, StartsOn = new(2026, 1, 1), EndsOn = new(2026, 6, 1) };
        var student = new Student { StudentNumber = tag, FullName = "Synthetic observer student" };
        CourseOffering? offering = null;
        var events = new List<EnrollmentApproved>();
        var errors = new CapturedLog<EnrollmentApprovedPublisher>();
        var failObserver = false;
        var reader = new Observer(async (e, _) => {
            await using var independent = factory();
            var saved = await independent.Enrollments.SingleAsync(x => x.Id == e.EnrollmentId);
            Check(saved.Status == EnrollmentStatus.Approved && saved.StudentId == e.StudentId
                && saved.CourseOfferingId == e.CourseOfferingId && e.EventId != Guid.Empty
                && e.OccurredAtUtc.Offset == TimeSpan.Zero, "observer sees independently committed Approved and correct IDs");
            events.Add(e);
        });
        var unreliable = new Observer((_, _) => failObserver ? Task.FromException(new InvalidOperationException("Synthetic failure")) : Task.CompletedTask);
        WebApplication? app = null;
        Exception? failure = null;
        try
        {
            await using (var db = factory())
            {
                Check(!db.Database.CreateExecutionStrategy().RetriesOnFailure && db.Database.CurrentTransaction is null, "no outer transaction/retry");
                db.AddRange(course, term, student); await db.SaveChangesAsync();
                offering = new() { CourseId = course.Id, AcademicTermId = term.Id, SectionCode = "A", Capacity = 40 };
                db.Add(offering); await db.SaveChangesAsync();
            }
            // Real endpoint mapping + scoped DI + Kestrel; only observers differ from production.
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddScoped(_ => factory());
            builder.Services.AddSingleton(new EnrollmentPolicy(30));
            builder.Services.AddScoped<AcademicEligibility>();
            builder.Services.AddScoped<IAcademicEligibilityEvaluator>(sp => new TimingAcademicEligibilityEvaluator(
                sp.GetRequiredService<AcademicEligibility>(), sp.GetRequiredService<ILogger<TimingAcademicEligibilityEvaluator>>()));
            builder.Services.AddScoped<IEnrollmentApprovedObserver>(_ => unreliable);
            builder.Services.AddScoped<IEnrollmentApprovedObserver>(_ => reader);
            builder.Services.AddSingleton<ILogger<EnrollmentApprovedPublisher>>(errors);
            builder.Services.AddScoped<EnrollmentApprovedPublisher>(); builder.Services.AddScoped<EnrollmentOperations>();
            builder.Services.AddProblemDetails();
            app = builder.Build();
            app.UseExceptionHandler(handler => handler.Run(context => Results.Problem(statusCode: 500).ExecuteAsync(context)));
            app.MapEnrollmentEndpoints(); await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
            async Task<JsonNode> Post(string suffix, int id, int expected)
            {
                using var response = await http.PostAsync($"/enrollments/{id}/{suffix}", null);
                Check((int)response.StatusCode == expected, $"{suffix} enrollment={id}: expected {expected}, actual {(int)response.StatusCode}");
                return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            }
            async Task<Enrollment> New(bool submitted = true)
            {
                await using var db = factory();
                // Each scenario removes only its own previous enrollment; student/offering stay fixed.
                await db.Enrollments.Where(e => e.StudentId == student.Id).ExecuteDeleteAsync();
                var e = new Enrollment { StudentId = student.Id, CourseOfferingId = offering.Id, CreatedAtUtc = DateTimeOffset.UtcNow };
                if (submitted) Check(e.TrySubmit(), "fixture submit");
                db.Add(e); await db.SaveChangesAsync(); return e;
            }
            async Task State(int id, EnrollmentStatus expected)
            { await using var db = factory(); Check((await db.Enrollments.SingleAsync(e => e.Id == id)).Status == expected, $"DB state enrollment={id} expected={expected}"); }
            Enrollment e;
            using (var created = await http.PostAsJsonAsync("/enrollments", new { studentId = student.Id, courseOfferingId = offering.Id }))
            {
                Check((int)created.StatusCode == 201 && events.Count == 0, "create 201 without approval event");
                var id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
                await using var db = factory(); e = await db.Enrollments.SingleAsync(x => x.Id == id);
            }
            await Post("submit", e.Id, 200); Check(events.Count == 0, "submit no event");
            var before = DateTimeOffset.UtcNow;
            var approved = await Post("approve", e.Id, 200);
            Check(approved["status"]!.GetValue<string>() == "Approved" && events.Count == 1
                && events[0].OccurredAtUtc >= before && events[0].OccurredAtUtc <= DateTimeOffset.UtcNow, "HTTP success/event creation interval");
            await Post("approve", e.Id, 409); await Post("approve", int.MaxValue, 404);
            Check(events.Count == 1, "repeat/missing no event");
            e = await New(false); await Post("approve", e.Id, 409); await Post("submit", e.Id, 200);
            Check(events.Count == 1, "invalid/submit no event");
            using (var response = await http.PostAsJsonAsync($"/enrollments/{e.Id}/reject", new { reason = "Synthetic" }))
                Check(response.IsSuccessStatusCode, "reject succeeds");
            Check(events.Count == 1, "reject no event");
            // Academic rejection after a legitimate Submitted fixture.
            e = await New();
            await using (var db = factory()) await db.Courses.Where(c => c.Id == course.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Credits, 31));
            await Post("approve", e.Id, 400); await State(e.Id, EnrollmentStatus.Submitted);
            Check(events.Count == 1, "academic rejection no event");
            await using (var db = factory()) await db.Courses.Where(c => c.Id == course.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Credits, 1));
            // Real SQL CHECK failure on UPDATE; no production fault switch or intercepted fake exception.
            await using (var db = factory()) await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Enrollments ADD CONSTRAINT CK_Day21_ApprovalFault CHECK (Status <> 2)");
            Exception? faultFailure = null;
            try { await Post("approve", e.Id, 500); await State(e.Id, EnrollmentStatus.Submitted); Check(events.Count == 1, "failed SQL write no event"); }
            catch (Exception error) { faultFailure = error; throw; }
            finally
            {
                try { await using var db = factory(); await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Enrollments DROP CONSTRAINT CK_Day21_ApprovalFault"); }
                catch (Exception error)
                {
                    Console.Error.WriteLine("CLEANUP observer fault constraint: " + error.GetType().Name);
                    if (faultFailure is null) throw new CleanupFailure();
                }
            }
            failObserver = true;
            await Post("approve", e.Id, 200); await State(e.Id, EnrollmentStatus.Approved);
            Check(events.Count == 2 && errors.Entries.Count == 1, "observer failure preserves HTTP success; next observer sees commit");
            using (var response = await http.GetAsync($"/enrollments/{e.Id}"))
                Check((await response.Content.ReadFromJsonAsync<JsonObject>())!["status"]!.GetValue<string>() == "Approved", "subsequent GET Approved");
            // Controlled postcommit cancellation at application boundary, not an HTTP fault endpoint.
            e = await New(); using var cancellation = new CancellationTokenSource();
            var never = new Observer((_, _) => Task.CompletedTask);
            var canceller = new Observer(async (notification, ct) => {
                Check(ct == cancellation.Token, "application forwards token");
                await State(notification.EnrollmentId, EnrollmentStatus.Approved);
                cancellation.Cancel(); ct.ThrowIfCancellationRequested();
            });
            await using (var db = factory())
            {
                var operation = new EnrollmentOperations(db, new(30), DecoratorChecks.TimedEvaluator(),
                    new([canceller, never], NullLogger<EnrollmentApprovedPublisher>.Instance));
                Check(await operation.ApproveAsync(e.Id, cancellation.Token) is EnrollmentTransitionResult.Updated, "postcommit cancellation retains success result");
            }
            await State(e.Id, EnrollmentStatus.Approved);
            Check(canceller.Calls == 1 && never.Calls == 0, "cancelled delivery skips remaining observer");
            e = await New();
            await using (var db = factory())
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                try { await new EnrollmentOperations(db, new(30), DecoratorChecks.TimedEvaluator(), EmptyPublisher()).ApproveAsync(e.Id, default);
                    throw new BehaviorFailure("Outer transaction accepted"); }
                catch (InvalidOperationException) { }
                await transaction.RollbackAsync();
            }
            await State(e.Id, EnrollmentStatus.Submitted);
            var selectCounts = new List<int>();
            foreach (var withLog in new[] { false, true })
            {
                e = await New();
                var capture = new CatalogSqlCapture();
                await using var source = factory();
                var options = new DbContextOptionsBuilder<UniversityDbContext>()
                    .UseSqlServer(source.Database.GetConnectionString()).AddInterceptors(capture).Options;
                await using var db = new UniversityDbContext(options);
                var publisher = withLog
                    ? new EnrollmentApprovedPublisher([new ApprovalLogObserver(NullLogger<ApprovalLogObserver>.Instance)], NullLogger<EnrollmentApprovedPublisher>.Instance)
                    : EmptyPublisher();
                Check(await new EnrollmentOperations(db, new(30), DecoratorChecks.TimedEvaluator(), publisher).ApproveAsync(e.Id, default)
                    is EnrollmentTransitionResult.Updated, "query comparison approval");
                selectCounts.Add(capture.Commands.Count(c => c.Text.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)));
            }
            Check(selectCounts[0] == 7 && selectCounts[1] == selectCounts[0], "production observer adds no SELECT to existing seven-query approval");
            Console.WriteLine("PASS: production log observer query comparison: 7 SELECT before/after; create/submit/reject emit no approval event");
            Console.WriteLine("PASS: real HTTP approval/409/404/academic400/SQL500; committed-state observer; observer fault preserves200; postcommit cancellation; outer transaction rejected");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            Exception? cleanupFailure = null;
            try { if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); } }
            catch (Exception error) { Console.Error.WriteLine("CLEANUP observer host: " + error.GetType().Name); cleanupFailure = error; }
            try
            {
                await using var db = factory();
                await db.Enrollments.Where(e => e.StudentId == student.Id).ExecuteDeleteAsync();
                if (offering is not null) await db.CourseOfferings.Where(o => o.Id == offering.Id).ExecuteDeleteAsync();
                await db.Students.Where(s => s.Id == student.Id).ExecuteDeleteAsync();
                await db.AcademicTerms.Where(t => t.Id == term.Id).ExecuteDeleteAsync();
                await db.Courses.Where(c => c.Id == course.Id).ExecuteDeleteAsync();
            }
            catch (Exception error) { Console.Error.WriteLine("CLEANUP observer fixture: " + error.GetType().Name); cleanupFailure ??= error; }
            if (failure is null && cleanupFailure is not null) throw new CleanupFailure();
        }
    }
}
