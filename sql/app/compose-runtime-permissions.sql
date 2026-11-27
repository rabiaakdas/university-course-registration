-- Separate provisioning, not an EF migration or application startup operation.
-- The login/user are prepared by compose.ps1. No db_owner/db_ddladmin grants.
IF DATABASE_PRINCIPAL_ID(N'UniversityRuntime') IS NULL CREATE ROLE UniversityRuntime;
IF NOT EXISTS (SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(N'UniversityRuntime') AND member_principal_id=DATABASE_PRINCIPAL_ID(N'university_runtime'))
    ALTER ROLE UniversityRuntime ADD MEMBER university_runtime;
DECLARE @objects TABLE (name sysname);
INSERT @objects VALUES
(N'Courses'),(N'AcademicTerms'),(N'CourseOfferings'),(N'Students'),(N'Enrollments'),
(N'CoursePrerequisites'),(N'StudentCompletedCourses'),(N'CourseOfferingTimeSlots'),
(N'WaitlistEntries'),(N'EnrollmentCreationReceipts'),(N'RefreshSessions'),(N'RefreshTokenRecords'),
(N'AspNetUsers'),(N'AspNetRoles'),(N'AspNetUserRoles'),(N'AspNetUserClaims'),
(N'AspNetUserLogins'),(N'AspNetUserTokens'),(N'AspNetRoleClaims');
DECLARE @name sysname, @sql nvarchar(max);
DECLARE objects_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM @objects;
OPEN objects_cursor;
FETCH NEXT FROM objects_cursor INTO @name;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @sql=N'GRANT SELECT,INSERT,UPDATE ON OBJECT::dbo.'+QUOTENAME(@name)+N' TO UniversityRuntime;';
    EXEC sp_executesql @sql;
    -- Identity stores remove claims/logins/role memberships; academic lifecycle uses UPDATE.
    IF @name LIKE N'AspNet%'
    BEGIN
        SET @sql=N'GRANT DELETE ON OBJECT::dbo.'+QUOTENAME(@name)+N' TO UniversityRuntime;';
        EXEC sp_executesql @sql;
    END;
    FETCH NEXT FROM objects_cursor INTO @name;
END;
CLOSE objects_cursor; DEALLOCATE objects_cursor;
GRANT SELECT ON OBJECT::dbo.OfferingCapacityReport TO UniversityRuntime;
-- NEXT VALUE FOR requires UPDATE; UPDLOCK/HOLDLOCK use the DML grants above.
GRANT UPDATE ON OBJECT::dbo.CourseIds TO UniversityRuntime;
GRANT UPDATE ON OBJECT::dbo.AcademicTermIds TO UniversityRuntime;
GRANT UPDATE ON OBJECT::dbo.CourseOfferingIds TO UniversityRuntime;
GRANT UPDATE ON OBJECT::dbo.StudentIds TO UniversityRuntime;
GRANT UPDATE ON OBJECT::dbo.EnrollmentIds TO UniversityRuntime;
GRANT UPDATE ON OBJECT::dbo.CourseOfferingTimeSlotIds TO UniversityRuntime;
GRANT UPDATE ON OBJECT::dbo.WaitlistQueueNumbers TO UniversityRuntime;
