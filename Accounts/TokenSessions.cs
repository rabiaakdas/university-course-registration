using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

public sealed record TokenSessionPolicy(int AccessTokenSeconds, int RefreshSessionDays);
public sealed record RefreshSessionResponse(Guid SessionId, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? RevokedAtUtc);
public sealed record TokenPairResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken);

public sealed class TokenSessions(UniversityDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, UniversityJwtKeys keys, UniversityJwtSettings settings, TokenSessionPolicy policy)
{
    public static string NewRefreshToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static bool IsValidFormat(string? token)
    {
        if (token is null || token.Length != 43 || token.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return false;
        try { var bytes = WebEncoders.Base64UrlDecode(token); return bytes.Length == 32 && WebEncoders.Base64UrlEncode(bytes) == token; }
        catch (FormatException) { return false; }
    }
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
    private async Task<bool> Eligible(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is not null && await signIn.CanSignInAsync(user) && !await users.IsLockedOutAsync(user);
    }
    public async Task<TokenPairResponse?> CreateAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var id = users.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(id)) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await AccountWriteLock.AcquireAsync(db, id, ct) is null || await signIn.ValidateSecurityStampAsync(principal) is null || !await Eligible(id)) return null;
        var now = DateTimeOffset.UtcNow;
        var refresh = NewRefreshToken();
        var session = new RefreshSession { UserId = id, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(policy.RefreshSessionDays) };
        var result = CreatePair(session, refresh, now);
        if (result is null) return null;
        db.RefreshSessions.Add(session);
        db.RefreshTokenRecords.Add(new() { SessionId = session.Id, TokenHash = Hash(refresh), CreatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
    // After the account lock, existing-family writes acquire this transaction-owned lock.
    private Task<RefreshSession?> LockFamily(Guid id, CancellationToken ct) => db.RefreshSessions
        .FromSqlInterpolated($"SELECT * FROM [RefreshSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
        .AsNoTracking().SingleOrDefaultAsync(ct);

    public async Task<TokenPairResponse?> RefreshAsync(string token, CancellationToken ct)
    {
        if (!IsValidFormat(token)) throw new ArgumentException("Invalid refresh token format.", nameof(token));
        var hash = Hash(token);
        // Only the immutable routing ID is used before the lock; state is read again below.
        var familyId = await db.RefreshTokenRecords.Where(x => x.TokenHash == hash).Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
        if (familyId is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var userId = await db.RefreshSessions.Where(x => x.Id == familyId.Value).Select(x => x.UserId).SingleOrDefaultAsync(ct);
        if (userId is null || await AccountWriteLock.AcquireAsync(db, userId, ct) is null) return null;
        var session = await LockFamily(familyId.Value, ct);
        var now = DateTimeOffset.UtcNow;
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now) return null;
        var record = await db.RefreshTokenRecords.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == session.Id && x.TokenHash == hash, ct);
        if (record is null) return null;
        if (record.UsedAtUtc is not null)
        {
            await db.RefreshSessions.Where(x => x.Id == session.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
            await transaction.CommitAsync(ct); // Rejection must not roll back the revocation.
            return null;
        }
        if (!await Eligible(session.UserId)) return null;
        now = DateTimeOffset.UtcNow;
        var refresh = NewRefreshToken();
        var result = CreatePair(session, refresh, now);
        if (result is null) return null;
        var consumed = await db.RefreshTokenRecords.Where(x => x.Id == record.Id && x.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, now), ct);
        if (consumed != 1) throw new InvalidOperationException("Refresh consumption invariant failed.");
        var next = new RefreshTokenRecord { SessionId = session.Id, TokenHash = Hash(refresh), CreatedAtUtc = now };
        db.RefreshTokenRecords.Add(next);
        await db.SaveChangesAsync(ct);
        await db.RefreshTokenRecords.Where(x => x.Id == record.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplacedById, next.Id), ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public Task<List<RefreshSessionResponse>> ListAsync(string userId, CancellationToken ct) => db.RefreshSessions
        .Where(x => x.UserId == userId).OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
        .Select(x => new RefreshSessionResponse(x.Id, x.CreatedAtUtc, x.ExpiresAtUtc, x.RevokedAtUtc)).ToListAsync(ct);

    public async Task<bool> RevokeAsync(Guid sessionId, string userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await AccountWriteLock.AcquireAsync(db, userId, ct) is null) return false;
        var session = await LockFamily(sessionId, ct);
        if (session is null || session.UserId != userId) return false;
        if (session.RevokedAtUtc is null)
            await db.RefreshSessions.Where(x => x.Id == sessionId && x.UserId == userId && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow), ct);
        await transaction.CommitAsync(ct);
        return true;
    }
    private TokenPairResponse? CreatePair(RefreshSession session, string refresh, DateTimeOffset now)
    {
        // Whole seconds match JWT NumericDate; never round expiry past session expiry.
        var issued = now.ToUnixTimeSeconds();
        var end = Math.Min(issued + policy.AccessTokenSeconds, session.ExpiresAtUtc.ToUnixTimeSeconds());
        if (end <= issued) return null;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer, Audience = settings.Audience,
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(issued).UtcDateTime,
            NotBefore = DateTimeOffset.FromUnixTimeSeconds(issued).UtcDateTime,
            Expires = DateTimeOffset.FromUnixTimeSeconds(end).UtcDateTime,
            Claims = new Dictionary<string, object> { ["sub"] = session.UserId, ["jti"] = Guid.NewGuid().ToString("N") },
            SigningCredentials = new SigningCredentials(keys.SigningKey!, SecurityAlgorithms.RsaSha256)
        };
        return new(new JsonWebTokenHandler().CreateToken(descriptor), "Bearer", checked((int)(end - issued)), refresh);
    }
}
