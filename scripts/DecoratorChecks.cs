using System.Diagnostics;
using Microsoft.Extensions.Logging;
using UniversityRegistration.Application;

// Compiled only into the existing check executable, never the web project.
internal static class DecoratorChecks
{
    static void Check(bool value, string label) { if (!value) throw new BehaviorFailure("Decorator: " + label); }
    sealed class CapturedLog : ILogger<TimingAcademicEligibilityEvaluator>
    {
        public List<(EventId Id, Dictionary<string, object?> Fields)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            => Entries.Add((id, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(p => p.Key, p => p.Value)));
    }
    sealed class Inner(Func<AcademicEvaluation> run) : IAcademicEligibilityEvaluator
    {
        public int Calls { get; private set; }
        public AcademicEvaluation Evaluate(IReadOnlyList<OfferingEligibility> requested,
            IReadOnlyDictionary<int,long> credits, IReadOnlyList<OfferingSchedule> existing, long limit)
        { Calls++; return run(); }
    }
    static AcademicEvaluation Invoke(IAcademicEligibilityEvaluator evaluator) => evaluator.Evaluate([], new Dictionary<int,long>(), [], 30);
    public static IAcademicEligibilityEvaluator TimedEvaluator() => new TimingAcademicEligibilityEvaluator(
        new AcademicEligibility(), Microsoft.Extensions.Logging.Abstractions.NullLogger<TimingAcademicEligibilityEvaluator>.Instance);
    public static async Task Run()
    {
        foreach (var failures in new AcademicRejection[][] { [], [new AcademicRejection.PrerequisiteNotSatisfied(2), new AcademicRejection.CreditLimitExceeded(1)] })
        {
            var expected = new AcademicEvaluation(failures); var inner = new Inner(() => expected); var log = new CapturedLog();
            var result = Invoke(new TimingAcademicEligibilityEvaluator(inner, log));
            Check(ReferenceEquals(result, expected) && inner.Calls == 1, "result identity, one invocation");
            Verify(log, result.IsEligible ? "eligible" : "ineligible", failures.Length);
        }
        var error = new InvalidOperationException("Synthetic failure");
        var failing = new Inner(() => ThrowOriginal(error)); var failureLog = new CapturedLog();
        try { Invoke(new TimingAcademicEligibilityEvaluator(failing, failureLog)); throw new BehaviorFailure("Expected original exception"); }
        catch (InvalidOperationException actual)
        { Check(ReferenceEquals(actual,error) && actual.StackTrace!.Contains(nameof(ThrowOriginal)), "same exception and original throw frame"); }
        Check(failing.Calls == 1, "failure invoked once"); Verify(failureLog,"failed",null);
        var pure = new AcademicEligibility();
        foreach(var scenario in new[] { new EligibilityScenarioBuilder(30).Build(),
            new EligibilityScenarioBuilder(30).Request(offeringId:1,termId:1,credits:31).Requires(1,[2]).Build() })
        {
            var expected = pure.Evaluate(scenario.Requested,scenario.CurrentCredits,scenario.Existing,scenario.CreditLimit);
            var actual = new TimingAcademicEligibilityEvaluator(pure,new CapturedLog()).Evaluate(scenario.Requested,scenario.CurrentCredits,scenario.Existing,scenario.CreditLimit);
            Check(actual.IsEligible == expected.IsEligible && actual.Failures.SequenceEqual(expected.Failures), "real evaluator equivalence/order");
        }
        await DecoratorExperiments.Run();
        Console.WriteLine("PASS: decorator identity, one call, structured outcomes/event/duration, exception identity/stack, pure equivalence; async and order experiments");
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static AcademicEvaluation ThrowOriginal(Exception error) => throw error;
    static void Verify(CapturedLog log,string outcome,int? count)
    {
        Check(log.Entries.Count==1,"exactly one completion log"); var e=log.Entries.Single();
        Check(e.Id==TimingAcademicEligibilityEvaluator.CalculationEvent && e.Id.Name=="AcademicEligibilityCalculation","stable event");
        Check((string)e.Fields["Outcome"]! == outcome && Equals(e.Fields["FailureCount"],count),"outcome/count including null on failure");
        var elapsed=(double)e.Fields["ElapsedMs"]!;Check(double.IsFinite(elapsed)&&elapsed>=0,"finite nonnegative duration");
        Check(e.Fields.Keys.Order().SequenceEqual(new[]{"ElapsedMs","FailureCount","Outcome","{OriginalFormat}"}.Order()),"no personal/connection/request data fields");
    }
}

internal static class DecoratorExperiments
{
    interface IAsyncCalculation { Task<int> Calculate(CancellationToken token); }
    sealed class Pending(TaskCompletionSource<int> source) : IAsyncCalculation
    {
        public CancellationToken Received; public int Calls;
        public Task<int> Calculate(CancellationToken token) { Received=token;Calls++;return source.Task; }
    }
    sealed class AsyncTiming(IAsyncCalculation inner, Action<double> record) : IAsyncCalculation
    {
        public async Task<int> Calculate(CancellationToken token)
        {
            var timer=Stopwatch.StartNew();
            try { return await inner.Calculate(token); }
            finally { timer.Stop(); record(timer.Elapsed.TotalMilliseconds); }
        }
    }
    interface IAction { void Run(); }
    sealed class Actual(Action action) : IAction { public void Run()=>action(); }
    // Both end events deliberately run in finally, including failures.
    sealed class Wrapper(string name,IAction inner,List<string> events) : IAction
    {
        public void Run(){events.Add(name+":start");try{inner.Run();}finally{events.Add(name+":end");}}
    }
    public static async Task Run()
    {
        void Check(bool ok,string label){if(!ok)throw new BehaviorFailure("Decorator experiment: "+label);}
        foreach(var outcome in new[]{"success","failed","cancelled"})
        {
            var source=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellation=new CancellationTokenSource(); var inner=new Pending(source);var measurements=new List<double>();
            var task=new AsyncTiming(inner,measurements.Add).Calculate(cancellation.Token);
            Check(!task.IsCompleted && measurements.Count==0 && inner.Received==cancellation.Token && inner.Calls==1,"await pending inner; same token/one call");
            var error=new InvalidOperationException("Synthetic async failure");
            if(outcome=="success")source.SetResult(42);
            else if(outcome=="failed")source.SetException(error);
            else {cancellation.Cancel();source.SetCanceled(cancellation.Token);}
            try {var value=await task;Check(outcome=="success"&&value==42,"result unchanged");}
            catch(InvalidOperationException actual){Check(outcome=="failed"&&ReferenceEquals(actual,error),"same async exception");}
            catch(OperationCanceledException actual){Check(outcome=="cancelled"&&task.IsCanceled&&actual.CancellationToken==cancellation.Token,"canceled task/token");}
            Check(measurements.Count==1&&double.IsFinite(measurements[0])&&measurements[0]>=0,"completion timing once");
        }
        foreach(var outer in new[]{"timing","logging"})foreach(var fail in new[]{false,true})
        {
            var events=new List<string>();var calls=0;var error=new InvalidOperationException("Synthetic order failure");
            var other=outer=="timing"?"logging":"timing";
            IAction actual=new Actual(()=>{calls++;events.Add("actual");if(fail)throw error;});
            try {new Wrapper(outer,new Wrapper(other,actual,events),events).Run();Check(!fail,"expected order failure");}
            catch(InvalidOperationException caught){Check(fail&&ReferenceEquals(caught,error),"order exception propagated");}
            Check(calls==1&&events.SequenceEqual(new[]{outer+":start",other+":start","actual",other+":end",outer+":end"}),"nesting/finally order");
        }
    }
}
