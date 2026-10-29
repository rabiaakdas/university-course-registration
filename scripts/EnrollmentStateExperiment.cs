using UniversityRegistration.Models;

// Learning only, excluded from the web assembly. The transition gate is private.
internal sealed class EnrollmentStateExperiment
{
    private State state = new Draft();
    public EnrollmentStatus Status => state.Status;
    public string? RejectionReason => state.Reason;
    public bool TrySubmit() => Move(state.Submit());
    public bool TryApprove() => Move(state.Approve());
    public bool TryReject(string? reason)
    {
        var cleaned = Enrollment.CleanRejectionReason(reason);
        return cleaned is not null && Move(state.Reject(cleaned));
    }
    private bool Move(State? next)
    { if(next is null) return false; state=next; return true; }

    private abstract class State
    {
        public abstract EnrollmentStatus Status { get; }
        public virtual string? Reason => null;
        public virtual State? Submit() => null;
        public virtual State? Approve() => null;
        public virtual State? Reject(string reason) => null;
    }
    private sealed class Draft : State
    {
        public override EnrollmentStatus Status => EnrollmentStatus.Draft;
        public override State Submit() => new Submitted();
    }
    private sealed class Submitted : State
    {
        public override EnrollmentStatus Status => EnrollmentStatus.Submitted;
        public override State Approve() => new Approved();
        public override State Reject(string reason) => new Rejected(reason);
    }
    private sealed class Approved : State
    { public override EnrollmentStatus Status => EnrollmentStatus.Approved; }
    private sealed class Rejected(string reason) : State
    {
        public override EnrollmentStatus Status => EnrollmentStatus.Rejected;
        public override string Reason => reason;
    }
}
