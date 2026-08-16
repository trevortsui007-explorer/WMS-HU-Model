USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    010_WeekCodeAndHuIdentifier.sql

    目标：
    1. WMS_HU_Item 保留原始 DateCode，并增加标准周码排序快照。
    2. 建立 HU 多标识模型，为 MES 箱码 / 客户箱码 / 客户条码做基础。
*/

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_HU_Item', N'U') IS NULL
    BEGIN
        THROW 51000, N'缺少 dbo.WMS_HU_Item，请先执行 HU Core 基础脚本。', 1;
    END;

    ----------------------------------------------------------------------
    -- 1. WMS_HU_Item 周码标准字段
    ----------------------------------------------------------------------

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekFormatCode') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekFormatCode VARCHAR(30) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekYear') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekYear SMALLINT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekNo') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekNo TINYINT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekSortKey') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekSortKey INT NULL;
    END;

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekRuleSource') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekRuleSource VARCHAR(30) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekRuleValue') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekRuleValue VARCHAR(100) NULL;
    END;

    IF COL_LENGTH('dbo.WMS_HU_Item', 'WeekResolvedTime') IS NULL
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD WeekResolvedTime DATETIME2(3) NULL;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_HU_Item_WeekNo'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_HU_Item')
    )
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD CONSTRAINT CK_WMS_HU_Item_WeekNo
        CHECK
        (
            WeekNo IS NULL
            OR WeekNo BETWEEN 1 AND 53
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_WMS_HU_Item_WeekYear'
          AND parent_object_id = OBJECT_ID(N'dbo.WMS_HU_Item')
    )
    BEGIN
        ALTER TABLE dbo.WMS_HU_Item
        ADD CONSTRAINT CK_WMS_HU_Item_WeekYear
        CHECK
        (
            WeekYear IS NULL
            OR WeekYear BETWEEN 2000 AND 2199
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_HU_Item_OutboundWeek'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Item')
    )
    BEGIN
        CREATE INDEX IX_WMS_HU_Item_OutboundWeek
        ON dbo.WMS_HU_Item
        (
            MaterialCode,
            MaterialRevision,
            StockStatus,
            WeekSortKey,
            HUId
        )
        INCLUDE
        (
            Qty,
            Uom,
            DateCode
        )
        WHERE IsActive = 1
          AND WeekSortKey IS NOT NULL;
    END;

    ----------------------------------------------------------------------
    -- 2. HU 标识表
    ----------------------------------------------------------------------

    IF OBJECT_ID(N'dbo.WMS_HU_Identifier', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_HU_Identifier
        (
            Id BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_WMS_HU_Identifier PRIMARY KEY,

            HUId BIGINT NOT NULL,

            IdentifierType VARCHAR(30) NOT NULL,
            IdentifierValue VARCHAR(200) NOT NULL,

            CustomerCode VARCHAR(50) NULL,
            RuleCode VARCHAR(50) NULL,

            IsPrimary BIT NOT NULL
                CONSTRAINT DF_WMS_HU_Identifier_IsPrimary DEFAULT (0),

            IsActive BIT NOT NULL
                CONSTRAINT DF_WMS_HU_Identifier_IsActive DEFAULT (1),

            EffectiveFrom DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_HU_Identifier_EffectiveFrom DEFAULT (SYSDATETIME()),

            EffectiveTo DATETIME2(3) NULL,

            SourceSystem VARCHAR(30) NULL,
            SourceEntityId VARCHAR(100) NULL,

            Remark NVARCHAR(500) NULL,

            CreateUser VARCHAR(50) NULL,
            CreateTime DATETIME2(3) NOT NULL
                CONSTRAINT DF_WMS_HU_Identifier_CreateTime DEFAULT (SYSDATETIME()),

            ModifyUser VARCHAR(50) NULL,
            ModifyTime DATETIME2(3) NULL,

            RowVersion ROWVERSION NOT NULL,

            CONSTRAINT FK_WMS_HU_Identifier_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT CK_WMS_HU_Identifier_EffectiveTime
                CHECK
                (
                    EffectiveTo IS NULL
                    OR EffectiveTo >= EffectiveFrom
                )
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_WMS_HU_Identifier_HU'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Identifier')
    )
    BEGIN
        CREATE INDEX IX_WMS_HU_Identifier_HU
        ON dbo.WMS_HU_Identifier
        (
            HUId,
            IsActive,
            IdentifierType
        )
        INCLUDE
        (
            IdentifierValue,
            CustomerCode,
            IsPrimary
        );
    END;

    /*
        CustomerCode 为空的内部码：
        MES_BOX_ID / MES_BOX_BARCODE 等按 IdentifierType + Value 全局唯一。
    */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_Identifier_GlobalActive'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Identifier')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_Identifier_GlobalActive
        ON dbo.WMS_HU_Identifier
        (
            IdentifierType,
            IdentifierValue
        )
        WHERE IsActive = 1
          AND CustomerCode IS NULL;
    END;

    /*
        客户码：
        同一个客户下，同一种类型、同一个码只能对应一个 Active HU。
    */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_Identifier_CustomerActive'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Identifier')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_Identifier_CustomerActive
        ON dbo.WMS_HU_Identifier
        (
            CustomerCode,
            IdentifierType,
            IdentifierValue
        )
        WHERE IsActive = 1
          AND CustomerCode IS NOT NULL;
    END;

    /*
        同一个 HU + Customer + IdentifierType
        最多一个 Active 标识。
    */
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_WMS_HU_Identifier_HUCustomerType'
          AND object_id = OBJECT_ID(N'dbo.WMS_HU_Identifier')
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_HU_Identifier_HUCustomerType
        ON dbo.WMS_HU_Identifier
        (
            HUId,
            CustomerCode,
            IdentifierType
        )
        WHERE IsActive = 1
          AND CustomerCode IS NOT NULL;
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
