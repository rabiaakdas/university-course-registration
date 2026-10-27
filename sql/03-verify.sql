:r sql/guard-lab.sql
SET NOCOUNT ON;
SET XACT_ABORT OFF;
-- Each experiment uses its own transaction and always rolls it back.
-- NOT NULL in SQL Server is column metadata, so test error 515 plus column name.
IF (SELECT COUNT(*) FROM dbo.Courses) <> 2
 OR (SELECT COUNT(*) FROM dbo.AcademicTerms) <> 2
 OR (SELECT COUNT(*) FROM dbo.CourseOfferings) <> 3
    THROW 51000, 'Unexpected seed row counts; inspect lab contents.', 1;
IF EXISTS (
    SELECT Id,Code,Name COLLATE Latin1_General_100_BIN2,Credits FROM dbo.Courses
    EXCEPT SELECT * FROM (VALUES
      (1,N'CSE201',N'Veri Yapıları',4),(2,N'CSE203',N'Veritabanı Yönetimi',3)
    ) expected(Id,Code,Name,Credits))
    THROW 51000, 'Course seed / Turkish text mismatch.', 1;
IF EXISTS (
    SELECT Id,Name COLLATE Latin1_General_100_BIN2,StartsOn,EndsOn FROM dbo.AcademicTerms
    EXCEPT SELECT * FROM (VALUES
      (1,N'2026-2027 Güz',CAST('20260921' AS date),CAST('20270115' AS date)),
      (2,N'2026-2027 Bahar',CAST('20270215' AS date),CAST('20270611' AS date))
    ) expected(Id,Name,StartsOn,EndsOn))
    THROW 51000, 'Term seed / Turkish text mismatch.', 1;
IF EXISTS (
    SELECT Id,CourseId,AcademicTermId,SectionCode,Capacity FROM dbo.CourseOfferings
    EXCEPT SELECT * FROM (VALUES (1,1,1,N'A',40),(2,1,1,N'B',30),(3,1,2,N'A',35))
    expected(Id,CourseId,AcademicTermId,SectionCode,Capacity))
    THROW 51000, 'Offering seed mismatch.', 1;
IF EXISTS (SELECT 1 FROM sys.columns
    WHERE object_id=OBJECT_ID(N'dbo.CourseOfferings') AND name=N'Capacity'
      AND (default_object_id<>0 OR is_nullable=1))
    THROW 51000, 'Capacity must be required without a default.', 1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.CourseOfferings')
    AND delete_referential_action=0 AND update_referential_action=0
    AND is_disabled=0 AND is_not_trusted=0) <> 2
    THROW 51000, 'Expected two enabled trusted NO ACTION foreign keys.', 1;
PRINT 'PASS 1: exact seed, Turkish text, capacity metadata, NO ACTION metadata';

SELECT * INTO #CoursesBefore FROM dbo.Courses;
SELECT * INTO #TermsBefore FROM dbo.AcademicTerms;
SELECT * INTO #OfferingsBefore FROM dbo.CourseOfferings;
DECLARE @Tests TABLE (Id int PRIMARY KEY, Label nvarchar(100), Statement nvarchar(max),
                      ExpectedError int, ExpectedDetail nvarchar(128));
INSERT @Tests VALUES
(2,N'duplicate PK',N'INSERT dbo.Courses VALUES (1,N''NEW201'',N''Test'',3)',2627,N'PK_Courses'),
(3,N'duplicate course code',N'INSERT dbo.Courses VALUES (90,N''CSE201'',N''Test'',3)',2627,N'UQ_Courses_Code'),
(4,N'missing course',N'INSERT dbo.CourseOfferings VALUES (90,999,1,N''C'',10)',547,N'FK_CourseOfferings_Courses'),
(5,N'missing term',N'INSERT dbo.CourseOfferings VALUES (90,1,999,N''C'',10)',547,N'FK_CourseOfferings_AcademicTerms'),
(6,N'duplicate course term section',N'INSERT dbo.CourseOfferings VALUES (90,1,1,N''A'',10)',2627,N'UQ_CourseOfferings_Course_Term_Section'),
(7,N'different section accepted',N'INSERT dbo.CourseOfferings VALUES (90,1,1,N''C'',10)',0,N''),
(8,N'different term accepted',N'INSERT dbo.CourseOfferings VALUES (90,1,2,N''B'',10)',0,N''),
(9,N'negative capacity',N'INSERT dbo.CourseOfferings VALUES (90,1,1,N''C'',-1)',547,N'CK_CourseOfferings_Capacity_Positive'),
(10,N'zero capacity',N'INSERT dbo.CourseOfferings VALUES (90,1,1,N''C'',0)',547,N'CK_CourseOfferings_Capacity_Positive'),
(11,N'null capacity',N'INSERT dbo.CourseOfferings VALUES (90,1,1,N''C'',NULL)',515,N'Capacity'),
(12,N'omitted capacity',N'INSERT dbo.CourseOfferings (Id,CourseId,AcademicTermId,SectionCode) VALUES (90,1,1,N''C'')',515,N'Capacity'),
(13,N'zero credits',N'INSERT dbo.Courses VALUES (90,N''NEW201'',N''Test'',0)',547,N'CK_Courses_Credits_Positive'),
(14,N'negative credits',N'INSERT dbo.Courses VALUES (90,N''NEW201'',N''Test'',-1)',547,N'CK_Courses_Credits_Positive'),
(15,N'null course name',N'INSERT dbo.Courses VALUES (90,N''NEW201'',NULL,3)',515,N'Name'),
(16,N'invalid date range',N'INSERT dbo.AcademicTerms VALUES (90,N''Test'',''20270102'',''20270101'')',547,N'CK_AcademicTerms_DateRange'),
(17,N'delete referenced course',N'DELETE dbo.Courses WHERE Id=1',547,N'FK_CourseOfferings_Courses'),
(18,N'delete referenced term',N'DELETE dbo.AcademicTerms WHERE Id=1',547,N'FK_CourseOfferings_AcademicTerms'),
(19,N'course without offerings',N'INSERT dbo.Courses VALUES (90,N''NEW201'',N''Açılışsız Ders'',3)',0,N''),
(20,N'one update reflected in all offerings',N'UPDATE dbo.Courses SET Name=N''Güncellenmiş Ders'' WHERE Id=1;
IF (SELECT COUNT(*) FROM dbo.CourseOfferings o JOIN dbo.Courses c ON c.Id=o.CourseId WHERE c.Name=N''Güncellenmiş Ders'') <> 3
    THROW 51000, ''JOIN did not reflect one-row update in all three offerings.'', 1;',0,N''),
(21,N'case insensitive code under existing CI collation',N'INSERT dbo.Courses VALUES (90,N''cse201'',N''Test'',3)',2627,N'UQ_Courses_Code'),
(22,N'parent identity update does not cascade',N'UPDATE dbo.Courses SET Id=90 WHERE Id=1',547,N'FK_CourseOfferings_Courses'),
(23,N'parent term identity update does not cascade',N'UPDATE dbo.AcademicTerms SET Id=90 WHERE Id=1',547,N'FK_CourseOfferings_AcademicTerms');

DECLARE @Id int=2, @Label nvarchar(100), @Statement nvarchar(max),
        @ExpectedError int, @ExpectedDetail nvarchar(128),
        @ActualError int, @ActualMessage nvarchar(4000), @Failure nvarchar(2048);
WHILE @Id <= 23
BEGIN
    SELECT @Label=Label, @Statement=Statement, @ExpectedError=ExpectedError,
           @ExpectedDetail=ExpectedDetail FROM @Tests WHERE Id=@Id;
    SELECT @ActualError=0, @ActualMessage=N'';
    BEGIN TRANSACTION;
    BEGIN TRY
        EXEC sys.sp_executesql @Statement;
    END TRY
    BEGIN CATCH
        SELECT @ActualError=ERROR_NUMBER(), @ActualMessage=ERROR_MESSAGE();
    END CATCH;

    -- Inspect BEFORE rollback as well: a rejected delete must preserve its rows.
    IF @Id IN (17,18) AND (
        NOT EXISTS (SELECT 1 FROM dbo.Courses WHERE Id=1)
        OR NOT EXISTS (SELECT 1 FROM dbo.AcademicTerms WHERE Id=1)
        OR (SELECT COUNT(*) FROM dbo.CourseOfferings)<>3)
    BEGIN
        IF XACT_STATE()<>0 ROLLBACK;
        THROW 51000, 'Rejected delete did not preserve parent and child rows.', 1;
    END;
    IF XACT_STATE()<>0 ROLLBACK;

    IF @ActualError<>@ExpectedError
       OR (@ExpectedError<>0 AND CHARINDEX(@ExpectedDetail,@ActualMessage)=0)
    BEGIN
        SET @Failure=CONCAT('FAIL ',@Id,' ',@Label,': expected ',@ExpectedError,
            ' / ',@ExpectedDetail,'; actual ',@ActualError,' / ',@ActualMessage);
        THROW 51000, @Failure, 1;
    END;
    PRINT CONCAT('PASS ',@Id,': ',@Label,' (error ',@ActualError,')');
    SET @Id+=1;
END;

IF EXISTS (SELECT * FROM dbo.Courses EXCEPT SELECT * FROM #CoursesBefore)
 OR EXISTS (SELECT * FROM #CoursesBefore EXCEPT SELECT * FROM dbo.Courses)
 OR EXISTS (SELECT * FROM dbo.AcademicTerms EXCEPT SELECT * FROM #TermsBefore)
 OR EXISTS (SELECT * FROM #TermsBefore EXCEPT SELECT * FROM dbo.AcademicTerms)
 OR EXISTS (SELECT * FROM dbo.CourseOfferings EXCEPT SELECT * FROM #OfferingsBefore)
 OR EXISTS (SELECT * FROM #OfferingsBefore EXCEPT SELECT * FROM dbo.CourseOfferings)
    THROW 51000, 'Experiments changed the baseline seed.', 1;
DROP TABLE #CoursesBefore, #TermsBefore, #OfferingsBefore;
PRINT 'PASS: all experiments rolled back; parent/child seed rows preserved.';
