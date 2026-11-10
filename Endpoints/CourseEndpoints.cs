using UniversityRegistration.Accounts;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class CourseEndpoints
{
    public static void MapCourseEndpoints(this WebApplication app)
    {
        app.MapGet("/courses", async (UniversityDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Courses.OrderBy(c => c.Id)
                .Select(c => new CourseListItemDto(c.Id, c.Code, c.Name)).ToListAsync(ct)));
        app.MapGet("/courses/catalog", CourseCatalogEndpoint.GetAsync);
        app.MapGet("/courses/{id:int}", GetAsync);
        app.MapPost("/courses", CreateAsync).RequireAuthorization(UniversityRoles.ManageCatalog).AddEndpointFilter<CatalogAntiforgeryFilter>();
        app.MapPut("/courses/{id:int}", UpdateAsync).RequireAuthorization(UniversityRoles.ManageCatalog).AddEndpointFilter<CatalogAntiforgeryFilter>();
    }

    public static async Task<IResult> GetAsync(int id, UniversityDbContext db, CancellationToken ct)
    {
        var course = await db.Courses.Where(c => c.Id == id)
            .Select(c => new CourseDetailDto(c.Id, c.Code, c.Name, c.Credits, c.RowVersion)).SingleOrDefaultAsync(ct);
        return course is null ? NotFound() : Results.Ok(course);
    }

    public static async Task<IResult> CreateAsync(CreateCourseRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var input = CourseInput.Validate(request.Code, request.Name, request.Credits);
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        if (await db.Courses.AnyAsync(c => c.Code == input.Code, ct)) return CodeConflict();

        var course = new Course { Code = input.Code, Name = input.Name, Credits = request.Credits };
        db.Courses.Add(course);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (CourseWriteErrors.IsDuplicateCode(ex)) { return CodeConflict(); }
        return Results.Created($"/courses/{course.Id}", new { course.Id, course.Code, course.Name, course.Credits });
    }

    public static async Task<IResult> UpdateAsync(int id, UpdateCourseRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (course is null) return NotFound();
        var input = CourseInput.Validate(request.Code, request.Name, request.Credits);
        var version = new byte[8];
        if (request.RowVersion is null || request.RowVersion.Length != 12
            || !Convert.TryFromBase64String(request.RowVersion, version, out var written) || written != 8
            || Convert.ToBase64String(version) != request.RowVersion)
            input.Errors["rowVersion"] = ["rowVersion ge\u00e7erli 8 baytl\u0131k Base64 s\u00fcr\u00fcm\u00fc olmal\u0131d\u0131r."];
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        if (await db.Courses.AnyAsync(c => c.Code == input.Code && c.Id != id, ct)) return CodeConflict();

        course.Code = input.Code;
        course.Name = input.Name;
        course.Credits = request.Credits;
        db.Entry(course).Property(c => c.RowVersion).OriginalValue = version;
        // Even an unchanged payload must issue a conditional UPDATE, never silently accept a stale version.
        db.Entry(course).Property(c => c.Name).IsModified = true;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(statusCode: 409, title: "Ders ba\u015fka bir i\u015flem taraf\u0131ndan de\u011fi\u015ftirildi. G\u00fcncel s\u00fcr\u00fcm\u00fc yeniden okuyun.",
                extensions: new Dictionary<string, object?> { ["code"] = "course_version_conflict" });
        }
        catch (DbUpdateException ex) when (CourseWriteErrors.IsDuplicateCode(ex)) { return CodeConflict(); }
        return Results.Ok(new CourseDetailDto(course.Id, course.Code, course.Name, course.Credits, course.RowVersion));
    }

    private static IResult NotFound() => Results.Problem(statusCode: 404, title: "Ders bulunamadı.");
    private static IResult CodeConflict() => Results.Problem(statusCode: 409, title: "Ders kodu zaten kullanılıyor.");
}
