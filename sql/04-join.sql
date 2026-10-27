:r sql/guard-lab.sql
SELECT o.Id AS OfferingId, c.Code AS CourseCode, c.Name AS CourseName,
       t.Name AS TermName, o.SectionCode, o.Capacity
FROM dbo.CourseOfferings AS o
JOIN dbo.Courses AS c ON c.Id = o.CourseId
JOIN dbo.AcademicTerms AS t ON t.Id = o.AcademicTermId
ORDER BY o.Id;
