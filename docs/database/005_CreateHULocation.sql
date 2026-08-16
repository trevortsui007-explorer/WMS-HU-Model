USE [WMS_DEV];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY

    BEGIN TRAN;

    ------------------------------------------------------------
    -- 1. HU移动历史
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_HU_MoveLog', 'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_HU_MoveLog
        (
            Id                      BIGINT IDENTITY(1,1) NOT NULL
                                    CONSTRAINT PK_WMS_HU_MoveLog
                                    PRIMARY KEY,

            MoveNo                  VARCHAR(50) NOT NULL,

            HUId                    BIGINT NOT NULL,

            ----------------------------------------------------
            -- 第一次进入WMS时允许From为空
            ----------------------------------------------------
            FromLocationId          BIGINT NULL,

            ToLocationId            BIGINT NOT NULL,

            ----------------------------------------------------
            -- RECEIVE
            -- PUTAWAY
            -- RELOCATION
            -- RETURN
            -- STAGE
            -- CORRECTION
            ----------------------------------------------------
            MoveType                VARCHAR(30) NOT NULL,

            ----------------------------------------------------
            -- 实际扫描的货位
            -- 以后如果扫描H201、系统分配H201-01时有意义
            ----------------------------------------------------
            ScannedLocationCode     VARCHAR(100) NULL,

            BusinessNo              VARCHAR(100) NULL,

            TaskNo                  VARCHAR(100) NULL,

            ----------------------------------------------------
            -- Postman/手持终端请求唯一号
            -- 用于防止重复提交
            ----------------------------------------------------
            RequestId               VARCHAR(100) NULL,

            ReasonCode              VARCHAR(50) NULL,

            Operator                VARCHAR(50) NULL,

            MoveTime                DATETIME2(0) NOT NULL
                                    CONSTRAINT DF_WMS_HU_MoveLog_MoveTime
                                    DEFAULT SYSDATETIME(),

            Remark                  NVARCHAR(500) NULL,

            CONSTRAINT FK_WMS_HU_MoveLog_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_HU_MoveLog_FromLocation
                FOREIGN KEY (FromLocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT FK_WMS_HU_MoveLog_ToLocation
                FOREIGN KEY (ToLocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT CK_WMS_HU_MoveLog_Location
                CHECK
                (
                    FromLocationId IS NULL
                    OR FromLocationId <> ToLocationId
                )
        );


        CREATE UNIQUE INDEX UX_WMS_HU_MoveLog_MoveNo
        ON dbo.WMS_HU_MoveLog(MoveNo);


        --------------------------------------------------------
        -- 防扫码枪/网络重试造成重复移动
        --------------------------------------------------------
        CREATE UNIQUE INDEX UX_WMS_HU_MoveLog_RequestId
        ON dbo.WMS_HU_MoveLog(RequestId)
        WHERE RequestId IS NOT NULL;


        CREATE INDEX IX_WMS_HU_MoveLog_HU
        ON dbo.WMS_HU_MoveLog
        (
            HUId,
            MoveTime DESC
        );


        CREATE INDEX IX_WMS_HU_MoveLog_ToLocation
        ON dbo.WMS_HU_MoveLog
        (
            ToLocationId,
            MoveTime DESC
        );

    END;


    ------------------------------------------------------------
    -- 2. HU当前直接位置
    ------------------------------------------------------------
    IF OBJECT_ID('dbo.WMS_HU_CurrentLocation', 'U') IS NULL
    BEGIN

        CREATE TABLE dbo.WMS_HU_CurrentLocation
        (
            ----------------------------------------------------
            -- 一个HU最多一条直接位置
            ----------------------------------------------------
            HUId                    BIGINT NOT NULL
                                    CONSTRAINT PK_WMS_HU_CurrentLocation
                                    PRIMARY KEY,

            LocationId              BIGINT NOT NULL,

            ----------------------------------------------------
            -- 何时开始位于当前位置
            ----------------------------------------------------
            LocationSince           DATETIME2(0) NOT NULL,

            LastMoveLogId           BIGINT NOT NULL,

            LastMoveNo              VARCHAR(50) NOT NULL,

            LastMoveType            VARCHAR(30) NOT NULL,

            LastOperator            VARCHAR(50) NULL,

            ModifyTime              DATETIME2(0) NOT NULL
                                    CONSTRAINT DF_WMS_HU_CurrentLocation_ModifyTime
                                    DEFAULT SYSDATETIME(),

            RowVersion              ROWVERSION,

            CONSTRAINT FK_WMS_HU_CurrentLocation_HU
                FOREIGN KEY (HUId)
                REFERENCES dbo.WMS_HU(Id),

            CONSTRAINT FK_WMS_HU_CurrentLocation_Location
                FOREIGN KEY (LocationId)
                REFERENCES dbo.WMS_Location(Id),

            CONSTRAINT FK_WMS_HU_CurrentLocation_LastMove
                FOREIGN KEY (LastMoveLogId)
                REFERENCES dbo.WMS_HU_MoveLog(Id)
        );


        CREATE INDEX IX_WMS_HU_CurrentLocation_Location
        ON dbo.WMS_HU_CurrentLocation(LocationId);

    END;


    ------------------------------------------------------------
    -- Schema Version
    ------------------------------------------------------------
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_SchemaVersion
        WHERE VersionNo = '0.4.0'
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
            '0.4.0',
            '005_CreateHULocation.sql',
            N'建立HU当前位置与移动历史模型'
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