namespace UniversityRegistration.Models;

public class Student
{
    public int Id { get; init; }
    public string StudentNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? UserId { get; private set; }

    public string? AdvisorUserId { get; private set; }
    // Existence and Advisor role are checked by the trusted assignment operation.
    public void AssignAdvisor(string? userId)
    {
        if (userId is not null && string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("Advisor id must not be blank.", nameof(userId));
        AdvisorUserId = userId;
    }

    // Linking is one-way here; existence and ownership require separate checks.
    public bool TryLinkAccount(string? userId)
    {
        if (UserId is not null || string.IsNullOrWhiteSpace(userId)) return false;
        UserId = userId;
        return true;
    }
}
