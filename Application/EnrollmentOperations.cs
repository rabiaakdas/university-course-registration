using UniversityRegistration.Specifications;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

// Scoped with its DbContext. Callers validate request shape before invoking these operations.
public sealed class EnrollmentOperations(UniversityDbContext db, EnrollmentPolicy policy,
    IAcademicEligibilityEvaluator evaluator, EnrollmentApprovedPublisher approvalPublisher)
{
    public async Task<SingleEnrollmentResult> CreateAsync(CreateEnrollmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var studentId = command.StudentId;
        var offeringId = command.CourseOfferingId;
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

    // All three transitions own their transaction; no publication inside an outer transaction.
    private void RequireOwnTransaction()
    {
        if (db.Database.CurrentTransaction is not null || db.Database.GetEnlistedTransaction() is not null
            || Transaction.Current is not null)
            throw new InvalidOperationException("Enrollment transitions require an independently committed transaction.");
    }

    private async Task<EnrollmentTransitionResult> AdvanceAsync(int id, bool approve, CancellationToken ct)
    {
        RequireOwnTransaction();
        var reference = await db.Enrollments.AsNoTracking().Where(e => e.Id == id)
            .Select(e => (int?)e.CourseOfferingId).SingleOrDefaultAsync(ct);
        if (reference is null) return new EnrollmentTransitionResult.NotFound();
        Enrollment enrollment;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await OfferingCoordination.LockAsync(db, reference.Value, ct);
            enrollment = await db.Enrollments.AsNoTracking().SingleAsync(e => e.Id == id, ct);
            var expected = approve ? EnrollmentStatus.Submitted : EnrollmentStatus.Draft;
            if (enrollment.Status != expected) return new EnrollmentTransitionResult.InvalidTransition();
            if (!approve && await db.WaitlistEntries.AnyAsync(w => w.CourseOfferingId == reference && w.Status == WaitlistStatus.Waiting, ct))
                return new EnrollmentTransitionResult.QueuePriority();
            var offering = await Offerings([reference.Value]).SingleAsync(ct);
            var academic = await EvaluateAsync(enrollment.StudentId, [offering], ct, enrollment.Id);
            if (!academic.IsEligible) return new EnrollmentTransitionResult.AcademicRejected(academic);
            if (!(approve ? enrollment.TryApprove() : enrollment.TrySubmit())) return new EnrollmentTransitionResult.InvalidTransition();
            if (!approve)
            {
                var reserved = await db.CourseOfferings.Where(o => o.Id == reference && o.OccupiedSeats < o.Capacity)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.OccupiedSeats, o => o.OccupiedSeats + 1), ct);
                if (reserved != 1) return new EnrollmentTransitionResult.CapacityUnavailable();
            }
            var changed = await db.Enrollments.Where(e => e.Id == id && e.Status == expected)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, enrollment.Status).SetProperty(e => e.RejectionReason, (string?)null), ct);
            if (changed != 1) throw new InvalidOperationException("Coordinated transition lost.");
            await transaction.CommitAsync(ct);
        }
        if (approve)
            await approvalPublisher.PublishAsync(new EnrollmentApproved(Guid.NewGuid(), enrollment.Id,
                enrollment.StudentId, enrollment.CourseOfferingId, DateTimeOffset.UtcNow), ct);
        return new EnrollmentTransitionResult.Updated(Response(enrollment));
    }

    public Task<EnrollmentTransitionResult> DropAsync(int id, CancellationToken ct) => ReleaseAsync(id, true, null, ct);
    public Task<EnrollmentTransitionResult> RejectAsync(int id, string? reason, CancellationToken ct) => ReleaseAsync(id, false, reason, ct);

    private async Task<EnrollmentTransitionResult> ReleaseAsync(int id, bool drop, string? reason, CancellationToken ct)
    {
        var cleaned = drop ? null : Enrollment.CleanRejectionReason(reason);
        if (!drop && cleaned is null) return new EnrollmentTransitionResult.InvalidReason();
        RequireOwnTransaction();
        var reference = await db.Enrollments.AsNoTracking().Where(e => e.Id == id)
            .Select(e => (int?)e.CourseOfferingId).SingleOrDefaultAsync(ct);
        if (reference is null) return new EnrollmentTransitionResult.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await OfferingCoordination.LockAsync(db, reference.Value, ct);
        var enrollment = await db.Enrollments.AsNoTracking().SingleAsync(e => e.Id == id, ct);
        if (drop && enrollment.Status == EnrollmentStatus.Dropped) return new EnrollmentTransitionResult.Updated(Response(enrollment));
        var expected = enrollment.Status;
        if (!(drop ? enrollment.TryDrop() : enrollment.TryReject(cleaned))) return new EnrollmentTransitionResult.InvalidTransition();
        var changed = await db.Enrollments.Where(e => e.Id == id && e.Status == expected)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, enrollment.Status).SetProperty(e => e.RejectionReason, enrollment.RejectionReason), ct);
        if (changed != 1) throw new InvalidOperationException("Coordinated release lost.");
        var released = await db.CourseOfferings.Where(o => o.Id == reference && o.OccupiedSeats > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OccupiedSeats, o => o.OccupiedSeats - 1), ct);
        if (released != 1) throw new InvalidOperationException("Seat invariant violated.");
        await PlaceWaitingAsync(reference.Value, ct);
        await transaction.CommitAsync(ct);
        return new EnrollmentTransitionResult.Updated(Response(enrollment));
    }

    // Private: caller holds offering lock and owns commit. No public bypass or nested commit.
    private async Task PlaceWaitingAsync(int offeringId, CancellationToken ct)
    {
        var offering = await Offerings([offeringId]).SingleAsync(ct);
        long after = 0;
        while (await db.CourseOfferings.AnyAsync(o => o.Id == offeringId && o.OccupiedSeats < o.Capacity, ct))
        {
            var page = await db.WaitlistEntries.AsNoTracking().Where(w => w.CourseOfferingId == offeringId
                && w.Status == WaitlistStatus.Waiting && w.QueueNumber > after).OrderBy(w => w.QueueNumber)
                .Take(32).ToArrayAsync(ct);
            if (page.Length == 0) return;
            var students = page.Select(w => w.StudentId).ToArray();
            var existing = await db.Enrollments.AsNoTracking().Where(e => e.CourseOfferingId == offeringId && students.Contains(e.StudentId))
                .ToDictionaryAsync(e => e.StudentId, ct);
            var evaluations = await EvaluateCandidatesAsync(students, offering, ct);
            foreach (var candidate in page)
            {
                after = candidate.QueueNumber;
                existing.TryGetValue(candidate.StudentId, out var enrollment);
                string? reason = enrollment?.Status switch
                {
                    EnrollmentStatus.Submitted or EnrollmentStatus.Approved => "already_active",
                    EnrollmentStatus.Rejected or EnrollmentStatus.Dropped => "terminal_enrollment",
                    _ => null
                };
                if (reason is null && !evaluations[candidate.StudentId].IsEligible)
                    reason = "academic:" + string.Join(",", evaluations[candidate.StudentId].Failures.Select(f => f.Code).Distinct());
                var now = DateTimeOffset.UtcNow;
                if (reason is not null)
                {
                    if (await db.WaitlistEntries.Where(w => w.Id == candidate.Id && w.Status == WaitlistStatus.Waiting)
                        .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WaitlistStatus.Ineligible)
                            .SetProperty(w => w.ClosedAtUtc, now).SetProperty(w => w.ClosureReason, reason), ct) != 1) throw new InvalidOperationException("Ineligible decision lost.");
                    continue;
                }
                if (enrollment is null)
                {
                    enrollment = new Enrollment { StudentId = candidate.StudentId, CourseOfferingId = offeringId, CreatedAtUtc = now };
                    if (!enrollment.TrySubmit()) throw new InvalidOperationException("New placement transition failed.");
                    db.Enrollments.Add(enrollment);
                    await db.SaveChangesAsync(ct);
                    db.Entry(enrollment).State = EntityState.Detached;
                }
                else
                {
                    if (!enrollment.TrySubmit()) throw new InvalidOperationException("Draft placement transition failed.");
                    if (await db.Enrollments.Where(e => e.Id == enrollment.Id && e.Status == EnrollmentStatus.Draft)
                        .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EnrollmentStatus.Submitted), ct) != 1)
                        throw new InvalidOperationException("Draft placement lost.");
                }
                if (await db.CourseOfferings.Where(o => o.Id == offeringId && o.OccupiedSeats < o.Capacity)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.OccupiedSeats, o => o.OccupiedSeats + 1), ct) != 1)
                    throw new InvalidOperationException("Placement seat lost.");
                if (await db.WaitlistEntries.Where(w => w.Id == candidate.Id && w.Status == WaitlistStatus.Waiting)
                    .ExecuteUpdateAsync(s => s.SetProperty(w => w.Status, WaitlistStatus.Placed)
                        .SetProperty(w => w.ClosedAtUtc, now).SetProperty(w => w.ClosureReason, "placed"), ct) != 1)
                    throw new InvalidOperationException("Placement entry lost.");
                if (!await db.CourseOfferings.AnyAsync(o => o.Id == offeringId && o.OccupiedSeats < o.Capacity, ct)) return;
            }
        }
    }

    // One bounded page's prerequisites, completed courses, active credits and schedules in bulk.
    private async Task<Dictionary<int, AcademicEvaluation>> EvaluateCandidatesAsync(int[] students, OfferingData offering, CancellationToken ct)
    {
        var required = await db.CoursePrerequisites.Where(p => p.CourseId == offering.CourseId).Select(p => p.PrerequisiteCourseId).ToArrayAsync(ct);
        var completed = await db.StudentCompletedCourses.Where(c => students.Contains(c.StudentId) && required.Contains(c.CourseId))
            .Select(c => new { c.StudentId, c.CourseId }).ToArrayAsync(ct);
        var active = db.Enrollments.Where(e => students.Contains(e.StudentId) && (e.Status == EnrollmentStatus.Submitted || e.Status == EnrollmentStatus.Approved));
        var loads = await (from e in active join o in db.CourseOfferings on e.CourseOfferingId equals o.Id
            join c in db.Courses on o.CourseId equals c.Id where o.AcademicTermId == offering.AcademicTermId
            group c by e.StudentId into g select new { StudentId = g.Key, Credits = g.Sum(c => (long)c.Credits) }).ToArrayAsync(ct);
        var slots = await db.CourseOfferingTimeSlots.Where(s => s.CourseOfferingId == offering.Id).AsNoTracking().ToArrayAsync(ct);
        var old = await (from e in active join o in db.CourseOfferings on e.CourseOfferingId equals o.Id
            join s in db.CourseOfferingTimeSlots on o.Id equals s.CourseOfferingId where o.AcademicTermId == offering.AcademicTermId
            select new { e.StudentId, o.Id, s.Day, s.StartsAt, s.EndsAt }).ToArrayAsync(ct);
        return students.ToDictionary(id => id, id => evaluator.Evaluate(
            [new OfferingEligibility(new OfferingSchedule(offering.Id, offering.AcademicTermId,
                slots.Select(s => new WeeklyTimeSlot(s.Day,s.StartsAt,s.EndsAt))), offering.Credits,
                new PrerequisiteInput(required, completed.Where(c => c.StudentId == id).Select(c => c.CourseId)))],
            new Dictionary<int,long> { [offering.AcademicTermId] = loads.SingleOrDefault(l => l.StudentId == id)?.Credits ?? 0 },
            old.Where(s => s.StudentId == id).GroupBy(s => s.Id).Select(g => new OfferingSchedule(g.Key, offering.AcademicTermId,
                g.Select(s => new WeeklyTimeSlot(s.Day,s.StartsAt,s.EndsAt)))).ToArray(), policy.MaxCreditsPerTerm));
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
        // Exclusion is private and derives only from the persisted transition target, never HTTP input.
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
