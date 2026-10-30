namespace UniversityRegistration.Application;

public sealed class ApprovalLogObserver(ILogger<ApprovalLogObserver> logger) : IEnrollmentApprovedObserver
{
    public static readonly EventId ApprovalObserved = new(2101, "EnrollmentApprovalObserved");

    public Task HandleAsync(EnrollmentApproved notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(ApprovalObserved, "Approval observed: {EventId} {EnrollmentId}",
            notification.EventId, notification.EnrollmentId);
        return Task.CompletedTask;
    }
}
