using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Endpoints;

internal static class CqrsChecks
{
    static void Check(bool ok, string label) { if (!ok) throw new BehaviorFailure("CQRS: " + label); }

    public static async Task Run(bool probe = false)
    {
        foreach (var pair in new[] { (0, 1), (-1, 1), (1, 0), (1, -1) })
        {
            try { _ = new CreateEnrollmentCommand(pair.Item1, pair.Item2); throw new BehaviorFailure("Invalid command accepted"); }
            catch (ArgumentOutOfRangeException error)
            { Check(error.ParamName == (pair.Item1 <= 0 ? "studentId" : "courseOfferingId"), "command parameter guard"); }
        }
        Check(RegistrationInput.Enrollment(new(0, 0)).Keys.Order().SequenceEqual(new[] { "courseOfferingId", "studentId" }), "same aggregated HTTP command validation");
        var valid = new CreateEnrollmentCommand(1, 2);
        Check(valid.StudentId == 1 && valid.CourseOfferingId == 2, "valid command carries identity");
        foreach (var input in new[] { (1, 0, 20), (1, -1, 20), (1, 1, 0), (1, 1, 101), (0, 1, 20), (-1, 1, 20), (1, int.MaxValue, 100) })
        {
            var errors = CourseCatalogQuery.Validate(input.Item1, input.Item2, input.Item3);
            Check(errors.Count > 0, "invalid query field errors");
            try { _ = new CourseCatalogQuery(null, input.Item1, input.Item2, input.Item3); throw new BehaviorFailure("Invalid direct query accepted"); }
            catch (ArgumentOutOfRangeException error) { Check(errors.ContainsKey(error.ParamName!), "same direct/HTTP query rule"); }
        }
        Check(CourseCatalogQuery.Validate(0, 0, 101).Count == 3, "independent query field errors preserved");
        var query = new CourseCatalogQuery("  abc  ");
        Check(query.Search == "abc" && query.Page == 1 && query.PageSize == 20 && query.Offset == 0 && query.Credits is null, "normalization/defaults");
        Check(new CourseCatalogQuery("   ").Search == "" && new CourseCatalogQuery(page: int.MaxValue, pageSize: 1).Offset == int.MaxValue - 1, "blank and supported large offset");
        // Provider-free context: guards must run before any DB access.
        await using (var db = new UniversityDbContext(new DbContextOptionsBuilder<UniversityDbContext>().Options))
        {
            try { await new CourseCatalogReader(db).ReadAsync(null!, default); throw new BehaviorFailure("Null query accepted"); }
            catch (ArgumentNullException error) { Check(error.ParamName == "request", "null query"); }
            try { await new EnrollmentOperations(db, new(30), DecoratorChecks.TimedEvaluator(), ObserverChecks.EmptyPublisher()).CreateAsync(null!, default);
                throw new BehaviorFailure("Null command accepted"); }
            catch (ArgumentNullException error) { Check(error.ParamName == "command", "null command"); }
        }
        await Mediator();
        Check(!typeof(CreateEnrollmentCommand).Assembly.GetTypes().Any(t => t.Name == nameof(DemoMediator)), "mediator experiment excluded from production");
        Check(!probe, "controlled runner failure probe");
        Console.WriteLine("PASS: controlled command/query guards, shared HTTP rules/defaults/offset/null; typed mediator direct equivalence/routing/one call/token/pending/error/cancel/missing handler (no DB)");
    }

    static async Task Mediator()
    {
        using var cancellation = new CancellationTokenSource();
        var values = new List<int>(); var commands = 0; var queries = 0;
        var receipt = new DemoReceipt(5);
        var command = new DemoRegisterHandler((request, ct) => {
            Check(ct == cancellation.Token, "command token"); commands++; values.Add(request.Value); return Task.FromResult(receipt);
        });
        var read = new DemoReadHandler((request, ct) => {
            Check(ct == cancellation.Token, "query token"); queries++; return Task.FromResult(new DemoSummary(request.Prefix + values.Count));
        });
        var mediator = new DemoMediator(command, read);
        var direct = await command.HandleAsync(new(5), cancellation.Token);
        Check(commands == 1 && queries == 0, "direct command once/no query");
        Check(ReferenceEquals(direct, await mediator.SendAsync(new DemoRegister(5), cancellation.Token)) && commands == 2 && queries == 0, "routed command result identity/once");
        var directQuery = await read.HandleAsync(new("Count="), cancellation.Token);
        Check(directQuery == await mediator.SendAsync(new DemoRead("Count="), cancellation.Token)
            && queries == 2 && commands == 2 && values.Count == 2, "query equivalence/no command or extra write");
        try { await new DemoMediator(null, read).SendAsync(new DemoRegister(1), default); throw new BehaviorFailure("Missing command handler accepted"); }
        catch (InvalidOperationException error) { Check(error.Message.Contains("DemoRegister"), "missing command identified"); }
        try { await new DemoMediator(command, null).SendAsync(new DemoRead(""), default); throw new BehaviorFailure("Missing query handler accepted"); }
        catch (InvalidOperationException error) { Check(error.Message.Contains("DemoRead"), "missing query identified"); }

        foreach (var kind in new[] { "command", "query" })
        foreach (var outcome in new[] { "success", "error", "cancel" })
        {
            using var token = new CancellationTokenSource();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0; var unexpected = new InvalidOperationException("Synthetic handler failure");
            var expected = new DemoSummary("unchanged");
            var pendingCommand = new DemoRegisterHandler(async (_, ct) => { Check(ct == token.Token, "pending command token"); calls++; await gate.Task; return receipt; });
            var pendingQuery = new DemoReadHandler(async (_, ct) => { Check(ct == token.Token, "pending query token"); calls++; await gate.Task; return expected; });
            var routed = new DemoMediator(pendingCommand, pendingQuery);
            Task task = kind == "command" ? routed.SendAsync(new DemoRegister(5), token.Token) : routed.SendAsync(new DemoRead(""), token.Token);
            Check(!task.IsCompleted && calls == 1, "pending task and exactly one routed handler");
            if (outcome == "success") gate.SetResult();
            else if (outcome == "error") gate.SetException(unexpected);
            else { token.Cancel(); gate.SetCanceled(token.Token); }
            try
            {
                await task; Check(outcome == "success", "failure/cancel not converted to success");
                Check(kind == "command" ? ReferenceEquals(((Task<DemoReceipt>)task).Result, receipt)
                    : ReferenceEquals(((Task<DemoSummary>)task).Result, expected), "async result unchanged");
            }
            catch (InvalidOperationException actual) { Check(outcome == "error" && ReferenceEquals(actual, unexpected), "same exception"); }
            catch (OperationCanceledException actual) { Check(outcome == "cancel" && task.IsCanceled && actual.CancellationToken == token.Token, "cancellation task/token preserved"); }
        }
    }
}
