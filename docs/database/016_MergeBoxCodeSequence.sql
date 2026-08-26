USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_HU', N'U') IS NULL
        THROW 51000, 'Required table dbo.WMS_HU is missing.', 1;

    IF OBJECT_ID(N'dbo.WMS_BoxCodeSequence', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_BoxCodeSequence
        (
            BusinessDate date NOT NULL,
            PlantCode varchar(20) NOT NULL,
            CurrentNo int NOT NULL,
            CreateTime datetime2(0) NOT NULL CONSTRAINT DF_WMS_BoxCodeSequence_CreateTime DEFAULT SYSDATETIME(),
            ModifyTime datetime2(0) NULL,
            CONSTRAINT PK_WMS_BoxCodeSequence PRIMARY KEY CLUSTERED (BusinessDate, PlantCode),
            CONSTRAINT CK_WMS_BoxCodeSequence_CurrentNo CHECK (CurrentNo BETWEEN 1 AND 9999)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.WMS_BoxCodeSequence') AND minor_id = 0 AND name = N'MS_Description'
    )
        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'WMS后台OUTER_BOX标准箱码日序列；按业务日期和厂别独立分配。',
            @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'WMS_BoxCodeSequence';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.WMS_BoxCodeSequence') AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.WMS_BoxCodeSequence'), N'BusinessDate', 'ColumnId') AND name = N'MS_Description'
    )
        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'WMS OUTER_BOX创建业务日期。',
            @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'WMS_BoxCodeSequence', @level2type=N'COLUMN', @level2name=N'BusinessDate';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.WMS_BoxCodeSequence') AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.WMS_BoxCodeSequence'), N'PlantCode', 'ColumnId') AND name = N'MS_Description'
    )
        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'厂别代码；与BusinessDate共同构成序列范围。',
            @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'WMS_BoxCodeSequence', @level2type=N'COLUMN', @level2name=N'PlantCode';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.WMS_BoxCodeSequence') AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.WMS_BoxCodeSequence'), N'CurrentNo', 'ColumnId') AND name = N'MS_Description'
    )
        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'当日该厂别已分配的最新序号，范围0001至9999。',
            @level0type=N'SCHEMA', @level0name=N'dbo', @level1type=N'TABLE', @level1name=N'WMS_BoxCodeSequence', @level2type=N'COLUMN', @level2name=N'CurrentNo';

    IF OBJECT_ID(N'dbo.WMS_SchemaVersion', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.WMS_SchemaVersion WHERE ScriptName = N'016_MergeBoxCodeSequence.sql')
    BEGIN
        INSERT INTO dbo.WMS_SchemaVersion(VersionNo, ScriptName, Remark)
        VALUES (N'1.1.0', N'016_MergeBoxCodeSequence.sql', N'Canonical OUTER_BOX code sequence for atomic merge-box.');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
