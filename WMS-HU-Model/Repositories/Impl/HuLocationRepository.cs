using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class HuLocationRepository : IHuLocationRepository
    {
        public const string MoveLogsSql = @"
SELECT TOP (@Top)
    m.Id,
    m.MoveNo,
    h.HUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    m.MoveType,
    toLoc.LocationCode AS ScannedLocationCode,
    m.BusinessNo,
    CAST(NULL AS VARCHAR(100)) AS TaskNo,
    m.RequestId,
    m.ReasonCode,
    m.MoveBy AS Operator,
    m.MoveDt AS MoveTime,
    m.Remark
FROM dbo.WMS_FG_HU_MoveLog m
INNER JOIN dbo.WMS_FG_HU h ON h.Id = m.HUId
LEFT JOIN dbo.WMS_FG_Location fromLoc ON fromLoc.Id = m.FromLocationId
INNER JOIN dbo.WMS_FG_Location toLoc ON toLoc.Id = m.ToLocationId
WHERE m.HUId = @HUId
ORDER BY m.MoveDt DESC, m.Id DESC;";

        public bool WarehouseAllowsPlant(
    string warehouseCode,
    string plantCode)
        {
            const string sql = @"
SELECT COUNT(1)

FROM dbo.WMS_FG_WarehousePlant wp

INNER JOIN dbo.WMS_FG_Warehouse w
    ON w.Id = wp.WarehouseId

WHERE
    w.WarehouseCode = @WarehouseCode
AND wp.PlantCode = @PlantCode
AND wp.IsActive = 1
AND w.IsActive = 1;
";

            return DbExecutor.ExecuteScalar<int>(
                sql,
                new
                {
                    WarehouseCode = warehouseCode,
                    PlantCode = plantCode
                }) > 0;
        }

        public int CountDifferentPlantAtLocation(
    long locationId,
    string plantCode)
        {
            const string sql = @"
SELECT COUNT(1)

FROM dbo.WMS_FG_HU_CurrentLocation cl

INNER JOIN dbo.WMS_FG_HU h
    ON h.Id = cl.HUId

WHERE
    cl.LocationId = @LocationId
AND h.PlantCode <> @PlantCode
AND h.LifecycleStatus = 'ACTIVE';
";

            return DbExecutor.ExecuteScalar<int>(
                sql,
                new
                {
                    LocationId = locationId,
                    PlantCode = plantCode
                });
        }

        public HuMoveLogDto GetMoveByRequestId(
    string requestId)
        {
            if (string.IsNullOrWhiteSpace(
                requestId))
                return null;

            const string sql = @"
SELECT TOP 1
    m.Id AS MoveLogId,
    m.MoveNo,
    h.HUCode,
    fromLoc.LocationCode
        AS FromLocationCode,
    toLoc.LocationCode
        AS ToLocationCode,
    m.MoveType,
    toLoc.LocationCode AS ScannedLocationCode,
    m.BusinessNo,
    CAST(NULL AS VARCHAR(100)) AS TaskNo,
    m.RequestId,
    m.ReasonCode,
    m.MoveBy AS Operator,
    m.MoveDt AS MoveTime,
    m.Remark

FROM dbo.WMS_FG_HU_MoveLog m

INNER JOIN dbo.WMS_FG_HU h
    ON h.Id = m.HUId

LEFT JOIN dbo.WMS_FG_Location fromLoc
    ON fromLoc.Id = m.FromLocationId

INNER JOIN dbo.WMS_FG_Location toLoc
    ON toLoc.Id = m.ToLocationId

WHERE m.RequestId = @RequestId;
";

            return DbExecutor.QueryFirstOrDefault<HuMoveLogDto>(
                sql,
                new { RequestId = requestId });
        }

        public MovePersistenceResult ExecuteMove(
    HuMoveCommand command)
        {
            using (SqlConnection conn =
                DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran =
                    conn.BeginTransaction(
                        IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // ------------------------------------------------
                        // 1. 锁HU
                        // ------------------------------------------------
                        const string lockHuSql = @"
SELECT Id
FROM dbo.WMS_FG_HU WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @HUId;
";

                        long? lockValue = DbExecutor.ExecuteScalar<long?>(
                            conn,
                            tran,
                            lockHuSql,
                            new { HUId = command.HuId });

                        if (!lockValue.HasValue)
                        {
                            tran.Rollback();

                            return
                                MovePersistenceResult.Fail(
                                    "HU不存在。");
                        }

                        // ------------------------------------------------
                        // 2. 再次检查RequestId
                        // ------------------------------------------------
                        if (!string.IsNullOrWhiteSpace(
                            command.RequestId))
                        {
                            HuMoveResultDto existing =
                                GetMoveByRequestIdInternal(
                                    conn,
                                    tran,
                                    command.RequestId);

                            if (existing != null)
                            {
                                if (!string.Equals(
                                    existing.HuCode,
                                    command.HuCode,
                                    StringComparison.OrdinalIgnoreCase))
                                {
                                    tran.Rollback();

                                    return
                                        MovePersistenceResult.Fail(
                                            "RequestId已被其他HU使用。");
                                }

                                tran.Commit();

                                return
                                    MovePersistenceResult.Success(
                                        existing);
                            }
                        }

                        // ------------------------------------------------
                        // 3. 读取并锁定当前位置
                        // ------------------------------------------------
                        long? currentLocationId = null;
                        string currentLocationCode = null;

                        const string currentSql = @"
SELECT
    cl.LocationId,
    l.LocationCode

FROM dbo.WMS_FG_HU_CurrentLocation cl
    WITH (UPDLOCK, HOLDLOCK)

INNER JOIN dbo.WMS_FG_Location l
    ON l.Id = cl.LocationId

WHERE cl.HUId = @HUId;
";

                        HuDirectLocationRow currentLocation =
                            DbExecutor.QueryFirstOrDefault<HuDirectLocationRow>(
                                conn,
                                tran,
                                currentSql,
                                new { HUId = command.HuId });

                        if (currentLocation != null)
                        {
                            currentLocationId = currentLocation.LocationId;
                            currentLocationCode = currentLocation.LocationCode;
                        }

                        // ------------------------------------------------
                        // 4. ExpectedFrom并发校验
                        // ------------------------------------------------
                        if (command.ExpectedFromSpecified)
                        {
                            if (currentLocationId !=
                                command.ExpectedFromLocationId)
                            {
                                tran.Rollback();

                                return
                                    MovePersistenceResult.Fail(
                                        "HU当前位置已变化，" +
                                        "请刷新后重新操作。");
                            }
                        }

                        // ------------------------------------------------
                        // 5. 不允许原地移动
                        // ------------------------------------------------
                        if (currentLocationId.HasValue &&
                            currentLocationId.Value ==
                            command.ToLocationId)
                        {
                            tran.Rollback();

                            return
                                MovePersistenceResult.Fail(
                                    "HU当前已经位于目标货位。");
                        }

                        // ------------------------------------------------
                        // 6. 插入MoveLog
                        // ------------------------------------------------
                        long moveLogId;
                        DateTime moveTime;

                        HuMoveLogWriter.HuMoveLogWriteResult insertedMove =
                            HuMoveLogWriter.InsertWithTime(
                                conn,
                                tran,
                                command.MoveNo,
                                command.HuId,
                                currentLocationId,
                                command.ToLocationId,
                                command.MoveType,
                                command.ScannedLocationCode,
                                command.BusinessNo,
                                command.TaskNo,
                                command.RequestId,
                                command.ReasonCode,
                                command.Operator,
                                command.Remark);

                        moveLogId = insertedMove.Id;
                        moveTime = insertedMove.MoveTime;

                        // ------------------------------------------------
                        // 7. UPSERT CurrentLocation
                        // ------------------------------------------------
                        const string upsertSql = @"
UPDATE dbo.WMS_FG_HU_CurrentLocation

SET
    LocationId      = @LocationId,
    LocationSince   = @LocationSince,
    LastMoveLogId   = @LastMoveLogId,
    LastEditBy      = @LastEditBy,
    LastEditDt      = SYSDATETIME()

WHERE HUId = @HUId;

IF @@ROWCOUNT = 0
BEGIN

    INSERT INTO dbo.WMS_FG_HU_CurrentLocation
    (
        HUId,
        LocationId,
        LocationSince,
        LastMoveLogId,
        CreateBy
    )
    VALUES
    (
        @HUId,
        @LocationId,
        @LocationSince,
        @LastMoveLogId,
        @LastEditBy
    );

END;
";

                        DbExecutor.Execute(
                            conn,
                            tran,
                            upsertSql,
                            new
                            {
                                HUId = command.HuId,
                                LocationId = command.ToLocationId,
                                LocationSince = moveTime,
                                LastMoveLogId = moveLogId,
                                LastEditBy = DbParameterValue.StringOrNull(command.Operator)
                            });

                        // ------------------------------------------------
                        // 8. 写统一HU OperationLog
                        // ------------------------------------------------
                        HuOperationLogWriter.Insert(
                            conn,
                            tran,
                            command.MoveNo,
                            "MOVE",
                            command.HuId,
                            relatedHuId: null,
                            businessNo: command.BusinessNo,
                            requestId: command.RequestId,
                            operatorCode: command.Operator,
                            operationTime: null,
                            detailJson: null,
                            remark: command.Remark);

                        tran.Commit();

                        HuMoveResultDto result = new HuMoveResultDto();

                        result.MoveLogId =
                            moveLogId;

                        result.MoveNo =
                            command.MoveNo;

                        result.HuCode =
                            command.HuCode;

                        result.MoveType =
                            command.MoveType;

                        result.FromLocationCode =
                            currentLocationCode;

                        result.ToLocationCode =
                            command.ToLocationCode;

                        result.MoveTime =
                            moveTime;

                        result.Operator =
                            command.Operator;

                        return
                            MovePersistenceResult.Success(
                                result);
                    }
                    catch
                    {
                        if (tran.Connection != null)
                            tran.Rollback();

                        throw;
                    }
                }
            }
        }

        public List<HuMoveLogDto> GetMoveLogs(
    long huId,
    int top)
        {
            return DbExecutor.Query<HuMoveLogDto>(
                MoveLogsSql,
                new
                {
                    Top = top,
                    HUId = huId
                });
        }

        private static HuMoveResultDto GetMoveByRequestIdInternal(
    SqlConnection conn,
    SqlTransaction tran,
    string requestId)
        {
            const string sql = @"
SELECT TOP 1
    m.Id,
    m.MoveNo,
    h.HUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    m.MoveType,
    m.MoveDt AS MoveTime,
    m.MoveBy AS Operator

FROM dbo.WMS_FG_HU_MoveLog m

INNER JOIN dbo.WMS_FG_HU h
    ON h.Id = m.HUId

LEFT JOIN dbo.WMS_FG_Location fromLoc
    ON fromLoc.Id = m.FromLocationId

INNER JOIN dbo.WMS_FG_Location toLoc
    ON toLoc.Id = m.ToLocationId

WHERE m.RequestId = @RequestId;
";

            return DbExecutor.QueryFirstOrDefault<HuMoveResultDto>(
                conn,
                tran,
                sql,
                new { RequestId = requestId });
        }

        private class HuDirectLocationRow
        {
            public long LocationId { get; set; }
            public string LocationCode { get; set; }
        }
    }
}
