namespace UniversityRegistration.Models;

public class Course
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Credits { get; init; }
}
