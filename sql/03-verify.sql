:r sql/guard-lab.sql
DECLARE @UniqueError int=2627,
        @CourseUniqueName nvarchar(128)=N'UQ_Courses_Code',
        @OfferingUniqueName nvarchar(128)=N'UQ_CourseOfferings_Course_Term_Section';
:r sql/shared/verify-constraints.sql
