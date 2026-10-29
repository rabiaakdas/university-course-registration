using UniversityRegistration.Contracts;

namespace UniversityRegistration.Application;

public enum SingleEnrollmentFailure { StudentMissing, OfferingMissing, BothReferencesMissing, Duplicate }
public enum BatchEnrollmentFailure { StudentMissing, OfferingsMissing, Duplicate, StudentRemoved, OfferingRemoved }

// Closed, operation-specific alternatives: a rejection never contains success data.
public abstract record SingleEnrollmentResult
{
    private SingleEnrollmentResult() { }
    public sealed record Created(EnrollmentResponse Enrollment) : SingleEnrollmentResult;
    public sealed record AcademicRejected(AcademicEvaluation Rejection) : SingleEnrollmentResult;
    public sealed record Rejected(SingleEnrollmentFailure Reason) : SingleEnrollmentResult;
}

public abstract record BatchEnrollmentResult
{
    private BatchEnrollmentResult() { }
    public sealed record Created(IReadOnlyList<EnrollmentResponse> Items) : BatchEnrollmentResult;
    public sealed record AcademicRejected(AcademicEvaluation Rejection) : BatchEnrollmentResult;
    public sealed record Rejected(BatchEnrollmentFailure Reason) : BatchEnrollmentResult;
}

public abstract record EligibilityResult
{
    private EligibilityResult() { }
    public sealed record Evaluated(AcademicEvaluation Evaluation) : EligibilityResult;
    public sealed record Rejected(BatchEnrollmentFailure Reason) : EligibilityResult;
}
