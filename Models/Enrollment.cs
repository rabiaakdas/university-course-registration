namespace UniversityRegistration.Models;

public class Enrollment
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public int CourseOfferingId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public EnrollmentStatus Status { get; private set; } = EnrollmentStatus.Draft;
    public string? RejectionReason { get; private set; }

    // Local lifecycle only: these methods do not authorize or evaluate academic rules.
    public bool TrySubmit()
    {
        if (Status != EnrollmentStatus.Draft) return false;
        Status = EnrollmentStatus.Submitted;
        return true;
    }

    public bool TryApprove()
    {
        if (Status != EnrollmentStatus.Submitted) return false;
        Status = EnrollmentStatus.Approved;
        return true;
    }

    public static string? CleanRejectionReason(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > 500 ? null : trimmed;
    }

    public bool TryReject(string? reason)
    {
        var cleaned = CleanRejectionReason(reason);
        if (Status != EnrollmentStatus.Submitted || cleaned is null) return false;
        Status = EnrollmentStatus.Rejected;
        RejectionReason = cleaned;
        return true;
    }
}
