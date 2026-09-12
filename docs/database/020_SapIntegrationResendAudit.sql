SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_FG_SapIntegrationLog', N'U') IS NULL
        THROW 50001, 'WMS_FG_SapIntegrationLog does not exist.', 1;

    IF COL_LENGTH(N'dbo.WMS_FG_SapIntegrationLog', N'RequestId') IS NULL
        ALTER TABLE dbo.WMS_FG_SapIntegrationLog ADD RequestId VARCHAR(100) NULL;

    IF COL_LENGTH(N'dbo.WMS_FG_SapIntegrationLog', N'TriggerUser') IS NULL
        ALTER TABLE dbo.WMS_FG_SapIntegrationLog ADD TriggerUser VARCHAR(50) NULL;

    IF COL_LENGTH(N'dbo.WMS_FG_SapIntegrationLog', N'TriggerRemark') IS NULL
        ALTER TABLE dbo.WMS_FG_SapIntegrationLog ADD TriggerRemark NVARCHAR(500) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.WMS_FG_SapIntegrationLog')
          AND name = N'UX_WMS_FG_SapIntegrationLog_RequestId'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_WMS_FG_SapIntegrationLog_RequestId
            ON dbo.WMS_FG_SapIntegrationLog(RequestId)
            WHERE RequestId IS NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
