namespace UniversityRegistration.Contracts;

public record CreateBatchEnrollmentRequest(int[]? CourseOfferingIds);
public record BatchEnrollmentResponse(IReadOnlyList<EnrollmentResponse> Items);
