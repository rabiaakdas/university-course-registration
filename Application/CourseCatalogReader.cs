using Microsoft.EntityFrameworkCore;
using UniversityRegistration.Contracts;
using UniversityRegistration.Data;
using UniversityRegistration.Models;

namespace UniversityRegistration.Application;

// Scoped read operation; no HTTP, write, repository or mediator dependency.
public sealed class CourseCatalogReader(UniversityDbContext db)
{
    public async Task<CourseCatalogResponse> ReadAsync(CourseCatalogQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        IQueryable<Course> query = db.Courses;
        var term = request.Search;
        if (!string.IsNullOrEmpty(term)) query = query.Where(c => c.Code.Contains(term) || c.Name.Contains(term));
        if (request.Credits.HasValue) query = query.Where(c => c.Credits == request.Credits.Value);
        // Sequential queries, not a guaranteed snapshot during concurrent writes.
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Id).Skip(request.Offset).Take(request.PageSize)
            .Select(c => new CourseListItemDto(c.Id, c.Code, c.Name)).ToListAsync(ct);
        // Pure scalar projection does not materialize or track entities.
        return new(items, request.Page, request.PageSize, totalCount);
    }
}
