-- Run manually against the approved database. This script never touches WMS_FG business tables.
-- SQL Server 2016+ (ISJSON). A repeat or partially pre-existing installation fails without changes.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.FG_MapLayout') IS NOT NULL
    OR OBJECT_ID('dbo.FG_MapLayoutDraft') IS NOT NULL
    OR OBJECT_ID('dbo.FG_MapLayoutVersion') IS NOT NULL
    OR OBJECT_ID('dbo.FG_MapLayoutVersionWarehouse') IS NOT NULL
    OR OBJECT_ID('dbo.FG_MapLayoutVersionBinding') IS NOT NULL
    OR OBJECT_ID('dbo.FG_MapOperationLog') IS NOT NULL
    THROW 51000, 'Map tables already exist. Review migration history before continuing.', 1;

CREATE TABLE dbo.FG_MapLayout (
    Id uniqueidentifier NOT NULL CONSTRAINT PK_FG_MapLayout PRIMARY KEY,
    LayoutCode nvarchar(64) NOT NULL CONSTRAINT UQ_FG_MapLayout_Code UNIQUE,
    LayoutName nvarchar(150) NOT NULL,
    CurrentPublishedVersion int NULL,
    IsActive bit NOT NULL CONSTRAINT DF_FG_MapLayout_Active DEFAULT 1,
    CreateBy nvarchar(128) NOT NULL, CreateDt datetimeoffset(3) NOT NULL,
    LastEditBy nvarchar(128) NULL, LastEditDt datetimeoffset(3) NULL
);
CREATE TABLE dbo.FG_MapLayoutVersion (
    LayoutId uniqueidentifier NOT NULL,
    PublishedVersion int NOT NULL CHECK (PublishedVersion > 0),
    SchemaVersion int NOT NULL CHECK (SchemaVersion > 0),
    SnapshotJson nvarchar(max) NOT NULL CHECK (ISJSON(SnapshotJson)=1),
    SourceDraftRevision bigint NOT NULL CHECK (SourceDraftRevision BETWEEN 0 AND 9007199254740991),
    BasedOnPublishedVersion int NULL,
    PublishedBy nvarchar(128) NOT NULL, PublishedDt datetimeoffset(3) NOT NULL,
    PublishNote nvarchar(500) NULL, PublishRequestId uniqueidentifier NOT NULL,
    CreateBy nvarchar(128) NOT NULL, CreateDt datetimeoffset(3) NOT NULL,
    LastEditBy nvarchar(128) NULL, LastEditDt datetimeoffset(3) NULL,
    CONSTRAINT PK_FG_MapLayoutVersion PRIMARY KEY(LayoutId,PublishedVersion),
    CONSTRAINT FK_FG_MapVersion_Layout FOREIGN KEY(LayoutId) REFERENCES dbo.FG_MapLayout(Id),
    CONSTRAINT UQ_FG_MapVersion_Request UNIQUE(LayoutId,PublishRequestId),
    CONSTRAINT FK_FG_MapVersion_Base FOREIGN KEY(LayoutId,BasedOnPublishedVersion)
        REFERENCES dbo.FG_MapLayoutVersion(LayoutId,PublishedVersion)
);
ALTER TABLE dbo.FG_MapLayout ADD CONSTRAINT FK_FG_MapLayout_Current
    FOREIGN KEY(Id,CurrentPublishedVersion) REFERENCES dbo.FG_MapLayoutVersion(LayoutId,PublishedVersion);

CREATE TABLE dbo.FG_MapLayoutDraft (
    LayoutId uniqueidentifier NOT NULL CONSTRAINT PK_FG_MapLayoutDraft PRIMARY KEY,
    DraftRevision bigint NOT NULL CHECK (DraftRevision BETWEEN 0 AND 9007199254740991),
    BasedOnPublishedVersion int NULL,
    SchemaVersion int NOT NULL CHECK (SchemaVersion > 0),
    LayoutJson nvarchar(max) NOT NULL CHECK (ISJSON(LayoutJson)=1),
    SavedBy nvarchar(128) NOT NULL, SavedDt datetimeoffset(3) NOT NULL,
    CreateBy nvarchar(128) NOT NULL, CreateDt datetimeoffset(3) NOT NULL,
    LastEditBy nvarchar(128) NULL, LastEditDt datetimeoffset(3) NULL,
    CONSTRAINT FK_FG_MapDraft_Layout FOREIGN KEY(LayoutId) REFERENCES dbo.FG_MapLayout(Id),
    CONSTRAINT FK_FG_MapDraft_Base FOREIGN KEY(LayoutId,BasedOnPublishedVersion)
        REFERENCES dbo.FG_MapLayoutVersion(LayoutId,PublishedVersion)
);
CREATE TABLE dbo.FG_MapLayoutVersionWarehouse (
    LayoutId uniqueidentifier NOT NULL, PublishedVersion int NOT NULL,
    WarehouseCode nvarchar(20) NOT NULL,
    CreateBy nvarchar(128) NOT NULL, CreateDt datetimeoffset(3) NOT NULL,
    LastEditBy nvarchar(128) NULL, LastEditDt datetimeoffset(3) NULL,
    CONSTRAINT PK_FG_MapVersionWarehouse PRIMARY KEY(LayoutId,PublishedVersion,WarehouseCode),
    CONSTRAINT FK_FG_MapWarehouse_Version FOREIGN KEY(LayoutId,PublishedVersion)
        REFERENCES dbo.FG_MapLayoutVersion(LayoutId,PublishedVersion)
);
CREATE TABLE dbo.FG_MapLayoutVersionBinding (
    LayoutId uniqueidentifier NOT NULL, PublishedVersion int NOT NULL,
    BindingId nvarchar(120) NOT NULL, TargetKind varchar(20) NOT NULL,
    TargetObjectId nvarchar(120) NOT NULL,
    WarehouseCode nvarchar(20) NOT NULL, WmsLocationId bigint NOT NULL CHECK(WmsLocationId>0),
    LocationCode nvarchar(50) NOT NULL, LocationKind varchar(16) NOT NULL,
    DisplayName nvarchar(150) NULL, Purpose nvarchar(50) NULL, IsActive bit NOT NULL,
    CreateBy nvarchar(128) NOT NULL, CreateDt datetimeoffset(3) NOT NULL,
    LastEditBy nvarchar(128) NULL, LastEditDt datetimeoffset(3) NULL,
    CONSTRAINT PK_FG_MapVersionBinding PRIMARY KEY(LayoutId,PublishedVersion,BindingId),
    CONSTRAINT UQ_FG_MapBinding_Code UNIQUE(LayoutId,PublishedVersion,WarehouseCode,LocationCode),
    CONSTRAINT UQ_FG_MapBinding_WmsId UNIQUE(LayoutId,PublishedVersion,WmsLocationId),
    CONSTRAINT FK_FG_MapBinding_Warehouse FOREIGN KEY(LayoutId,PublishedVersion,WarehouseCode)
        REFERENCES dbo.FG_MapLayoutVersionWarehouse(LayoutId,PublishedVersion,WarehouseCode),
    CONSTRAINT CK_FG_MapBinding_Kind CHECK (
        (TargetKind IN ('RACK','SLOT','FLOOR_POSITION') AND LocationKind='PHYSICAL') OR
        (TargetKind='AREA' AND LocationKind='VIRTUAL'))
);
CREATE UNIQUE INDEX UX_FG_MapBinding_PhysicalTarget
    ON dbo.FG_MapLayoutVersionBinding(LayoutId,PublishedVersion,TargetObjectId) WHERE LocationKind='PHYSICAL';
CREATE INDEX IX_FG_MapBinding_Location
    ON dbo.FG_MapLayoutVersionBinding(WarehouseCode,LocationCode,LayoutId,PublishedVersion);

CREATE TABLE dbo.FG_MapOperationLog (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FG_MapOperationLog PRIMARY KEY,
    LayoutId uniqueidentifier NOT NULL, Action varchar(40) NOT NULL,
    DraftRevision bigint NULL, PublishedVersion int NULL, RequestId uniqueidentifier NOT NULL,
    Summary nvarchar(500) NULL, DetailsJson nvarchar(max) NULL CHECK(DetailsJson IS NULL OR ISJSON(DetailsJson)=1),
    CreateBy nvarchar(128) NOT NULL, CreateDt datetimeoffset(3) NOT NULL,
    LastEditBy nvarchar(128) NULL, LastEditDt datetimeoffset(3) NULL,
    CONSTRAINT FK_FG_MapLog_Layout FOREIGN KEY(LayoutId) REFERENCES dbo.FG_MapLayout(Id)
);
CREATE INDEX IX_FG_MapLog_LayoutDate ON dbo.FG_MapOperationLog(LayoutId,CreateDt DESC);
CREATE INDEX IX_FG_MapLog_Request ON dbo.FG_MapOperationLog(RequestId);
COMMIT;
