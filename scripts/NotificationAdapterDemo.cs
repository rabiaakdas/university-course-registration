// Entirely fake, production-excluded learning example. No network, DB or delivery.
namespace UniversityRegistration.DemoNotifications;

internal sealed class NotificationMessage
{
    public string Recipient { get; }
    public string Subject { get; }
    public string Body { get; }
    public NotificationMessage(string recipient, string subject, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        // Validate, but preserve every supplied character (no trim/truncation).
        Recipient = recipient; Subject = subject; Body = body;
    }
}

internal enum NotificationSendStatus { Accepted, RecipientRejected, ProviderUnavailable }
internal sealed record NotificationSendResult
{
    public NotificationSendStatus Status { get; }
    public string? ProviderReference { get; }
    private NotificationSendResult(NotificationSendStatus status, string? reference)
    { Status = status; ProviderReference = reference; }
    public static NotificationSendResult Accepted(string? reference)
    {
        if (reference is not null) ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        return new(NotificationSendStatus.Accepted, reference);
    }
    public static NotificationSendResult RecipientRejected() => new(NotificationSendStatus.RecipientRejected, null);
    public static NotificationSendResult ProviderUnavailable() => new(NotificationSendStatus.ProviderUnavailable, null);
}

internal interface INotificationSender
{
    Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

// Adaptee A: separate fields; known failures are typed exceptions.
internal sealed class FakeRecipientRejectedException : Exception;
internal sealed class FakeProviderUnavailableException : Exception;
internal sealed class FakeMailProvider(Func<string, string, string, CancellationToken, Task<string>> behavior)
{
    public int Calls { get; private set; }
    public (string Recipient, string Subject, string Body, CancellationToken Token)? LastCall { get; private set; }
    public Task<string> SendAsync(string recipient, string subject, string body, CancellationToken token)
    { Calls++; LastCall = (recipient, subject, body, token); return behavior(recipient, subject, body, token); }
}

internal sealed class MailNotificationAdapter(FakeMailProvider provider) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            var reference = await provider.SendAsync(message.Recipient, message.Subject, message.Body, cancellationToken);
            if (string.IsNullOrWhiteSpace(reference)) throw new InvalidOperationException("Fake mail returned an invalid acceptance reference.");
            return NotificationSendResult.Accepted(reference);
        }
        catch (FakeRecipientRejectedException) { return NotificationSendResult.RecipientRejected(); }
        catch (FakeProviderUnavailableException) { return NotificationSendResult.ProviderUnavailable(); }
        // Unexpected exceptions and OperationCanceledException retain their meaning.
    }
}

// Adaptee B: own request shape and result/error-code protocol.
internal sealed record FakePublishRequest(string Address, string Heading, string Content);
internal sealed record FakePublishResult(bool Accepted, string? Reference, string? ErrorCode);
internal sealed class FakePublishProvider(Func<FakePublishRequest, CancellationToken, Task<FakePublishResult>> behavior)
{
    public int Calls { get; private set; }
    public (FakePublishRequest Request, CancellationToken Token)? LastCall { get; private set; }
    public Task<FakePublishResult> PublishAsync(FakePublishRequest request, CancellationToken token)
    { Calls++; LastCall = (request, token); return behavior(request, token); }
}

internal sealed class PublishNotificationAdapter(FakePublishProvider provider) : INotificationSender
{
    public async Task<NotificationSendResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = await provider.PublishAsync(new(message.Recipient, message.Subject, message.Body), cancellationToken);
        if (result is null || result.Accepted && result.ErrorCode is not null ||
            !result.Accepted && (result.ErrorCode is null || result.Reference is not null) ||
            result.Reference is not null && string.IsNullOrWhiteSpace(result.Reference))
            throw new InvalidOperationException("Fake publish returned a contradictory or malformed result.");
        if (result.Accepted) return NotificationSendResult.Accepted(result.Reference);
        return result.ErrorCode switch
        {
            "INVALID_ADDRESS" => NotificationSendResult.RecipientRejected(),
            "SERVICE_UNAVAILABLE" => NotificationSendResult.ProviderUnavailable(),
            _ => throw new InvalidOperationException("Fake publish returned an unknown error code.")
        };
    }
}

internal static class NotificationDemoCaller
{
    // Same caller for both adapters. It knows no provider request/error types.
    public static async Task<NotificationSendResult> SendAsync(INotificationSender sender,
        NotificationMessage message, CancellationToken token) => await sender.SendAsync(message, token);
}
