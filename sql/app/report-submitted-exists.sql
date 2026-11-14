-- Offerings having at least one waiting-for-approval enrollment; EXISTS does not multiply rows.
SELECT o.Id AS OfferingId FROM CourseOfferings o
WHERE o.AcademicTermId=@termId
AND EXISTS (SELECT 1 FROM Enrollments e WHERE e.CourseOfferingId=o.Id AND e.Status=1)
ORDER BY o.Id OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
