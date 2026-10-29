using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

internal static class AcademicRejectionHttp
{
    public static IResult Problem(AcademicRejection rejection) => rejection switch
    {
        AcademicRejection.PrerequisiteNotSatisfied missing => Results.Problem(statusCode: 400,
            title: "Ders ön koşulu sağlanmadı.", detail: "Seçilen dersin doğrudan ön koşulları başarıyla tamamlanmalıdır.",
            extensions: new Dictionary<string, object?> { ["code"] = "prerequisite_not_satisfied", ["courseOfferingId"] = missing.CourseOfferingId }),
        AcademicRejection.CreditLimitExceeded limit => Results.Problem(statusCode: 400,
            title: "Dönemlik kredi sınırı aşılıyor.", detail: "Mevcut kayıtlar ve yeni seçimler dönemlik kredi sınırını aşamaz.",
            extensions: new Dictionary<string, object?> { ["code"] = "credit_limit_exceeded", ["academicTermId"] = limit.AcademicTermId }),
        _ => throw new InvalidOperationException("Unknown academic rejection.")
    };
}
