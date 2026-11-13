namespace UniversityRegistration.Models;

// A reservation is visible only inside its transaction; only completed successes commit.
public sealed class EnrollmentCreationReceipt
{
    public long Id { get; private set; }
    public string AccountId { get; set; } = "";
    public string Operation { get; set; } = "enrollment.create.v1";
    public Guid Key { get; set; }
    public byte[] RequestHash { get; set; } = [];
    public int? EnrollmentId { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseJson { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
