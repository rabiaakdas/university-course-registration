using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class BatchEnrollmentEndpoint
{
    public static async Task<IResult> CreateAsync(int id, CreateBatchEnrollmentRequest request, UniversityDbContext db, CancellationToken ct)
    {
        var ids = request.CourseOfferingIds;
        var messages = new List<string>();
        if (ids is null || ids.Length is < 1 or > 50) messages.Add("1 ile 50 arasında açılış seçilmelidir.");
        if (ids is not null)
        {
            if (ids.Any(value => value <= 0)) messages.Add("Açılış kimlikleri pozitif olmalıdır.");
            if (ids.Distinct().Count() != ids.Length) messages.Add("Aynı açılış birden fazla seçilemez.");
        }
        if (messages.Count > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseOfferingIds"] = messages.ToArray() });
        if (!await db.Students.AnyAsync(s => s.Id == id, ct)) return Results.Problem(statusCode: 404, title: "Öğrenci bulunamadı.");
        var found = await db.CourseOfferings.Where(o => ids!.Contains(o.Id)).Select(o => o.Id).ToListAsync(ct);
        if (found.Count != ids!.Length) return Results.ValidationProblem(new Dictionary<string, string[]> { ["courseOfferingIds"] = ["Seçilen açılışlardan biri bulunamadı."] });
        if (await db.Enrollments.AnyAsync(e => e.StudentId == id && ids.Contains(e.CourseOfferingId), ct)) return Conflict();

        var now = DateTimeOffset.UtcNow;
        var enrollments = ids.Select(offeringId => new Enrollment { StudentId = id, CourseOfferingId = offeringId, CreatedAtUtc = now }).ToArray();
        db.Enrollments.AddRange(enrollments);
        // One SaveChanges gives one atomic write boundary; no outer transaction is needed.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (RegistrationWriteErrors.Classify(ex) is "duplicate" or "studentId" or "courseOfferingId")
        {
            var error = RegistrationWriteErrors.Classify(ex)!;
            return error == "duplicate" ? Conflict() : Results.ValidationProblem(new Dictionary<string, string[]>
                { [error] = [error == "studentId" ? "Öğrenci bulunamadı." : "Ders açılışı bulunamadı."] });
        }
        var items = enrollments.Select(e => new EnrollmentResponse(e.Id, e.StudentId, e.CourseOfferingId, e.CreatedAtUtc)).ToArray();
        return Results.Json(new BatchEnrollmentResponse(items), statusCode: StatusCodes.Status201Created);
    }

    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Seçilen açılışlardan birine zaten kayıt var; toplu kayıt oluşturulmadı.");
}
