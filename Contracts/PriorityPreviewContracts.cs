namespace UniversityRegistration.Contracts;

public record PriorityPreviewRequest(string? Policy, int? CompletedCredits, int? RequiredCredits);
public record PriorityPreviewResponse(string Policy, int Score);
