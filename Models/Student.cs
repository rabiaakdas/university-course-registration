namespace UniversityRegistration.Models;

public class Student
{
    public int Id { get; init; }
    public string StudentNumber { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? UserId { get; private set; }

    // Linking is one-way here; existence and ownership require separate checks.
    public bool TryLinkAccount(string? userId)
    {
        if (UserId is not null || string.IsNullOrWhiteSpace(userId)) return false;
        UserId = userId;
        return true;
    }
}
