USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    011_OutboundOrder.sql

    目标：
    1. 接收 SAP 主动推送的交货单。
    2. 保留 SAP Header / Item 原始业务事实。
    3. 同一 VBELN 支持多个 MATNR、多个 POSNR。
    4. 第一版不支持同一 VBELN 的业务内容覆盖修改。
*/

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_Warehouse', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_Warehouse，请先执行基础主数据脚本。', 1;
    END;

    ----------------------------------------------------------------------
    -- 1. Outbound Order Header
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundOrder', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundOrder
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundOrder PRIMARY KEY,

            OutboundOrderNo VARCHAR(30) NOT NULL,
            SAPDeliveryNo VARCHAR(30) NOT NULL,

            WarehouseId BIGINT NOT NULL,

            PlantCode VARCHAR(20) NOT NULL,
            SAPStorageLocation VARCHAR(20) NULL,

            DeliveryType VARCHAR(10) NULL,
            CustomerCode VARCHAR(50) NULL,
            SoldToCode VARCHAR(50) NULL,
            SalesOrgCode VARCHAR(20) NULL,

            PlannedShipDate DATE NULL,
            PlannedShipTime TIME(0) NULL,

            ShippingType VARCHAR(20) NULL,
            SpecialHandlingCode VARCHAR(20) NULL,
            BorderTransportMode VARCHAR(10) NULL,

            SAPCreatedBy VARCHAR(50) NULL,
            SAPCreateDate DATE NULL,

            CustomerAddress NVARCHAR(300) NULL,
            ContactName NVARCHAR(100) NULL,
            Telephone VARCHAR(50) NULL,

            OrderStatus VARCHAR(30) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrder_Status DEFAULT ('WAIT_PICK'),

            SourceSystem VARCHAR(30) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrder_SourceSystem DEFAULT ('SAP'),

            ReceivedRequestId VARCHAR(100) NULL,
            SourcePayloadHash CHAR(64) NULL,
            SourcePayloadJson NVARCHAR(MAX) NULL,

            ReceivedTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrder_ReceivedTime DEFAULT (SYSDATETIME()),

            StartPickTime DATETIME2(3) NULL,
            PickedTime DATETIME2(3) NULL,
            ShippedTime DATETIME2(3) NULL,

            VoidTime DATETIME2(3) NULL,
            VoidReason NVARCHAR(500) NULL,

            Remark NVARCHAR(500) NULL,

            CreateUser VARCHAR(50) NULL,
            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrder_CreateTime DEFAULT (SYSDATETIME()),

            ModifyUser VARCHAR(50) NULL,
            ModifyTime DATETIME2(3) NULL,

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_OutboundOrder_Warehouse
                FOREIGN KEY (WarehouseId)
                REFERENCES dbo.WMS_Warehouse(Id),

            CONSTRAINT CK_WMS_OutboundOrder_Status
                CHECK
                (
                    OrderStatus IN
                    (
                        'WAIT_PICK',
                        'ALLOCATED',
                        'PICKING',
                        'PICKED',
                        'EXCEPTION',
                        'SHIPPED',
                        'VOID'
                    )
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrder_No'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrder')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundOrder_No
        ON dbo.WMS_OutboundOrder(OutboundOrderNo);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrder_SAPDeliveryNo'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrder')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundOrder_SAPDeliveryNo
        ON dbo.WMS_OutboundOrder(SAPDeliveryNo);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrder_ReceiveRequest'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrder')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundOrder_ReceiveRequest
        ON dbo.WMS_OutboundOrder(ReceivedRequestId)
        WHERE ReceivedRequestId IS NOT NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundOrder_Status'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrder')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundOrder_Status
        ON dbo.WMS_OutboundOrder
        (
            OrderStatus,
            PlannedShipDate,
            ReceivedTime
        )
        INCLUDE
        (
            SAPDeliveryNo,
            PlantCode,
            SAPStorageLocation,
            CustomerCode
        );
    END;

    ----------------------------------------------------------------------
    -- 2. Outbound Order Line
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundOrderLine', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundOrderLine
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundOrderLine PRIMARY KEY,

            OutboundOrderId BIGINT NOT NULL,

            SAPItemNo VARCHAR(20) NOT NULL,

            MaterialCode VARCHAR(50) NOT NULL,
            MaterialRevision VARCHAR(30) NULL,

            RequiredQty DECIMAL(18,4) NOT NULL,
            SpareQty DECIMAL(18,4) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_SpareQty DEFAULT (0),

            SAPGiftQty DECIMAL(18,4) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_SAPGiftQty DEFAULT (0),

            GiftQty DECIMAL(18,4) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_GiftQty DEFAULT (0),

            EffectiveRequiredQty DECIMAL(18,4) NOT NULL,

            Uom VARCHAR(20) NOT NULL,

            PlantCode VARCHAR(20) NOT NULL,
            SAPStorageLocation VARCHAR(20) NULL,

            ItemCategory VARCHAR(20) NULL,
            MaterialFreightGroup VARCHAR(20) NULL,
            ItemDescription NVARCHAR(200) NULL,

            SourceOrderNo VARCHAR(30) NULL,
            SourceOrderItem VARCHAR(20) NULL,

            CustomerPO VARCHAR(100) NULL,
            CustomerMaterialCode VARCHAR(100) NULL,
            FinalCustomerPO VARCHAR(100) NULL,

            GiftQtySource VARCHAR(50) NULL,
            GiftQtyResolvedTime DATETIME2(3) NULL,

            AllocatedQty DECIMAL(18,4) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_AllocatedQty DEFAULT (0),

            EffectivePickedQty DECIMAL(18,4) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_EffectivePickedQty DEFAULT (0),

            ShippedQty DECIMAL(18,4) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_ShippedQty DEFAULT (0),

            LineStatus VARCHAR(30) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_Status DEFAULT ('WAIT_PICK'),

            SourcePayloadJson NVARCHAR(MAX) NULL,
            Remark NVARCHAR(500) NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderLine_CreateTime DEFAULT (SYSDATETIME()),

            ModifyTime DATETIME2(3) NULL,

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_OutboundOrderLine_Order
                FOREIGN KEY (OutboundOrderId)
                REFERENCES dbo.WMS_OutboundOrder(Id),

            CONSTRAINT CK_WMS_OutboundOrderLine_RequiredQty
                CHECK
                (
                    RequiredQty >= 0
                    AND SpareQty >= 0
                    AND SAPGiftQty >= 0
                    AND GiftQty >= 0
                    AND EffectiveRequiredQty >= 0
                    AND AllocatedQty >= 0
                    AND EffectivePickedQty >= 0
                    AND ShippedQty >= 0
                ),

            CONSTRAINT CK_WMS_OutboundOrderLine_Status
                CHECK
                (
                    LineStatus IN
                    (
                        'WAIT_PICK',
                        'ALLOCATED',
                        'PICKING',
                        'PICKED',
                        'EXCEPTION',
                        'SHIPPED',
                        'VOID'
                    )
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrderLine_OrderItem'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrderLine')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundOrderLine_OrderItem
        ON dbo.WMS_OutboundOrderLine
        (
            OutboundOrderId,
            SAPItemNo
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundOrderLine_Material'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrderLine')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundOrderLine_Material
        ON dbo.WMS_OutboundOrderLine
        (
            OutboundOrderId,
            MaterialCode,
            MaterialRevision,
            Uom
        )
        INCLUDE
        (
            SAPItemNo,
            EffectiveRequiredQty,
            AllocatedQty,
            EffectivePickedQty,
            LineStatus
        );
    END;

    ----------------------------------------------------------------------
    -- 3. SAP Receive Log
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_OutboundOrderReceiveLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundOrderReceiveLog
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_OutboundOrderReceiveLog PRIMARY KEY,

            RequestId VARCHAR(100) NULL,
            InterfaceCode VARCHAR(50) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderReceiveLog_Interface DEFAULT ('SAP_OUTBOUND_ORDER'),

            SourceSystem VARCHAR(30) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderReceiveLog_Source DEFAULT ('SAP'),

            SAPDeliveryNo VARCHAR(30) NULL,
            OutboundOrderId BIGINT NULL,

            RequestHash CHAR(64) NULL,
            RequestJson NVARCHAR(MAX) NULL,

            ReceiveTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderReceiveLog_ReceiveTime DEFAULT (SYSDATETIME()),

            ProcessStatus VARCHAR(20) NOT NULL,
            ResultCode VARCHAR(30) NULL,
            ResultMessage NVARCHAR(1000) NULL,

            DurationMs INT NULL,

            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_OutboundOrderReceiveLog_CreateTime DEFAULT (SYSDATETIME()),

            CONSTRAINT FK_WMS_OutboundOrderReceiveLog_Order
                FOREIGN KEY (OutboundOrderId)
                REFERENCES dbo.WMS_OutboundOrder(Id),

            CONSTRAINT CK_WMS_OutboundOrderReceiveLog_Status
                CHECK
                (
                    ProcessStatus IN
                    (
                        'SUCCESS',
                        'FAILED',
                        'DUPLICATE',
                        'REJECTED'
                    )
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_OutboundOrderReceiveLog_Request'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrderReceiveLog')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_OutboundOrderReceiveLog_Request
        ON dbo.WMS_OutboundOrderReceiveLog(RequestId)
        WHERE RequestId IS NOT NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_OutboundOrderReceiveLog_Delivery'
          AND object_id = OBJECT_ID(N'dbo.WMS_OutboundOrderReceiveLog')
    )
    BEGIN
        CREATE INDEX IX_WMS_OutboundOrderReceiveLog_Delivery
        ON dbo.WMS_OutboundOrderReceiveLog
        (
            SAPDeliveryNo,
            ReceiveTime
        )
        INCLUDE
        (
            ProcessStatus,
            ResultCode,
            OutboundOrderId
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
