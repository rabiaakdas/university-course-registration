:ON ERROR EXIT
IF DB_NAME() <> N'university_registration_lab'
    THROW 51000, 'Wrong database: only university_registration_lab is allowed.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE class = 0 AND name = N'UniversityRegistrationLab'
                 AND CONVERT(nvarchar(128), value) = N'day3-v1')
    THROW 51000, 'Missing lab ownership marker; inspect before proceeding.', 1;
GO
