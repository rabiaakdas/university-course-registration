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
        app.MapPost("/courses", CreateAsync);
        app.MapPut("/courses/{id:int}", UpdateAsync);
    }

    public static async Task<IResult> GetAsync(int id, UniversityDbContext db, CancellationToken ct)
    {
        var course = await db.Courses.Where(c => c.Id == id)
            .Select(c => new { c.Id, c.Code, c.Name, c.Credits }).SingleOrDefaultAsync(ct);
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
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        if (await db.Courses.AnyAsync(c => c.Code == input.Code && c.Id != id, ct)) return CodeConflict();

        course.Code = input.Code;
        course.Name = input.Name;
        course.Credits = request.Credits;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (CourseWriteErrors.IsDuplicateCode(ex)) { return CodeConflict(); }
        return Results.Ok(new { course.Id, course.Code, course.Name, course.Credits });
    }

    private static IResult NotFound() => Results.Problem(statusCode: 404, title: "Ders bulunamadı.");
    private static IResult CodeConflict() => Results.Problem(statusCode: 409, title: "Ders kodu zaten kullanılıyor.");
}
