namespace UniversityRegistration.Contracts;

public record CreateStudentRequest(string? StudentNumber, string? FullName);
public record CreateEnrollmentRequest(int StudentId, int CourseOfferingId);
public record StudentResponse(int Id, string StudentNumber, string FullName);
public record RejectEnrollmentRequest(string? Reason);
public record EnrollmentResponse(int Id, int StudentId, int CourseOfferingId, DateTimeOffset CreatedAtUtc,
    string Status, string? RejectionReason);
public record StudentEnrollmentDto(int Id, int CourseOfferingId, string CourseCode,
    string CourseName, string TermName, string SectionCode, DateTimeOffset CreatedAtUtc, string Status, string? RejectionReason);
