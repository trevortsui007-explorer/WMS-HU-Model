USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY

    BEGIN TRAN;


    ------------------------------------------------------------
    -- HU关系关闭操作号
    ------------------------------------------------------------
    IF COL_LENGTH(
        'dbo.WMS_HU_Relation',
        'CloseOperationNo') IS NULL
    BEGIN

        ALTER TABLE dbo.WMS_HU_Relation
        ADD CloseOperationNo VARCHAR(50) NULL;

    END;


    ------------------------------------------------------------
    -- Active关系的数据完整性
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name =
            'CK_WMS_HU_Relation_ActiveTime'
    )
    BEGIN

        ALTER TABLE dbo.WMS_HU_Relation

        ADD CONSTRAINT
            CK_WMS_HU_Relation_ActiveTime

        CHECK
        (
            (
                IsActive = 1
                AND EffectiveTo IS NULL
            )
            OR
            (
                IsActive = 0
                AND EffectiveTo IS NOT NULL
            )
        );

    END;


    ------------------------------------------------------------
    -- 查询关闭操作
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name =
            'IX_WMS_HU_Relation_CloseOperationNo'
        AND object_id =
            OBJECT_ID(
                'dbo.WMS_HU_Relation')
    )
    BEGIN

        CREATE INDEX
            IX_WMS_HU_Relation_CloseOperationNo

        ON dbo.WMS_HU_Relation
        (
            CloseOperationNo
        )

        WHERE CloseOperationNo IS NOT NULL;

    END;


    ------------------------------------------------------------
    -- Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.5.0'
    )
    BEGIN

        INSERT INTO dbo.WMS_SchemaVersion
        (
            VersionNo,
            ScriptName,
            Remark
        )
        VALUES
        (
            '0.5.0',
            '006_AddRelationCloseOperation.sql',
            N'补充HU关系关闭操作号及关系状态完整性约束'
        );

    END;


    COMMIT;

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK;

    THROW;

END CATCH;
GO