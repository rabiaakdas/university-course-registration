using UniversityRegistration.Accounts;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Application;

namespace UniversityRegistration.Endpoints;

public static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this WebApplication app)
    {
        app.MapPost("/enrollments", CreateIdempotentAsync).WithStudentAccess(StudentAccessKind.Manage, body: true).AddEndpointFilter<CatalogAntiforgeryFilter>();
        app.MapPost("/enrollments/{id:int}/submit", async (int id, EnrollmentOperations operations, CancellationToken ct) =>
            Transition(await operations.SubmitAsync(id, ct))).WithStudentAccess(StudentAccessKind.Manage, enrollment: true).AddEndpointFilter<CatalogAntiforgeryFilter>();
        app.MapPost("/enrollments/{id:int}/approve", async (int id, EnrollmentOperations operations, CancellationToken ct) =>
            Transition(await operations.ApproveAsync(id, ct))).WithStudentAccess(StudentAccessKind.Decide, enrollment: true).AddEndpointFilter<CatalogAntiforgeryFilter>();
        app.MapPost("/enrollments/{id:int}/reject", async (int id, RejectEnrollmentRequest request, EnrollmentOperations operations, CancellationToken ct) =>
            Transition(await operations.RejectAsync(id, request.Reason, ct))).WithStudentAccess(StudentAccessKind.Decide, enrollment: true).AddEndpointFilter<CatalogAntiforgeryFilter>();
        app.MapGet("/enrollments/{id:int}", async (int id, UniversityDbContext db, CancellationToken ct) =>
        {
            var enrollment = await db.Enrollments.Where(e => e.Id == id)
                .Select(e => new EnrollmentResponse(e.Id, e.StudentId, e.CourseOfferingId, e.CreatedAtUtc, e.Status.ToString(), e.RejectionReason)).SingleOrDefaultAsync(ct);
            return enrollment is null ? Results.Problem(statusCode: 404, title: "Kayıt bulunamadı.") : Results.Ok(enrollment);
        }).WithStudentAccess(StudentAccessKind.Read, enrollment: true);
    }

    private static IResult Transition(EnrollmentTransitionResult result) => result switch
    {
        EnrollmentTransitionResult.Updated updated => Results.Ok(updated.Enrollment),
        EnrollmentTransitionResult.NotFound => Results.Problem(statusCode: 404, title: "Kayıt bulunamadı."),
        EnrollmentTransitionResult.InvalidTransition => Results.Problem(statusCode: 409, title: "Bu durumda istenen kayıt geçişi yapılamaz."),
        EnrollmentTransitionResult.CapacityUnavailable => Results.Problem(statusCode: 409, title: "Ders açılışında boş kontenjan yok.",
            extensions: new Dictionary<string, object?> { ["code"] = "capacity_unavailable" }),
        EnrollmentTransitionResult.InvalidReason => Results.ValidationProblem(new Dictionary<string, string[]>
        { ["reason"] = ["Ret gerekçesi boş olamaz ve temizlendikten sonra en fazla 500 karakter olmalıdır."] }),
        EnrollmentTransitionResult.AcademicRejected rejected => AcademicRejectionHttp.Problem(rejected.Evaluation),
        _ => throw new InvalidOperationException("Unknown enrollment transition result.")
    };

    private static async Task<IResult> CreateIdempotentAsync(CreateEnrollmentRequest request, HttpContext http,
        UniversityDbContext db, IdempotentEnrollmentCreation creation, CancellationToken ct)
    {
        var errors = RegistrationInput.Enrollment(request);
        var values = http.Request.Headers["Idempotency-Key"];
        if (values.Count != 1 || !Guid.TryParseExact(values[0], "D", out var parsed) || parsed == Guid.Empty)
            errors["idempotencyKey"] = ["A non-empty UUID Idempotency-Key header is required."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var ids = http.User.FindAll(System.Security.Claims.ClaimTypes.NameIdentifier).Select(x => x.Value).ToArray();
        if (ids.Length != 1 || string.IsNullOrWhiteSpace(ids[0])) return Results.Unauthorized();
        var accountId = ids[0];
        if (!await db.Users.AnyAsync(x => x.Id == accountId && x.EmailConfirmed, ct)) return Results.Unauthorized();
        if (!await (from membership in db.UserRoles join role in db.Roles on membership.RoleId equals role.Id
            where membership.UserId == accountId && role.Name == UniversityRoles.Student select membership).AnyAsync(ct))
            return Results.Forbid();
        var outcome = await creation.CreateAsync(accountId, Guid.ParseExact(values[0]!, "D"),
            new CreateEnrollmentCommand(request.StudentId, request.CourseOfferingId), ct);
        if (outcome.Conflict is not null) return Results.Problem(statusCode: 409, title: "Idempotency key cannot be used for this request.",
            extensions: new Dictionary<string, object?> { ["code"] = outcome.Conflict });
        if (outcome.Receipt is { } receipt)
        {
            http.Response.Headers.Location = receipt.Location;
            return Results.Content(receipt.ResponseJson!, "application/json", statusCode: receipt.StatusCode);
        }
        return CreationResult(outcome.Rejection!, errors);
    }

    public static async Task<IResult> CreateAsync(CreateEnrollmentRequest request, EnrollmentOperations operations, CancellationToken ct)
    {
        var errors = RegistrationInput.Enrollment(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var result = await operations.CreateAsync(new CreateEnrollmentCommand(request.StudentId, request.CourseOfferingId), ct);
        return CreationResult(result, errors);
    }

    private static IResult CreationResult(SingleEnrollmentResult result, Dictionary<string, string[]> errors)
    {
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
