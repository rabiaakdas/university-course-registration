using System.Collections.ObjectModel;
using UniversityRegistration.Application;
using UniversityRegistration.Models;
using UniversityRegistration.Specifications;

// One already-filtered student's prepared evaluator input, not a DB fixture/model.
internal sealed class EligibilityScenario
{
    public IReadOnlyList<OfferingEligibility> Requested { get; }
    public IReadOnlyDictionary<int,long> CurrentCredits { get; }
    public IReadOnlyList<OfferingSchedule> Existing { get; }
    public long CreditLimit { get; }
    internal EligibilityScenario(IEnumerable<OfferingEligibility> requested, IDictionary<int,long> credits,
        IEnumerable<OfferingSchedule> existing, long limit)
    {
        // Elements are immutable production values: schedules own read-only slot copies;
        // WeeklyTimeSlot is sealed/get-only and prerequisite sets are FrozenSet.
        Requested=Array.AsReadOnly(requested.ToArray());
        CurrentCredits=new ReadOnlyDictionary<int,long>(new Dictionary<int,long>(credits));
        Existing=Array.AsReadOnly(existing.ToArray());
        CreditLimit=limit;
    }
}

internal sealed class EligibilityScenarioBuilder
{
    // Mutable preparation only; never shared with built results.
    private sealed class Definition(int id,int termId,int credits,EnrollmentStatus? status)
    {
        public int Id { get; }=id;
        public int TermId { get; }=termId;
        public int Credits { get; }=credits;
        public EnrollmentStatus? Status { get; }=status; // null means a new selection
        public List<WeeklyTimeSlot> Slots { get; }=[];
        public HashSet<int> Required { get; }=[];
    }
    readonly Dictionary<int,Definition> definitions=[];
    readonly List<int> requestOrder=[];
    readonly HashSet<int> completed=[];
    readonly long limit;
    public EligibilityScenarioBuilder(long creditLimit)
    {
        if(creditLimit<=0)throw new ArgumentOutOfRangeException(nameof(creditLimit));
        limit=creditLimit;
    }
    public EligibilityScenarioBuilder Request(int offeringId,int termId,int credits)
    {
        Add(offeringId,termId,credits,null);requestOrder.Add(offeringId);return this;
    }
    public EligibilityScenarioBuilder Existing(int offeringId,int termId,int credits,EnrollmentStatus status)
    {
        if(!Enum.IsDefined(status))throw new ArgumentOutOfRangeException(nameof(status));
        Add(offeringId,termId,credits,status);return this;
    }
    void Add(int id,int term,int credits,EnrollmentStatus? status)
    {
        if(id<=0)throw new ArgumentOutOfRangeException(nameof(id));
        if(term<=0)throw new ArgumentOutOfRangeException(nameof(term));
        if(credits<=0)throw new ArgumentOutOfRangeException(nameof(credits));
        if(definitions.ContainsKey(id))throw new ArgumentException($"Offering {id} is already defined; definitions cannot be overwritten.",nameof(id));
        definitions.Add(id,new(id,term,credits,status));
    }
    Definition Defined(int id)
    {
        if(id<=0)throw new ArgumentOutOfRangeException(nameof(id));
        return definitions.TryGetValue(id,out var entry)?entry:throw new ArgumentException($"Offering {id} must be defined first.",nameof(id));
    }
    public EligibilityScenarioBuilder WithSlots(int offeringId,IEnumerable<WeeklyTimeSlot> slots)
    {
        var entry=Defined(offeringId);ArgumentNullException.ThrowIfNull(slots);
        var copy=slots.ToArray();
        if(copy.Any(s=>s is null))throw new ArgumentException("Slots cannot contain null.",nameof(slots));
        // Exact duplicates are rejected, not silently removed. Overlaps remain valid data.
        var keys=entry.Slots.Select(s=>(s.Day,s.StartsAt,s.EndsAt)).ToHashSet();
        foreach(var slot in copy)
            if(!keys.Add((slot.Day,slot.StartsAt,slot.EndsAt)))throw new ArgumentException($"Duplicate slot for offering {offeringId}.",nameof(slots));
        entry.Slots.AddRange(copy);return this;
    }
    public EligibilityScenarioBuilder Requires(int offeringId,IEnumerable<int> courseIds)
    {
        var entry=Defined(offeringId);
        if(entry.Status is not null)throw new ArgumentException("Prerequisites are prepared only for requested offerings.",nameof(offeringId));
        entry.Required.UnionWith(CopyIds(courseIds));return this;
    }
    public EligibilityScenarioBuilder Completed(IEnumerable<int> courseIds)
    { completed.UnionWith(CopyIds(courseIds));return this; }
    static int[] CopyIds(IEnumerable<int> courseIds)
    {
        ArgumentNullException.ThrowIfNull(courseIds);var copy=courseIds.ToArray();
        if(copy.Any(id=>id<=0))throw new ArgumentOutOfRangeException(nameof(courseIds));
        return copy; // sets match the production prerequisite input's set semantics
    }
    public EligibilityScenario Build()
    {
        var credits=new Dictionary<int,long>();var existing=new List<OfferingSchedule>();
        foreach(var entry in definitions.Values.Where(e=>e.Status is EnrollmentStatus.Submitted or EnrollmentStatus.Approved))
        {
            credits[entry.TermId]=checked(credits.GetValueOrDefault(entry.TermId)+(long)entry.Credits);
            existing.Add(new(entry.Id,entry.TermId,entry.Slots));
        }
        var requested=requestOrder.Select(id=>definitions[id]).Select(e=>
            new OfferingEligibility(new(e.Id,e.TermId,e.Slots),e.Credits,new(e.Required,completed))).ToArray();
        // No evaluation, normalization, auto-completion, reset or I/O.
        return new(requested,credits,existing,limit);
    }
}
