:r sql/guard-lab.sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT dbo.Courses (Id, Code, Name, Credits) VALUES
    (1, N'CSE201', N'Veri Yapıları', 4),
    (2, N'CSE203', N'Veritabanı Yönetimi', 3);
INSERT dbo.AcademicTerms (Id, Name, StartsOn, EndsOn) VALUES
    (1, N'2026-2027 Güz', '20260921', '20270115'),
    (2, N'2026-2027 Bahar', '20270215', '20270611');
INSERT dbo.CourseOfferings (Id, CourseId, AcademicTermId, SectionCode, Capacity) VALUES
    (1, 1, 1, N'A', 40),
    (2, 1, 1, N'B', 30),
    (3, 1, 2, N'A', 35);

COMMIT;
