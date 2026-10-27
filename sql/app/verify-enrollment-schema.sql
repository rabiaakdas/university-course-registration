-- Read-only metadata assertions; also included by verify.sql.
:r sql/app/guard-app.sql
DECLARE @NewColumns TABLE (TableName sysname, ColumnName sysname, TypeName sysname, ByteLength int);
INSERT @NewColumns VALUES
    (N'Students',N'Id',N'int',4),(N'Students',N'StudentNumber',N'nvarchar',60),(N'Students',N'FullName',N'nvarchar',400),
    (N'Enrollments',N'Id',N'int',4),(N'Enrollments',N'StudentId',N'int',4),
    (N'Enrollments',N'CourseOfferingId',N'int',4),(N'Enrollments',N'CreatedAtUtc',N'datetimeoffset',10);
IF EXISTS (SELECT 1 FROM @NewColumns e
    LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+e.TableName) AND c.name=e.ColumnName
    WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.TypeName OR c.max_length<>e.ByteLength
        OR c.is_nullable<>0 OR c.is_identity<>0 OR (e.ColumnName<>N'Id' AND c.default_object_id<>0))
    THROW 51000, 'Student/enrollment column mapping mismatch.', 1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id IN (OBJECT_ID(N'dbo.Students'),OBJECT_ID(N'dbo.Enrollments')))<>7
    THROW 51000, 'Unexpected student/enrollment columns.', 1;
IF (SELECT COUNT(*) FROM sys.key_constraints WHERE type='PK' AND name IN (N'PK_Students',N'PK_Enrollments'))<>2
    THROW 51000, 'Missing primary keys.', 1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN (N'FK_Enrollments_Students',N'FK_Enrollments_CourseOfferings')
    AND delete_referential_action=0 AND update_referential_action=0 AND is_disabled=0 AND is_not_trusted=0)<>2
    THROW 51000, 'Expected enrollment NO ACTION foreign keys.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE constraint_object_id=OBJECT_ID(N'dbo.FK_Enrollments_Students')
    AND parent_object_id=OBJECT_ID(N'dbo.Enrollments') AND COL_NAME(parent_object_id,parent_column_id)=N'StudentId'
    AND referenced_object_id=OBJECT_ID(N'dbo.Students') AND COL_NAME(referenced_object_id,referenced_column_id)=N'Id')
    THROW 51000, 'Student foreign key columns mismatch.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns WHERE constraint_object_id=OBJECT_ID(N'dbo.FK_Enrollments_CourseOfferings')
    AND parent_object_id=OBJECT_ID(N'dbo.Enrollments') AND COL_NAME(parent_object_id,parent_column_id)=N'CourseOfferingId'
    AND referenced_object_id=OBJECT_ID(N'dbo.CourseOfferings') AND COL_NAME(referenced_object_id,referenced_column_id)=N'Id')
    THROW 51000, 'Offering foreign key columns mismatch.', 1;
IF (SELECT COUNT(*) FROM sys.indexes WHERE name IN (N'UX_Students_StudentNumber',N'UX_Enrollments_Student_Offering')
    AND is_unique=1 AND is_disabled=0 AND has_filter=0)<>2
    THROW 51000, 'Expected unique indexes.', 1;
IF (SELECT STRING_AGG(c.name,',') WITHIN GROUP (ORDER BY ic.key_ordinal)
    FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE i.object_id=OBJECT_ID(N'dbo.Enrollments') AND i.name=N'UX_Enrollments_Student_Offering' AND ic.key_ordinal>0)<>N'StudentId,CourseOfferingId'
    THROW 51000, 'Enrollment unique key columns mismatch.', 1;
IF EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    WHERE i.object_id=OBJECT_ID(N'dbo.Students') AND i.is_unique=1 AND COL_NAME(ic.object_id,ic.column_id)=N'FullName')
    THROW 51000, 'FullName must not be unique.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Students')
    AND name=N'DF_Students_Id' AND definition=N'(NEXT VALUE FOR [dbo].[StudentIds])')
 OR NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Enrollments')
    AND name=N'DF_Enrollments_Id' AND definition=N'(NEXT VALUE FOR [dbo].[EnrollmentIds])')
    THROW 51000, 'Expected sequence defaults.', 1;
IF (SELECT COUNT(*) FROM sys.sequences WHERE name IN (N'StudentIds',N'EnrollmentIds') AND SCHEMA_NAME(schema_id)=N'dbo'
    AND TYPE_NAME(user_type_id)=N'int' AND CONVERT(bigint,increment)=1 AND is_cycling=0)<>2
    THROW 51000, 'Expected two int sequences.', 1;
PRINT 'PASS: student/enrollment types, required fields, PK/FK/unique, NO ACTION and sequences.';
