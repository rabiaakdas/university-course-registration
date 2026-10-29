-- Optional synthetic demonstration; only a marked, isolated test DB. Always rollback.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'university[_]registration[_]tests[_]day6[_]%'
    OR NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'UniversityRegistrationTests' AND CONVERT(nvarchar(20),value)=N'day6')
    THROW 51000,'Time example requires an owned, marked day6 test database; app/lab are forbidden.',1;
IF OBJECT_ID(N'dbo.CourseOfferingTimeSlots') IS NULL THROW 51000,'Apply time slot migration first.',1;
DECLARE @Code nvarchar(20)=N'T15_'+LEFT(REPLACE(CONVERT(nvarchar(36),NEWID()),N'-',N''),12);
IF EXISTS(SELECT 1 FROM dbo.Courses WHERE Code=@Code) THROW 51000,'Example code already exists.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Courses TABLE(Id int); DECLARE @Terms TABLE(Id int); DECLARE @Offers TABLE(Id int);
    INSERT dbo.Courses(Code,Name,Credits) OUTPUT inserted.Id INTO @Courses VALUES(@Code,N'Sentetik saat örneği',2);
    INSERT dbo.AcademicTerms(Name,StartsOn,EndsOn) OUTPUT inserted.Id INTO @Terms VALUES(@Code,'20260101','20260601');
    DECLARE @Course int=(SELECT Id FROM @Courses), @Term int=(SELECT Id FROM @Terms);
    IF @Course IS NULL OR @Term IS NULL THROW 51000,'Missing generated example IDs.',1;
    INSERT dbo.CourseOfferings(CourseId,AcademicTermId,SectionCode,Capacity) OUTPUT inserted.Id INTO @Offers VALUES(@Course,@Term,N'A',40);
    DECLARE @Offering int=(SELECT Id FROM @Offers);
    IF @Offering IS NULL THROW 51000,'Missing example offering ID.',1;
    -- Monday=1; non-overlapping, same-day, university local weekly times.
    INSERT dbo.CourseOfferingTimeSlots(CourseOfferingId,Day,StartsAt,EndsAt)
        VALUES(@Offering,1,'09:00:00.0000001','10:00:00.0000001'),(@Offering,3,'09:00','10:00');
    IF (SELECT COUNT(*) FROM dbo.CourseOfferingTimeSlots WHERE CourseOfferingId=@Offering)<>2
        THROW 51000,'Example time slots missing.',1;
    ROLLBACK TRANSACTION;
    PRINT 'PASS: synthetic time slot example rolled back; no existing offering changed; sequence values are not rewound.';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
