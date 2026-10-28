using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

// Scoped with its DbContext. Callers validate request shape before invoking these operations.
public sealed class EnrollmentOperations(UniversityDbContext db)
{
    public async Task<SingleEnrollmentResult> CreateAsync(int studentId, int offeringId, CancellationToken ct)
    {
        var studentExists = await db.Students.AnyAsync(s => s.Id == studentId, ct);
        var offeringExists = await db.CourseOfferings.AnyAsync(o => o.Id == offeringId, ct);
        if (!studentExists || !offeringExists)
            return new SingleEnrollmentResult.Rejected(!studentExists && !offeringExists
                ? SingleEnrollmentFailure.BothReferencesMissing
                : !studentExists ? SingleEnrollmentFailure.StudentMissing : SingleEnrollmentFailure.OfferingMissing);
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseOfferingId == offeringId, ct))
            return new SingleEnrollmentResult.Rejected(SingleEnrollmentFailure.Duplicate);
        var enrollment = new Enrollment { StudentId = studentId, CourseOfferingId = offeringId, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.Enrollments.Add(enrollment);
        var failure = await SaveAsync(ct);
        return failure switch
        {
            WriteFailure.Duplicate => new SingleEnrollmentResult.Rejected(SingleEnrollmentFailure.Duplicate),
            WriteFailure.StudentRemoved => new SingleEnrollmentResult.Rejected(SingleEnrollmentFailure.StudentMissing),
            WriteFailure.OfferingRemoved => new SingleEnrollmentResult.Rejected(SingleEnrollmentFailure.OfferingMissing),
            null => new SingleEnrollmentResult.Created(Response(enrollment)),
            _ => throw new InvalidOperationException("Unknown enrollment write result.")
        };
    }

    public async Task<BatchEnrollmentResult> CreateBatchAsync(int studentId, int[] offeringIds, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId, ct))
            return new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.StudentMissing);
        var found = await db.CourseOfferings.Where(o => offeringIds.Contains(o.Id)).Select(o => o.Id).ToListAsync(ct);
        if (found.Count != offeringIds.Length)
            return new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.OfferingsMissing);
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && offeringIds.Contains(e.CourseOfferingId), ct))
            return new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.Duplicate);
        var now = DateTimeOffset.UtcNow;
        var enrollments = offeringIds.Select(offeringId => new Enrollment
            { StudentId = studentId, CourseOfferingId = offeringId, CreatedAtUtc = now }).ToArray();
        db.Enrollments.AddRange(enrollments);
        // Never call the single operation in a loop: this entire batch has one SaveChanges.
        var failure = await SaveAsync(ct);
        return failure switch
        {
            WriteFailure.Duplicate => new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.Duplicate),
            WriteFailure.StudentRemoved => new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.StudentRemoved),
            WriteFailure.OfferingRemoved => new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.OfferingRemoved),
            null => new BatchEnrollmentResult.Created(enrollments.Select(Response).ToArray()),
            _ => throw new InvalidOperationException("Unknown enrollment write result.")
        };
    }

    private enum WriteFailure { Duplicate, StudentRemoved, OfferingRemoved }
    private async Task<WriteFailure?> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return null; }
        catch (DbUpdateException ex) when (RegistrationWriteErrors.Classify(ex) is "duplicate" or "studentId" or "courseOfferingId")
        {
            return RegistrationWriteErrors.Classify(ex) switch
            {
                "duplicate" => WriteFailure.Duplicate,
                "studentId" => WriteFailure.StudentRemoved,
                "courseOfferingId" => WriteFailure.OfferingRemoved,
                _ => throw new InvalidOperationException("Unknown enrollment constraint.")
            };
        }
        // Unknown SQL errors and cancellation propagate; no retry with a failed tracker.
    }

    private static EnrollmentResponse Response(Enrollment e) => new(e.Id, e.StudentId, e.CourseOfferingId, e.CreatedAtUtc);
}
