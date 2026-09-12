SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_FG_Warehouse', N'U') IS NULL
        THROW 51019, 'Required table dbo.WMS_FG_Warehouse is missing.', 1;

    IF OBJECT_ID(N'dbo.WMS_FG_MapLayout', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_FG_MapLayout
        (
            Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WMS_FG_MapLayout PRIMARY KEY,
            WarehouseId bigint NOT NULL,
            LayoutCode varchar(50) NOT NULL,
            LayoutName nvarchar(100) NOT NULL,
            FloorNo int NOT NULL,
            CoordinateMode varchar(20) NOT NULL,
            CoordinateUnit varchar(20) NOT NULL,
            MinX decimal(18,4) NOT NULL,
            MinY decimal(18,4) NOT NULL,
            MaxX decimal(18,4) NOT NULL,
            MaxY decimal(18,4) NOT NULL,
            SourceType varchar(20) NOT NULL,
            BackgroundAsset nvarchar(500) NULL,
            VersionNo int NOT NULL,
            Status varchar(20) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_WMS_FG_MapLayout_IsActive DEFAULT (0),
            Remark nvarchar(500) NULL,
            CreateDt datetime2(0) NOT NULL CONSTRAINT DF_WMS_FG_MapLayout_CreateDt DEFAULT (SYSDATETIME()),
            CreateBy varchar(50) NULL,
            LastEditDt datetime2(0) NULL,
            LastEditBy varchar(50) NULL,
            CONSTRAINT FK_WMS_FG_MapLayout_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.WMS_FG_Warehouse(Id),
            CONSTRAINT CK_WMS_FG_MapLayout_Bounds CHECK (MaxX > MinX AND MaxY > MinY),
            CONSTRAINT CK_WMS_FG_MapLayout_Floor CHECK (FloorNo > 0),
            CONSTRAINT CK_WMS_FG_MapLayout_Status CHECK (Status IN ('DRAFT','PUBLISHED','ARCHIVED')),
            CONSTRAINT CK_WMS_FG_MapLayout_ActiveStatus CHECK
            (
                (Status='DRAFT' AND IsActive=0)
                OR (Status='PUBLISHED' AND IsActive=1)
                OR (Status='ARCHIVED' AND IsActive=0)
            )
        );
        CREATE UNIQUE INDEX UX_WMS_FG_MapLayout_CodeVersion ON dbo.WMS_FG_MapLayout(WarehouseId, LayoutCode, FloorNo, VersionNo);
        CREATE UNIQUE INDEX UX_WMS_FG_MapLayout_ActivePublished ON dbo.WMS_FG_MapLayout(WarehouseId, FloorNo) WHERE IsActive=1 AND Status='PUBLISHED';
    END;
    ELSE
    BEGIN
        DECLARE @ActiveStatusDefinition nvarchar(max);
        SELECT @ActiveStatusDefinition=UPPER(REPLACE(REPLACE(REPLACE(REPLACE(definition,N' ',N''),N'[',N''),N']',N''),N'(',N''))
        FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.WMS_FG_MapLayout')
          AND name=N'CK_WMS_FG_MapLayout_ActiveStatus';

        IF @ActiveStatusDefinition IS NULL
           OR @ActiveStatusDefinition NOT LIKE N'%STATUS=''DRAFT''ANDISACTIVE=0%'
           OR @ActiveStatusDefinition NOT LIKE N'%STATUS=''PUBLISHED''ANDISACTIVE=1%'
           OR @ActiveStatusDefinition NOT LIKE N'%STATUS=''ARCHIVED''ANDISACTIVE=0%'
            THROW 51019, 'MapLayout ActiveStatus CHECK is missing or differently named; human schema review is required.', 1;

        IF NOT EXISTS
        (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id=dc.parent_object_id AND c.column_id=dc.parent_column_id
            WHERE dc.parent_object_id=OBJECT_ID(N'dbo.WMS_FG_MapLayout')
              AND c.name=N'IsActive'
              AND REPLACE(REPLACE(dc.definition,N'(',N''),N')',N'')=N'0'
        )
            THROW 51019, 'MapLayout IsActive default differs from 0; human schema review is required.', 1;
    END;

    IF OBJECT_ID(N'dbo.WMS_FG_MapObject', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WMS_FG_MapObject
        (
            Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_WMS_FG_MapObject PRIMARY KEY,
            MapLayoutId bigint NOT NULL,
            ParentObjectId bigint NULL,
            ObjectCode varchar(80) NOT NULL,
            ObjectName nvarchar(120) NULL,
            ObjectType varchar(30) NOT NULL,
            BindingType varchar(30) NULL,
            BindingCode varchar(100) NULL,
            GeometryType varchar(30) NOT NULL,
            X decimal(18,4) NOT NULL,
            Y decimal(18,4) NOT NULL,
            Z decimal(18,4) NOT NULL,
            SizeX decimal(18,4) NOT NULL,
            SizeY decimal(18,4) NOT NULL,
            SizeZ decimal(18,4) NOT NULL,
            RotationZ decimal(18,4) NOT NULL,
            GeometryJson nvarchar(max) NULL,
            StyleKey varchar(50) NULL,
            StyleJson nvarchar(max) NULL,
            LayerCode varchar(50) NULL,
            SortNo int NOT NULL CONSTRAINT DF_WMS_FG_MapObject_SortNo DEFAULT (0),
            IsVisible bit NOT NULL CONSTRAINT DF_WMS_FG_MapObject_IsVisible DEFAULT (1),
            Remark nvarchar(500) NULL,
            CreateDt datetime2(0) NOT NULL CONSTRAINT DF_WMS_FG_MapObject_CreateDt DEFAULT (SYSDATETIME()),
            CreateBy varchar(50) NULL,
            LastEditDt datetime2(0) NULL,
            LastEditBy varchar(50) NULL,
            CONSTRAINT FK_WMS_FG_MapObject_Layout FOREIGN KEY (MapLayoutId) REFERENCES dbo.WMS_FG_MapLayout(Id),
            CONSTRAINT FK_WMS_FG_MapObject_Parent FOREIGN KEY (ParentObjectId) REFERENCES dbo.WMS_FG_MapObject(Id),
            CONSTRAINT CK_WMS_FG_MapObject_Size CHECK (SizeX >= 0 AND SizeY >= 0 AND SizeZ >= 0),
            CONSTRAINT CK_WMS_FG_MapObject_Binding CHECK
            (
                (BindingType IS NULL AND BindingCode IS NULL)
                OR (BindingType='NONE' AND BindingCode IS NULL)
                OR (BindingType IS NOT NULL AND BindingType<>'NONE' AND BindingCode IS NOT NULL)
            )
        );
        CREATE UNIQUE INDEX UX_WMS_FG_MapObject_Code ON dbo.WMS_FG_MapObject(MapLayoutId, ObjectCode);
        CREATE UNIQUE INDEX UX_WMS_FG_MapObject_LocationBinding ON dbo.WMS_FG_MapObject(MapLayoutId, BindingCode) WHERE BindingType='LOCATION';
        CREATE INDEX IX_WMS_FG_MapObject_Parent ON dbo.WMS_FG_MapObject(ParentObjectId) WHERE ParentObjectId IS NOT NULL;
    END;

    IF OBJECT_ID(N'dbo.WMS_SchemaVersion', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.WMS_SchemaVersion WHERE ScriptName=N'019_FinishedGoodsWarehouseMap.sql')
        INSERT dbo.WMS_SchemaVersion(VersionNo,ScriptName,Remark)
        VALUES(N'1.3.0',N'019_FinishedGoodsWarehouseMap.sql',N'Finished-goods warehouse physical map layout and geometry.');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
