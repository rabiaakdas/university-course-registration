namespace UniversityRegistration.Application;

public abstract record AcademicRejection
{
    private AcademicRejection() { }
    public string Code => this switch
    {
        PrerequisiteNotSatisfied => "prerequisite_not_satisfied",
        CreditLimitExceeded => "credit_limit_exceeded",
        ScheduleConflict => "schedule_conflict",
        _ => throw new InvalidOperationException()
    };
    public string Message => this switch
    {
        PrerequisiteNotSatisfied => "Dersin doğrudan ön koşulları başarıyla tamamlanmalıdır.",
        CreditLimitExceeded => "Mevcut kayıtlar ve seçimler dönemlik kredi sınırını aşıyor.",
        ScheduleConflict => "Seçilen açılışın haftalık saatleri başka bir açılışla çakışıyor.",
        _ => throw new InvalidOperationException()
    };
    public sealed record PrerequisiteNotSatisfied(int CourseOfferingId) : AcademicRejection;
    public sealed record CreditLimitExceeded(int AcademicTermId) : AcademicRejection;
    public sealed record ScheduleConflict(int CourseOfferingId, int ConflictingCourseOfferingId) : AcademicRejection;
}

public sealed class AcademicEvaluation
{
    public IReadOnlyList<AcademicRejection> Failures { get; }
    public bool IsEligible => Failures.Count == 0;

    public AcademicEvaluation(IEnumerable<AcademicRejection> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        var copy = failures.ToArray();
        if (copy.Any(f => f is null)) throw new ArgumentException("Failures cannot contain null.", nameof(failures));
        Failures = Array.AsReadOnly(copy);
    }
}
