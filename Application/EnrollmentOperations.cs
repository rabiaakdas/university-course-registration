using UniversityRegistration.Specifications;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

// Scoped with its DbContext. Callers validate request shape before invoking these operations.
public sealed class EnrollmentOperations(UniversityDbContext db, EnrollmentPolicy policy)
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
        if (academic is not null) return new SingleEnrollmentResult.AcademicRejected(academic);
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
        var found = await Offerings(offeringIds).ToListAsync(ct);
        if (found.Count != offeringIds.Length)
            return new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.OfferingsMissing);
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && offeringIds.Contains(e.CourseOfferingId), ct))
            return new BatchEnrollmentResult.Rejected(BatchEnrollmentFailure.Duplicate);
        var byId = found.ToDictionary(o => o.Id);
        var academic = await EvaluateAsync(studentId, offeringIds.Select(id => byId[id]).ToArray(), ct);
        if (academic is not null) return new BatchEnrollmentResult.AcademicRejected(academic);
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

    private sealed record OfferingData(int Id, int CourseId, int AcademicTermId, int Credits);
    private IQueryable<OfferingData> Offerings(int[] ids) =>
        from offering in db.CourseOfferings
        join course in db.Courses on offering.CourseId equals course.Id
        where ids.Contains(offering.Id)
        select new OfferingData(offering.Id, offering.CourseId, offering.AcademicTermId, course.Credits);

    // Bounded projections, not whole tables. Evaluation order comes from the request.
    private async Task<AcademicRejection?> EvaluateAsync(int studentId, OfferingData[] ordered, CancellationToken ct)
    {
        var courseIds = ordered.Select(o => o.CourseId).Distinct().ToArray();
        var required = await db.CoursePrerequisites.Where(p => courseIds.Contains(p.CourseId))
            .Select(p => new { p.CourseId, p.PrerequisiteCourseId }).ToListAsync(ct);
        var prerequisiteIds = required.Select(p => p.PrerequisiteCourseId).Distinct().ToArray();
        var completed = await db.StudentCompletedCourses
            .Where(c => c.StudentId == studentId && prerequisiteIds.Contains(c.CourseId))
            .Select(c => c.CourseId).ToArrayAsync(ct);
        var byCourse = required.ToLookup(p => p.CourseId, p => p.PrerequisiteCourseId);
        var prerequisites = new PrerequisitesSatisfiedSpecification();
        foreach (var offering in ordered)
            if (!prerequisites.IsSatisfiedBy(new PrerequisiteInput(byCourse[offering.CourseId], completed)))
                return new AcademicRejection.PrerequisiteNotSatisfied(offering.Id);

        var termIds = ordered.Select(o => o.AcademicTermId).Distinct().ToArray();
        var loads = await (from enrollment in db.Enrollments
            join offering in db.CourseOfferings on enrollment.CourseOfferingId equals offering.Id
            join course in db.Courses on offering.CourseId equals course.Id
            where enrollment.StudentId == studentId && termIds.Contains(offering.AcademicTermId)
            group course by offering.AcademicTermId into term
            select new { TermId = term.Key, Credits = term.Sum(c => (long)c.Credits) })
            .ToDictionaryAsync(x => x.TermId, x => x.Credits, ct);
        var creditLimit = new CreditLimitSpecification();
        // LINQ-to-Objects GroupBy preserves first-seen term order in this ordered input.
        foreach (var term in ordered.GroupBy(o => o.AcademicTermId))
        {
            var requested = term.Sum(o => (long)o.Credits); // At most 50 positive Int32 values.
            if (!creditLimit.IsSatisfiedBy(new CreditLoad(loads.GetValueOrDefault(term.Key), requested, policy.MaxCreditsPerTerm)))
                return new AcademicRejection.CreditLimitExceeded(term.Key);
        }
        return null;
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
