namespace UniversityRegistration.Application;

public sealed class EnrollmentPolicy
{
    public long MaxCreditsPerTerm { get; }
    public EnrollmentPolicy(long maxCreditsPerTerm)
    {
        if (maxCreditsPerTerm <= 0) throw new ArgumentOutOfRangeException(nameof(maxCreditsPerTerm));
        MaxCreditsPerTerm = maxCreditsPerTerm;
    }
}
