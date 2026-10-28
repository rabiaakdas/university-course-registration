namespace UniversityRegistration.Priority;

public sealed class PriorityInput
{
    public int CompletedCredits { get; }
    public int RequiredCredits { get; }

    public PriorityInput(int completedCredits, int requiredCredits)
    {
        if (completedCredits < 0) throw new ArgumentOutOfRangeException(nameof(completedCredits));
        if (requiredCredits <= 0) throw new ArgumentOutOfRangeException(nameof(requiredCredits));
        CompletedCredits = completedCredits;
        RequiredCredits = requiredCredits;
    }
}
