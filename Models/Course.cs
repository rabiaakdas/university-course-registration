namespace UniversityRegistration.Models;

public class Course
{
    public int Id { get; init; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Credits { get; set; }
}
