using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Application;

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

    public static async Task<IResult> CreateAsync(CreateEnrollmentRequest request, EnrollmentOperations operations, CancellationToken ct)
    {
        var errors = RegistrationInput.Enrollment(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await operations.CreateAsync(request.StudentId, request.CourseOfferingId, ct);
        if (result is SingleEnrollmentResult.AcademicRejected academic) return AcademicRejectionHttp.Problem(academic.Rejection);
        if (result is SingleEnrollmentResult.Created created)
            return Results.Created($"/enrollments/{created.Enrollment.Id}", created.Enrollment);
        var reason = ((SingleEnrollmentResult.Rejected)result).Reason;
        if (reason == SingleEnrollmentFailure.Duplicate)
            return Results.Problem(statusCode: 409, title: "Öğrenci bu açılışa zaten kayıtlı.");
        if (reason is SingleEnrollmentFailure.StudentMissing or SingleEnrollmentFailure.BothReferencesMissing)
            errors["studentId"] = ["Öğrenci bulunamadı."];
        if (reason is SingleEnrollmentFailure.OfferingMissing or SingleEnrollmentFailure.BothReferencesMissing)
            errors["courseOfferingId"] = ["Ders açılışı bulunamadı."];
        return Results.ValidationProblem(errors);
    }
}
