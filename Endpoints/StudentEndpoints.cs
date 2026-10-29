using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this WebApplication app)
    {
        app.MapPost("/students", CreateAsync);
        app.MapGet("/students/{id:int}", async (int id, UniversityDbContext db, CancellationToken ct) =>
        {
            var student = await db.Students.Where(s => s.Id == id)
                .Select(s => new StudentResponse(s.Id, s.StudentNumber, s.FullName)).SingleOrDefaultAsync(ct);
            return student is null ? NotFound() : Results.Ok(student);
        });
        app.MapGet("/students/{id:int}/enrollments", ListEnrollmentsAsync);
        app.MapPost("/students/{id:int}/enrollments/batch", BatchEnrollmentEndpoint.CreateAsync);
        app.MapPost("/students/{id:int}/enrollments/eligibility", BatchEnrollmentEndpoint.PreviewAsync);
    }

    public static async Task<IResult> CreateAsync(CreateStudentRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var input = RegistrationInput.Student(request);
        if (input.Errors.Count > 0) return Results.ValidationProblem(input.Errors);
        if (await db.Students.AnyAsync(s => s.StudentNumber == input.Number, ct)) return Conflict();
        var student = new Student { StudentNumber = input.Number, FullName = input.Name };
        db.Students.Add(student);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (RegistrationWriteErrors.Classify(ex) == "studentNumber") { return Conflict(); }
        return Results.Created($"/students/{student.Id}", new StudentResponse(student.Id, student.StudentNumber, student.FullName));
    }

    public static async Task<IResult> ListEnrollmentsAsync(int id, UniversityDbContext db, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == id, ct)) return NotFound();
        var query = from e in db.Enrollments
                    join o in db.CourseOfferings on e.CourseOfferingId equals o.Id
                    join c in db.Courses on o.CourseId equals c.Id
                    join t in db.AcademicTerms on o.AcademicTermId equals t.Id
                    where e.StudentId == id
                    orderby e.Id
                    select new StudentEnrollmentDto(e.Id, o.Id, c.Code, c.Name, t.Name, o.SectionCode, e.CreatedAtUtc, e.Status.ToString(), e.RejectionReason);
        return Results.Ok(await query.ToListAsync(ct));
    }

    private static IResult NotFound() => Results.Problem(statusCode: 404, title: "Öğrenci bulunamadı.");
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Öğrenci numarası zaten kullanılıyor.");
}
