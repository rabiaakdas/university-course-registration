using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class CourseOfferingEndpoints
{
    public static IQueryable<CourseOfferingResponse> Query(UniversityDbContext db) =>
        from o in db.CourseOfferings
        join c in db.Courses on o.CourseId equals c.Id
        join t in db.AcademicTerms on o.AcademicTermId equals t.Id
        select new CourseOfferingResponse { Id = o.Id, CourseId = c.Id, CourseCode = c.Code, CourseName = c.Name,
            AcademicTermId = t.Id, TermName = t.Name, SectionCode = o.SectionCode, Capacity = o.Capacity };

    public static void MapCourseOfferingEndpoints(this WebApplication app)
    {
        app.MapGet("/course-offerings", async (UniversityDbContext db, CancellationToken ct) =>
            Results.Ok(await Query(db).OrderBy(o => o.Id).ToListAsync(ct)));
        app.MapGet("/course-offerings/{id:int}", async (int id, UniversityDbContext db, CancellationToken ct) =>
        {
            var offering = await Query(db).SingleOrDefaultAsync(o => o.Id == id, ct);
            return offering is null ? Results.Problem(statusCode: 404, title: "Ders açılışı bulunamadı.") : Results.Ok(offering);
        });
        app.MapPost("/course-offerings", CreateAsync);
    }

    public static async Task<IResult> CreateAsync(CreateCourseOfferingRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var input = OfferingInput.Offering(request);
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        var course = await db.Courses.Where(c => c.Id == request.CourseId).Select(c => new { c.Code, c.Name }).SingleOrDefaultAsync(ct);
        var term = await db.AcademicTerms.Where(t => t.Id == request.AcademicTermId).Select(t => new { t.Name }).SingleOrDefaultAsync(ct);
        if (course is null) input.Errors["courseId"] = ["Ders bulunamadı."];
        if (term is null) input.Errors["academicTermId"] = ["Dönem bulunamadı."];
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        if (await db.CourseOfferings.AnyAsync(o => o.CourseId == request.CourseId
            && o.AcademicTermId == request.AcademicTermId && o.SectionCode == input.Section, ct)) return Conflict();

        var offering = new CourseOffering { CourseId = request.CourseId, AcademicTermId = request.AcademicTermId,
            SectionCode = input.Section, Capacity = request.Capacity };
        db.CourseOfferings.Add(offering);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (OfferingWriteErrors.Classify(ex) is not null)
        {
            var error = OfferingWriteErrors.Classify(ex)!;
            return error == "duplicate" ? Conflict() : Results.ValidationProblem(new Dictionary<string, string[]>
                { [error] = [error == "courseId" ? "Ders bulunamadı." : "Dönem bulunamadı."] });
        }
        return Results.Created($"/course-offerings/{offering.Id}", new CourseOfferingResponse { Id = offering.Id,
            CourseId = offering.CourseId, CourseCode = course!.Code, CourseName = course.Name,
            AcademicTermId = offering.AcademicTermId, TermName = term!.Name, SectionCode = offering.SectionCode, Capacity = offering.Capacity });
    }

    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Bu ders, dönem ve şube için açılış zaten var.");
}
