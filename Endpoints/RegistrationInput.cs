using UniversityRegistration.Contracts;

namespace UniversityRegistration.Endpoints;

public static class RegistrationInput
{
    public static (string Number, string Name, Dictionary<string, string[]> Errors) Student(CreateStudentRequest request)
    {
        var number = request.StudentNumber?.Trim() ?? "";
        var name = request.FullName?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (number.Length == 0 || number.Length > 30)
            errors["studentNumber"] = ["Öğrenci numarası zorunludur ve en fazla 30 karakter olabilir."];
        if (name.Length == 0 || name.Length > 200)
            errors["fullName"] = ["Ad soyad zorunludur ve en fazla 200 karakter olabilir."];
        return (number, name, errors);
    }

    public static Dictionary<string, string[]> Enrollment(CreateEnrollmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.StudentId <= 0) errors["studentId"] = ["Öğrenci kimliği pozitif olmalıdır."];
        if (request.CourseOfferingId <= 0) errors["courseOfferingId"] = ["Açılış kimliği pozitif olmalıdır."];
        return errors;
    }
}
