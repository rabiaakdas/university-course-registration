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
        var query = Filter(request);
        // Sequential queries, not a guaranteed snapshot during concurrent writes.
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Id).Skip(request.Offset).Take(request.PageSize)
            .Select(c => new CourseListItemDto(c.Id, c.Code, c.Name)).ToListAsync(ct);
        // Pure scalar projection does not materialize or track entities.
        return new(items, request.Page, request.PageSize, totalCount);
    }
    IQueryable<Course> Filter(CourseCatalogQuery request)
    {
        IQueryable<Course> query=db.Courses;
        if(!string.IsNullOrEmpty(request.Search))query=query.Where(c=>c.Code.Contains(request.Search)||c.Name.Contains(request.Search));
        if(request.Credits.HasValue)query=query.Where(c=>c.Credits==request.Credits.Value);
        return query;
    }
    public async Task<CourseKeysetResponse> ReadKeysetAsync(CourseKeysetQuery request,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query=Filter(request.Filter);
        if(request.AfterId.HasValue)query=query.Where(c=>c.Id>request.AfterId.Value);
        var rows=await query.OrderBy(c=>c.Id).Take(request.Filter.PageSize+1)
            .Select(c=>new CourseListItemDto(c.Id,c.Code,c.Name)).ToListAsync(ct);
        bool more=rows.Count>request.Filter.PageSize;
        if(more)rows.RemoveAt(rows.Count-1);
        return new(rows,more,more?rows[^1].Id:null);
    }
}
