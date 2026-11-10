namespace UniversityRegistration.Models;

public class CourseOffering
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public int AcademicTermId { get; init; }
    public string SectionCode { get; init; } = string.Empty;
    public int Capacity { get; init; }
    // Written only by coordinated enrollment transitions; never a request field.
    public int OccupiedSeats { get; private set; }
}
