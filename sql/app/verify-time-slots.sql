:r sql/app/guard-app.sql
SET NOCOUNT ON;
DECLARE @Table int=OBJECT_ID(N'dbo.CourseOfferingTimeSlots');
IF @Table IS NULL OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=@Table)<>5
    THROW 51000,'Expected five time slot columns.',1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@Table AND (is_nullable=1 OR is_identity=1))
    THROW 51000,'Time slot columns must be required and non-IDENTITY.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@Table AND name IN (N'StartsAt',N'EndsAt')
    AND TYPE_NAME(user_type_id)=N'time' AND scale=7)<>2
    THROW 51000,'Expected time(7) start/end.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@Table AND name IN (N'Id',N'CourseOfferingId',N'Day')
    AND TYPE_NAME(user_type_id)=N'int')<>3
    THROW 51000,'Expected int identifiers and day.',1;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=@Table AND type='PK' AND name=N'PK_CourseOfferingTimeSlots')
    THROW 51000,'Missing time slot primary key.',1;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=@Table AND referenced_object_id=OBJECT_ID(N'dbo.CourseOfferings')
    AND name=N'FK_CourseOfferingTimeSlots_CourseOfferings' AND delete_referential_action=0 AND update_referential_action=0
    AND is_disabled=0 AND is_not_trusted=0)
    THROW 51000,'Expected trusted NO ACTION offering FK.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=@Table AND is_disabled=0 AND is_not_trusted=0
    AND name IN (N'CK_CourseOfferingTimeSlots_Day',N'CK_CourseOfferingTimeSlots_Range'))<>2
    THROW 51000,'Expected trusted day/range CHECKs.',1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=@Table AND name=N'UX_CourseOfferingTimeSlots_Offering_Day_Range'
    AND is_unique=1 AND has_filter=0 AND is_disabled=0)
    THROW 51000,'Expected unfiltered unique time slot index.',1;
IF (SELECT STRING_AGG(CONVERT(nvarchar(max),c.name),N',') WITHIN GROUP (ORDER BY ic.key_ordinal)
    FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
    WHERE i.object_id=@Table AND i.name=N'UX_CourseOfferingTimeSlots_Offering_Day_Range' AND ic.key_ordinal>0)
    <>N'CourseOfferingId,Day,StartsAt,EndsAt'
    THROW 51000,'Wrong unique time slot key order.',1;
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=@Table AND name=N'DF_CourseOfferingTimeSlots_Id'
    AND definition=N'(NEXT VALUE FOR [dbo].[CourseOfferingTimeSlotIds])')
    THROW 51000,'Expected time slot sequence default.',1;
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE object_id=OBJECT_ID(N'dbo.CourseOfferingTimeSlotIds')
    AND TYPE_NAME(user_type_id)=N'int' AND increment=1 AND is_cycling=0)
    THROW 51000,'Expected non-cycling time slot sequence.',1;
PRINT 'PASS: time slot time(7), required columns, sequence, PK/FK/UNIQUE/CHECK and NO ACTION metadata.';
