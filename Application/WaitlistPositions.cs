using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

public sealed record WaitingPosition(long Id, int CourseOfferingId, long QueueNumber,
    DateTimeOffset JoinedAtUtc, long Position, long TotalWaiting);

// Trusted internal read. HTTP ownership is enforced before this query is called.
public static class WaitlistPositions
{
    public static Dictionary<string, string[]> Validate(int studentId, int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        if (studentId <= 0) errors["studentId"] = ["Pozitif öğrenci kimliği gereklidir."];
        if (page < 1 || ((long)page - 1) * pageSize > int.MaxValue)
            errors["page"] = ["Geçerli bir sayfa gereklidir."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["Sayfa boyutu 1..100 olmalıdır."];
        return errors;
    }

    public static Task<List<WaitingPosition>> ReadAsync(UniversityDbContext db, int studentId,
        int page, int pageSize, CancellationToken ct)
    {
        if (Validate(studentId, page, pageSize).Count != 0)
            throw new ArgumentOutOfRangeException(nameof(page), "Invalid student or pagination.");
        var offset = checked((page - 1) * pageSize);
        // SqlQuery binds values as parameters. The EXISTS limits whole partitions, not
        // the rows within a partition. Student filtering and paging MUST stay outside.
        return db.Database.SqlQuery<WaitingPosition>($"""
            SELECT r.Id, r.CourseOfferingId, r.QueueNumber, r.JoinedAtUtc,
                   r.Position, r.TotalWaiting
            FROM (
                SELECT w.Id, w.StudentId, w.CourseOfferingId, w.QueueNumber, w.JoinedAtUtc,
                       ROW_NUMBER() OVER(PARTITION BY w.CourseOfferingId ORDER BY w.QueueNumber) AS Position,
                       COUNT_BIG(*) OVER(PARTITION BY w.CourseOfferingId) AS TotalWaiting
                FROM dbo.WaitlistEntries AS w
                WHERE w.Status = {(int)WaitlistStatus.Waiting}
                  AND EXISTS (
                      SELECT 1 FROM dbo.WaitlistEntries AS own
                      WHERE own.StudentId = {studentId}
                        AND own.Status = {(int)WaitlistStatus.Waiting}
                        AND own.CourseOfferingId = w.CourseOfferingId
                  )
            ) AS r
            WHERE r.StudentId = {studentId}
            ORDER BY r.CourseOfferingId, r.QueueNumber
            OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY
            """).ToListAsync(ct);
    }
}
