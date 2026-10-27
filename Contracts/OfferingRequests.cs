namespace UniversityRegistration.Contracts;

public record CreateAcademicTermRequest(string? Name, DateOnly? StartsOn, DateOnly? EndsOn);
public record CreateCourseOfferingRequest(int CourseId, int AcademicTermId, string? SectionCode, int Capacity);

public record CourseOfferingResponse
{
    public int Id { get; init; }
    public int CourseId { get; init; }
    public string CourseCode { get; init; } = "";
    public string CourseName { get; init; } = "";
    public int AcademicTermId { get; init; }
    public string TermName { get; init; } = "";
    public string SectionCode { get; init; } = "";
    public int Capacity { get; init; }
}
