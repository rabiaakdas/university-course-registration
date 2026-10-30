namespace UniversityRegistration.Application;

// Application event, not a durable delivery record or a database commit timestamp.
public sealed record EnrollmentApproved(Guid EventId, int EnrollmentId, int StudentId,
    int CourseOfferingId, DateTimeOffset OccurredAtUtc);

public interface IEnrollmentApprovedObserver
{
    // Null is rejected with ArgumentNullException. Reactions are noncritical.
    Task HandleAsync(EnrollmentApproved notification, CancellationToken cancellationToken);
}
