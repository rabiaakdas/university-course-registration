namespace UniversityRegistration.Models;

public class Enrollment
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public int CourseOfferingId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
