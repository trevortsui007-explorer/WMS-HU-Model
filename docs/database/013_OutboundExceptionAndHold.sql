USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    013_OutboundExceptionAndHold.sql

    目标：
    1. 创建 RETURNED 虚拟货位。
    2. 建立 HU Hold / Release 历史。
    3. 建立单箱出库异常记录。
*/

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_HU', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_HU。', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_Location', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_Location。', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_OutboundPickBatchDetail。', 1;
    END;

    ----------------------------------------------------------------------
    -- 1. RETURNED 虚拟货位
    --
    -- 当前 MasterData 已存在 RETURN-PENDING。
    -- 为避免依赖 WMS_Location 后续扩展列，本脚本动态复制 RETURN-PENDING，
    -- 再覆盖 RETURNED 需要改变的字段。
    ----------------------------------------------------------------------

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_Location
        WHERE LocationCode = 'RETURNED'
    )
    BEGIN
        DECLARE @SourceLocationId BIGINT;

        SELECT TOP 1
            @SourceLocationId = Id
        FROM dbo.WMS_Location
        WHERE LocationCode = 'RETURN-PENDING'
        ORDER BY Id;

        IF @SourceLocationId IS NULL
        BEGIN
            THROW 51000, N'未找到 RETURN-PENDING，无法自动创建 RETURNED 虚拟货位。', 1;
        END;

        DECLARE @InsertColumns NVARCHAR(MAX) = N'';
        DECLARE @SelectColumns NVARCHAR(MAX) = N'';
        DECLARE @CreateReturnedSql NVARCHAR(MAX);

        SELECT
            @InsertColumns =
                @InsertColumns
                + CASE
                    WHEN LEN(@InsertColumns) = 0 THEN N''
                    ELSE N','
                  END
                + QUOTENAME(c.name),

            @SelectColumns =
                @SelectColumns
                + CASE
                    WHEN LEN(@SelectColumns) = 0 THEN N''
                    ELSE N','
                  END
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
            N'INSERT INTO dbo.WMS_Location (' + @InsertColumns + N') '
            + N'SELECT ' + @SelectColumns + N' '
            + N'FROM dbo.WMS_Location src '
            + N'WHERE src.Id = @SourceLocationId;';

        EXEC sys.sp_executesql
            @CreateReturnedSql,
            N'@SourceLocationId BIGINT',
            @SourceLocationId = @SourceLocationId;
    END;

    ----------------------------------------------------------------------
    -- 2. HU Hold Log
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_HU_HoldLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_HU_HoldLog
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_HU_HoldLog PRIMARY KEY,

            HoldNo VARCHAR(50) NOT NULL,
            HUId BIGINT NOT NULL,

            HoldType VARCHAR(30) NOT NULL,
            HoldReasonCode VARCHAR(50) NOT NULL,
            HoldReason NVARCHAR(500) NULL,

            SourceBizType VARCHAR(50) NULL,
            SourceBizId BIGINT NULL,
            SourceBizNo VARCHAR(100) NULL,

            IsActive BIT NOT NULL
                CONSTRAINT DF_WMS_HU_HoldLog_IsActive DEFAULT (1),

            HoldTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_HU_HoldLog_HoldTime DEFAULT (SYSDATETIME()),

            HoldUser VARCHAR(50) NOT NULL,
            HoldRequestId VARCHAR(100) NULL,

            ReleaseTime DATETIME2(3) NULL,
            ReleaseUser VARCHAR(50) NULL,
            ReleaseReason NVARCHAR(500) NULL,
            ReleaseRequestId VARCHAR(100) NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_HU_HoldLog_CreateTime DEFAULT (SYSDATETIME()),

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_HU_HoldLog_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT CK_WMS_HU_HoldLog_Release
                CHECK
                (
                    (IsActive = 1 AND ReleaseTime IS NULL)
                    OR
                    (IsActive = 0 AND ReleaseTime IS NOT NULL)
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_HoldLog_No'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_HoldLog')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_HoldLog_No
        ON dbo.WMS_HU_HoldLog(HoldNo);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_HoldLog_ActiveHU'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_HoldLog')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_HoldLog_ActiveHU
        ON dbo.WMS_HU_HoldLog(HUId)
        WHERE IsActive = 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_HoldLog_HoldRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_HoldLog')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_HoldLog_HoldRequest
        ON dbo.WMS_HU_HoldLog(HoldRequestId)
        WHERE HoldRequestId IS NOT NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_HoldLog_ReleaseRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_HoldLog')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_HoldLog_ReleaseRequest
        ON dbo.WMS_HU_HoldLog(ReleaseRequestId)
        WHERE ReleaseRequestId IS NOT NULL;
    END;

    ----------------------------------------------------------------------
    -- 3. Outbound Pick Exception
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundPickException', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundPickException
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundPickException PRIMARY KEY,

            ExceptionNo VARCHAR(50) NOT NULL,

            OutboundOrderId BIGINT NOT NULL,
            PickBatchId BIGINT NOT NULL,

            ReservationId BIGINT NULL,
            PickDetailId BIGINT NULL,

            HUId BIGINT NOT NULL,

            ExceptionType VARCHAR(30) NOT NULL,
            ReasonCode VARCHAR(50) NOT NULL,
            Reason NVARCHAR(500) NULL,

            ExceptionStatus VARCHAR(30) NOT NULL
                CONSTRAINT DF_WMS_OutboundPickException_Status DEFAULT ('OPEN'),

            RequiresHold BIT NOT NULL
                CONSTRAINT DF_WMS_OutboundPickException_RequiresHold DEFAULT (0),

            HoldLogId BIGINT NULL,

            ReturnedLocationId BIGINT NULL,

            RaisedTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundPickException_RaisedTime DEFAULT (SYSDATETIME()),

            RaisedUser VARCHAR(50) NOT NULL,

            ReturnConfirmedTime DATETIME2(3) NULL,
            ReturnConfirmedUser VARCHAR(50) NULL,

            ReplacementReservationId BIGINT NULL,
            ReplacementPickDetailId BIGINT NULL,

            ResolvedTime DATETIME2(3) NULL,
            ResolvedUser VARCHAR(50) NULL,
            ResolutionCode VARCHAR(50) NULL,

            RequestId VARCHAR(100) NOT NULL,

            Remark NVARCHAR(500) NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundPickException_CreateTime DEFAULT (SYSDATETIME()),

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_OutboundPickException_Order
                FOREIGN KEY (OutboundOrderId)
                REFERENCES dbo.WMS_OutboundOrder(Id),

            CONSTRAINT FK_WMS_OutboundPickException_Batch
                FOREIGN KEY (PickBatchId)
                REFERENCES dbo.WMS_OutboundPickBatch(Id),

            CONSTRAINT FK_WMS_OutboundPickException_Reservation
                FOREIGN KEY (ReservationId)
                REFERENCES dbo.WMS_HU_Reservation(Id),

            CONSTRAINT FK_WMS_OutboundPickException_Detail
                FOREIGN KEY (PickDetailId)
                REFERENCES dbo.WMS_OutboundPickBatchDetail(Id),

            CONSTRAINT FK_WMS_OutboundPickException_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_OutboundPickException_HoldLog
                FOREIGN KEY (HoldLogId)
                REFERENCES dbo.WMS_HU_HoldLog(Id),

            CONSTRAINT FK_WMS_OutboundPickException_ReturnedLocation
                FOREIGN KEY (ReturnedLocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT FK_WMS_OutboundPickException_ReplacementReservation
                FOREIGN KEY (ReplacementReservationId)
                REFERENCES dbo.WMS_HU_Reservation(Id),

            CONSTRAINT FK_WMS_OutboundPickException_ReplacementDetail
                FOREIGN KEY (ReplacementPickDetailId)
                REFERENCES dbo.WMS_OutboundPickBatchDetail(Id),

            CONSTRAINT CK_WMS_OutboundPickException_Source
                CHECK
                (
                    ReservationId IS NOT NULL
                    OR PickDetailId IS NOT NULL
                ),

            CONSTRAINT CK_WMS_OutboundPickException_Status
                CHECK
                (
                    ExceptionStatus IN
                    (
                        'OPEN',
                        'RETURN_CONFIRMED',
                        'REPLACEMENT_RESERVED',
                        'RESOLVED',
                        'CANCELLED'
                    )
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundPickException_No'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickException')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundPickException_No
        ON dbo.WMS_OutboundPickException(ExceptionNo);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundPickException_Request'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickException')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundPickException_Request
        ON dbo.WMS_OutboundPickException(RequestId);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundPickException_Order'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundPickException')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundPickException_Order
        ON dbo.WMS_OutboundPickException
        (
            OutboundOrderId,
            PickBatchId,
            ExceptionStatus
        )
        INCLUDE
        (
            HUId,
            ExceptionType,
            ReasonCode,
            RequiresHold,
            RaisedTime
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
