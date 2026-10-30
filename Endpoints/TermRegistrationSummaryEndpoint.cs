using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

public static class TermRegistrationSummaryEndpoint
{
    public static void MapTermRegistrationSummaryEndpoint(this WebApplication app) =>
        app.MapGet("/students/{studentId:int}/terms/{termId:int}/registration-summary", GetAsync);

    public static async Task<IResult> GetAsync(int studentId, int termId,
        TermRegistrationSummaryFacade facade, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (studentId <= 0) errors["studentId"] = ["Öğrenci kimliği pozitif olmalıdır."];
        if (termId <= 0) errors["termId"] = ["Dönem kimliği pozitif olmalıdır."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return await facade.GetAsync(studentId, termId, ct) switch
        {
            TermRegistrationSummaryResult.Found found => Results.Ok(found.Summary),
            TermRegistrationSummaryResult.StudentMissing => Results.Problem(statusCode: 404, title: "Öğrenci bulunamadı."),
            TermRegistrationSummaryResult.TermMissing => Results.Problem(statusCode: 404, title: "Akademik dönem bulunamadı."),
            _ => throw new InvalidOperationException("Unknown summary result.")
        };
    }
}
