namespace UniversityRegistration.Models;

public class AcademicTerm
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateOnly StartsOn { get; init; }
    public DateOnly EndsOn { get; init; }
}
