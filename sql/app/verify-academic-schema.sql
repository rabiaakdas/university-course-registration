-- Read-only checks for the two new relation tables.
:r sql/app/guard-app.sql
DECLARE @Columns TABLE(TableName sysname, ColumnName sysname);
INSERT @Columns VALUES (N'CoursePrerequisites',N'CourseId'),(N'CoursePrerequisites',N'PrerequisiteCourseId'),
(N'StudentCompletedCourses',N'StudentId'),(N'StudentCompletedCourses',N'CourseId');
IF EXISTS(SELECT 1 FROM @Columns e LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+e.TableName) AND c.name=e.ColumnName
    WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id)<>N'int' OR c.is_nullable<>0 OR c.is_identity<>0 OR c.default_object_id<>0)
    THROW 51000,'Academic relation column mismatch.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id IN (OBJECT_ID(N'dbo.CoursePrerequisites'),OBJECT_ID(N'dbo.StudentCompletedCourses')))<>4
    THROW 51000,'Unexpected academic relation columns.',1;
DECLARE @Keys TABLE(TableName sysname, ColumnName sysname, Ordinal int);
INSERT @Keys VALUES (N'CoursePrerequisites',N'CourseId',1),(N'CoursePrerequisites',N'PrerequisiteCourseId',2),
(N'StudentCompletedCourses',N'StudentId',1),(N'StudentCompletedCourses',N'CourseId',2);
IF EXISTS(SELECT 1 FROM @Keys e WHERE NOT EXISTS(SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE i.object_id=OBJECT_ID(N'dbo.'+e.TableName) AND i.is_primary_key=1 AND i.name=N'PK_'+e.TableName AND c.name=e.ColumnName AND ic.key_ordinal=e.Ordinal))
    THROW 51000,'Composite primary key mismatch.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN (N'FK_CoursePrerequisites_Courses',N'FK_CoursePrerequisites_PrerequisiteCourses',
N'FK_StudentCompletedCourses_Students',N'FK_StudentCompletedCourses_Courses') AND delete_referential_action=0 AND update_referential_action=0 AND is_disabled=0 AND is_not_trusted=0)<>4
    THROW 51000,'Expected four trusted NO ACTION FKs.',1;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_CoursePrerequisites_NotSelf' AND is_disabled=0 AND is_not_trusted=0)
    THROW 51000,'Missing self prerequisite CHECK.',1;
PRINT 'PASS: academic relations required int/composite PK/four NO ACTION FKs/self CHECK.';
