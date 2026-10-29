using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

internal static class AcademicRejectionHttp
{
    public static object[] Failures(AcademicEvaluation evaluation) => evaluation.Failures.Select<AcademicRejection, object>(f => f switch
    {
        AcademicRejection.PrerequisiteNotSatisfied p => new { p.Code, p.Message, p.CourseOfferingId },
        AcademicRejection.CreditLimitExceeded c => new { c.Code, c.Message, c.AcademicTermId },
        AcademicRejection.ScheduleConflict s => new { s.Code, s.Message, s.CourseOfferingId, s.ConflictingCourseOfferingId },
        _ => throw new InvalidOperationException("Unknown academic rejection.")
    }).ToArray();

    public static IResult Problem(AcademicEvaluation evaluation) => Results.Problem(statusCode: 400,
        title: "Kayıt koşulları sağlanmadı.", extensions: new Dictionary<string, object?> { ["failures"] = Failures(evaluation) });
}
