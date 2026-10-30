namespace UniversityRegistration.Application;

public enum ApprovalPublication { Completed, Cancelled }

public sealed class EnrollmentApprovedPublisher
{
    public static readonly EventId ObserverFailed = new(2102, "EnrollmentApprovalObserverFailed");
    private readonly IEnrollmentApprovedObserver[] observers;
    private readonly ILogger<EnrollmentApprovedPublisher> logger;

    public EnrollmentApprovedPublisher(IEnumerable<IEnrollmentApprovedObserver> observers,
        ILogger<EnrollmentApprovedPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(observers);
        this.observers = observers.ToArray();
        if (this.observers.Any(o => o is null)) throw new ArgumentException("Observer cannot be null.", nameof(observers));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApprovalPublication> PublishAsync(EnrollmentApproved notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        foreach (var observer in observers)
        {
            if (cancellationToken.IsCancellationRequested) return ApprovalPublication.Cancelled;
            try { await observer.HandleAsync(notification, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { return ApprovalPublication.Cancelled; }
            catch (Exception error)
            {
                // Do not log arbitrary exception messages/objects: they can contain sensitive data.
                logger.LogError(ObserverFailed,
                    "Approval observer failed: {EventId} {EnrollmentId} {ObserverType} {ExceptionType}",
                    notification.EventId, notification.EnrollmentId, observer.GetType().FullName, error.GetType().FullName);
            }
        }
        return cancellationToken.IsCancellationRequested ? ApprovalPublication.Cancelled : ApprovalPublication.Completed;
    }
}
