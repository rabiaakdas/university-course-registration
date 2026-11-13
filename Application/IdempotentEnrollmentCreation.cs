using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

public sealed class IdempotentEnrollmentCreation(UniversityDbContext db, EnrollmentOperations operations,
    DbContextOptions<UniversityDbContext> options)
{
    public sealed record Result(EnrollmentCreationReceipt? Receipt = null, SingleEnrollmentResult? Rejection = null, string? Conflict = null);
    const string Operation = "enrollment.create.v1";
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result> CreateAsync(string accountId, Guid key, CreateEnrollmentCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountId) || key == Guid.Empty) throw new ArgumentException("Account and key required.");
        if (db.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
            throw new InvalidOperationException("Creation receipt requires its own transaction.");
        // Validated integer fields and operation version, not raw JSON or client headers.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{Operation}\n{command.StudentId}\n{command.CourseOfferingId}")));
        Result Replay(EnrollmentCreationReceipt receipt)
        {
            if (receipt.ExpiresAtUtc <= DateTimeOffset.UtcNow) return new(Conflict: "idempotency_key_expired");
            if (!receipt.RequestHash.AsSpan().SequenceEqual(hash)) return new(Conflict: "idempotency_key_reused");
            if (receipt.StatusCode != 201 || receipt.EnrollmentId is null || receipt.ResponseJson is null || receipt.Location is null)
                throw new InvalidOperationException("Incomplete committed creation receipt.");
            return new(Receipt: receipt);
        }
        var existing = await db.EnrollmentCreationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.Operation == Operation && x.Key == key, ct);
        if (existing is not null) return Replay(existing);
        var now = DateTimeOffset.UtcNow;
        var reservation = new EnrollmentCreationReceipt { AccountId = accountId, Operation = Operation, Key = key,
            RequestHash = hash, CreatedAtUtc = now, ExpiresAtUtc = now.AddHours(24) };
        var collision = false;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            db.EnrollmentCreationReceipts.Add(reservation);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (RegistrationWriteErrors.Classify(error) == "idempotencyKey")
            {
                await tx.RollbackAsync(ct);
                collision = true;
            }
            if (!collision)
            {
                var outcome = await operations.CreateAsync(command, ct);
                if (outcome is not SingleEnrollmentResult.Created created)
                {
                    await tx.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                    return new(Rejection: outcome);
                }
                reservation.EnrollmentId = created.Enrollment.Id;
                reservation.StatusCode = 201;
                reservation.Location = $"/enrollments/{created.Enrollment.Id}";
                reservation.ResponseJson = JsonSerializer.Serialize(created.Enrollment, Json);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(Receipt: reservation);
            }
        }
        // Never query a failed transaction/tracker. SQL UNIQUE waits for the competing commit.
        db.ChangeTracker.Clear();
        await using var fresh = new UniversityDbContext(options);
        var committed = await fresh.EnrollmentCreationReceipts.AsNoTracking()
            .SingleAsync(x => x.AccountId == accountId && x.Operation == Operation && x.Key == key, ct);
        return Replay(committed);
    }
}
