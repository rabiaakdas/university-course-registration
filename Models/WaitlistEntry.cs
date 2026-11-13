namespace UniversityRegistration.Models;

public enum WaitlistStatus { Waiting, Cancelled, Placed, Ineligible }

public sealed class WaitlistEntry
{
    public long Id { get; private set; }
    public int StudentId { get; init; }
    public int CourseOfferingId { get; init; }
    public long QueueNumber { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; init; }
    public WaitlistStatus Status { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public string? ClosureReason { get; private set; }
}
