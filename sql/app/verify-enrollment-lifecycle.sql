:r sql/app/guard-app.sql
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Enrollments')
    AND name=N'Status' AND TYPE_NAME(user_type_id)=N'int' AND is_nullable=0)
    THROW 51000,'Expected required int Status.',1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Enrollments')
    AND name=N'RejectionReason' AND TYPE_NAME(user_type_id)=N'nvarchar' AND max_length=1000 AND is_nullable=1)
    THROW 51000,'Expected nullable nvarchar(500) RejectionReason.',1;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Enrollments')
    AND name=N'DF_Enrollments_Status' AND definition IN (N'((0))',N'(0)'))
    THROW 51000,'Expected permanent Draft=0 default, not Approved.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Enrollments')
    AND name IN (N'CK_Enrollments_Status',N'CK_Enrollments_RejectionReason') AND is_disabled=0 AND is_not_trusted=0)<>2
    THROW 51000,'Expected trusted status/reason CHECKs.',1;
PRINT 'PASS: enrollment lifecycle Status/Unicode reason/default/check metadata.';
