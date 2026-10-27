:ON ERROR EXIT
USE master;
GO
IF DB_ID(N'university_registration_lab') IS NOT NULL
    THROW 51000, 'Lab already exists. Inspect ownership and contents; do not overwrite.', 1;
GO
CREATE DATABASE university_registration_lab;
GO
USE university_registration_lab;
GO
EXEC sys.sp_addextendedproperty
    @name = N'UniversityRegistrationLab', @value = N'day3-v1';
SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(40)) AS ServerVersion,
       DB_NAME() AS LabDatabase,
       CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128)) AS Collation;
