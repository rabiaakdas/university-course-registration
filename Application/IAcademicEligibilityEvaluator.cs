namespace UniversityRegistration.Application;

public interface IAcademicEligibilityEvaluator
{
    AcademicEvaluation Evaluate(IReadOnlyList<OfferingEligibility> requested,
        IReadOnlyDictionary<int, long> currentCredits, IReadOnlyList<OfferingSchedule> existing, long limit);
}
