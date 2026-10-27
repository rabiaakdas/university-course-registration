using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Endpoints;

public static class CourseCatalogEndpoint
{
    public static async Task<IResult> GetAsync(UniversityDbContext db, CancellationToken ct,
        string? search = null, int? credits = null, int page = 1, int pageSize = 20)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["page"] = ["Sayfa en az 1 olmalıdır."];
        if (pageSize < 1 || pageSize > 100) errors["pageSize"] = ["Sayfa boyutu 1 ile 100 arasında olmalıdır."];
        if (credits is <= 0) errors["credits"] = ["Kredi pozitif olmalıdır."];
        var offset = ((long)page - 1) * pageSize;
        if (page >= 1 && pageSize is >= 1 and <= 100 && offset > int.MaxValue)
            errors["page"] = ["İstenen sayfanın başlangıcı desteklenen aralığı aşıyor."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        IQueryable<Course> query = db.Courses;
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term)) query = query.Where(c => c.Code.Contains(term) || c.Name.Contains(term));
        if (credits.HasValue) query = query.Where(c => c.Credits == credits.Value);

        // Two sequential SQL queries; not a guaranteed snapshot during concurrent writes.
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Id).Skip((int)offset).Take(pageSize)
            .Select(c => new CourseListItemDto(c.Id, c.Code, c.Name)).ToListAsync(ct);
        // Pure scalar DTO projection does not materialize or track Course entities.
        return Results.Ok(new CourseCatalogResponse(items, page, pageSize, totalCount));
    }
}
