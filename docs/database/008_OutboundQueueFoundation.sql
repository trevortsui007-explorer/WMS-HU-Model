USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY

    BEGIN TRAN;


    ------------------------------------------------------------
    -- HU Item FIFO / 出库候选查询索引
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name =
            'IX_WMS_HU_Item_OutboundQueue'
        AND object_id =
            OBJECT_ID('dbo.WMS_HU_Item')
    )
    BEGIN

        CREATE INDEX
            IX_WMS_HU_Item_OutboundQueue

        ON dbo.WMS_HU_Item
        (
            MaterialCode,
            StockStatus,
            CreateTime,
            HUId
        )

        INCLUDE
        (
            MaterialRevision,
            BatchNo,
            ProductionOrderNo,
            Qty,
            Uom
        )

        WHERE IsActive = 1;

    END;


    ------------------------------------------------------------
    -- 当前外箱搜索
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name =
            'IX_WMS_HU_OutboundQueue'
        AND object_id =
            OBJECT_ID('dbo.WMS_HU')
    )
    BEGIN

        CREATE INDEX
            IX_WMS_HU_OutboundQueue

        ON dbo.WMS_HU
        (
            HUType,
            PlantCode,
            LifecycleStatus,
            IsHold
        )

        INCLUDE
        (
            HUCode
        );

    END;


    ------------------------------------------------------------
    -- Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.7.0'
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
            '0.7.0',
            '008_OutboundQueueFoundation.sql',
            N'建立FIFO拣货队列查询基础索引'
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