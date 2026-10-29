namespace UniversityRegistration.Application;

public abstract record AcademicRejection
{
    private AcademicRejection() { }
    public sealed record PrerequisiteNotSatisfied(int CourseOfferingId) : AcademicRejection;
    public sealed record CreditLimitExceeded(int AcademicTermId) : AcademicRejection;
}
