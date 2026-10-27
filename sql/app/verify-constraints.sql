:r sql/app/guard-app.sql
-- The legacy destructive experiments are rollback-only but require a fresh seed.
-- Day 5 runs them only in an isolated, explicitly marked test database.
IF DB_NAME() NOT LIKE N'university[_]registration[_]tests[_]day5[_]%'
    THROW 51000, 'Constraint experiments require an isolated day5 test database.', 1;
DECLARE @UniqueError int=2601,
        @CourseUniqueName nvarchar(128)=N'UX_Courses_Code',
        @OfferingUniqueName nvarchar(128)=N'UX_CourseOfferings_Course_Term_Section';
:r sql/shared/verify-constraints.sql
