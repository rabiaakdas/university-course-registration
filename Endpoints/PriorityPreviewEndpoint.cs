using UniversityRegistration.Contracts;
using UniversityRegistration.Priority;

namespace UniversityRegistration.Endpoints;

public static class PriorityPreviewEndpoint
{
    public static void MapPriorityPreviewEndpoint(this WebApplication app) =>
        app.MapPost("/registration-priority/preview", Preview);

    public static IResult Preview(PriorityPreviewRequest request)
    {
        var policy = request.Policy?.Trim().ToLowerInvariant();
        IRegistrationPriorityStrategy? strategy = policy switch
        {
            "graduation" => new GraduationPriorityStrategy(),
            "completed-credits" => new CompletedCreditsPriorityStrategy(),
            _ => null
        };
        var errors = new Dictionary<string, string[]>();
        if (strategy is null) errors["policy"] = ["graduation veya completed-credits politikası seçilmelidir."];
        if (request.CompletedCredits is null or < 0)
            errors["completedCredits"] = ["Tamamlanan kredi zorunludur ve negatif olamaz."];
        if (request.RequiredCredits is null or <= 0)
            errors["requiredCredits"] = ["Gereken kredi zorunludur ve sıfırdan büyük olmalıdır."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var input = new PriorityInput(request.CompletedCredits!.Value, request.RequiredCredits!.Value);
        return Results.Ok(new PriorityPreviewResponse(policy!, strategy!.Calculate(input)));
    }
}
