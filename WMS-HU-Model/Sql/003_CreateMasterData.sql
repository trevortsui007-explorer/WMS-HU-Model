USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRAN;

    ------------------------------------------------------------
    -- 1. 物理仓库
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_Warehouse', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_Warehouse
        (
            Id              BIGINT IDENTITY(1,1) NOT NULL
                            CONSTRAINT PK_WMS_Warehouse PRIMARY KEY,

            WarehouseCode   VARCHAR(30) NOT NULL,

            WarehouseName   NVARCHAR(100) NOT NULL,

            WarehouseType   VARCHAR(30) NOT NULL
                            CONSTRAINT DF_WMS_Warehouse_Type
                            DEFAULT ('FINISHED_GOODS'),

            IsActive        BIT NOT NULL
                            CONSTRAINT DF_WMS_Warehouse_IsActive
                            DEFAULT (1),

            Remark          NVARCHAR(500) NULL,

            CreateUser      VARCHAR(50) NULL,

            CreateTime      DATETIME2(0) NOT NULL
                            CONSTRAINT DF_WMS_Warehouse_CreateTime
                            DEFAULT SYSDATETIME(),

            ModifyUser      VARCHAR(50) NULL,

            ModifyTime      DATETIME2(0) NULL,

            RowVersion      ROWVERSION
        );

        CREATE UNIQUE INDEX UX_WMS_Warehouse_WarehouseCode
        ON dbo.WMS_Warehouse(WarehouseCode);
    END;


    ------------------------------------------------------------
    -- 2. 仓库允许的厂别
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_WarehousePlant', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_WarehousePlant
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_WarehousePlant PRIMARY KEY,

            WarehouseId         BIGINT NOT NULL,

            PlantCode           VARCHAR(20) NOT NULL,

            SAPStorageLocation  VARCHAR(20) NULL,

            IsActive            BIT NOT NULL
                                CONSTRAINT DF_WMS_WarehousePlant_IsActive
                                DEFAULT (1),

            Remark              NVARCHAR(500) NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_WarehousePlant_CreateTime
                                DEFAULT SYSDATETIME(),

            CONSTRAINT FK_WMS_WarehousePlant_Warehouse
                FOREIGN KEY (WarehouseId)
                REFERENCES dbo.WMS_Warehouse(Id)
        );

        CREATE UNIQUE INDEX UX_WMS_WarehousePlant
        ON dbo.WMS_WarehousePlant(WarehouseId, PlantCode);
    END;


    ------------------------------------------------------------
    -- 3. 功能区域
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_Zone', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_Zone
        (
            Id              BIGINT IDENTITY(1,1) NOT NULL
                            CONSTRAINT PK_WMS_Zone PRIMARY KEY,

            WarehouseId     BIGINT NOT NULL,

            ZoneCode        VARCHAR(30) NOT NULL,

            ZoneName        NVARCHAR(100) NOT NULL,

            ZoneType        VARCHAR(30) NOT NULL,

            IsActive        BIT NOT NULL
                            CONSTRAINT DF_WMS_Zone_IsActive
                            DEFAULT (1),

            SortNo          INT NOT NULL
                            CONSTRAINT DF_WMS_Zone_SortNo
                            DEFAULT (0),

            Remark          NVARCHAR(500) NULL,

            CreateTime      DATETIME2(0) NOT NULL
                            CONSTRAINT DF_WMS_Zone_CreateTime
                            DEFAULT SYSDATETIME(),

            CONSTRAINT FK_WMS_Zone_Warehouse
                FOREIGN KEY (WarehouseId)
                REFERENCES dbo.WMS_Warehouse(Id)
        );

        CREATE UNIQUE INDEX UX_WMS_Zone
        ON dbo.WMS_Zone(WarehouseId, ZoneCode);
    END;


    ------------------------------------------------------------
    -- 4. 货位
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_Location', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_Location
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                                CONSTRAINT PK_WMS_Location PRIMARY KEY,

            WarehouseId         BIGINT NOT NULL,

            ZoneId              BIGINT NOT NULL,

            LocationCode        VARCHAR(50) NOT NULL,

            LocationName        NVARCHAR(100) NULL,

            ----------------------------------------------------
            -- 为未来 H201-01 等更细粒度预留
            ----------------------------------------------------
            ParentLocationId    BIGINT NULL,

            LocationLevel       VARCHAR(20) NOT NULL
                                CONSTRAINT DF_WMS_Location_Level
                                DEFAULT ('BASE'),

            ----------------------------------------------------
            -- DOUBLE_RACK / FLOOR / REMNANT_RACK / VIRTUAL
            ----------------------------------------------------
            LocationTypeCode    VARCHAR(30) NOT NULL,

            ----------------------------------------------------
            -- CAPACITY_LOCATION
            -- SINGLE_SLOT
            -- LOGICAL_SLOT
            -- VIRTUAL
            ----------------------------------------------------
            ManageMode          VARCHAR(30) NOT NULL,

            IsVirtual           BIT NOT NULL
                                CONSTRAINT DF_WMS_Location_IsVirtual
                                DEFAULT (0),

            IsScannable         BIT NOT NULL
                                CONSTRAINT DF_WMS_Location_IsScannable
                                DEFAULT (1),

            ScanCode            VARCHAR(100) NULL,

            ----------------------------------------------------
            -- STACK_POSITION
            -- BOX_QTY
            -- UNLIMITED
            ----------------------------------------------------
            CapacityMode        VARCHAR(30) NOT NULL,

            ----------------------------------------------------
            -- H201 例如 4 个栈位
            -- 注意不是“4箱”
            ----------------------------------------------------
            CapacityQty         DECIMAL(18,4) NULL,

            MaxWeightKg         DECIMAL(18,4) NULL,

            ----------------------------------------------------
            -- UNRESTRICTED
            -- SAME_PLANT
            -- DEDICATED
            -- PREFERRED
            ----------------------------------------------------
            PlantPolicy         VARCHAR(20) NOT NULL
                                CONSTRAINT DF_WMS_Location_PlantPolicy
                                DEFAULT ('SAME_PLANT'),

            DedicatedPlantCode  VARCHAR(20) NULL,

            AllowMixedMaterial  BIT NOT NULL
                                CONSTRAINT DF_WMS_Location_MixedMaterial
                                DEFAULT (0),

            AllowMixedBatch     BIT NOT NULL
                                CONSTRAINT DF_WMS_Location_MixedBatch
                                DEFAULT (0),

            AllowMixedOrder     BIT NOT NULL
                                CONSTRAINT DF_WMS_Location_MixedOrder
                                DEFAULT (0),

            PutawayPriority     INT NOT NULL
                                CONSTRAINT DF_WMS_Location_PutawayPriority
                                DEFAULT (100),

            PickingPriority     INT NOT NULL
                                CONSTRAINT DF_WMS_Location_PickingPriority
                                DEFAULT (100),

            RouteSequence       INT NOT NULL
                                CONSTRAINT DF_WMS_Location_RouteSequence
                                DEFAULT (0),

            ----------------------------------------------------
            -- ACTIVE / DISABLED / MAINTENANCE / CLOSED
            ----------------------------------------------------
            MasterStatus        VARCHAR(20) NOT NULL
                                CONSTRAINT DF_WMS_Location_Status
                                DEFAULT ('ACTIVE'),

            Remark              NVARCHAR(500) NULL,

            CreateUser          VARCHAR(50) NULL,

            CreateTime          DATETIME2(0) NOT NULL
                                CONSTRAINT DF_WMS_Location_CreateTime
                                DEFAULT SYSDATETIME(),

            ModifyUser          VARCHAR(50) NULL,

            ModifyTime          DATETIME2(0) NULL,

            RowVersion          ROWVERSION,

            CONSTRAINT FK_WMS_Location_Warehouse
                FOREIGN KEY (WarehouseId)
                REFERENCES dbo.WMS_Warehouse(Id),

            CONSTRAINT FK_WMS_Location_Zone
                FOREIGN KEY (ZoneId)
                REFERENCES dbo.WMS_Zone(Id),

            CONSTRAINT FK_WMS_Location_Parent
                FOREIGN KEY (ParentLocationId)
                REFERENCES dbo.WMS_Location(Id)
        );

        CREATE UNIQUE INDEX UX_WMS_Location_Code
        ON dbo.WMS_Location(WarehouseId, LocationCode);

        CREATE UNIQUE INDEX UX_WMS_Location_ScanCode
        ON dbo.WMS_Location(WarehouseId, ScanCode)
        WHERE ScanCode IS NOT NULL;

        CREATE INDEX IX_WMS_Location_Zone
        ON dbo.WMS_Location(ZoneId, MasterStatus);
    END;


    ------------------------------------------------------------
    -- 5. 外包装箱规格
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_PackageSpec', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_PackageSpec
        (
            Id                      BIGINT IDENTITY(1,1) NOT NULL
                                    CONSTRAINT PK_WMS_PackageSpec PRIMARY KEY,

            PackageSpecCode         VARCHAR(30) NOT NULL,

            PackageSpecName         NVARCHAR(100) NOT NULL,

            LengthMm                DECIMAL(18,2) NULL,

            WidthMm                 DECIMAL(18,2) NULL,

            HeightMm                DECIMAL(18,2) NULL,

            IsStackable             BIT NOT NULL
                                    CONSTRAINT DF_WMS_PackageSpec_Stackable
                                    DEFAULT (1),

            MaxStackLayers          INT NULL,

            ----------------------------------------------------
            -- 占用多少个“栈位当量”
            ----------------------------------------------------
            StackPositionEquivalent DECIMAL(18,4) NULL,

            IsActive                BIT NOT NULL
                                    CONSTRAINT DF_WMS_PackageSpec_IsActive
                                    DEFAULT (1),

            Remark                  NVARCHAR(500) NULL,

            CreateTime              DATETIME2(0) NOT NULL
                                    CONSTRAINT DF_WMS_PackageSpec_CreateTime
                                    DEFAULT SYSDATETIME(),

            ModifyTime              DATETIME2(0) NULL,

            RowVersion              ROWVERSION
        );

        CREATE UNIQUE INDEX UX_WMS_PackageSpec_Code
        ON dbo.WMS_PackageSpec(PackageSpecCode);
    END;


    ------------------------------------------------------------
    -- 初始化物理仓库
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_Warehouse
        WHERE WarehouseCode = 'FG01'
    )
    BEGIN
        INSERT INTO dbo.WMS_Warehouse
        (
            WarehouseCode,
            WarehouseName,
            WarehouseType,
            Remark
        )
        VALUES
        (
            'FG01',
            N'成品仓',
            'FINISHED_GOODS',
            N'PCB成品物理仓库'
        );
    END;


    DECLARE @WarehouseId BIGINT;

    SELECT @WarehouseId = Id
    FROM dbo.WMS_Warehouse
    WHERE WarehouseCode = 'FG01';


    ------------------------------------------------------------
    -- 多厂别
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_WarehousePlant
        WHERE WarehouseId = @WarehouseId
          AND PlantCode = '1010'
    )
        INSERT INTO dbo.WMS_WarehousePlant
        (WarehouseId, PlantCode)
        VALUES (@WarehouseId, '1010');


    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_WarehousePlant
        WHERE WarehouseId = @WarehouseId
          AND PlantCode = '1020'
    )
        INSERT INTO dbo.WMS_WarehousePlant
        (WarehouseId, PlantCode)
        VALUES (@WarehouseId, '1020');


    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_WarehousePlant
        WHERE WarehouseId = @WarehouseId
          AND PlantCode = '9010'
    )
        INSERT INTO dbo.WMS_WarehousePlant
        (WarehouseId, PlantCode)
        VALUES (@WarehouseId, '9010');


    ------------------------------------------------------------
    -- 静态区域
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Zone
        WHERE WarehouseId = @WarehouseId
          AND ZoneCode = 'STORAGE'
    )
        INSERT INTO dbo.WMS_Zone
        (WarehouseId, ZoneCode, ZoneName, ZoneType, SortNo)
        VALUES
        (@WarehouseId, 'STORAGE', N'成品储存区', 'STORAGE', 10);


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Zone
        WHERE WarehouseId = @WarehouseId
          AND ZoneCode = 'RECEIVE'
    )
        INSERT INTO dbo.WMS_Zone
        (WarehouseId, ZoneCode, ZoneName, ZoneType, SortNo)
        VALUES
        (@WarehouseId, 'RECEIVE', N'成品收货区', 'RECEIVE', 20);


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Zone
        WHERE WarehouseId = @WarehouseId
          AND ZoneCode = 'RETURN'
    )
        INSERT INTO dbo.WMS_Zone
        (WarehouseId, ZoneCode, ZoneName, ZoneType, SortNo)
        VALUES
        (@WarehouseId, 'RETURN', N'退货区', 'RETURN', 30);


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Zone
        WHERE WarehouseId = @WarehouseId
          AND ZoneCode = 'STAGING'
    )
        INSERT INTO dbo.WMS_Zone
        (WarehouseId, ZoneCode, ZoneName, ZoneType, SortNo)
        VALUES
        (@WarehouseId, 'STAGING', N'出库备货区', 'STAGING', 40);


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Zone
        WHERE WarehouseId = @WarehouseId
          AND ZoneCode = 'SYSTEM'
    )
        INSERT INTO dbo.WMS_Zone
        (WarehouseId, ZoneCode, ZoneName, ZoneType, SortNo)
        VALUES
        (@WarehouseId, 'SYSTEM', N'系统虚拟区域', 'SYSTEM', 99);


    ------------------------------------------------------------
    -- 建立几个关键虚拟货位
    ------------------------------------------------------------
    DECLARE @ReceiveZoneId BIGINT;
    DECLARE @ReturnZoneId BIGINT;
    DECLARE @StagingZoneId BIGINT;
    DECLARE @SystemZoneId BIGINT;

    SELECT @ReceiveZoneId = Id
    FROM dbo.WMS_Zone
    WHERE WarehouseId = @WarehouseId
      AND ZoneCode = 'RECEIVE';

    SELECT @ReturnZoneId = Id
    FROM dbo.WMS_Zone
    WHERE WarehouseId = @WarehouseId
      AND ZoneCode = 'RETURN';

    SELECT @StagingZoneId = Id
    FROM dbo.WMS_Zone
    WHERE WarehouseId = @WarehouseId
      AND ZoneCode = 'STAGING';

    SELECT @SystemZoneId = Id
    FROM dbo.WMS_Zone
    WHERE WarehouseId = @WarehouseId
      AND ZoneCode = 'SYSTEM';


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Location
        WHERE WarehouseId = @WarehouseId
          AND LocationCode = 'INBOUND-PENDING'
    )
    BEGIN
        INSERT INTO dbo.WMS_Location
        (
            WarehouseId,
            ZoneId,
            LocationCode,
            LocationName,
            LocationTypeCode,
            ManageMode,
            IsVirtual,
            IsScannable,
            CapacityMode,
            PlantPolicy,
            AllowMixedMaterial,
            AllowMixedBatch,
            AllowMixedOrder
        )
        VALUES
        (
            @WarehouseId,
            @ReceiveZoneId,
            'INBOUND-PENDING',
            N'待上架',
            'VIRTUAL',
            'VIRTUAL',
            1,
            0,
            'UNLIMITED',
            'UNRESTRICTED',
            1,1,1
        );
    END;


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Location
        WHERE WarehouseId = @WarehouseId
          AND LocationCode = 'RETURN-PENDING'
    )
    BEGIN
        INSERT INTO dbo.WMS_Location
        (
            WarehouseId,
            ZoneId,
            LocationCode,
            LocationName,
            LocationTypeCode,
            ManageMode,
            IsVirtual,
            IsScannable,
            CapacityMode,
            PlantPolicy,
            AllowMixedMaterial,
            AllowMixedBatch,
            AllowMixedOrder
        )
        VALUES
        (
            @WarehouseId,
            @ReturnZoneId,
            'RETURN-PENDING',
            N'退货待处理',
            'VIRTUAL',
            'VIRTUAL',
            1,
            0,
            'UNLIMITED',
            'UNRESTRICTED',
            1,1,1
        );
    END;


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Location
        WHERE WarehouseId = @WarehouseId
          AND LocationCode = 'OUTBOUND-STAGING'
    )
    BEGIN
        INSERT INTO dbo.WMS_Location
        (
            WarehouseId,
            ZoneId,
            LocationCode,
            LocationName,
            LocationTypeCode,
            ManageMode,
            IsVirtual,
            IsScannable,
            CapacityMode,
            PlantPolicy,
            AllowMixedMaterial,
            AllowMixedBatch,
            AllowMixedOrder
        )
        VALUES
        (
            @WarehouseId,
            @StagingZoneId,
            'OUTBOUND-STAGING',
            N'出库待发',
            'VIRTUAL',
            'VIRTUAL',
            1,
            0,
            'UNLIMITED',
            'UNRESTRICTED',
            1,1,1
        );
    END;


    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WMS_Location
        WHERE WarehouseId = @WarehouseId
          AND LocationCode = 'UNLOCATED'
    )
    BEGIN
        INSERT INTO dbo.WMS_Location
        (
            WarehouseId,
            ZoneId,
            LocationCode,
            LocationName,
            LocationTypeCode,
            ManageMode,
            IsVirtual,
            IsScannable,
            CapacityMode,
            PlantPolicy,
            AllowMixedMaterial,
            AllowMixedBatch,
            AllowMixedOrder
        )
        VALUES
        (
            @WarehouseId,
            @SystemZoneId,
            'UNLOCATED',
            N'位置未知',
            'VIRTUAL',
            'VIRTUAL',
            1,
            0,
            'UNLIMITED',
            'UNRESTRICTED',
            1,1,1
        );
    END;


    ------------------------------------------------------------
    -- Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.2.0'
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
            '0.2.0',
            '003_CreateMasterData.sql',
            N'建立仓库、厂别、区域、货位、箱型基础模型'
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