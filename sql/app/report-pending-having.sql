-- @minimumCount int >= 1. HAVING filters groups, WHERE filters input enrollments.
-- NULL AdvisorUserId is the unassigned group, not an account named "unassigned".
SELECT s.AdvisorUserId, COUNT(*) AS SubmittedCount
FROM Enrollments e JOIN CourseOfferings o ON o.Id=e.CourseOfferingId
JOIN Students s ON s.Id=e.StudentId
WHERE o.AcademicTermId=@termId AND e.Status=1
GROUP BY s.AdvisorUserId
HAVING COUNT(*) >= @minimumCount
ORDER BY s.AdvisorUserId OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
