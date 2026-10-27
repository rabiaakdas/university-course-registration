namespace UniversityRegistration.Contracts;

public record CourseListItemDto(int Id, string Code, string Name);
public record CourseCatalogResponse(IReadOnlyList<CourseListItemDto> Items, int Page, int PageSize, int TotalCount);
