CREATE TABLE dbo.WMS_SchemaVersion
(
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    VersionNo       VARCHAR(20) NOT NULL,
    ScriptName      NVARCHAR(200) NOT NULL,
    Remark          NVARCHAR(500) NULL,
    AppliedAt       DATETIME2(0) NOT NULL
                    CONSTRAINT DF_WMS_SchemaVersion_AppliedAt
                    DEFAULT SYSDATETIME()
);
GO

INSERT INTO dbo.WMS_SchemaVersion
(
    VersionNo,
    ScriptName,
    Remark
)
VALUES
(
    '0.1.0',
    '002_CreateSchemaVersion.sql',
    N'初始化WMS开发数据库'
);
GO