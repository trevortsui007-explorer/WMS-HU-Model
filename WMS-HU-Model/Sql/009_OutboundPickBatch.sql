USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY

    BEGIN TRAN;


    ------------------------------------------------------------
    -- 1. FIFO批量拣货头
    ------------------------------------------------------------
    IF OBJECT_ID(
        'dbo.WMS_OutboundPickBatch',
        'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_OutboundPickBatch
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_OutboundPickBatch
                                PRIMARY KEY,

            PickBatchNo         VARCHAR(50) NOT NULL,

            RequestId           VARCHAR(100) NOT NULL,

            WarehouseId         BIGINT NOT NULL,

            PlantCode           VARCHAR(20) NOT NULL,

            MaterialCode        VARCHAR(50) NOT NULL,

            MaterialRevision    VARCHAR(30) NULL,

            FilterBatchNo       VARCHAR(100) NULL,

            ProductionOrderNo   VARCHAR(50) NULL,

            RequiredQty         DECIMAL(18,4) NOT NULL,

            PickedQty           DECIMAL(18,4) NOT NULL,

            DifferenceQty       DECIMAL(18,4) NOT NULL,

            Uom                 VARCHAR(20) NOT NULL,

            ----------------------------------------------------
            -- FIFO_WHOLE_BOX
            ----------------------------------------------------
            PickMode            VARCHAR(30) NOT NULL,

            ----------------------------------------------------
            -- EXACT
            -- OVERPICK
            ----------------------------------------------------
            ResultType          VARCHAR(20) NOT NULL,

            BusinessNo          VARCHAR(100) NULL,

            Operator            VARCHAR(50) NULL,

            PickTime            DATETIME2(0) NOT NULL,

            Remark              NVARCHAR(500) NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_OutboundPickBatch_CreateTime
                                DEFAULT SYSDATETIME(),

            RowVersion          ROWVERSION,

            CONSTRAINT FK_WMS_OutboundPickBatch_Warehouse
                FOREIGN KEY (WarehouseId)
                REFERENCES dbo.WMS_Warehouse(Id),

            CONSTRAINT CK_WMS_OutboundPickBatch_RequiredQty
                CHECK (RequiredQty > 0),

            CONSTRAINT CK_WMS_OutboundPickBatch_PickedQty
                CHECK (PickedQty > 0)
        );


        CREATE UNIQUE INDEX
            UX_WMS_OutboundPickBatch_BatchNo

        ON dbo.WMS_OutboundPickBatch
        (
            PickBatchNo
        );


        CREATE UNIQUE INDEX
            UX_WMS_OutboundPickBatch_RequestId

        ON dbo.WMS_OutboundPickBatch
        (
            RequestId
        );


        CREATE INDEX
            IX_WMS_OutboundPickBatch_Business

        ON dbo.WMS_OutboundPickBatch
        (
            BusinessNo,
            PickTime
        )

        WHERE BusinessNo IS NOT NULL;

    END;


    ------------------------------------------------------------
    -- 2. FIFO批量拣货明细
    ------------------------------------------------------------
    IF OBJECT_ID(
        'dbo.WMS_OutboundPickBatchDetail',
        'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_OutboundPickBatchDetail
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_OutboundPickBatchDetail
                                PRIMARY KEY,

            PickBatchId         BIGINT NOT NULL,

            SequenceNo          INT NOT NULL,

            BoxHUId             BIGINT NOT NULL,

            FromLocationId      BIGINT NOT NULL,

            ToLocationId        BIGINT NOT NULL,

            FifoTime            DATETIME2(0) NOT NULL,

            BoxQty              DECIMAL(18,4) NOT NULL,

            Uom                 VARCHAR(20) NOT NULL,

            MoveLogId           BIGINT NOT NULL,

            OperationNo         VARCHAR(50) NOT NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_OutboundPickBatchDetail_CreateTime
                                DEFAULT SYSDATETIME(),

            CONSTRAINT FK_WMS_OutboundPickBatchDetail_Batch
                FOREIGN KEY (PickBatchId)
                REFERENCES dbo.WMS_OutboundPickBatch(Id),

            CONSTRAINT FK_WMS_OutboundPickBatchDetail_HU
                FOREIGN KEY (BoxHUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_OutboundPickBatchDetail_FromLocation
                FOREIGN KEY (FromLocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT FK_WMS_OutboundPickBatchDetail_ToLocation
                FOREIGN KEY (ToLocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT FK_WMS_OutboundPickBatchDetail_MoveLog
                FOREIGN KEY (MoveLogId)
                REFERENCES dbo.WMS_HU_MoveLog(Id),

            CONSTRAINT CK_WMS_OutboundPickBatchDetail_Qty
                CHECK (BoxQty > 0)
        );


        CREATE UNIQUE INDEX
            UX_WMS_OutboundPickBatchDetail_Sequence

        ON dbo.WMS_OutboundPickBatchDetail
        (
            PickBatchId,
            SequenceNo
        );


        CREATE UNIQUE INDEX
            UX_WMS_OutboundPickBatchDetail_Box

        ON dbo.WMS_OutboundPickBatchDetail
        (
            PickBatchId,
            BoxHUId
        );

    END;


    ------------------------------------------------------------
    -- 3. Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.8.0'
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
            '0.8.0',
            '009_OutboundPickBatch.sql',
            N'建立FIFO多箱批量拣货执行记录'
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