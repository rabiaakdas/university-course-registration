:ON ERROR EXIT
-- OPTIONAL ROLLBACK-ONLY DEMO. Refuses app/lab and unmarked databases.
IF DB_NAME() NOT LIKE N'university[_]registration[_]tests[_]day6[_]%'
 OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'UniversityRegistrationTests' AND CONVERT(nvarchar(30),value)=N'day6')
    THROW 51000,'Use only an owned, marked day6 test database.',1;
IF OBJECT_ID(N'dbo.CoursePrerequisites') IS NULL OR OBJECT_ID(N'dbo.StudentCompletedCourses') IS NULL
    THROW 51000,'Apply academic migration first.',1;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @tag nvarchar(16)=N'D14_'+LEFT(REPLACE(CONVERT(nvarchar(36),NEWID()),N'-',N''),12);
    IF EXISTS(SELECT 1 FROM dbo.Courses WHERE Code IN (@tag+N'A',@tag+N'B')) OR EXISTS(SELECT 1 FROM dbo.Students WHERE StudentNumber=@tag)
        THROW 51000,'Example names already exist; no overwrite.',1;
    DECLARE @courses TABLE(Id int,Code nvarchar(20)); DECLARE @students TABLE(Id int);
    INSERT dbo.Courses(Code,Name,Credits) OUTPUT inserted.Id,inserted.Code INTO @courses VALUES(@tag+N'A',N'Örnek temel ders',4),(@tag+N'B',N'Örnek ileri ders',4);
    INSERT dbo.Students(StudentNumber,FullName) OUTPUT inserted.Id INTO @students VALUES(@tag,N'Yalnız deney öğrencisi');
    DECLARE @base int=(SELECT Id FROM @courses WHERE Code=@tag+N'A'),@target int=(SELECT Id FROM @courses WHERE Code=@tag+N'B'),@student int=(SELECT Id FROM @students);
    IF @base IS NULL OR @target IS NULL OR @student IS NULL OR @base=@target THROW 51000,'Invalid captured fixture identities.',1;
    INSERT dbo.CoursePrerequisites(CourseId,PrerequisiteCourseId) VALUES(@target,@base);
    INSERT dbo.StudentCompletedCourses(StudentId,CourseId) VALUES(@student,@base);
    IF NOT EXISTS(SELECT 1 FROM dbo.CoursePrerequisites p JOIN dbo.StudentCompletedCourses c ON c.CourseId=p.PrerequisiteCourseId WHERE p.CourseId=@target AND c.StudentId=@student)
        THROW 51000,'Example relation did not match.',1;
    ROLLBACK;
    PRINT 'PASS: optional academic demo created only synthetic rows then rolled back; sequence gaps are expected.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
