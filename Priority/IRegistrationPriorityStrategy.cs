namespace UniversityRegistration.Priority;

// Pure and deterministic. Higher scores mean higher priority within this policy only.
// Requires a valid non-null input; null always throws ArgumentNullException.
public interface IRegistrationPriorityStrategy
{
    int Calculate(PriorityInput input);
}
