-- Parameters: @termId int, @offset int, @pageSize int. One row per offering.
SELECT o.Id AS OfferingId, c.Code AS CourseCode, o.Capacity,
       COUNT(e.Id) AS ActiveCount, o.OccupiedSeats,
       100.0 * COUNT(e.Id) / o.Capacity AS OccupancyPercent
FROM CourseOfferings o
JOIN Courses c ON c.Id = o.CourseId
LEFT JOIN Enrollments e ON e.CourseOfferingId = o.Id AND e.Status IN (1,2)
WHERE o.AcademicTermId = @termId
GROUP BY o.Id, c.Code, o.Capacity, o.OccupiedSeats
ORDER BY o.Id OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
