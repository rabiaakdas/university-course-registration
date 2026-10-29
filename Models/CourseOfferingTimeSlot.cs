namespace UniversityRegistration.Models;

public sealed class CourseOfferingTimeSlot
{
    public int Id { get; init; }
    public int CourseOfferingId { get; init; }
    public DayOfWeek Day { get; init; }
    public TimeOnly StartsAt { get; init; }
    public TimeOnly EndsAt { get; init; }
}
