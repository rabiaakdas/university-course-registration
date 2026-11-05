using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

public sealed class PasswordResetOperations(UniversityDbContext db, UserManager<ApplicationUser> users)
{
    public async Task<IdentityResult> ResetAsync(string userId, string token, string password, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await AccountWriteLock.AcquireAsync(db, userId, ct);
        if (user is null || !user.EmailConfirmed) return IdentityResult.Failed(users.ErrorDescriber.InvalidToken());
        ct.ThrowIfCancellationRequested();
        // The EF Identity store saves using this context; it does not commit our outer transaction.
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded) return result;
        await db.RefreshSessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTimeOffset.UtcNow), ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
