namespace UniversityRegistration.Specifications;

// University local time, repeating weekly; [start, end) within one day.
public sealed class WeeklyTimeSlot
{
    public DayOfWeek Day { get; }
    public TimeOnly StartsAt { get; }
    public TimeOnly EndsAt { get; }

    public WeeklyTimeSlot(DayOfWeek day, TimeOnly startsAt, TimeOnly endsAt)
    {
        if (!Enum.IsDefined(day)) throw new ArgumentOutOfRangeException(nameof(day));
        if (startsAt >= endsAt) throw new ArgumentOutOfRangeException(nameof(endsAt));
        Day = day; StartsAt = startsAt; EndsAt = endsAt;
    }

    public bool Overlaps(WeeklyTimeSlot other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Day == other.Day && StartsAt < other.EndsAt && other.StartsAt < EndsAt;
    }
}
