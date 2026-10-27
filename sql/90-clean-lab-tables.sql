-- Optional, destructive only for the three marked lab tables. Never run on user DBs.
:r sql/guard-lab.sql
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DROP TABLE dbo.CourseOfferings;
DROP TABLE dbo.AcademicTerms;
DROP TABLE dbo.Courses;
COMMIT;
-- Keep the database and marker. Re-run 01-schema.sql, then 02-seed.sql.
