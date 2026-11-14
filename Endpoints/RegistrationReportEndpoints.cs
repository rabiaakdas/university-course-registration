using UniversityRegistration.Accounts;
using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

public static class RegistrationReportEndpoints
{
    public static void MapRegistrationReportEndpoints(this WebApplication app)
    {
        app.MapGet("/reports/terms/{termId:int}/occupancy", async (int termId, RegistrationReports reader,
            HttpContext http, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var errors = RegistrationReports.Validate(termId, page, pageSize);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await reader.OccupancyAsync(termId, page, pageSize, ct));
        }).RequireAuthorization(UniversityRoles.ManageCatalog);
        app.MapGet("/reports/terms/{termId:int}/pending-approvals", async (int termId, RegistrationReports reader,
            HttpContext http, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var errors = RegistrationReports.Validate(termId, page, pageSize);
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            return Results.Ok(await reader.PendingAsync(termId, page, pageSize, ct));
        }).RequireAuthorization(UniversityRoles.ManageCatalog);
    }
}
