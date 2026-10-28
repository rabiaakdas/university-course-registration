namespace UniversityRegistration.Priority;

public sealed class GraduationPriorityStrategy : IRegistrationPriorityStrategy
{
    public int Calculate(PriorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var remaining = Math.Max(0, input.RequiredCredits - input.CompletedCredits);
        return remaining <= 20 ? 100 : 0;
    }
}
