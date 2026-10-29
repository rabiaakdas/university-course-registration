namespace UniversityRegistration.Models;

public sealed class CoursePrerequisite
{
    public int CourseId { get; init; }
    public int PrerequisiteCourseId { get; init; }
}
