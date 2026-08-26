USE [WMS_DEV];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WMS_Location', N'U') IS NULL
        THROW 51000, 'Required table dbo.WMS_Location is missing.', 1;

    IF COL_LENGTH(N'dbo.WMS_Location', N'PhysicalRackGroupCode') IS NULL
        ALTER TABLE dbo.WMS_Location ADD PhysicalRackGroupCode varchar(30) NULL;

    IF COL_LENGTH(N'dbo.WMS_Location', N'PhysicalRowCode') IS NULL
        ALTER TABLE dbo.WMS_Location ADD PhysicalRowCode varchar(30) NULL;

    IF COL_LENGTH(N'dbo.WMS_Location', N'PositionSequence') IS NULL
        ALTER TABLE dbo.WMS_Location ADD PositionSequence int NULL;

    CREATE TABLE #ConfirmedHTopology
    (
        LocationCode varchar(50) NOT NULL PRIMARY KEY,
        PhysicalRackGroupCode varchar(30) NOT NULL,
        PhysicalRowCode varchar(30) NOT NULL,
        PositionSequence int NOT NULL
    );

    INSERT INTO #ConfirmedHTopology(LocationCode, PhysicalRackGroupCode, PhysicalRowCode, PositionSequence)
    VALUES
                ('H101','H12','H1',1),('H102','H12','H1',2),('H103','H12','H1',3),('H104','H12','H1',4),
                ('H105','H12','H1',5),('H106','H12','H1',6),('H107','H12','H1',7),('H108','H12','H1',8),
                ('H109','H12','H1',9),('H110','H12','H1',10),('H111','H12','H1',11),('H112','H12','H1',12),
                ('H201','H12','H2',1),('H202','H12','H2',2),('H203','H12','H2',3),('H204','H12','H2',4),
                ('H205','H12','H2',5),('H206','H12','H2',6),('H207','H12','H2',7),('H208','H12','H2',8),
                ('H209','H12','H2',9),('H210','H12','H2',10),('H211','H12','H2',11),('H212','H12','H2',12),
                ('H301','H34','H3',1),('H302','H34','H3',2),('H303','H34','H3',3),('H304','H34','H3',4),
                ('H305','H34','H3',5),('H306','H34','H3',6),('H307','H34','H3',7),('H308','H34','H3',8),
                ('H309','H34','H3',9),('H310','H34','H3',10),
                ('H401','H34','H4',1),('H402','H34','H4',2),('H403','H34','H4',3),('H404','H34','H4',4),
                ('H405','H34','H4',5),('H406','H34','H4',6),('H407','H34','H4',7),('H408','H34','H4',8),
                ('H409','H34','H4',9),('H410','H34','H4',10),
                ('H501','H56','H5',1),('H502','H56','H5',2),('H503','H56','H5',3),('H504','H56','H5',4),
                ('H505','H56','H5',5),('H506','H56','H5',6),('H507','H56','H5',7),('H508','H56','H5',8),
                ('H509','H56','H5',9),('H510','H56','H5',10),('H511','H56','H5',11),
                ('H601','H56','H6',1),('H602','H56','H6',2),('H603','H56','H6',3),('H604','H56','H6',4),
                ('H605','H56','H6',5),('H606','H56','H6',6),('H607','H56','H6',7),('H608','H56','H6',8),
                ('H609','H56','H6',9),('H610','H56','H6',10),('H611','H56','H6',11),
                ('H701','H7','H7',1),('H702','H7','H7',2),('H703','H7','H7',3),('H704','H7','H7',4),
                ('H705','H7','H7',5),('H706','H7','H7',6),('H707','H7','H7',7),('H708','H7','H7',8),
                ('H709','H7','H7',9),('H710','H7','H7',10),
                ('H801','H89','H8',1),('H802','H89','H8',2),('H803','H89','H8',3),('H804','H89','H8',4),
                ('H805','H89','H8',5),('H806','H89','H8',6),('H807','H89','H8',7),('H808','H89','H8',8),
                ('H809','H89','H8',9),('H810','H89','H8',10),('H811','H89','H8',11),('H812','H89','H8',12),
                ('H901','H89','H9',1),('H902','H89','H9',2),('H903','H89','H9',3),('H904','H89','H9',4),
                ('H905','H89','H9',5),('H906','H89','H9',6),('H907','H89','H9',7),('H908','H89','H9',8),
                ('H909','H89','H9',9),('H910','H89','H9',10),('H911','H89','H9',11),('H912','H89','H9',12),
                ('H913','H89','H9',13);

    EXEC sys.sp_executesql N'
        UPDATE location
        SET PhysicalRackGroupCode = topology.PhysicalRackGroupCode,
            PhysicalRowCode = topology.PhysicalRowCode,
            PositionSequence = topology.PositionSequence
        FROM dbo.WMS_Location location
        INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = location.WarehouseId
        INNER JOIN #ConfirmedHTopology topology ON topology.LocationCode = location.LocationCode
        WHERE warehouse.WarehouseCode = ''FG01''
          AND
          (
              ISNULL(location.PhysicalRackGroupCode, '''') <> topology.PhysicalRackGroupCode
              OR ISNULL(location.PhysicalRowCode, '''') <> topology.PhysicalRowCode
              OR ISNULL(location.PositionSequence, -1) <> topology.PositionSequence
          );

        IF EXISTS
        (
            SELECT 1
            FROM dbo.WMS_Location
            WHERE PhysicalRowCode IS NOT NULL AND PositionSequence IS NOT NULL
            GROUP BY WarehouseId, PhysicalRowCode, PositionSequence
            HAVING COUNT(1) > 1
        )
            THROW 51001, ''Duplicate WarehouseId + PhysicalRowCode + PositionSequence topology exists.'', 1;
    ';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.WMS_Location')
          AND name = N'UX_WMS_Location_PhysicalRowPosition'
    )
        EXEC sys.sp_executesql N'
            CREATE UNIQUE INDEX UX_WMS_Location_PhysicalRowPosition
                ON dbo.WMS_Location(WarehouseId, PhysicalRowCode, PositionSequence)
                WHERE PhysicalRowCode IS NOT NULL AND PositionSequence IS NOT NULL;
        ';

    IF OBJECT_ID(N'dbo.WMS_SchemaVersion', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.WMS_SchemaVersion WHERE ScriptName = N'017_LocationPhysicalTopology.sql')
    BEGIN
        INSERT INTO dbo.WMS_SchemaVersion(VersionNo, ScriptName, Remark)
        VALUES (N'1.2.0', N'017_LocationPhysicalTopology.sql', N'Physical rack/row/position topology for V1_DYNAMIC_CLUSTER putaway recommendations.');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
