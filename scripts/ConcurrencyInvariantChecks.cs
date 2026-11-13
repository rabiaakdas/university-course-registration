using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

// Test-only, scoped to owned offering IDs, after every worker has settled.
internal static class ConcurrencyInvariantChecks
{
    public static async Task Verify(Func<UniversityDbContext> factory, int[] offeringIds, string step)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var db = factory();
        var offerings = db.CourseOfferings.Where(o => offeringIds.Contains(o.Id));
        if (offeringIds.Length == 0 || await offerings.CountAsync(stop.Token) != offeringIds.Distinct().Count())
            throw new BehaviorFailure("Invariant " + step + ": fixture offering missing");
        if (await offerings.AnyAsync(o => o.OccupiedSeats < 0 || o.OccupiedSeats > o.Capacity ||
            o.OccupiedSeats != db.Enrollments.Count(e => e.CourseOfferingId == o.Id &&
                (e.Status == EnrollmentStatus.Submitted || e.Status == EnrollmentStatus.Approved)), stop.Token))
            throw new BehaviorFailure("Invariant " + step + ": occupied range/active count");
        if (await db.Enrollments.Where(e => offeringIds.Contains(e.CourseOfferingId))
            .GroupBy(e => new { e.StudentId, e.CourseOfferingId }).AnyAsync(g => g.Count() > 1, stop.Token))
            throw new BehaviorFailure("Invariant " + step + ": duplicate enrollment");
        if (await db.WaitlistEntries.Where(w => offeringIds.Contains(w.CourseOfferingId) && w.Status == WaitlistStatus.Waiting)
            .GroupBy(w => new { w.StudentId, w.CourseOfferingId }).AnyAsync(g => g.Count() > 1, stop.Token))
            throw new BehaviorFailure("Invariant " + step + ": duplicate Waiting");
        // Placed is historical. Later Approved/Rejected/Dropped is legal; never require permanently Submitted.
        if (await db.WaitlistEntries.Where(w => offeringIds.Contains(w.CourseOfferingId) && w.Status == WaitlistStatus.Placed)
            .AnyAsync(w => !db.Enrollments.Any(e => e.StudentId == w.StudentId && e.CourseOfferingId == w.CourseOfferingId), stop.Token))
            throw new BehaviorFailure("Invariant " + step + ": placement without enrollment");
        Console.WriteLine("PASS invariant: " + step + "; range/count/enrollment unique/Waiting unique/placement reference");
    }

    public static async Task Run(Func<UniversityDbContext> factory, CatalogTestIdentity identity, Action stop, Func<Task> start)
    {
        Console.WriteLine("SCENARIO: seat transitions / controlled SQL connections");
        await SeatCapacityChecks.Run(factory);
        Console.WriteLine("SCENARIO: idempotent create / HTTPS plus UNIQUE claim race");
        await IdempotencyChecks.Run(factory, identity, stop, start);
        Console.WriteLine("SCENARIO: drop and placement / SQL plus authenticated HTTPS");
        await DropPlacementChecks.Run(factory, identity);
    }
}
