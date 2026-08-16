USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    012_OutboundReservationAndPick.sql

    前提：
    - 009 阶段已存在 dbo.WMS_OutboundPickBatch
    - 009 阶段已存在 dbo.WMS_OutboundPickBatchDetail

    正式升级：
    1. 增加订单级 Allocation。
    2. 增加 OUTER_BOX Reservation。
    3. 将已有 PickBatch 升级为订单驱动模式。
    4. PickBatchDetail 继续只记录真实扫描后的物理箱动作。
    5. 新增 PickDetailItem 保存 SAP 行项目分配与周码快照。
*/

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_OutboundOrder', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_OutboundOrder，请先执行 011_OutboundOrder.sql。', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_OutboundOrderLine', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_OutboundOrderLine，请先执行 011_OutboundOrder.sql。', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatch', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_OutboundPickBatch，请先执行现有 009 Outbound PickBatch 基础脚本。', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_OutboundPickBatchDetail，请先执行现有 009 Outbound PickBatch 基础脚本。', 1;
    END;

    ----------------------------------------------------------------------
    -- 1. Allocation
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundAllocation', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundAllocation
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundAllocation PRIMARY KEY,

            AllocationNo VARCHAR(50) NOT NULL,
            OutboundOrderId BIGINT NOT NULL,

            RequestId VARCHAR(100) NOT NULL,

            AllocationStatus VARCHAR(20) NOT NULL
                CONSTRAINT DF_WMS_OutboundAllocation_Status DEFAULT ('ACTIVE'),

            AllocateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundAllocation_AllocateTime DEFAULT (SYSDATETIME()),

            ExpireTime DATETIME2(3) NOT NULL,

            FirstPickTime DATETIME2(3) NULL,
            CompleteTime DATETIME2(3) NULL,

            ReleaseTime DATETIME2(3) NULL,
            ReleaseReason NVARCHAR(500) NULL,

            Operator VARCHAR(50) NULL,
            Remark NVARCHAR(500) NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundAllocation_CreateTime DEFAULT (SYSDATETIME()),

            ModifyTime DATETIME2(3) NULL,

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_OutboundAllocation_Order
                FOREIGN KEY (OutboundOrderId)
                REFERENCES dbo.WMS_OutboundOrder(Id),

            CONSTRAINT CK_WMS_OutboundAllocation_Status
                CHECK
                (
                    AllocationStatus IN
                    (
                        'ACTIVE',
                        'PICKING',
                        'COMPLETED',
                        'RELEASED',
                        'EXPIRED',
                        'CANCELLED'
                    )
                ),

            CONSTRAINT CK_WMS_OutboundAllocation_Expire
                CHECK
                (
                    ExpireTime > AllocateTime
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundAllocation_No'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundAllocation')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundAllocation_No
        ON dbo.WMS_OutboundAllocation(AllocationNo);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundAllocation_Request'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundAllocation')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundAllocation_Request
        ON dbo.WMS_OutboundAllocation(RequestId);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundAllocation_OrderStatus'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundAllocation')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundAllocation_OrderStatus
        ON dbo.WMS_OutboundAllocation
        (
            OutboundOrderId,
            AllocationStatus,
            ExpireTime
        )
        INCLUDE
        (
            AllocationNo,
            RequestId,
            FirstPickTime
        );
    END;

    ----------------------------------------------------------------------
    -- 2. Upgrade PickBatch
    ----------------------------------------------------------------------

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'AllocationId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD AllocationId BIGINT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'OutboundOrderId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD OutboundOrderId BIGINT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'BatchStatus') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD BatchStatus VARCHAR(20) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'PlannedQty') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD PlannedQty DECIMAL(18,4) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'EffectivePickedQty') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD EffectivePickedQty DECIMAL(18,4) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'HistoricalPickedQty') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD HistoricalPickedQty DECIMAL(18,4) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'ReservationExpireTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD ReservationExpireTime DATETIME2(3) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'StartPickTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD StartPickTime DATETIME2(3) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'CompleteTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CompleteTime DATETIME2(3) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'CancelTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CancelTime DATETIME2(3) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'CancelReason') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CancelReason NVARCHAR(500) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatch', 'GroupKey') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD GroupKey VARCHAR(200) NULL;
    END;

    /*
        兼容已有 009 测试数据：
        旧记录视为已 PICKED。
    */
    UPDATE dbo.WMS_OutboundPickBatch
    SET
        BatchStatus = ISNULL(BatchStatus, 'PICKED'),
        PlannedQty = ISNULL(PlannedQty, RequiredQty),
        EffectivePickedQty = ISNULL(EffectivePickedQty, PickedQty),
        HistoricalPickedQty = ISNULL(HistoricalPickedQty, PickedQty)
    WHERE
        BatchStatus IS NULL
        OR PlannedQty IS NULL
        OR EffectivePickedQty IS NULL
        OR HistoricalPickedQty IS NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_WMS_OutboundPickBatch_Allocation'
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CONSTRAINT FK_WMS_OutboundPickBatch_Allocation
            FOREIGN KEY (AllocationId)
            REFERENCES dbo.WMS_OutboundAllocation(Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_WMS_OutboundPickBatch_Order'
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CONSTRAINT FK_WMS_OutboundPickBatch_Order
            FOREIGN KEY (OutboundOrderId)
            REFERENCES dbo.WMS_OutboundOrder(Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundPickBatch_Order'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundPickBatch_Order
        ON dbo.WMS_OutboundPickBatch
        (
            OutboundOrderId,
            AllocationId,
            BatchStatus
        )
        INCLUDE
        (
            PickBatchNo,
            MaterialCode,
            RequiredQty,
            PlannedQty,
            EffectivePickedQty,
            HistoricalPickedQty
        );
    END;

    ----------------------------------------------------------------------
    -- 3. Reservation
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_HU_Reservation', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_HU_Reservation
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_HU_Reservation PRIMARY KEY,

            ReservationNo VARCHAR(50) NOT NULL,

            AllocationId BIGINT NOT NULL,
            OutboundOrderId BIGINT NOT NULL,
            PickBatchId BIGINT NOT NULL,

            HUId BIGINT NOT NULL,
            SourceLocationId BIGINT NOT NULL,

            ReservationScope VARCHAR(20) NOT NULL
                CONSTRAINT DF_WMS_HU_Reservation_Scope DEFAULT ('OUTER_BOX'),

            ReservedQty DECIMAL(18,4) NOT NULL,
            Uom VARCHAR(20) NOT NULL,

            MinWeekSortKey INT NULL,
            MaxWeekSortKey INT NULL,
            DistinctWeekCount INT NULL,
            WeekSummary VARCHAR(500) NULL,

            ReservationStatus VARCHAR(20) NOT NULL
                CONSTRAINT DF_WMS_HU_Reservation_Status DEFAULT ('ACTIVE'),

            ReservedTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_HU_Reservation_ReservedTime DEFAULT (SYSDATETIME()),

            ExpireTime DATETIME2(3) NOT NULL,

            ConsumedTime DATETIME2(3) NULL,
            ReleasedTime DATETIME2(3) NULL,
            ReleaseReason NVARCHAR(500) NULL,

            Operator VARCHAR(50) NULL,
            RequestId VARCHAR(100) NULL,
            Remark NVARCHAR(500) NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_HU_Reservation_CreateTime DEFAULT (SYSDATETIME()),

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_HU_Reservation_Allocation
                FOREIGN KEY (AllocationId)
                REFERENCES dbo.WMS_OutboundAllocation(Id),

            CONSTRAINT FK_WMS_HU_Reservation_Order
                FOREIGN KEY (OutboundOrderId)
                REFERENCES dbo.WMS_OutboundOrder(Id),

            CONSTRAINT FK_WMS_HU_Reservation_Batch
                FOREIGN KEY (PickBatchId)
                REFERENCES dbo.WMS_OutboundPickBatch(Id),

            CONSTRAINT FK_WMS_HU_Reservation_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_HU_Reservation_Location
                FOREIGN KEY (SourceLocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT CK_WMS_HU_Reservation_Qty
                CHECK (ReservedQty > 0),

            CONSTRAINT CK_WMS_HU_Reservation_Status
                CHECK
                (
                    ReservationStatus IN
                    (
                        'ACTIVE',
                        'CONSUMED',
                        'RELEASED',
                        'EXPIRED',
                        'CANCELLED'
                    )
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_Reservation_No'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Reservation')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_Reservation_No
        ON dbo.WMS_HU_Reservation(ReservationNo);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_Reservation_ActiveHU'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Reservation')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_Reservation_ActiveHU
        ON dbo.WMS_HU_Reservation(HUId)
        WHERE ReservationStatus = 'ACTIVE';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_HU_Reservation_Expire'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Reservation')
    )
    BEGIN
        CREATE INDEX IX_WMS_HU_Reservation_Expire
        ON dbo.WMS_HU_Reservation
        (
            ReservationStatus,
            ExpireTime
        )
        INCLUDE
        (
            AllocationId,
            OutboundOrderId,
            PickBatchId,
            HUId
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_HU_Reservation_Batch'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Reservation')
    )
    BEGIN
        CREATE INDEX IX_WMS_HU_Reservation_Batch
        ON dbo.WMS_HU_Reservation
        (
            PickBatchId,
            ReservationStatus
        )
        INCLUDE
        (
            HUId,
            SourceLocationId,
            ReservedQty,
            MinWeekSortKey,
            ExpireTime
        );
    END;

    ----------------------------------------------------------------------
    -- 4. Upgrade PickBatchDetail
    ----------------------------------------------------------------------

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'DetailStatus') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD DetailStatus VARCHAR(20) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'EffectiveQty') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD EffectiveQty DECIMAL(18,4) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'ReplacementForDetailId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD ReplacementForDetailId BIGINT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'IsReplacement') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD IsReplacement BIT NOT NULL
            CONSTRAINT DF_WMS_OutboundPickBatchDetail_IsReplacement DEFAULT (0);
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'ReturnedTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD ReturnedTime DATETIME2(3) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'CancelledTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD CancelledTime DATETIME2(3) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'MinWeekSortKey') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD MinWeekSortKey INT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'MaxWeekSortKey') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD MaxWeekSortKey INT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'DistinctWeekCount') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD DistinctWeekCount INT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickBatchDetail', 'WeekSummary') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD WeekSummary VARCHAR(500) NULL;
    END;

    UPDATE dbo.WMS_OutboundPickBatchDetail
    SET
        DetailStatus = ISNULL(DetailStatus, 'PICKED'),
        EffectiveQty = ISNULL(EffectiveQty, BoxQty)
    WHERE DetailStatus IS NULL
       OR EffectiveQty IS NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = 'FK_WMS_OutboundPickBatchDetail_Replacement'
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD CONSTRAINT FK_WMS_OutboundPickBatchDetail_Replacement
            FOREIGN KEY (ReplacementForDetailId)
            REFERENCES dbo.WMS_OutboundPickBatchDetail(Id);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundPickBatchDetail_Status'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundPickBatchDetail_Status
        ON dbo.WMS_OutboundPickBatchDetail
        (
            PickBatchId,
            DetailStatus
        )
        INCLUDE
        (
            BoxHUId,
            EffectiveQty,
            IsReplacement,
            ReplacementForDetailId,
            MinWeekSortKey
        );
    END;

    ----------------------------------------------------------------------
    -- 5. PickDetailItem
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundPickDetailItem', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundPickDetailItem
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundPickDetailItem PRIMARY KEY,

            PickDetailId BIGINT NOT NULL,
            OutboundOrderLineId BIGINT NOT NULL,

            InnerHUId BIGINT NOT NULL,
            HUItemId BIGINT NOT NULL,

            MaterialCode VARCHAR(50) NOT NULL,
            MaterialRevision VARCHAR(30) NULL,

            DateCode VARCHAR(50) NULL,
            WeekSortKey INT NULL,

            AllocatedQty DECIMAL(18,4) NOT NULL,
            Uom VARCHAR(20) NOT NULL,

            Remark NVARCHAR(500) NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundPickDetailItem_CreateTime DEFAULT (SYSDATETIME()),

            CONSTRAINT FK_WMS_OutboundPickDetailItem_Detail
                FOREIGN KEY (PickDetailId)
                REFERENCES dbo.WMS_OutboundPickBatchDetail(Id),

            CONSTRAINT FK_WMS_OutboundPickDetailItem_OrderLine
                FOREIGN KEY (OutboundOrderLineId)
                REFERENCES dbo.WMS_OutboundOrderLine(Id),

            CONSTRAINT FK_WMS_OutboundPickDetailItem_InnerHU
                FOREIGN KEY (InnerHUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_OutboundPickDetailItem_HUItem
                FOREIGN KEY (HUItemId)
                REFERENCES dbo.WMS_HU_Item(Id),

            CONSTRAINT CK_WMS_OutboundPickDetailItem_Qty
                CHECK (AllocatedQty > 0)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundPickDetailItem_Detail'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickDetailItem')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundPickDetailItem_Detail
        ON dbo.WMS_OutboundPickDetailItem
        (
            PickDetailId,
            OutboundOrderLineId
        )
        INCLUDE
        (
            InnerHUId,
            HUItemId,
            MaterialCode,
            DateCode,
            WeekSortKey,
            AllocatedQty,
            Uom
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundPickDetailItem_Line'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickDetailItem')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundPickDetailItem_Line
        ON dbo.WMS_OutboundPickDetailItem
        (
            OutboundOrderLineId,
            MaterialCode
        )
        INCLUDE
        (
            PickDetailId,
            HUItemId,
            AllocatedQty,
            WeekSortKey
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO
