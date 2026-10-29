:r sql/app/guard-app.sql
SET NOCOUNT ON;
DECLARE @Columns TABLE (TableName sysname, ColumnName sysname, TypeName sysname, ByteLength int);
INSERT @Columns VALUES
    (N'Courses',N'Id',N'int',4),(N'Courses',N'Code',N'nvarchar',40),
    (N'Courses',N'Name',N'nvarchar',400),(N'Courses',N'Credits',N'int',4),
    (N'AcademicTerms',N'Id',N'int',4),(N'AcademicTerms',N'Name',N'nvarchar',200),
    (N'AcademicTerms',N'StartsOn',N'date',3),(N'AcademicTerms',N'EndsOn',N'date',3),
    (N'CourseOfferings',N'Id',N'int',4),(N'CourseOfferings',N'CourseId',N'int',4),
    (N'CourseOfferings',N'AcademicTermId',N'int',4),
    (N'CourseOfferings',N'SectionCode',N'nvarchar',20),(N'CourseOfferings',N'Capacity',N'int',4);
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>9
    THROW 51000, 'Expected eight application tables and migration history.', 1;
IF EXISTS (SELECT 1 FROM @Columns e
    LEFT JOIN sys.tables t ON t.name=e.TableName AND SCHEMA_NAME(t.schema_id)=N'dbo'
    LEFT JOIN sys.columns c ON c.object_id=t.object_id AND c.name=e.ColumnName
    WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.TypeName
       OR c.max_length<>e.ByteLength OR c.is_nullable<>0 OR c.is_identity<>0
       OR (c.default_object_id<>0 AND e.ColumnName<>N'Id'))
    THROW 51000, 'Column type/length/required/identity/default mapping mismatch.', 1;
IF (SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
    WHERE t.name IN (N'Courses',N'AcademicTerms',N'CourseOfferings'))<>13
    THROW 51000, 'Unexpected model columns.', 1;
IF (SELECT COUNT(*) FROM sys.key_constraints
    WHERE name IN (N'PK_Courses',N'PK_AcademicTerms',N'PK_CourseOfferings') AND type='PK')<>3
    THROW 51000, 'Expected three named primary keys.', 1;
IF (SELECT COUNT(*) FROM sys.indexes
    WHERE name IN (N'UX_Courses_Code',N'UX_CourseOfferings_Course_Term_Section')
      AND is_unique=1 AND is_unique_constraint=0 AND has_filter=0 AND is_disabled=0)<>2
    THROW 51000, 'Expected two unfiltered unique indexes.', 1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=0 AND is_not_trusted=0
    AND name IN (N'CK_Courses_Credits_Positive',N'CK_AcademicTerms_DateRange',N'CK_CourseOfferings_Capacity_Positive'))<>3
    THROW 51000, 'Expected three enabled trusted CHECK constraints.', 1;
SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Courses')
    AND name=N'DF_Courses_Id' AND definition=N'(NEXT VALUE FOR [dbo].[CourseIds])')
    THROW 51000, 'Expected Course sequence default.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE object_id=OBJECT_ID(N'dbo.CourseIds')
    AND TYPE_NAME(user_type_id)=N'int' AND CONVERT(bigint,increment)=1 AND is_cycling=0)
    THROW 51000, 'Expected non-cycling int sequence.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.AcademicTerms')
    AND name=N'DF_AcademicTerms_Id' AND definition=N'(NEXT VALUE FOR [dbo].[AcademicTermIds])')
    THROW 51000, 'Expected AcademicTerms sequence default.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE object_id=OBJECT_ID(N'dbo.AcademicTermIds')
    AND TYPE_NAME(user_type_id)=N'int' AND CONVERT(bigint,increment)=1 AND is_cycling=0)
    THROW 51000, 'Expected AcademicTermIds int sequence.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.CourseOfferings')
    AND name=N'DF_CourseOfferings_Id' AND definition=N'(NEXT VALUE FOR [dbo].[CourseOfferingIds])')
    THROW 51000, 'Expected CourseOfferings sequence default.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE object_id=OBJECT_ID(N'dbo.CourseOfferingIds')
    AND TYPE_NAME(user_type_id)=N'int' AND CONVERT(bigint,increment)=1 AND is_cycling=0)
    THROW 51000, 'Expected CourseOfferingIds int sequence.', 1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN (N'FK_CourseOfferings_Courses',N'FK_CourseOfferings_AcademicTerms')
    AND delete_referential_action=0 AND update_referential_action=0 AND is_disabled=0 AND is_not_trusted=0)<>2
    THROW 51000, 'Expected two trusted NO ACTION foreign keys.', 1;
PRINT 'PASS: schema, three sequence defaults, NO ACTION foreign keys, migration history.';
:r sql/app/verify-enrollment-schema.sql
:r sql/app/verify-academic-schema.sql
:r sql/app/verify-time-slots.sql
:r sql/app/verify-enrollment-lifecycle.sql
