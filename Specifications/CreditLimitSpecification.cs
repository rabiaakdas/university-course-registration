namespace UniversityRegistration.Specifications;

public sealed class CreditLoad
{
    public long CurrentCredits { get; }
    public long RequestedCredits { get; }
    public long CreditLimit { get; }

    public CreditLoad(long currentCredits, long requestedCredits, long creditLimit)
    {
        if (currentCredits < 0) throw new ArgumentOutOfRangeException(nameof(currentCredits));
        if (requestedCredits < 0) throw new ArgumentOutOfRangeException(nameof(requestedCredits));
        if (creditLimit <= 0) throw new ArgumentOutOfRangeException(nameof(creditLimit));
        CurrentCredits = currentCredits;
        RequestedCredits = requestedCredits;
        CreditLimit = creditLimit;
    }
}

public sealed class CreditLimitSpecification
{
    public bool IsSatisfiedBy(CreditLoad input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.CurrentCredits <= input.CreditLimit
            && input.RequestedCredits <= input.CreditLimit - input.CurrentCredits;
    }
}
