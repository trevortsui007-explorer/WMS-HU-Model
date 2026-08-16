USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    014_OutboundV1Compatibility.sql

    已部署 010-013 后的正式出库兼容迁移：
    1. 保留 009 历史，允许 PickBatch 在实际扫码前以 PickedQty = 0 存在。
    2. 固化正式 Batch / Detail 状态。
    3. 补充 SAP 批次快照及一对多 Replacement 关系。
    4. 补充 Release / VOID / SHIPPED 幂等键。
    5. 建立 SHIPPED 虚拟货位与 Reservation 过期过程。
*/

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatch', N'U') IS NULL
        THROW 51000, N'缺少 dbo.WMS_OutboundPickBatch。', 1;

    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail', N'U') IS NULL
        THROW 51000, N'缺少 dbo.WMS_OutboundPickBatchDetail。', 1;

    IF OBJECT_ID(N'dbo.WMS_OutboundPickDetailItem', N'U') IS NULL
        THROW 51000, N'缺少 dbo.WMS_OutboundPickDetailItem。', 1;

    ----------------------------------------------------------------------
    -- 1. PickBatch 正式兼容
    ----------------------------------------------------------------------

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_OutboundPickBatch_PickedQty'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        DROP CONSTRAINT CK_WMS_OutboundPickBatch_PickedQty;
    END;

    ALTER TABLE dbo.WMS_OutboundPickBatch
    ALTER COLUMN PickedQty DECIMAL(18,4) NOT NULL;

    ALTER TABLE dbo.WMS_OutboundPickBatch
    ALTER COLUMN PickTime DATETIME2(0) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_OutboundPickBatch_PickedQty'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CONSTRAINT CK_WMS_OutboundPickBatch_PickedQty
            CHECK (PickedQty >= 0);
    END;

    UPDATE dbo.WMS_OutboundPickBatch
    SET BatchStatus = ISNULL(BatchStatus, 'PICKED')
    WHERE BatchStatus IS NULL;

    ALTER TABLE dbo.WMS_OutboundPickBatch
    ALTER COLUMN BatchStatus VARCHAR(20) NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
          AND c.name = 'BatchStatus'
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CONSTRAINT DF_WMS_OutboundPickBatch_BatchStatus
            DEFAULT ('ALLOCATED') FOR BatchStatus;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_OutboundPickBatch_BatchStatus'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CONSTRAINT CK_WMS_OutboundPickBatch_BatchStatus
            CHECK
            (
                BatchStatus IN
                (
                    'ALLOCATED',
                    'PICKING',
                    'PICKED',
                    'EXCEPTION',
                    'SHIPPED',
                    'RELEASED',
                    'EXPIRED'
                )
            );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundPickBatch_AllocationGroup'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundPickBatch_AllocationGroup
        ON dbo.WMS_OutboundPickBatch(AllocationId, GroupKey)
        WHERE AllocationId IS NOT NULL
          AND GroupKey IS NOT NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE name = 'CK_WMS_OutboundPickBatch_FormalQty'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatch')
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatch
        ADD CONSTRAINT CK_WMS_OutboundPickBatch_FormalQty
            CHECK
            (
                AllocationId IS NULL
                OR
                (
                    OutboundOrderId IS NOT NULL
                    AND GroupKey IS NOT NULL
                    AND PlannedQty > 0
                    AND EffectivePickedQty >= 0
                    AND HistoricalPickedQty >= 0
                    AND HistoricalPickedQty >= EffectivePickedQty
                )
            );
    END;

    ----------------------------------------------------------------------
    -- 2. PickDetail 正式状态
    ----------------------------------------------------------------------

    ALTER TABLE dbo.WMS_OutboundPickBatchDetail
    ALTER COLUMN FifoTime DATETIME2(0) NULL;

    UPDATE dbo.WMS_OutboundPickBatchDetail
    SET
        DetailStatus = ISNULL(DetailStatus, 'PICKED'),
        EffectiveQty = ISNULL(EffectiveQty, BoxQty)
    WHERE DetailStatus IS NULL
       OR EffectiveQty IS NULL;

    ALTER TABLE dbo.WMS_OutboundPickBatchDetail
    ALTER COLUMN DetailStatus VARCHAR(20) NOT NULL;

    ALTER TABLE dbo.WMS_OutboundPickBatchDetail
    ALTER COLUMN EffectiveQty DECIMAL(18,4) NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
           AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail')
          AND c.name = 'DetailStatus'
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD CONSTRAINT DF_WMS_OutboundPickBatchDetail_DetailStatus
            DEFAULT ('PICKED') FOR DetailStatus;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_OutboundPickBatchDetail_Status'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail')
    )
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickBatchDetail
        ADD CONSTRAINT CK_WMS_OutboundPickBatchDetail_Status
            CHECK
            (
                DetailStatus IN
                (
                    'PICKED',
                    'RETURNED',
                    'QUALITY_HOLD',
                    'CANCELLED',
                    'SHIPPED'
                )
            );
    END;

    ----------------------------------------------------------------------
    -- 3. PickDetailItem SAP 批次快照
    ----------------------------------------------------------------------

    IF COL_LENGTH('dbo.WMS_OutboundPickDetailItem', 'BatchNo') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickDetailItem
        ADD BatchNo VARCHAR(100) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickDetailItem', 'SAPBatchNo') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickDetailItem
        ADD SAPBatchNo VARCHAR(100) NULL;
    END;

    ----------------------------------------------------------------------
    -- 4. 写动作幂等键
    ----------------------------------------------------------------------

    IF COL_LENGTH('dbo.WMS_OutboundAllocation', 'ReleaseRequestId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundAllocation
        ADD ReleaseRequestId VARCHAR(100) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundOrder', 'VoidRequestId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundOrder
        ADD VoidRequestId VARCHAR(100) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundOrder', 'ShipRequestId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundOrder
        ADD ShipRequestId VARCHAR(100) NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundAllocation_ReleaseRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundAllocation')
    )
    BEGIN
        EXEC sys.sp_executesql N'
CREATE UNIQUE INDEX UX_WMS_OutboundAllocation_ReleaseRequest
ON dbo.WMS_OutboundAllocation(ReleaseRequestId)
WHERE ReleaseRequestId IS NOT NULL;';
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrder_VoidRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrder')
    )
    BEGIN
        EXEC sys.sp_executesql N'
CREATE UNIQUE INDEX UX_WMS_OutboundOrder_VoidRequest
ON dbo.WMS_OutboundOrder(VoidRequestId)
WHERE VoidRequestId IS NOT NULL;';
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrder_ShipRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrder')
    )
    BEGIN
        EXEC sys.sp_executesql N'
CREATE UNIQUE INDEX UX_WMS_OutboundOrder_ShipRequest
ON dbo.WMS_OutboundOrder(ShipRequestId)
WHERE ShipRequestId IS NOT NULL;';
    END;

    ----------------------------------------------------------------------
    -- 5. 一个异常可关联多个 Replacement
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundExceptionReplacement', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundExceptionReplacement
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundExceptionReplacement PRIMARY KEY,

            ExceptionId BIGINT NOT NULL,
            SequenceNo INT NOT NULL,
            ReservationId BIGINT NULL,
            PickDetailId BIGINT NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundExceptionReplacement_CreateTime
                DEFAULT (SYSDATETIME()),

            CONSTRAINT FK_WMS_OutboundExceptionReplacement_Exception
                FOREIGN KEY (ExceptionId)
                REFERENCES dbo.WMS_OutboundPickException(Id),

            CONSTRAINT FK_WMS_OutboundExceptionReplacement_Reservation
                FOREIGN KEY (ReservationId)
                REFERENCES dbo.WMS_HU_Reservation(Id),

            CONSTRAINT FK_WMS_OutboundExceptionReplacement_Detail
                FOREIGN KEY (PickDetailId)
                REFERENCES dbo.WMS_OutboundPickBatchDetail(Id),

            CONSTRAINT CK_WMS_OutboundExceptionReplacement_Source
                CHECK (ReservationId IS NOT NULL OR PickDetailId IS NOT NULL)
        );

        CREATE UNIQUE INDEX UX_WMS_OutboundExceptionReplacement_Sequence
        ON dbo.WMS_OutboundExceptionReplacement(ExceptionId, SequenceNo);

        CREATE UNIQUE INDEX UX_WMS_OutboundExceptionReplacement_Reservation
        ON dbo.WMS_OutboundExceptionReplacement(ExceptionId, ReservationId)
        WHERE ReservationId IS NOT NULL;

        CREATE UNIQUE INDEX UX_WMS_OutboundExceptionReplacement_Detail
        ON dbo.WMS_OutboundExceptionReplacement(ExceptionId, PickDetailId)
        WHERE PickDetailId IS NOT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_OutboundPickException', 'ReplacementRequestId') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_OutboundPickException
        ADD ReplacementRequestId VARCHAR(100) NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundPickException_ReplacementRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickException')
    )
    BEGIN
        EXEC sys.sp_executesql N'
CREATE UNIQUE INDEX UX_WMS_OutboundPickException_ReplacementRequest
ON dbo.WMS_OutboundPickException(ReplacementRequestId)
WHERE ReplacementRequestId IS NOT NULL;';
    END;

    ----------------------------------------------------------------------
    -- 6. 修正 013 仅创建单个 RETURNED 的兼容问题，并创建每仓库 SHIPPED
    ----------------------------------------------------------------------

    DECLARE @ReturnedInsertColumns NVARCHAR(MAX) = N'';
    DECLARE @ReturnedSelectColumns NVARCHAR(MAX) = N'';
    DECLARE @CreateReturnedSql NVARCHAR(MAX);

    SELECT
        @ReturnedInsertColumns = @ReturnedInsertColumns
            + CASE WHEN LEN(@ReturnedInsertColumns) = 0 THEN N'' ELSE N',' END
            + QUOTENAME(c.name),
        @ReturnedSelectColumns = @ReturnedSelectColumns
            + CASE WHEN LEN(@ReturnedSelectColumns) = 0 THEN N'' ELSE N',' END
            + CASE c.name
                WHEN 'LocationCode' THEN N'''RETURNED'''
                WHEN 'LocationName' THEN N'N''出库异常退回区'''
                WHEN 'ScanCode' THEN N'NULL'
                WHEN 'IsVirtual' THEN N'CAST(1 AS BIT)'
                WHEN 'IsScannable' THEN N'CAST(0 AS BIT)'
                WHEN 'MasterStatus' THEN N'''ACTIVE'''
                WHEN 'CreateUser' THEN N'''SYSTEM'''
                WHEN 'ModifyUser' THEN N'''SYSTEM'''
                WHEN 'CreateTime' THEN N'SYSDATETIME()'
                WHEN 'ModifyTime' THEN N'SYSDATETIME()'
                ELSE N'src.' + QUOTENAME(c.name)
              END
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.WMS_Location')
      AND c.is_identity = 0
      AND c.is_computed = 0
      AND c.system_type_id <> 189
    ORDER BY c.column_id;

    SET @CreateReturnedSql =
        N'INSERT INTO dbo.WMS_Location (' + @ReturnedInsertColumns + N') '
        + N'SELECT ' + @ReturnedSelectColumns + N' '
        + N'FROM dbo.WMS_Location src '
        + N'WHERE src.LocationCode = ''RETURN-PENDING'' '
        + N'AND NOT EXISTS '
        + N'(SELECT 1 FROM dbo.WMS_Location existing '
        + N' WHERE existing.WarehouseId = src.WarehouseId '
        + N' AND existing.LocationCode = ''RETURNED'');';

    EXEC sys.sp_executesql @CreateReturnedSql;

    DECLARE @InsertColumns NVARCHAR(MAX) = N'';
    DECLARE @SelectColumns NVARCHAR(MAX) = N'';
    DECLARE @CreateShippedSql NVARCHAR(MAX);

    SELECT
        @InsertColumns = @InsertColumns
            + CASE WHEN LEN(@InsertColumns) = 0 THEN N'' ELSE N',' END
            + QUOTENAME(c.name),
        @SelectColumns = @SelectColumns
            + CASE WHEN LEN(@SelectColumns) = 0 THEN N'' ELSE N',' END
            + CASE c.name
                WHEN 'LocationCode' THEN N'''SHIPPED'''
                WHEN 'LocationName' THEN N'N''已正式出库'''
                WHEN 'ScanCode' THEN N'NULL'
                WHEN 'IsVirtual' THEN N'CAST(1 AS BIT)'
                WHEN 'IsScannable' THEN N'CAST(0 AS BIT)'
                WHEN 'MasterStatus' THEN N'''ACTIVE'''
                WHEN 'CreateUser' THEN N'''SYSTEM'''
                WHEN 'ModifyUser' THEN N'''SYSTEM'''
                WHEN 'CreateTime' THEN N'SYSDATETIME()'
                WHEN 'ModifyTime' THEN N'SYSDATETIME()'
                ELSE N'src.' + QUOTENAME(c.name)
              END
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.WMS_Location')
      AND c.is_identity = 0
      AND c.is_computed = 0
      AND c.system_type_id <> 189
    ORDER BY c.column_id;

    SET @CreateShippedSql =
        N'INSERT INTO dbo.WMS_Location (' + @InsertColumns + N') '
        + N'SELECT ' + @SelectColumns + N' '
        + N'FROM dbo.WMS_Location src '
        + N'WHERE src.LocationCode = ''OUTBOUND-STAGING'' '
        + N'AND NOT EXISTS '
        + N'(SELECT 1 FROM dbo.WMS_Location existing '
        + N' WHERE existing.WarehouseId = src.WarehouseId '
        + N' AND existing.LocationCode = ''SHIPPED'');';

    EXEC sys.sp_executesql @CreateShippedSql;

    ----------------------------------------------------------------------
    -- 7. Reservation 自动过期过程
    ----------------------------------------------------------------------

    DECLARE @ExpireProcedureSql NVARCHAR(MAX) = N'
CREATE OR ALTER PROCEDURE dbo.usp_WMS_ExpireOutboundReservations
    @AsOf DATETIME2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AsOf IS NULL SET @AsOf = SYSDATETIME();

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Expired TABLE
        (
            AllocationId BIGINT NOT NULL PRIMARY KEY,
            OutboundOrderId BIGINT NOT NULL
        );

        INSERT INTO @Expired(AllocationId, OutboundOrderId)
        SELECT a.Id, a.OutboundOrderId
        FROM dbo.WMS_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK)
        WHERE a.AllocationStatus = ''ACTIVE''
          AND a.FirstPickTime IS NULL
          AND a.ExpireTime <= @AsOf;

        UPDATE r
        SET
            r.ReservationStatus = ''EXPIRED'',
            r.ReleasedTime = @AsOf,
            r.ReleaseReason = N''Reservation 超过 3 小时且尚未开始实际拣货''
        FROM dbo.WMS_HU_Reservation r
        INNER JOIN @Expired e ON e.AllocationId = r.AllocationId
        WHERE r.ReservationStatus = ''ACTIVE'';

        UPDATE b
        SET
            b.BatchStatus = ''EXPIRED'',
            b.CancelTime = @AsOf,
            b.CancelReason = N''Reservation expired''
        FROM dbo.WMS_OutboundPickBatch b
        INNER JOIN @Expired e ON e.AllocationId = b.AllocationId
        WHERE b.BatchStatus = ''ALLOCATED'';

        UPDATE a
        SET
            a.AllocationStatus = ''EXPIRED'',
            a.ReleaseTime = @AsOf,
            a.ReleaseReason = N''Reservation expired'',
            a.ModifyTime = @AsOf
        FROM dbo.WMS_OutboundAllocation a
        INNER JOIN @Expired e ON e.AllocationId = a.Id;

        UPDATE l
        SET
            l.AllocatedQty = 0,
            l.LineStatus = ''WAIT_PICK'',
            l.ModifyTime = @AsOf
        FROM dbo.WMS_OutboundOrderLine l
        INNER JOIN @Expired e ON e.OutboundOrderId = l.OutboundOrderId
        WHERE l.LineStatus = ''ALLOCATED'';

        UPDATE o
        SET
            o.OrderStatus = ''WAIT_PICK'',
            o.StartPickTime = NULL,
            o.ModifyTime = @AsOf
        FROM dbo.WMS_OutboundOrder o
        INNER JOIN @Expired e ON e.OutboundOrderId = o.Id
        WHERE o.OrderStatus = ''ALLOCATED'';

        SELECT COUNT(1) AS ExpiredAllocationCount FROM @Expired;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;';

    EXEC sys.sp_executesql @ExpireProcedureSql;

    ----------------------------------------------------------------------
    -- 8. Schema Version
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_SchemaVersion', N'U') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.WMS_SchemaVersion
           WHERE VersionNo = '1.0.0'
       )
    BEGIN
        INSERT INTO dbo.WMS_SchemaVersion(VersionNo, ScriptName, Remark)
        VALUES
        (
            '1.0.0',
            '014_OutboundV1Compatibility.sql',
            N'正式出库兼容迁移、批次快照、多替代箱、幂等动作与Reservation过期过程'
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
