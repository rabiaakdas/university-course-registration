using UniversityRegistration.Specifications;

namespace UniversityRegistration.Application;

// Prepared scalar data. Each schedule defensively copies its immutable slots.
public sealed class OfferingSchedule
{
    public int Id { get; }
    public int AcademicTermId { get; }
    public IReadOnlyList<WeeklyTimeSlot> Slots { get; }
    public OfferingSchedule(int id, int academicTermId, IEnumerable<WeeklyTimeSlot> slots)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (academicTermId <= 0) throw new ArgumentOutOfRangeException(nameof(academicTermId));
        ArgumentNullException.ThrowIfNull(slots);
        var copy = slots.ToArray();
        if (copy.Any(s => s is null)) throw new ArgumentException("Slots cannot contain null.", nameof(slots));
        Id = id; AcademicTermId = academicTermId; Slots = Array.AsReadOnly(copy);
    }
}

public sealed record OfferingEligibility(OfferingSchedule Schedule, int Credits, PrerequisiteInput Prerequisites);

// Pure evaluation: no EF, SQL or HTTP. Inputs are prepared for this request only.
public sealed class AcademicEligibility : IAcademicEligibilityEvaluator
{
    public AcademicEvaluation Evaluate(IReadOnlyList<OfferingEligibility> requested,
        IReadOnlyDictionary<int, long> currentCredits, IReadOnlyList<OfferingSchedule> existing, long limit)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(currentCredits);
        ArgumentNullException.ThrowIfNull(existing);
        var failures = new List<AcademicRejection>();
        var prerequisites = new PrerequisitesSatisfiedSpecification();
        foreach (var selection in requested)
            if (!prerequisites.IsSatisfiedBy(selection.Prerequisites))
                failures.Add(new AcademicRejection.PrerequisiteNotSatisfied(selection.Schedule.Id));

        var credits = new CreditLimitSpecification();
        foreach (var term in requested.GroupBy(o => o.Schedule.AcademicTermId))
            if (!credits.IsSatisfiedBy(new CreditLoad(currentCredits.GetValueOrDefault(term.Key),
                    term.Sum(o => (long)o.Credits), limit)))
                failures.Add(new AcademicRejection.CreditLimitExceeded(term.Key));

        // New selections in request order; existing IDs ascending, then later new selections.
        var seen = new HashSet<(int, int)>();
        for (var i = 0; i < requested.Count; i++)
        {
            var first = requested[i].Schedule;
            foreach (var second in existing.OrderBy(o => o.Id)) AddConflict(first, second);
            for (var j = i + 1; j < requested.Count; j++) AddConflict(first, requested[j].Schedule);
        }
        return new AcademicEvaluation(failures);

        void AddConflict(OfferingSchedule first, OfferingSchedule second)
        {
            if (first.Id == second.Id || first.AcademicTermId != second.AcademicTermId) return;
            if (!first.Slots.Any(a => second.Slots.Any(a.Overlaps))) return;
            if (seen.Add((Math.Min(first.Id, second.Id), Math.Max(first.Id, second.Id))))
                failures.Add(new AcademicRejection.ScheduleConflict(first.Id, second.Id));
        }
    }
}
