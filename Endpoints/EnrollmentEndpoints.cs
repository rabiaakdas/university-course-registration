using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this WebApplication app)
    {
        app.MapPost("/enrollments", CreateAsync);
        app.MapGet("/enrollments/{id:int}", async (int id, UniversityDbContext db, CancellationToken ct) =>
        {
            var enrollment = await db.Enrollments.Where(e => e.Id == id)
                .Select(e => new EnrollmentResponse(e.Id, e.StudentId, e.CourseOfferingId, e.CreatedAtUtc)).SingleOrDefaultAsync(ct);
            return enrollment is null ? Results.Problem(statusCode: 404, title: "Kayıt bulunamadı.") : Results.Ok(enrollment);
        });
    }

    public static async Task<IResult> CreateAsync(CreateEnrollmentRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var errors = RegistrationInput.Enrollment(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!await db.Students.AnyAsync(s => s.Id == request.StudentId, ct)) errors["studentId"] = ["Öğrenci bulunamadı."];
        if (!await db.CourseOfferings.AnyAsync(o => o.Id == request.CourseOfferingId, ct)) errors["courseOfferingId"] = ["Ders açılışı bulunamadı."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (await db.Enrollments.AnyAsync(e => e.StudentId == request.StudentId && e.CourseOfferingId == request.CourseOfferingId, ct)) return Conflict();
        var enrollment = new Enrollment { StudentId = request.StudentId, CourseOfferingId = request.CourseOfferingId, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Enrollments.Add(enrollment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (RegistrationWriteErrors.Classify(ex) is "duplicate" or "studentId" or "courseOfferingId")
        {
            var error = RegistrationWriteErrors.Classify(ex)!;
            return error == "duplicate" ? Conflict() : Results.ValidationProblem(new Dictionary<string, string[]>
                { [error] = [error == "studentId" ? "Öğrenci bulunamadı." : "Ders açılışı bulunamadı."] });
        }
        return Results.Created($"/enrollments/{enrollment.Id}", new EnrollmentResponse(enrollment.Id,
            enrollment.StudentId, enrollment.CourseOfferingId, enrollment.CreatedAtUtc));
    }

    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Öğrenci bu açılışa zaten kayıtlı.");
}
