-- Same result, aggregate subquery; no time-slot join.
SELECT o.Id AS OfferingId, c.Code AS CourseCode, o.Capacity,
       COALESCE(a.ActiveCount,0) AS ActiveCount, o.OccupiedSeats,
       100.0 * COALESCE(a.ActiveCount,0) / o.Capacity AS OccupancyPercent
FROM CourseOfferings o
JOIN Courses c ON c.Id = o.CourseId
LEFT JOIN (SELECT CourseOfferingId, COUNT(*) AS ActiveCount FROM Enrollments
           WHERE Status IN (1,2) GROUP BY CourseOfferingId) a ON a.CourseOfferingId=o.Id
WHERE o.AcademicTermId=@termId
ORDER BY o.Id OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
