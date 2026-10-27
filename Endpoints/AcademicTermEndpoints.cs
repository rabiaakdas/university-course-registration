using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class AcademicTermEndpoints
{
    public static void MapAcademicTermEndpoints(this WebApplication app)
    {
        app.MapGet("/academic-terms", async (UniversityDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AcademicTerms.OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.Name, t.StartsOn, t.EndsOn }).ToListAsync(ct)));
        app.MapGet("/academic-terms/{id:int}", async (int id, UniversityDbContext db, CancellationToken ct) =>
        {
            var term = await db.AcademicTerms.Where(t => t.Id == id)
                .Select(t => new { t.Id, t.Name, t.StartsOn, t.EndsOn }).SingleOrDefaultAsync(ct);
            return term is null ? Results.Problem(statusCode: 404, title: "Dönem bulunamadı.") : Results.Ok(term);
        });
        app.MapPost("/academic-terms", CreateAsync);
    }

    public static async Task<IResult> CreateAsync(CreateAcademicTermRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var input = OfferingInput.Term(request);
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        var term = new AcademicTerm { Name = input.Name, StartsOn = request.StartsOn!.Value, EndsOn = request.EndsOn!.Value };
        db.AcademicTerms.Add(term);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/academic-terms/{term.Id}", new { term.Id, term.Name, term.StartsOn, term.EndsOn });
    }
}
