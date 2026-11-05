-- Metadata only; never creates an account or changes academic data.
:r sql/app/guard-app.sql
IF (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN
    (N'AspNetUsers',N'AspNetRoles',N'AspNetUserClaims',N'AspNetRoleClaims',N'AspNetUserRoles',N'AspNetUserLogins',N'AspNetUserTokens'))<>7
    THROW 51000, 'Expected seven default Identity tables.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Students') AND name=N'UserId'
    AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=900 AND is_nullable=1)
    THROW 51000, 'Expected optional nvarchar(450) account link.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Students') AND name=N'UX_Students_UserId'
    AND is_unique=1 AND has_filter=1 AND filter_definition=N'([UserId] IS NOT NULL)' AND is_disabled=0)
    THROW 51000, 'Expected unique non-null account link filter.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
    WHERE f.name=N'FK_Students_AspNetUsers' AND f.parent_object_id=OBJECT_ID(N'dbo.Students')
    AND f.referenced_object_id=OBJECT_ID(N'dbo.AspNetUsers') AND f.delete_referential_action=0
    AND f.update_referential_action=0 AND f.is_disabled=0 AND f.is_not_trusted=0
    AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'UserId'
    AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'Id')
    THROW 51000, 'Expected trusted NO ACTION Student/account foreign key.', 1;
PRINT 'PASS: Identity tables and optional filtered unique NO ACTION Student/account relationship.';
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Students') AND name=N'AdvisorUserId'
    AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=900 AND is_nullable=1)
    THROW 51000, 'Expected nullable advisor account id.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.index_columns c ON c.object_id=i.object_id AND c.index_id=i.index_id
    WHERE i.object_id=OBJECT_ID(N'dbo.Students') AND i.name=N'IX_Students_AdvisorUserId' AND i.is_unique=0 AND i.is_disabled=0
    AND COL_NAME(c.object_id,c.column_id)=N'AdvisorUserId')
    THROW 51000, 'Expected nonunique advisor index.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns c ON c.constraint_object_id=f.object_id
    WHERE f.name=N'FK_Students_AdvisorUser' AND f.parent_object_id=OBJECT_ID(N'dbo.Students')
    AND f.referenced_object_id=OBJECT_ID(N'dbo.AspNetUsers') AND f.delete_referential_action=0 AND f.update_referential_action=0
    AND f.is_disabled=0 AND f.is_not_trusted=0 AND COL_NAME(c.parent_object_id,c.parent_column_id)=N'AdvisorUserId'
    AND COL_NAME(c.referenced_object_id,c.referenced_column_id)=N'Id')
    THROW 51000, 'Expected trusted NO ACTION advisor foreign key.', 1;
PRINT 'PASS: nullable nonunique NO ACTION advisor relationship.';
