namespace UniversityRegistration.Models;

public sealed class RefreshTokenRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public byte[] TokenHash { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? UsedAtUtc { get; set; }
    public Guid? ReplacedById { get; set; }
}
