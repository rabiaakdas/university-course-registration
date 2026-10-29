using System.Collections.Frozen;

namespace UniversityRegistration.Specifications;

public sealed class PrerequisiteInput
{
    public FrozenSet<int> RequiredCourseIds { get; }
    public FrozenSet<int> CompletedCourseIds { get; }

    public PrerequisiteInput(IEnumerable<int> requiredCourseIds, IEnumerable<int> completedCourseIds)
    {
        ArgumentNullException.ThrowIfNull(requiredCourseIds);
        ArgumentNullException.ThrowIfNull(completedCourseIds);
        RequiredCourseIds = requiredCourseIds.ToFrozenSet();
        CompletedCourseIds = completedCourseIds.ToFrozenSet();
        if (RequiredCourseIds.Any(id => id <= 0)) throw new ArgumentOutOfRangeException(nameof(requiredCourseIds));
        if (CompletedCourseIds.Any(id => id <= 0)) throw new ArgumentOutOfRangeException(nameof(completedCourseIds));
    }
}

public sealed class PrerequisitesSatisfiedSpecification
{
    public bool IsSatisfiedBy(PrerequisiteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.RequiredCourseIds.IsSubsetOf(input.CompletedCourseIds);
    }
}
