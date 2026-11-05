using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Accounts;

// All reset/session mutations use account -> family -> token order, inside their caller's transaction.
internal static class AccountWriteLock
{
    public static async Task<ApplicationUser?> AcquireAsync(UniversityDbContext db, string userId, CancellationToken ct)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
            .SingleOrDefaultAsync(ct);
        // Authentication may already have tracked this account. Re-read under the acquired lock.
        if (user is not null) await db.Entry(user).ReloadAsync(ct);
        return user;
    }
}
