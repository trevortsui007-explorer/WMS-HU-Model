USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY

    BEGIN TRAN;


    ------------------------------------------------------------
    -- 1. 修改MoveLog位置约束
    --
    -- 普通移动仍不允许 From = To
    --
    -- 但以下包装变化允许：
    -- REPACK_TRANSFER
    -- UNPACK_INNER
    -- PACK_INNER
    ------------------------------------------------------------
    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_HU_MoveLog_Location'
    )
    BEGIN

        ALTER TABLE dbo.WMS_HU_MoveLog
        DROP CONSTRAINT CK_WMS_HU_MoveLog_Location;

    END;


    ALTER TABLE dbo.WMS_HU_MoveLog

    ADD CONSTRAINT CK_WMS_HU_MoveLog_Location

    CHECK
    (
        FromLocationId IS NULL

        OR FromLocationId <> ToLocationId

        OR MoveType IN
        (
            'REPACK_TRANSFER',
            'UNPACK_INNER',
            'PACK_INNER'
        )
    );


    ------------------------------------------------------------
    -- 2. OperationLog RequestId查询索引
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_HU_OperationLog_RequestId'
        AND object_id =
            OBJECT_ID('dbo.WMS_HU_OperationLog')
    )
    BEGIN

        CREATE INDEX IX_WMS_HU_OperationLog_RequestId

        ON dbo.WMS_HU_OperationLog
        (
            RequestId,
            OperationType
        )

        WHERE RequestId IS NOT NULL;

    END;


    ------------------------------------------------------------
    -- 3. Relation操作号索引
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_HU_Relation_OperationNo'
        AND object_id =
            OBJECT_ID('dbo.WMS_HU_Relation')
    )
    BEGIN

        CREATE INDEX IX_WMS_HU_Relation_OperationNo

        ON dbo.WMS_HU_Relation
        (
            OperationNo
        )

        WHERE OperationNo IS NOT NULL;

    END;


    ------------------------------------------------------------
    -- Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.6.0'
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
            '0.6.0',
            '007_RepackFoundation.sql',
            N'建立拆箱合箱换箱基础能力，支持包装关系变化时同货位MoveLog'
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