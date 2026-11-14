-- @studentId int, @termId int. Zero load stays zero; cast BEFORE SUM.
SELECT COALESCE(SUM(CAST(c.Credits AS bigint)),CAST(0 AS bigint)) AS ActiveCredits
FROM Enrollments e JOIN CourseOfferings o ON o.Id=e.CourseOfferingId
JOIN Courses c ON c.Id=o.CourseId
WHERE e.StudentId=@studentId AND o.AcademicTermId=@termId AND e.Status IN (1,2);
