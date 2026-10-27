namespace UniversityRegistration.Contracts;

public record CreateCourseRequest(string? Code, string? Name, int Credits);
public record UpdateCourseRequest(string? Code, string? Name, int Credits);
