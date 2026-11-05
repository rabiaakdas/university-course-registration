namespace UniversityRegistration.Models;

public sealed class RefreshSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = "";
    public byte[] RefreshTokenHash { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
