using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Accounts;
using UniversityRegistration.Application;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class WaitlistEndpoints
{
    public sealed record JoinRequest(int CourseOfferingId);
    public sealed record EntryResponse(long Id, int StudentId, int CourseOfferingId, long QueueNumber,
        DateTimeOffset JoinedAtUtc, string Status, DateTimeOffset? ClosedAtUtc, string? ClosureReason, long? Position);

    public static void MapWaitlistEndpoints(this WebApplication app)
    {
        app.MapPost("/students/{studentId:int}/waitlist", JoinAsync)
            .WithStudentAccess(StudentAccessKind.Manage, routeKey: "studentId")
            .AddEndpointFilter<CatalogAntiforgeryFilter>();
        app.MapGet("/students/{studentId:int}/waitlist/{entryId:long}", ReadAsync)
            .WithStudentAccess(StudentAccessKind.Manage, routeKey: "studentId");
        app.MapPost("/students/{studentId:int}/waitlist/{entryId:long}/cancel", CancelAsync)
            .WithStudentAccess(StudentAccessKind.Manage, routeKey: "studentId")
            .AddEndpointFilter<CatalogAntiforgeryFilter>();
    }

    static IResult Missing() => Results.Problem(statusCode: 404, title: "Kaynak bulunamadı.");
    static IResult Conflict(string code) => Results.Problem(statusCode: 409, title: "Bekleme listesi işlemi yapılamadı.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    static async Task<IResult> JoinAsync(int studentId, JoinRequest request, UniversityDbContext db, CancellationToken ct)
    {
        if (request.CourseOfferingId <= 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseOfferingId"] = ["Pozitif açılış kimliği gereklidir."] });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await OfferingCoordination.LockAsync(db, request.CourseOfferingId, ct) is null) return Missing();
        var offering = await db.CourseOfferings.Where(x => x.Id == request.CourseOfferingId)
            .Select(x => new { x.Capacity, x.OccupiedSeats }).SingleOrDefaultAsync(ct);
        if (offering is null) return Missing();
        if (await db.Enrollments.AnyAsync(x => x.StudentId == studentId && x.CourseOfferingId == request.CourseOfferingId
            && x.Status != EnrollmentStatus.Draft, ct)) return Conflict("waitlist_enrollment_exists");
        if (offering.OccupiedSeats < offering.Capacity) return Conflict("waitlist_capacity_available");
        var entry = new WaitlistEntry { StudentId = studentId, CourseOfferingId = request.CourseOfferingId, JoinedAtUtc = DateTimeOffset.UtcNow };
        db.WaitlistEntries.Add(entry);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (RegistrationWriteErrors.Classify(error) == "waitlistDuplicate")
        { return Conflict("waitlist_already_waiting"); }
        await transaction.CommitAsync(ct);
        return Results.Created($"/students/{studentId}/waitlist/{entry.Id}", await ResponseAsync(db, studentId, entry.Id, ct));
    }

    static Task<EntryResponse?> ResponseAsync(UniversityDbContext db, int studentId, long entryId, CancellationToken ct) =>
        db.WaitlistEntries.Where(x => x.Id == entryId && x.StudentId == studentId)
            .Select(x => new EntryResponse(x.Id, x.StudentId, x.CourseOfferingId, x.QueueNumber, x.JoinedAtUtc,
                x.Status.ToString(), x.ClosedAtUtc, x.ClosureReason,
                x.Status == WaitlistStatus.Waiting ? (long?)db.WaitlistEntries.LongCount(y =>
                    y.CourseOfferingId == x.CourseOfferingId && y.Status == WaitlistStatus.Waiting && y.QueueNumber <= x.QueueNumber) : null))
            .SingleOrDefaultAsync(ct);

    static async Task<IResult> ReadAsync(int studentId, long entryId, UniversityDbContext db, CancellationToken ct)
    {
        var result = await ResponseAsync(db, studentId, entryId, ct);
        return result is null ? Missing() : Results.Ok(result);
    }

    static async Task<IResult> CancelAsync(int studentId, long entryId, UniversityDbContext db, CancellationToken ct)
    {
        var offeringId = await db.WaitlistEntries.Where(w => w.Id == entryId && w.StudentId == studentId).Select(w => (int?)w.CourseOfferingId).SingleOrDefaultAsync(ct);
        if (offeringId is null) return Missing();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await OfferingCoordination.LockAsync(db, offeringId.Value, ct);
        var now = DateTimeOffset.UtcNow;
        var changed = await db.WaitlistEntries.Where(x => x.Id == entryId && x.StudentId == studentId && x.Status == WaitlistStatus.Waiting)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WaitlistStatus.Cancelled)
                .SetProperty(x => x.ClosedAtUtc, now).SetProperty(x => x.ClosureReason, "student_cancelled"), ct);
        if (changed == 1) { await transaction.CommitAsync(ct); return Results.NoContent(); }
        var status = await db.WaitlistEntries.Where(x => x.Id == entryId && x.StudentId == studentId)
            .Select(x => (WaitlistStatus?)x.Status).SingleOrDefaultAsync(ct);
        return status is null ? Missing() : status == WaitlistStatus.Cancelled ? Results.NoContent() : Conflict("waitlist_closed");
    }
}
