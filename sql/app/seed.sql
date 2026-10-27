:r sql/app/guard-app.sql
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @Courses TABLE (Id int, Code nvarchar(20), Name nvarchar(200), Credits int);
DECLARE @Terms TABLE (Id int, Name nvarchar(100), StartsOn date, EndsOn date);
DECLARE @Offerings TABLE (Id int, CourseId int, AcademicTermId int, SectionCode nvarchar(10), Capacity int);
INSERT @Courses VALUES (1,N'CSE201',N'Veri Yapıları',4),(2,N'CSE203',N'Veritabanı Yönetimi',3);
INSERT @Terms VALUES
    (1,N'2026-2027 Güz','20260921','20270115'),
    (2,N'2026-2027 Bahar','20270215','20270611');
INSERT @Offerings VALUES (1,1,1,N'A',40),(2,1,1,N'B',30),(3,1,2,N'A',35);

-- Binary comparison also detects case / trailing-space changes in existing rows.
IF EXISTS (SELECT 1 FROM @Courses e JOIN dbo.Courses a ON a.Id=e.Id
    WHERE CONVERT(varbinary(max),a.Code)<>CONVERT(varbinary(max),e.Code)
       OR CONVERT(varbinary(max),a.Name)<>CONVERT(varbinary(max),e.Name) OR a.Credits<>e.Credits)
    THROW 51000, 'Course seed conflicts with existing values; nothing will be overwritten.', 1;
IF EXISTS (SELECT 1 FROM @Terms e JOIN dbo.AcademicTerms a ON a.Id=e.Id
    WHERE CONVERT(varbinary(max),a.Name)<>CONVERT(varbinary(max),e.Name)
       OR a.StartsOn<>e.StartsOn OR a.EndsOn<>e.EndsOn)
    THROW 51000, 'Term seed conflicts with existing values; nothing will be overwritten.', 1;
IF EXISTS (SELECT 1 FROM @Offerings e JOIN dbo.CourseOfferings a ON a.Id=e.Id
    WHERE a.CourseId<>e.CourseId OR a.AcademicTermId<>e.AcademicTermId
       OR CONVERT(varbinary(max),a.SectionCode)<>CONVERT(varbinary(max),e.SectionCode)
       OR a.Capacity<>e.Capacity)
    THROW 51000, 'Offering seed conflicts with existing values; nothing will be overwritten.', 1;

INSERT dbo.Courses (Id,Code,Name,Credits)
SELECT e.Id,e.Code,e.Name,e.Credits FROM @Courses e
WHERE NOT EXISTS (SELECT 1 FROM dbo.Courses a WHERE a.Id=e.Id);
INSERT dbo.AcademicTerms (Id,Name,StartsOn,EndsOn)
SELECT e.Id,e.Name,e.StartsOn,e.EndsOn FROM @Terms e
WHERE NOT EXISTS (SELECT 1 FROM dbo.AcademicTerms a WHERE a.Id=e.Id);
INSERT dbo.CourseOfferings (Id,CourseId,AcademicTermId,SectionCode,Capacity)
SELECT e.Id,e.CourseId,e.AcademicTermId,e.SectionCode,e.Capacity FROM @Offerings e
WHERE NOT EXISTS (SELECT 1 FROM dbo.CourseOfferings a WHERE a.Id=e.Id);

COMMIT;
PRINT 'PASS: seed inserted missing rows only; existing values matched.';
