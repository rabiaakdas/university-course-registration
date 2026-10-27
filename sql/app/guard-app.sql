:ON ERROR EXIT
IF DB_NAME() <> N'university_registration_app' AND NOT (
    DB_NAME() LIKE N'university[_]registration[_]tests[_]day[56][_]%'
    AND EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0
        AND name=N'UniversityRegistrationTests' AND CONVERT(nvarchar(30),value) IN (N'day5',N'day6')))
    THROW 51000, 'Wrong database: expected app or explicitly marked day5/day6 test database.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
    THROW 51000, 'Apply the reviewed EF migration before using app SQL scripts.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId=N'20261027110911_InitialUniversitySchema' AND ProductVersion=N'10.0.14')
    THROW 51000, 'Expected initial migration was not applied.', 1;
GO
