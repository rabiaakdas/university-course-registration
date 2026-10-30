using System.Diagnostics;

namespace UniversityRegistration.Application;

// Measures prepared-data computation only: no query, SaveChanges or HTTP timing.
public sealed class TimingAcademicEligibilityEvaluator(
    IAcademicEligibilityEvaluator inner, ILogger<TimingAcademicEligibilityEvaluator> logger)
    : IAcademicEligibilityEvaluator
{
    public static readonly EventId CalculationEvent = new(1701, "AcademicEligibilityCalculation");

    public AcademicEvaluation Evaluate(IReadOnlyList<OfferingEligibility> requested,
        IReadOnlyDictionary<int, long> currentCredits, IReadOnlyList<OfferingSchedule> existing, long limit)
    {
        var watch = Stopwatch.StartNew();
        AcademicEvaluation? result = null;
        try
        {
            result = inner.Evaluate(requested, currentCredits, existing, limit);
            return result;
        }
        finally
        {
            watch.Stop();
            // No catch: the original exception/stack propagates. A failing logger can
            // still interrupt this finally; provider recovery is outside this example.
            logger.LogInformation(CalculationEvent,
                "Academic evaluation calculation: {ElapsedMs} ms; {Outcome}; failures {FailureCount}",
                watch.Elapsed.TotalMilliseconds,
                result is null ? "failed" : result.IsEligible ? "eligible" : "ineligible",
                result?.Failures.Count);
        }
    }
}
