:ON ERROR EXIT
IF DB_NAME() <> N'university_registration_app'
    THROW 51000, 'Wrong database: only university_registration_app is allowed.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL
    THROW 51000, 'Apply the reviewed EF migration before using app SQL scripts.', 1;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId=N'20261027110911_InitialUniversitySchema' AND ProductVersion=N'10.0.14')
    THROW 51000, 'Expected initial migration was not applied.', 1;
GO
