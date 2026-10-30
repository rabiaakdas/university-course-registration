namespace UniversityRegistration.Contracts;

public sealed record RegistrationStatusCounts(int Draft, int Submitted, int Approved, int Rejected);
public sealed record TermEnrollmentDto(int Id, int CourseOfferingId, string CourseCode,
    string CourseName, int Credits, string Status, string? RejectionReason);
public sealed record ActiveScheduleDto(int CourseOfferingId, string Day, TimeOnly StartsAt, TimeOnly EndsAt);
public sealed record TermRegistrationSummaryDto(int StudentId, int AcademicTermId, long ActiveCredits,
    RegistrationStatusCounts Counts, IReadOnlyList<TermEnrollmentDto> Enrollments,
    IReadOnlyList<ActiveScheduleDto> ActiveSchedule, IReadOnlyList<int> UnscheduledActiveOfferingIds);
