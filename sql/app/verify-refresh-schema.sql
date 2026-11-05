:r sql/app/guard-app.sql
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.RefreshSessions') AND name=N'PK_RefreshSessions' AND type='PK')
    THROW 51000, 'Refresh session primary key missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.RefreshTokenRecords') AND name=N'TokenHash' AND TYPE_NAME(user_type_id)=N'binary' AND max_length=32 AND is_nullable=0)
    THROW 51000, 'Refresh hash must be required binary(32).', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.RefreshTokenRecords') AND name=N'UX_RefreshTokens_Hash' AND is_unique=1 AND is_disabled=0 AND has_filter=0)
    THROW 51000, 'Refresh hash unique index missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_RefreshSessions_AspNetUsers' AND delete_referential_action=0 AND is_disabled=0 AND is_not_trusted=0)
    THROW 51000, 'Refresh account NO ACTION FK missing.', 1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.RefreshSessions') AND name IN (N'CK_RefreshSessions_Expiry',N'CK_RefreshSessions_Revocation') AND is_disabled=0 AND is_not_trusted=0)<>2
    THROW 51000, 'Refresh date constraints missing.', 1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.RefreshSessions') AND name IN (N'CreatedAtUtc',N'ExpiresAtUtc') AND TYPE_NAME(user_type_id)=N'datetimeoffset' AND scale=7 AND is_nullable=0)<>2
    THROW 51000, 'Refresh required dates mismatch.', 1;
PRINT 'PASS: refresh PK, binary(32) unique hash, account NO ACTION and date constraints.';
IF COL_LENGTH(N'dbo.RefreshSessions',N'RefreshTokenHash') IS NOT NULL
    THROW 51000, 'Obsolete current hash must be removed.', 1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.RefreshTokenRecords') AND delete_referential_action=0 AND is_disabled=0 AND is_not_trusted=0)<>2
    THROW 51000, 'Token session/replacement NO ACTION foreign keys missing.', 1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.RefreshTokenRecords') AND is_disabled=0 AND is_not_trusted=0)<>2
    THROW 51000, 'Token history checks missing.', 1;
PRINT 'PASS: token history and same-family replacement metadata; no duplicate current hash.';
