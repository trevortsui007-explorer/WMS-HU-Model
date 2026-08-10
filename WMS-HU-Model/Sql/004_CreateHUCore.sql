USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY

    BEGIN TRAN;

    ------------------------------------------------------------
    -- 1. HU 主表
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_HU', 'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_HU
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_HU PRIMARY KEY,

            HUCode              VARCHAR(100) NOT NULL,

            ----------------------------------------------------
            -- PALLET
            -- OUTER_BOX
            -- INNER_PACK
            ----------------------------------------------------
            HUType              VARCHAR(30) NOT NULL,

            ----------------------------------------------------
            -- 库存所属厂别
            ----------------------------------------------------
            PlantCode           VARCHAR(20) NOT NULL,

            ----------------------------------------------------
            -- 外箱可关联箱型
            ----------------------------------------------------
            PackageSpecId       BIGINT NULL,

            ----------------------------------------------------
            -- ACTIVE / CLOSED / VOID
            ----------------------------------------------------
            LifecycleStatus     VARCHAR(20) NOT NULL
                                CONSTRAINT DF_WMS_HU_LifecycleStatus
                                DEFAULT ('ACTIVE'),

            ----------------------------------------------------
            -- 冻结和生命周期分开
            ----------------------------------------------------
            IsHold              BIT NOT NULL
                                CONSTRAINT DF_WMS_HU_IsHold
                                DEFAULT (0),

            HoldReason          NVARCHAR(500) NULL,

            ----------------------------------------------------
            -- 用于关联旧 MES / 包装系统
            ----------------------------------------------------
            SourceSystem        VARCHAR(30) NULL,

            SourceEntityId      VARCHAR(100) NULL,

            SourceBusinessNo    VARCHAR(100) NULL,

            Remark              NVARCHAR(500) NULL,

            CreateUser          VARCHAR(50) NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_HU_CreateTime
                                DEFAULT SYSDATETIME(),

            ModifyUser          VARCHAR(50) NULL,

            ModifyTime          DATETIME2(0) NULL,

            RowVersion          ROWVERSION,

            CONSTRAINT FK_WMS_HU_PackageSpec
                FOREIGN KEY (PackageSpecId)
                REFERENCES dbo.WMS_PackageSpec(Id)
        );


        CREATE UNIQUE INDEX UX_WMS_HU_HUCode
        ON dbo.WMS_HU(HUCode);


        CREATE INDEX IX_WMS_HU_TypePlant
        ON dbo.WMS_HU
        (
            HUType,
            PlantCode,
            LifecycleStatus
        );

    END;


    ------------------------------------------------------------
    -- 2. HU 包装关系
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_HU_Relation', 'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_HU_Relation
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_HU_Relation
                                PRIMARY KEY,

            ParentHUId          BIGINT NOT NULL,

            ChildHUId           BIGINT NOT NULL,

            RelationType        VARCHAR(30) NOT NULL
                                CONSTRAINT DF_WMS_HU_Relation_Type
                                DEFAULT ('CONTAIN'),

            EffectiveFrom       DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_HU_Relation_From
                                DEFAULT SYSDATETIME(),

            EffectiveTo         DATETIME2(0) NULL,

            IsActive            BIT NOT NULL
                                CONSTRAINT DF_WMS_HU_Relation_IsActive
                                DEFAULT (1),

            OperationNo         VARCHAR(50) NULL,

            CreateUser          VARCHAR(50) NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_HU_Relation_CreateTime
                                DEFAULT SYSDATETIME(),

            CloseUser           VARCHAR(50) NULL,

            CloseReason         NVARCHAR(500) NULL,

            CONSTRAINT FK_WMS_HU_Relation_Parent
                FOREIGN KEY (ParentHUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_HU_Relation_Child
                FOREIGN KEY (ChildHUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT CK_WMS_HU_Relation_NotSelf
                CHECK (ParentHUId <> ChildHUId)
        );


        --------------------------------------------------------
        -- 一个子HU同一时间只能属于一个父HU
        --------------------------------------------------------
        CREATE UNIQUE INDEX UX_WMS_HU_Relation_ActiveChild
        ON dbo.WMS_HU_Relation(ChildHUId)
        WHERE IsActive = 1;


        CREATE INDEX IX_WMS_HU_Relation_Parent
        ON dbo.WMS_HU_Relation
        (
            ParentHUId,
            IsActive
        );

    END;


    ------------------------------------------------------------
    -- 3. HU库存内容
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_HU_Item', 'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_HU_Item
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_HU_Item
                                PRIMARY KEY,

            HUId                BIGINT NOT NULL,

            MaterialCode        VARCHAR(50) NOT NULL,

            MaterialRevision    VARCHAR(30) NULL,

            BatchNo             VARCHAR(100) NULL,

            SAPBatchNo          VARCHAR(100) NULL,

            ProductionOrderNo   VARCHAR(50) NULL,

            CustomerCode        VARCHAR(50) NULL,

            SalesOrderNo        VARCHAR(50) NULL,

            SalesOrderItem      VARCHAR(30) NULL,

            DateCode            VARCHAR(50) NULL,

            Qty                 DECIMAL(18,4) NOT NULL,

            Uom                 VARCHAR(20) NOT NULL
                                CONSTRAINT DF_WMS_HU_Item_Uom
                                DEFAULT ('PCS'),

            ----------------------------------------------------
            -- AVAILABLE / HOLD / QUALITY / BLOCKED
            ----------------------------------------------------
            StockStatus         VARCHAR(20) NOT NULL
                                CONSTRAINT DF_WMS_HU_Item_StockStatus
                                DEFAULT ('AVAILABLE'),

            IsActive            BIT NOT NULL
                                CONSTRAINT DF_WMS_HU_Item_IsActive
                                DEFAULT (1),

            Remark              NVARCHAR(500) NULL,

            CreateUser          VARCHAR(50) NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_HU_Item_CreateTime
                                DEFAULT SYSDATETIME(),

            ModifyUser          VARCHAR(50) NULL,

            ModifyTime          DATETIME2(0) NULL,

            RowVersion          ROWVERSION,

            CONSTRAINT FK_WMS_HU_Item_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT CK_WMS_HU_Item_Qty
                CHECK (Qty > 0)
        );


        CREATE INDEX IX_WMS_HU_Item_HU
        ON dbo.WMS_HU_Item
        (
            HUId,
            IsActive
        );


        CREATE INDEX IX_WMS_HU_Item_Material
        ON dbo.WMS_HU_Item
        (
            MaterialCode,
            BatchNo,
            ProductionOrderNo
        );

    END;


    ------------------------------------------------------------
    -- 4. HU操作流水
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_HU_OperationLog', 'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_HU_OperationLog
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_HU_OperationLog
                                PRIMARY KEY,

            OperationNo         VARCHAR(50) NOT NULL,

            ----------------------------------------------------
            -- CREATE_HU
            -- ATTACH_HU
            -- DETACH_HU
            -- ADD_ITEM
            -- MOVE
            -- HOLD
            -- RELEASE
            -- CLOSE
            ----------------------------------------------------
            OperationType       VARCHAR(30) NOT NULL,

            HUId                BIGINT NOT NULL,

            RelatedHUId         BIGINT NULL,

            BusinessNo          VARCHAR(100) NULL,

            RequestId           VARCHAR(100) NULL,

            DetailJson          NVARCHAR(MAX) NULL,

            Operator            VARCHAR(50) NULL,

            OperationTime       DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_HU_OperationLog_Time
                                DEFAULT SYSDATETIME(),

            Remark              NVARCHAR(500) NULL,

            CONSTRAINT FK_WMS_HU_OperationLog_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_HU_OperationLog_RelatedHU
                FOREIGN KEY (RelatedHUId)
                REFERENCES dbo.WMS_HU(Id)
        );


        CREATE INDEX IX_WMS_HU_OperationLog_HU
        ON dbo.WMS_HU_OperationLog
        (
            HUId,
            OperationTime DESC
        );


        CREATE INDEX IX_WMS_HU_OperationLog_OperationNo
        ON dbo.WMS_HU_OperationLog(OperationNo);

    END;


    ------------------------------------------------------------
    -- Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.3.0'
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
            '0.3.0',
            '004_CreateHUCore.sql',
            N'建立HU主表、包装关系、库存内容和操作流水'
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