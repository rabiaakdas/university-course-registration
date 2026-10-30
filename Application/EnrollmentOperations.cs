using UniversityRegistration.Specifications;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

// Scoped with its DbContext. Callers validate request shape before invoking these operations.
public sealed class EnrollmentOperations(UniversityDbContext db, EnrollmentPolicy policy,
    IAcademicEligibilityEvaluator evaluator)
{
    public async Task<SingleEnrollmentResult> CreateAsync(int studentId, int offeringId, CancellationToken ct)
    {
        var studentExists = await db.Students.AnyAsync(s => s.Id == studentId, ct);
        var offering = await Offerings([offeringId]).SingleOrDefaultAsync(ct);
        var offeringExists = offering is not null;
        if (!studentExists || !offeringExists)
            return new SingleEnrollmentResult.Rejected(!studentExists && !offeringExists
                ? SingleEnrollmentFailure.BothReferencesMissing
                : !studentExists ? SingleEnrollmentFailure.StudentMissing : SingleEnrollmentFailure.OfferingMissing);
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.CourseOfferingId == offeringId, ct))
            return new SingleEnrollmentResult.Rejected(SingleEnrollmentFailure.Duplicate);
        var academic = await EvaluateAsync(studentId, [offering!], ct);
        if (!academic.IsEligible) return new SingleEnrollmentResult.AcademicRejected(academic);
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
        var evaluation = await EvaluateBatchAsync(studentId, offeringIds, ct);
        if (evaluation is EligibilityResult.Rejected rejected) return new BatchEnrollmentResult.Rejected(rejected.Reason);
        var academic = ((EligibilityResult.Evaluated)evaluation).Evaluation;
        if (!academic.IsEligible) return new BatchEnrollmentResult.AcademicRejected(academic);
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

    // Shared preflight, always reads fresh data. No reservation, Add or SaveChanges here.
    public async Task<EligibilityResult> EvaluateBatchAsync(int studentId, int[] offeringIds, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId, ct))
            return new EligibilityResult.Rejected(BatchEnrollmentFailure.StudentMissing);
        var found = await Offerings(offeringIds).ToListAsync(ct);
        if (found.Count != offeringIds.Length)
            return new EligibilityResult.Rejected(BatchEnrollmentFailure.OfferingsMissing);
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && offeringIds.Contains(e.CourseOfferingId), ct))
            return new EligibilityResult.Rejected(BatchEnrollmentFailure.Duplicate);
        var byId = found.ToDictionary(o => o.Id);
        var academic = await EvaluateAsync(studentId, offeringIds.Select(id => byId[id]).ToArray(), ct);
        return new EligibilityResult.Evaluated(academic);
    }

    public Task<EnrollmentTransitionResult> SubmitAsync(int id, CancellationToken ct) => AdvanceAsync(id, false, ct);
    public Task<EnrollmentTransitionResult> ApproveAsync(int id, CancellationToken ct) => AdvanceAsync(id, true, ct);

    private async Task<EnrollmentTransitionResult> AdvanceAsync(int id, bool approve, CancellationToken ct)
    {
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (enrollment is null) return new EnrollmentTransitionResult.NotFound();
        var expected = approve ? EnrollmentStatus.Submitted : EnrollmentStatus.Draft;
        if (enrollment.Status != expected) return new EnrollmentTransitionResult.InvalidTransition();
        // FK guarantees the offering. Infrastructure/data failures must not become academic rejections.
        var offering = await Offerings([enrollment.CourseOfferingId]).SingleAsync(ct);
        var academic = await EvaluateAsync(enrollment.StudentId, [offering], ct, enrollment.Id);
        if (!academic.IsEligible) return new EnrollmentTransitionResult.AcademicRejected(academic);
        var changed = approve ? enrollment.TryApprove() : enrollment.TrySubmit();
        if (!changed) return new EnrollmentTransitionResult.InvalidTransition();
        await db.SaveChangesAsync(ct);
        return new EnrollmentTransitionResult.Updated(Response(enrollment));
    }

    public async Task<EnrollmentTransitionResult> RejectAsync(int id, string? reason, CancellationToken ct)
    {
        var cleaned = Enrollment.CleanRejectionReason(reason);
        if (cleaned is null) return new EnrollmentTransitionResult.InvalidReason();
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (enrollment is null) return new EnrollmentTransitionResult.NotFound();
        if (!enrollment.TryReject(cleaned)) return new EnrollmentTransitionResult.InvalidTransition();
        await db.SaveChangesAsync(ct);
        return new EnrollmentTransitionResult.Updated(Response(enrollment));
    }

    private sealed record OfferingData(int Id, int CourseId, int AcademicTermId, int Credits);
    private IQueryable<OfferingData> Offerings(int[] ids) =>
        from offering in db.CourseOfferings
        join course in db.Courses on offering.CourseId equals course.Id
        where ids.Contains(offering.Id)
        select new OfferingData(offering.Id, offering.CourseId, offering.AcademicTermId, course.Credits);

    // Bounded projections, not whole tables. Evaluation order comes from the request.
    private async Task<AcademicEvaluation> EvaluateAsync(int studentId, OfferingData[] ordered, CancellationToken ct, int? excludedEnrollmentId = null)
    {
        var courseIds = ordered.Select(o => o.CourseId).Distinct().ToArray();
        var required = await db.CoursePrerequisites.Where(p => courseIds.Contains(p.CourseId))
            .Select(p => new { p.CourseId, p.PrerequisiteCourseId }).ToListAsync(ct);
        var prerequisiteIds = required.Select(p => p.PrerequisiteCourseId).Distinct().ToArray();
        var completed = await db.StudentCompletedCourses
            .Where(c => c.StudentId == studentId && prerequisiteIds.Contains(c.CourseId))
            .Select(c => c.CourseId).ToArrayAsync(ct);
        var byCourse = required.ToLookup(p => p.CourseId, p => p.PrerequisiteCourseId);
        var termIds = ordered.Select(o => o.AcademicTermId).Distinct().ToArray();
        // Exclusion is private and derives only from the tracked transition target, never HTTP input.
        var active = db.Enrollments.Where(e => e.StudentId == studentId
            && (e.Status == EnrollmentStatus.Submitted || e.Status == EnrollmentStatus.Approved));
        if (excludedEnrollmentId is int excluded) active = active.Where(e => e.Id != excluded);
        var loads = await (from enrollment in active
            join offering in db.CourseOfferings on enrollment.CourseOfferingId equals offering.Id
            join course in db.Courses on offering.CourseId equals course.Id
            where termIds.Contains(offering.AcademicTermId)
            group course by offering.AcademicTermId into term
            select new { TermId = term.Key, Credits = term.Sum(c => (long)c.Credits) })
            .ToDictionaryAsync(x => x.TermId, x => x.Credits, ct);
        var ids = ordered.Select(o => o.Id).ToArray();
        var newSlots = await db.CourseOfferingTimeSlots.Where(s => ids.Contains(s.CourseOfferingId))
            .Select(s => new { s.CourseOfferingId, s.Day, s.StartsAt, s.EndsAt }).ToListAsync(ct);
        var oldSlots = await (from enrollment in active
            join offering in db.CourseOfferings on enrollment.CourseOfferingId equals offering.Id
            join slot in db.CourseOfferingTimeSlots on offering.Id equals slot.CourseOfferingId
            where termIds.Contains(offering.AcademicTermId)
            select new { offering.Id, offering.AcademicTermId, slot.Day, slot.StartsAt, slot.EndsAt }).ToListAsync(ct);
        var slotsByOffering = newSlots.ToLookup(s => s.CourseOfferingId);
        var requested = ordered.Select(o => new OfferingEligibility(
            new OfferingSchedule(o.Id, o.AcademicTermId, slotsByOffering[o.Id].Select(s => new WeeklyTimeSlot(s.Day, s.StartsAt, s.EndsAt))),
            o.Credits, new PrerequisiteInput(byCourse[o.CourseId], completed))).ToArray();
        var existing = oldSlots.GroupBy(s => new { s.Id, s.AcademicTermId }).Select(g =>
            new OfferingSchedule(g.Key.Id, g.Key.AcademicTermId, g.Select(s => new WeeklyTimeSlot(s.Day, s.StartsAt, s.EndsAt)))).ToArray();
        return evaluator.Evaluate(requested, loads, existing, policy.MaxCreditsPerTerm);
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

    private static EnrollmentResponse Response(Enrollment e) => new(e.Id, e.StudentId, e.CourseOfferingId, e.CreatedAtUtc, e.Status.ToString(), e.RejectionReason);
}
