using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Dapper;
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
    m.ScannedLocationCode,
    m.BusinessNo,
    m.TaskNo,
    m.RequestId,
    m.ReasonCode,
    m.Operator,
    m.MoveTime,
    m.Remark
FROM dbo.WMS_HU_MoveLog m
INNER JOIN dbo.WMS_HU h ON h.Id = m.HUId
LEFT JOIN dbo.WMS_Location fromLoc ON fromLoc.Id = m.FromLocationId
INNER JOIN dbo.WMS_Location toLoc ON toLoc.Id = m.ToLocationId
WHERE m.HUId = @HUId
ORDER BY m.MoveTime DESC, m.Id DESC;";

        public HuLocationDto GetLocation(
            long huId)
        {
            const string sql = @"
;WITH Ancestors AS
(
    SELECT
        h.Id AS HUId,
        h.HUCode,
        CAST(0 AS INT) AS LevelNo

    FROM dbo.WMS_HU h

    WHERE h.Id = @HUId

    UNION ALL

    SELECT
        parent.Id AS HUId,
        parent.HUCode,
        a.LevelNo + 1

    FROM Ancestors a

    INNER JOIN dbo.WMS_HU_Relation r
        ON r.ChildHUId = a.HUId
       AND r.IsActive = 1

    INNER JOIN dbo.WMS_HU parent
        ON parent.Id = r.ParentHUId
)

SELECT
    h.Id AS HuId,
    h.HUCode,
    h.HUType,
    h.PlantCode,

    directLoc.Id AS DirectLocationId,
    directLoc.LocationCode AS DirectLocationCode,
    directWarehouse.WarehouseCode
        AS DirectWarehouseCode,
    directZone.ZoneCode
        AS DirectZoneCode,
    directLoc.LocationTypeCode
        AS DirectLocationTypeCode,

    directCurrent.LocationSince
        AS DirectLocationSince,

    directCurrent.LastMoveLogId,
    directCurrent.LastMoveNo,
    directCurrent.LastMoveType,

    effectiveInfo.LocationId
        AS EffectiveLocationId,

    effectiveInfo.LocationCode
        AS EffectiveLocationCode,

    effectiveInfo.WarehouseCode
        AS EffectiveWarehouseCode,

    effectiveInfo.ZoneCode
        AS EffectiveZoneCode,

    effectiveInfo.LocationTypeCode
        AS EffectiveLocationTypeCode,

    effectiveInfo.SourceHUId
        AS LocationSourceHuId,

    effectiveInfo.SourceHUCode
        AS LocationSourceHuCode,

    CASE
        WHEN effectiveInfo.SourceHUId IS NULL THEN 0
        WHEN effectiveInfo.SourceHUId = h.Id THEN 0
        ELSE 1
    END AS IsInherited

FROM dbo.WMS_HU h

LEFT JOIN dbo.WMS_HU_CurrentLocation directCurrent
    ON directCurrent.HUId = h.Id

LEFT JOIN dbo.WMS_Location directLoc
    ON directLoc.Id =
       directCurrent.LocationId

LEFT JOIN dbo.WMS_Warehouse directWarehouse
    ON directWarehouse.Id =
       directLoc.WarehouseId

LEFT JOIN dbo.WMS_Zone directZone
    ON directZone.Id =
       directLoc.ZoneId

OUTER APPLY
(
    SELECT TOP 1

        loc.Id AS LocationId,
        loc.LocationCode,
        loc.LocationTypeCode,

        wh.WarehouseCode,
        z.ZoneCode,

        a.HUId AS SourceHUId,
        a.HUCode AS SourceHUCode

    FROM Ancestors a

    INNER JOIN dbo.WMS_HU_CurrentLocation cl
        ON cl.HUId = a.HUId

    INNER JOIN dbo.WMS_Location loc
        ON loc.Id = cl.LocationId

    INNER JOIN dbo.WMS_Warehouse wh
        ON wh.Id = loc.WarehouseId

    INNER JOIN dbo.WMS_Zone z
        ON z.Id = loc.ZoneId

    ORDER BY
        a.LevelNo ASC

) effectiveInfo

WHERE h.Id = @HUId

OPTION (MAXRECURSION 20);
";

            return DbExecutor.QueryFirstOrDefault<HuLocationDto>(
                sql,
                new { HUId = huId });
        }

        public List<HuLocationDto> GetLocations(IList<long> huIds)
        {
            List<long> ids = (huIds ?? new List<long>()).Distinct().ToList();
            List<HuLocationDto> results = new List<HuLocationDto>();
            if (ids.Count == 0) return results;
            StringBuilder parameters = new StringBuilder();
            for (int index = 0; index < ids.Count; index++)
            {
                if (index > 0) parameters.Append(",");
                parameters.Append("@HUId").Append(index);
            }
            string sql = @"
;WITH Roots AS
(
    SELECT h.Id AS RootHUId, h.Id AS HUId, h.HUCode, CAST(0 AS INT) AS LevelNo
    FROM dbo.WMS_HU h
    WHERE h.Id IN (" + parameters + @")
),
Ancestors AS
(
    SELECT RootHUId, HUId, HUCode, LevelNo FROM Roots
    UNION ALL
    SELECT a.RootHUId, parent.Id, parent.HUCode, a.LevelNo + 1
    FROM Ancestors a
    INNER JOIN dbo.WMS_HU_Relation relation ON relation.ChildHUId = a.HUId AND relation.IsActive = 1
    INNER JOIN dbo.WMS_HU parent ON parent.Id = relation.ParentHUId
)
SELECT
    h.Id AS HuId, h.HUCode, h.HUType, h.PlantCode,
    directLoc.Id AS DirectLocationId, directLoc.LocationCode AS DirectLocationCode,
    directWarehouse.WarehouseCode AS DirectWarehouseCode, directZone.ZoneCode AS DirectZoneCode,
    directLoc.LocationTypeCode AS DirectLocationTypeCode, directCurrent.LocationSince AS DirectLocationSince,
    directCurrent.LastMoveLogId, directCurrent.LastMoveNo, directCurrent.LastMoveType,
    effectiveInfo.LocationId AS EffectiveLocationId, effectiveInfo.LocationCode AS EffectiveLocationCode,
    effectiveInfo.WarehouseCode AS EffectiveWarehouseCode, effectiveInfo.ZoneCode AS EffectiveZoneCode,
    effectiveInfo.LocationTypeCode AS EffectiveLocationTypeCode,
    effectiveInfo.SourceHUId AS LocationSourceHuId, effectiveInfo.SourceHUCode AS LocationSourceHuCode,
    CAST(CASE WHEN effectiveInfo.SourceHUId IS NULL OR effectiveInfo.SourceHUId = h.Id THEN 0 ELSE 1 END AS BIT) AS IsInherited
FROM dbo.WMS_HU h
LEFT JOIN dbo.WMS_HU_CurrentLocation directCurrent ON directCurrent.HUId = h.Id
LEFT JOIN dbo.WMS_Location directLoc ON directLoc.Id = directCurrent.LocationId
LEFT JOIN dbo.WMS_Warehouse directWarehouse ON directWarehouse.Id = directLoc.WarehouseId
LEFT JOIN dbo.WMS_Zone directZone ON directZone.Id = directLoc.ZoneId
OUTER APPLY
(
    SELECT TOP 1 loc.Id AS LocationId, loc.LocationCode, loc.LocationTypeCode,
        warehouse.WarehouseCode, zone.ZoneCode, ancestor.HUId AS SourceHUId, ancestor.HUCode AS SourceHUCode
    FROM Ancestors ancestor
    INNER JOIN dbo.WMS_HU_CurrentLocation currentLocation ON currentLocation.HUId = ancestor.HUId
    INNER JOIN dbo.WMS_Location loc ON loc.Id = currentLocation.LocationId
    INNER JOIN dbo.WMS_Warehouse warehouse ON warehouse.Id = loc.WarehouseId
    INNER JOIN dbo.WMS_Zone zone ON zone.Id = loc.ZoneId
    WHERE ancestor.RootHUId = h.Id
    ORDER BY ancestor.LevelNo
) effectiveInfo
WHERE h.Id IN (" + parameters + @")
OPTION (MAXRECURSION 20);";

            using (SqlConnection connection = DbConnectionFactory.Create())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                for (int index = 0; index < ids.Count; index++) command.Parameters.Add("@HUId" + index, SqlDbType.BigInt).Value = ids[index];
                connection.Open();
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new HuLocationDto
                        {
                            HuId = Convert.ToInt64(reader["HuId"]),
                            HuCode = DbString(reader, "HUCode"),
                            HuType = DbString(reader, "HUType"),
                            PlantCode = DbString(reader, "PlantCode"),
                            DirectLocationId = DbInt64(reader, "DirectLocationId"),
                            DirectLocationCode = DbString(reader, "DirectLocationCode"),
                            DirectWarehouseCode = DbString(reader, "DirectWarehouseCode"),
                            DirectZoneCode = DbString(reader, "DirectZoneCode"),
                            DirectLocationTypeCode = DbString(reader, "DirectLocationTypeCode"),
                            DirectLocationSince = DbDateTime(reader, "DirectLocationSince"),
                            EffectiveLocationId = DbInt64(reader, "EffectiveLocationId"),
                            EffectiveLocationCode = DbString(reader, "EffectiveLocationCode"),
                            EffectiveWarehouseCode = DbString(reader, "EffectiveWarehouseCode"),
                            EffectiveZoneCode = DbString(reader, "EffectiveZoneCode"),
                            EffectiveLocationTypeCode = DbString(reader, "EffectiveLocationTypeCode"),
                            LocationSourceHuId = DbInt64(reader, "LocationSourceHuId"),
                            LocationSourceHuCode = DbString(reader, "LocationSourceHuCode"),
                            IsInherited = Convert.ToBoolean(reader["IsInherited"]),
                            LastMoveLogId = DbInt64(reader, "LastMoveLogId"),
                            LastMoveNo = DbString(reader, "LastMoveNo"),
                            LastMoveType = DbString(reader, "LastMoveType")
                        });
                    }
                }
            }
            return results;
        }

        private static string DbString(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index)); }
        private static long? DbInt64(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (long?)null : Convert.ToInt64(reader.GetValue(index)); }
        private static DateTime? DbDateTime(SqlDataReader reader, string name) { int index = reader.GetOrdinal(name); return reader.IsDBNull(index) ? (DateTime?)null : Convert.ToDateTime(reader.GetValue(index)); }

        public bool WarehouseAllowsPlant(
    string warehouseCode,
    string plantCode)
        {
            const string sql = @"
SELECT COUNT(1)

FROM dbo.WMS_WarehousePlant wp

INNER JOIN dbo.WMS_Warehouse w
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

FROM dbo.WMS_HU_CurrentLocation cl

INNER JOIN dbo.WMS_HU h
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
    m.ScannedLocationCode,
    m.BusinessNo,
    m.TaskNo,
    m.RequestId,
    m.ReasonCode,
    m.Operator,
    m.MoveTime,
    m.Remark

FROM dbo.WMS_HU_MoveLog m

INNER JOIN dbo.WMS_HU h
    ON h.Id = m.HUId

LEFT JOIN dbo.WMS_Location fromLoc
    ON fromLoc.Id = m.FromLocationId

INNER JOIN dbo.WMS_Location toLoc
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
FROM dbo.WMS_HU WITH (UPDLOCK, HOLDLOCK)
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

FROM dbo.WMS_HU_CurrentLocation cl
    WITH (UPDLOCK, HOLDLOCK)

INNER JOIN dbo.WMS_Location l
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
UPDATE dbo.WMS_HU_CurrentLocation

SET
    LocationId      = @LocationId,
    LocationSince   = @LocationSince,
    LastMoveLogId   = @LastMoveLogId,
    LastMoveNo      = @LastMoveNo,
    LastMoveType    = @LastMoveType,
    LastOperator    = @LastOperator,
    ModifyTime      = SYSDATETIME()

WHERE HUId = @HUId;

IF @@ROWCOUNT = 0
BEGIN

    INSERT INTO dbo.WMS_HU_CurrentLocation
    (
        HUId,
        LocationId,
        LocationSince,
        LastMoveLogId,
        LastMoveNo,
        LastMoveType,
        LastOperator
    )
    VALUES
    (
        @HUId,
        @LocationId,
        @LocationSince,
        @LastMoveLogId,
        @LastMoveNo,
        @LastMoveType,
        @LastOperator
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
                                LastMoveNo = command.MoveNo,
                                LastMoveType = command.MoveType,
                                LastOperator = DbParameterValue.StringOrNull(command.Operator)
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
    m.MoveTime,
    m.Operator

FROM dbo.WMS_HU_MoveLog m

INNER JOIN dbo.WMS_HU h
    ON h.Id = m.HUId

LEFT JOIN dbo.WMS_Location fromLoc
    ON fromLoc.Id = m.FromLocationId

INNER JOIN dbo.WMS_Location toLoc
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
