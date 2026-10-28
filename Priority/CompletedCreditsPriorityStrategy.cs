namespace UniversityRegistration.Priority;

public sealed class CompletedCreditsPriorityStrategy : IRegistrationPriorityStrategy
{
    public int Calculate(PriorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.CompletedCredits;
    }
}
