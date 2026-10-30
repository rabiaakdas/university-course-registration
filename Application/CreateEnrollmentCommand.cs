namespace UniversityRegistration.Application;

// HTTP requests may be malformed; an application command cannot contain invalid IDs.
public sealed class CreateEnrollmentCommand
{
    public int StudentId { get; }
    public int CourseOfferingId { get; }

    public CreateEnrollmentCommand(int studentId, int courseOfferingId)
    {
        var errors = Validate(studentId, courseOfferingId);
        if (errors.Count > 0) throw new ArgumentOutOfRangeException(errors.Keys.First());
        StudentId = studentId;
        CourseOfferingId = courseOfferingId;
    }

    public static Dictionary<string, string[]> Validate(int studentId, int courseOfferingId)
    {
        var errors = new Dictionary<string, string[]>();
        if (studentId <= 0) errors["studentId"] = ["Öğrenci kimliği pozitif olmalıdır."];
        if (courseOfferingId <= 0) errors["courseOfferingId"] = ["Açılış kimliği pozitif olmalıdır."];
        return errors;
    }
}
