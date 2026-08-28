USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_OutboundOrder', N'U') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required table dbo.WMS_OutboundOrder is missing.', 1;
    IF OBJECT_ID(N'dbo.WMS_OutboundOrderLine', N'U') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required table dbo.WMS_OutboundOrderLine is missing.', 1;
    IF OBJECT_ID(N'dbo.WMS_OutboundAllocation', N'U') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required table dbo.WMS_OutboundAllocation is missing.', 1;
    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatch', N'U') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required table dbo.WMS_OutboundPickBatch is missing.', 1;
    IF OBJECT_ID(N'dbo.WMS_OutboundPickBatchDetail', N'U') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required table dbo.WMS_OutboundPickBatchDetail is missing.', 1;
    IF OBJECT_ID(N'dbo.WMS_OutboundPickDetailItem', N'U') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required table dbo.WMS_OutboundPickDetailItem is missing.', 1;
    IF COL_LENGTH(N'dbo.WMS_OutboundPickDetailItem', N'BatchNo') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required column WMS_OutboundPickDetailItem.BatchNo is missing.', 1;
    IF COL_LENGTH(N'dbo.WMS_OutboundPickDetailItem', N'SAPBatchNo') IS NULL
        THROW 51000, 'HUMAN_REVIEW_REQUIRED: Required column WMS_OutboundPickDetailItem.SAPBatchNo is missing.', 1;

    IF OBJECT_ID(N'dbo.WMS_OutboundSapPosting', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundSapPosting
        (
            Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WMS_OutboundSapPosting PRIMARY KEY,
            OutboundOrderId BIGINT NOT NULL,
            SAPDeliveryNo VARCHAR(30) NOT NULL,
            RequestId VARCHAR(100) NOT NULL,
            CurrentAttemptNo INT NOT NULL CONSTRAINT DF_WMS_OutboundSapPosting_Attempt DEFAULT (0),
            PostingStatus VARCHAR(20) NOT NULL CONSTRAINT DF_WMS_OutboundSapPosting_Status DEFAULT ('READY'),
            PayloadJson NVARCHAR(MAX) NOT NULL,
            PayloadHash CHAR(64) NOT NULL,
            FirstSendTime DATETIME2(3) NULL,
            LastSendTime DATETIME2(3) NULL,
            SuccessTime DATETIME2(3) NULL,
            SapCode VARCHAR(20) NULL,
            SapMessage NVARCHAR(1000) NULL,
            ResponseJson NVARCHAR(MAX) NULL,
            RetryCount INT NOT NULL CONSTRAINT DF_WMS_OutboundSapPosting_Retry DEFAULT (0),
            SuccessConfirmSource VARCHAR(30) NULL,
            ManualConfirmUser VARCHAR(50) NULL,
            ManualConfirmTime DATETIME2(3) NULL,
            ManualConfirmRemark NVARCHAR(500) NULL,
            CreateUser VARCHAR(50) NOT NULL,
            CreateTime DATETIME2(3) NOT NULL CONSTRAINT DF_WMS_OutboundSapPosting_CreateTime DEFAULT (SYSDATETIME()),
            ModifyUser VARCHAR(50) NULL,
            ModifyTime DATETIME2(3) NULL,
            RowVersion ROWVERSION NOT NULL,
            CONSTRAINT FK_WMS_OutboundSapPosting_Order FOREIGN KEY (OutboundOrderId) REFERENCES dbo.WMS_OutboundOrder(Id),
            CONSTRAINT CK_WMS_OutboundSapPosting_Status CHECK (PostingStatus IN ('READY','SENDING','SUCCESS','FAILED','UNKNOWN')),
            CONSTRAINT CK_WMS_OutboundSapPosting_Attempt CHECK (CurrentAttemptNo >= 0 AND RetryCount >= 0),
            CONSTRAINT CK_WMS_OutboundSapPosting_Source CHECK (SuccessConfirmSource IS NULL OR SuccessConfirmSource IN ('SAP_RESPONSE','MANUAL_CONFIRM'))
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_OutboundSapPosting') AND name = N'UX_WMS_OutboundSapPosting_Delivery')
        CREATE UNIQUE INDEX UX_WMS_OutboundSapPosting_Delivery ON dbo.WMS_OutboundSapPosting(SAPDeliveryNo);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_OutboundSapPosting') AND name = N'UX_WMS_OutboundSapPosting_Order')
        CREATE UNIQUE INDEX UX_WMS_OutboundSapPosting_Order ON dbo.WMS_OutboundSapPosting(OutboundOrderId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_OutboundSapPosting') AND name = N'IX_WMS_OutboundSapPosting_Status')
        CREATE INDEX IX_WMS_OutboundSapPosting_Status ON dbo.WMS_OutboundSapPosting(PostingStatus, LastSendTime) INCLUDE(SAPDeliveryNo, SapCode, SapMessage);

    IF OBJECT_ID(N'dbo.WMS_OutboundSapPostingDetail', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_OutboundSapPostingDetail
        (
            Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WMS_OutboundSapPostingDetail PRIMARY KEY,
            PostingId BIGINT NOT NULL,
            [LineNo] INT NOT NULL,
            VBELN VARCHAR(30) NOT NULL,
            POSNR VARCHAR(20) NOT NULL,
            LGORT VARCHAR(20) NOT NULL,
            MATNR VARCHAR(50) NOT NULL,
            BOXNO VARCHAR(100) NOT NULL,
            CHARG VARCHAR(100) NOT NULL,
            LFIMG DECIMAL(18,4) NOT NULL,
            LFIMG_BOX DECIMAL(18,4) NOT NULL,
            NTGEW DECIMAL(18,4) NOT NULL,
            BRGEW DECIMAL(18,4) NOT NULL,
            GEWEI VARCHAR(10) NOT NULL,
            VRKME VARCHAR(20) NOT NULL,
            BOXSIZE VARCHAR(100) NOT NULL,
            CreateTime DATETIME2(3) NOT NULL CONSTRAINT DF_WMS_OutboundSapPostingDetail_CreateTime DEFAULT (SYSDATETIME()),
            CONSTRAINT FK_WMS_OutboundSapPostingDetail_Posting FOREIGN KEY (PostingId) REFERENCES dbo.WMS_OutboundSapPosting(Id),
            CONSTRAINT CK_WMS_OutboundSapPostingDetail_Qty CHECK (LFIMG > 0 AND LFIMG_BOX > 0 AND NTGEW >= 0 AND BRGEW >= 0)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_OutboundSapPostingDetail') AND name = N'UX_WMS_OutboundSapPostingDetail_Line')
        CREATE UNIQUE INDEX UX_WMS_OutboundSapPostingDetail_Line ON dbo.WMS_OutboundSapPostingDetail(PostingId, [LineNo]);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_OutboundSapPostingDetail') AND name = N'IX_WMS_OutboundSapPostingDetail_Business')
        CREATE INDEX IX_WMS_OutboundSapPostingDetail_Business ON dbo.WMS_OutboundSapPostingDetail(VBELN, POSNR, BOXNO, CHARG);

    IF OBJECT_ID(N'dbo.WMS_SapIntegrationLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_SapIntegrationLog
        (
            Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WMS_SapIntegrationLog PRIMARY KEY,
            InterfaceCode VARCHAR(50) NOT NULL,
            Direction VARCHAR(20) NOT NULL,
            BusinessType VARCHAR(50) NOT NULL,
            BusinessKey VARCHAR(100) NULL,
            RequestId VARCHAR(100) NULL,
            CorrelationId VARCHAR(100) NULL,
            AttemptNo INT NOT NULL CONSTRAINT DF_WMS_SapIntegrationLog_Attempt DEFAULT (0),
            RequestTime DATETIME2(3) NOT NULL,
            ResponseTime DATETIME2(3) NULL,
            DurationMs BIGINT NULL,
            RequestJson NVARCHAR(MAX) NULL,
            ResponseJson NVARCHAR(MAX) NULL,
            ResultStatus VARCHAR(20) NOT NULL,
            ExternalCode VARCHAR(50) NULL,
            ExternalMessage NVARCHAR(1000) NULL,
            HttpStatusCode INT NULL,
            ExceptionType VARCHAR(200) NULL,
            ExceptionMessage NVARCHAR(2000) NULL,
            PayloadHash CHAR(64) NULL,
            CreateTime DATETIME2(3) NOT NULL CONSTRAINT DF_WMS_SapIntegrationLog_CreateTime DEFAULT (SYSDATETIME()),
            CONSTRAINT CK_WMS_SapIntegrationLog_Direction CHECK (Direction IN ('INBOUND','OUTBOUND')),
            CONSTRAINT CK_WMS_SapIntegrationLog_Result CHECK (ResultStatus IN ('SENDING','SUCCESS','FAILED','UNKNOWN','REJECTED','DUPLICATE')),
            CONSTRAINT CK_WMS_SapIntegrationLog_Attempt CHECK (AttemptNo >= 0)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_SapIntegrationLog') AND name = N'IX_WMS_SapIntegrationLog_Search')
        CREATE INDEX IX_WMS_SapIntegrationLog_Search ON dbo.WMS_SapIntegrationLog(InterfaceCode, Direction, ResultStatus, RequestTime DESC) INCLUDE(BusinessKey, RequestId, AttemptNo, ExternalCode, DurationMs);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_SapIntegrationLog') AND name = N'IX_WMS_SapIntegrationLog_Business')
        CREATE INDEX IX_WMS_SapIntegrationLog_Business ON dbo.WMS_SapIntegrationLog(BusinessType, BusinessKey, RequestTime DESC);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WMS_SapIntegrationLog') AND name = N'UX_WMS_SapIntegrationLog_OutgoingRequest')
        CREATE UNIQUE INDEX UX_WMS_SapIntegrationLog_OutgoingRequest ON dbo.WMS_SapIntegrationLog(InterfaceCode, RequestId) WHERE Direction = 'OUTBOUND' AND RequestId IS NOT NULL;

    IF OBJECT_ID(N'dbo.WMS_SchemaVersion', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.WMS_SchemaVersion WHERE ScriptName = N'018_OutboundSapPosting.sql')
    BEGIN
        INSERT INTO dbo.WMS_SchemaVersion(VersionNo, ScriptName, Remark)
        VALUES (N'1.3.0', N'018_OutboundSapPosting.sql', N'VBELN aggregate SAP outbound posting snapshot, state and generic integration audit.');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
