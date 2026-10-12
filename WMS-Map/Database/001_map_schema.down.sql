-- Empty-install rollback ONLY. Never drops a layout or its history.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.FG_MapLayout)
    THROW 51001, 'Rollback refused: map data exists. Restore a reviewed backup instead.', 1;
DROP TABLE dbo.FG_MapOperationLog;
DROP TABLE dbo.FG_MapLayoutVersionBinding;
DROP TABLE dbo.FG_MapLayoutVersionWarehouse;
DROP TABLE dbo.FG_MapLayoutDraft;
ALTER TABLE dbo.FG_MapLayout DROP CONSTRAINT FK_FG_MapLayout_Current;
DROP TABLE dbo.FG_MapLayoutVersion;
DROP TABLE dbo.FG_MapLayout;
COMMIT;
