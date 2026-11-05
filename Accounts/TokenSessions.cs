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
    public async Task<TokenPairResponse?> CreateAsync(string id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!await Eligible(id)) return null;
        var now = DateTimeOffset.UtcNow;
        var refresh = NewRefreshToken();
        var session = new RefreshSession { UserId = id, CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(policy.RefreshSessionDays), RefreshTokenHash = Hash(refresh) };
        var result = CreatePair(session, refresh, now);
        if (result is null) return null;
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return result;
    }
    public async Task<TokenPairResponse?> RefreshAsync(string token, CancellationToken ct)
    {
        if (!IsValidFormat(token)) throw new ArgumentException("Invalid refresh token format.", nameof(token));
        var hash = Hash(token);
        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= DateTimeOffset.UtcNow) return null;
        if (!await Eligible(session.UserId)) return null;
        var refresh = NewRefreshToken();
        var result = CreatePair(session, refresh, DateTimeOffset.UtcNow);
        if (result is null) return null;
        session.RefreshTokenHash = Hash(refresh);
        await db.SaveChangesAsync(ct); // One update; no history/reuse/concurrency guarantee.
        return result;
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
