using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;

namespace UniversityRegistration.Application;

// Caller owns transaction; offering before children for every seat/waitlist writer.
internal static class OfferingCoordination
{
    internal static Task<int?> LockAsync(UniversityDbContext db, int id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Offering lock requires a transaction.");
        return db.Database.SqlQuery<int?>($"SELECT [Id] AS [Value] FROM [dbo].[CourseOfferings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync(ct);
    }
}
