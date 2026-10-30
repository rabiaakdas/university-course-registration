using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

public abstract record TermRegistrationSummaryResult
{
    private TermRegistrationSummaryResult() { }
    public sealed record Found(TermRegistrationSummaryDto Summary) : TermRegistrationSummaryResult;
    public sealed record StudentMissing : TermRegistrationSummaryResult;
    public sealed record TermMissing : TermRegistrationSummaryResult;
}

// Scoped read operation: scalar projections do not add entities to the tracker.
public sealed class TermRegistrationSummaryFacade(UniversityDbContext db)
{
    public async Task<TermRegistrationSummaryResult> GetAsync(int studentId, int termId, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId, ct)) return new TermRegistrationSummaryResult.StudentMissing();
        if (!await db.AcademicTerms.AnyAsync(t => t.Id == termId, ct)) return new TermRegistrationSummaryResult.TermMissing();

        // One row per enrollment; joining slots here would multiply credit/count totals.
        var rows = await (from e in db.Enrollments
                          join o in db.CourseOfferings on e.CourseOfferingId equals o.Id
                          join c in db.Courses on o.CourseId equals c.Id
                          where e.StudentId == studentId && o.AcademicTermId == termId
                          orderby e.Id
                          select new { e.Id, e.CourseOfferingId, c.Code, c.Name, c.Credits, e.Status, e.RejectionReason })
            .ToListAsync(ct);
        var active = rows.Where(e => e.Status is EnrollmentStatus.Submitted or EnrollmentStatus.Approved).ToArray();
        long credits = 0;
        foreach (var row in active) credits = checked(credits + (long)row.Credits);
        var activeIds = active.Select(e => e.CourseOfferingId).Distinct().Order().ToArray();
        var schedule = new List<ActiveScheduleDto>();
        if (activeIds.Length > 0)
        {
            // Separate query: no claim that all summary data is one atomic snapshot.
            var slots = await db.CourseOfferingTimeSlots.Where(s => activeIds.Contains(s.CourseOfferingId))
                .OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.StartsAt).ThenBy(s => s.EndsAt)
                .ThenBy(s => s.CourseOfferingId).ThenBy(s => s.Id)
                .Select(s => new { s.CourseOfferingId, s.Day, s.StartsAt, s.EndsAt }).ToListAsync(ct);
            schedule.AddRange(slots.Select(s => new ActiveScheduleDto(s.CourseOfferingId, s.Day.ToString(), s.StartsAt, s.EndsAt)));
        }
        var scheduledIds = schedule.Select(s => s.CourseOfferingId).ToHashSet();
        return new TermRegistrationSummaryResult.Found(new(studentId, termId, credits,
            new(rows.Count(e => e.Status == EnrollmentStatus.Draft), rows.Count(e => e.Status == EnrollmentStatus.Submitted),
                rows.Count(e => e.Status == EnrollmentStatus.Approved), rows.Count(e => e.Status == EnrollmentStatus.Rejected)),
            rows.Select(e => new TermEnrollmentDto(e.Id, e.CourseOfferingId, e.Code, e.Name, e.Credits, e.Status.ToString(), e.RejectionReason)).ToArray(),
            schedule, activeIds.Where(id => !scheduledIds.Contains(id)).ToArray()));
    }
}
