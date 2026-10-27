using UniversityRegistration.Contracts;

namespace UniversityRegistration.Endpoints;

public static class OfferingInput
{
    public static (string Name, Dictionary<string, string[]> Errors) Term(CreateAcademicTermRequest request)
    {
        var name = request.Name?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (name.Length == 0 || name.Length > 100) errors["name"] = ["Dönem adı zorunludur ve en fazla 100 karakter olabilir."];
        if (request.StartsOn is null) errors["startsOn"] = ["Başlangıç tarihi zorunludur."];
        if (request.EndsOn is null) errors["endsOn"] = ["Bitiş tarihi zorunludur."];
        else if (request.StartsOn is not null && request.EndsOn < request.StartsOn)
            errors["endsOn"] = ["Bitiş tarihi başlangıçtan önce olamaz."];
        return (name, errors);
    }

    public static (string Section, Dictionary<string, string[]> Errors) Offering(CreateCourseOfferingRequest request)
    {
        var section = request.SectionCode?.Trim().ToUpperInvariant() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (request.CourseId <= 0) errors["courseId"] = ["Ders kimliği pozitif olmalıdır."];
        if (request.AcademicTermId <= 0) errors["academicTermId"] = ["Dönem kimliği pozitif olmalıdır."];
        if (section.Length == 0 || section.Length > 10) errors["sectionCode"] = ["Şube kodu zorunludur ve en fazla 10 karakter olabilir."];
        if (request.Capacity <= 0) errors["capacity"] = ["Kontenjan pozitif olmalıdır."];
        return (section, errors);
    }
}
