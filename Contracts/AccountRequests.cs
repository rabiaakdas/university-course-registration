namespace UniversityRegistration.Contracts;

public sealed record RegisterAccountRequest(string? Email, string? Password);
public sealed record ConfirmEmailRequest(string? UserId, string? Token);
public sealed record RegisteredAccountResponse(string Id, bool EmailConfirmationRequired, string ConfirmationDispatchStatus);
public sealed record ConfirmedEmailResponse(bool EmailConfirmed);
