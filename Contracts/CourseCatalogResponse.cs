namespace UniversityRegistration.Contracts;

public record CourseListItemDto(int Id, string Code, string Name);
public record CourseCatalogResponse(IReadOnlyList<CourseListItemDto> Items, int Page, int PageSize, int TotalCount);
public record CourseKeysetResponse(IReadOnlyList<CourseListItemDto> Items, bool HasMore, int? NextAfterId);
