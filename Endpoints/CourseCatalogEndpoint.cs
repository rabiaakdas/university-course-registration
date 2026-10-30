using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

public static class CourseCatalogEndpoint
{
    public static async Task<IResult> GetAsync(CourseCatalogReader reader, CancellationToken ct,
        string? search = null, int? credits = null, int page = 1, int pageSize = 20)
    {
        var errors = CourseCatalogQuery.Validate(credits, page, pageSize);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        return Results.Ok(await reader.ReadAsync(new CourseCatalogQuery(search, credits, page, pageSize), ct));
    }
}
