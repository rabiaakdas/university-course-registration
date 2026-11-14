using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

public sealed record OfferingOccupancy(int OfferingId, string CourseCode, int Capacity,
    int ActiveCount, int OccupiedSeats, decimal OccupancyPercent);
public sealed record AdvisorPending(string? AdvisorUserId, int SubmittedCount);
public sealed record ReportPage<T>(int Page, int PageSize, IReadOnlyList<T> Items);

// Each method executes one bounded DTO query. No schedule join, entity tracking or writes.
public sealed class RegistrationReports(UniversityDbContext db)
{
    public static Dictionary<string, string[]> Validate(int termId, int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (termId <= 0) errors["termId"] = ["Dönem kimliği pozitif olmalıdır."];
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue)
            errors["page"] = ["Sayfa geçerli bir pozitif offset üretmelidir."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Sayfa boyutu 1..100 olmalıdır."];
        return errors;
    }
    static void Guard(int termId, int page, int pageSize)
    {
        if (Validate(termId, page, pageSize).Count != 0) throw new ArgumentOutOfRangeException(nameof(page));
    }
    public async Task<ReportPage<OfferingOccupancy>> OccupancyAsync(int termId, int page, int pageSize, CancellationToken ct)
    {
        Guard(termId, page, pageSize);
        var active = db.Enrollments.Where(e => e.Status == EnrollmentStatus.Submitted || e.Status == EnrollmentStatus.Approved)
            .GroupBy(e => e.CourseOfferingId).Select(g => new { OfferingId = g.Key, Count = g.Count() });
        var rows = await (from o in db.CourseOfferings
                          join c in db.Courses on o.CourseId equals c.Id
                          join a in active on o.Id equals a.OfferingId into counts
                          from a in counts.DefaultIfEmpty()
                          where o.AcademicTermId == termId
                          orderby o.Id
                          select new OfferingOccupancy(o.Id, c.Code, o.Capacity, (int?)a.Count ?? 0,
                              o.OccupiedSeats, 100m * ((int?)a.Count ?? 0) / o.Capacity))
            .Skip(checked((page - 1) * pageSize)).Take(pageSize).ToListAsync(ct);
        return new(page, pageSize, rows);
    }
    public async Task<ReportPage<AdvisorPending>> PendingAsync(int termId, int page, int pageSize, CancellationToken ct)
    {
        Guard(termId, page, pageSize);
        var rows = await (from e in db.Enrollments
                          join o in db.CourseOfferings on e.CourseOfferingId equals o.Id
                          join s in db.Students on e.StudentId equals s.Id
                          where o.AcademicTermId == termId && e.Status == EnrollmentStatus.Submitted
                          group e by s.AdvisorUserId into g
                          orderby g.Key
                          select new AdvisorPending(g.Key, g.Count()))
            .Skip(checked((page - 1) * pageSize)).Take(pageSize).ToListAsync(ct);
        return new(page, pageSize, rows);
    }
}
