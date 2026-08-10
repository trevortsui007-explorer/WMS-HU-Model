using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.HU;

namespace WMS_HU_Model.Repositories
{
    public class HuLocationRepository
        : IHuLocationRepository
    {
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

            using (SqlConnection conn =
                DbConnectionFactory.Create())

            using (SqlCommand cmd =
                new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(
                    "@HUId",
                    SqlDbType.BigInt)
                    .Value = huId;

                conn.Open();

                using (SqlDataReader reader =
                    cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return MapLocation(reader);
                }
            }
        }
        private static HuLocationDto MapLocation(
    IDataRecord reader)
        {
            HuLocationDto dto =
                new HuLocationDto();

            dto.HuId =
                Convert.ToInt64(
                    reader["HuId"]);

            dto.HuCode =
                Convert.ToString(
                    reader["HUCode"]);

            dto.HuType =
                Convert.ToString(
                    reader["HUType"]);

            dto.PlantCode =
                Convert.ToString(
                    reader["PlantCode"]);


            dto.DirectLocationId =
                DbLong(
                    reader["DirectLocationId"]);

            dto.DirectLocationCode =
                DbString(
                    reader["DirectLocationCode"]);

            dto.DirectWarehouseCode =
                DbString(
                    reader["DirectWarehouseCode"]);

            dto.DirectZoneCode =
                DbString(
                    reader["DirectZoneCode"]);

            dto.DirectLocationSince =
                DbDateTime(
                    reader["DirectLocationSince"]);


            dto.EffectiveLocationId =
                DbLong(
                    reader["EffectiveLocationId"]);

            dto.EffectiveLocationCode =
                DbString(
                    reader["EffectiveLocationCode"]);

            dto.EffectiveWarehouseCode =
                DbString(
                    reader["EffectiveWarehouseCode"]);

            dto.EffectiveZoneCode =
                DbString(
                    reader["EffectiveZoneCode"]);

            dto.LocationSourceHuId =
                DbLong(
                    reader["LocationSourceHuId"]);

            dto.LocationSourceHuCode =
                DbString(
                    reader["LocationSourceHuCode"]);

            dto.IsInherited =
                Convert.ToBoolean(
                    reader["IsInherited"]);


            dto.LastMoveLogId =
                DbLong(
                    reader["LastMoveLogId"]);

            dto.LastMoveNo =
                DbString(
                    reader["LastMoveNo"]);

            dto.LastMoveType =
                DbString(
                    reader["LastMoveType"]);

            return dto;
        }


        private static string DbString(
            object value)
        {
            return value == DBNull.Value
                ? null
                : Convert.ToString(value);
        }


        private static long? DbLong(
            object value)
        {
            return value == DBNull.Value
                ? (long?)null
                : Convert.ToInt64(value);
        }


        private static DateTime? DbDateTime(
            object value)
        {
            return value == DBNull.Value
                ? (DateTime?)null
                : Convert.ToDateTime(value);
        }

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

            using (SqlConnection conn =
                DbConnectionFactory.Create())

            using (SqlCommand cmd =
                new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@WarehouseCode",
                    warehouseCode);

                cmd.Parameters.AddWithValue(
                    "@PlantCode",
                    plantCode);

                conn.Open();

                int count =
                    Convert.ToInt32(
                        cmd.ExecuteScalar());

                return count > 0;
            }
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

            using (SqlConnection conn =
                DbConnectionFactory.Create())

            using (SqlCommand cmd =
                new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@LocationId",
                    locationId);

                cmd.Parameters.AddWithValue(
                    "@PlantCode",
                    plantCode);

                conn.Open();

                return Convert.ToInt32(
                    cmd.ExecuteScalar());
            }
        }

        public HuMoveLogDto GetMoveByRequestId(
    string requestId)
        {
            if (string.IsNullOrWhiteSpace(
                requestId))
                return null;

            const string sql = @"
SELECT TOP 1
    m.Id,
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

            using (SqlConnection conn =
                DbConnectionFactory.Create())

            using (SqlCommand cmd =
                new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@RequestId",
                    requestId);

                conn.Open();

                using (SqlDataReader reader =
                    cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    return MapMoveLog(reader);
                }
            }
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

                        using (SqlCommand lockCmd =
                            new SqlCommand(
                                lockHuSql,
                                conn,
                                tran))
                        {
                            lockCmd.Parameters.AddWithValue(
                                "@HUId",
                                command.HuId);

                            object lockValue =
                                lockCmd.ExecuteScalar();

                            if (lockValue == null)
                            {
                                tran.Rollback();

                                return
                                    MovePersistenceResult.Fail(
                                        "HU不存在。");
                            }
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

                        using (SqlCommand currentCmd =
                            new SqlCommand(
                                currentSql,
                                conn,
                                tran))
                        {
                            currentCmd.Parameters.AddWithValue(
                                "@HUId",
                                command.HuId);

                            using (SqlDataReader reader =
                                currentCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    currentLocationId =
                                        Convert.ToInt64(
                                            reader["LocationId"]);

                                    currentLocationCode =
                                        Convert.ToString(
                                            reader["LocationCode"]);
                                }
                            }
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

                        const string insertMoveSql = @"
INSERT INTO dbo.WMS_HU_MoveLog
(
    MoveNo,
    HUId,
    FromLocationId,
    ToLocationId,
    MoveType,
    ScannedLocationCode,
    BusinessNo,
    TaskNo,
    RequestId,
    ReasonCode,
    Operator,
    Remark
)

OUTPUT
    INSERTED.Id,
    INSERTED.MoveTime

VALUES
(
    @MoveNo,
    @HUId,
    @FromLocationId,
    @ToLocationId,
    @MoveType,
    @ScannedLocationCode,
    @BusinessNo,
    @TaskNo,
    @RequestId,
    @ReasonCode,
    @Operator,
    @Remark
);
";

                        using (SqlCommand insertMoveCmd =
                            new SqlCommand(
                                insertMoveSql,
                                conn,
                                tran))
                        {
                            AddMoveParameters(
                                insertMoveCmd,
                                command,
                                currentLocationId);

                            using (SqlDataReader reader =
                                insertMoveCmd.ExecuteReader())
                            {
                                reader.Read();

                                moveLogId =
                                    Convert.ToInt64(
                                        reader["Id"]);

                                moveTime =
                                    Convert.ToDateTime(
                                        reader["MoveTime"]);
                            }
                        }


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

                        using (SqlCommand upsertCmd =
                            new SqlCommand(
                                upsertSql,
                                conn,
                                tran))
                        {
                            upsertCmd.Parameters.AddWithValue(
                                "@HUId",
                                command.HuId);

                            upsertCmd.Parameters.AddWithValue(
                                "@LocationId",
                                command.ToLocationId);

                            upsertCmd.Parameters.AddWithValue(
                                "@LocationSince",
                                moveTime);

                            upsertCmd.Parameters.AddWithValue(
                                "@LastMoveLogId",
                                moveLogId);

                            upsertCmd.Parameters.AddWithValue(
                                "@LastMoveNo",
                                command.MoveNo);

                            upsertCmd.Parameters.AddWithValue(
                                "@LastMoveType",
                                command.MoveType);

                            upsertCmd.Parameters.AddWithValue(
                                "@LastOperator",
                                DbValue(command.Operator));

                            upsertCmd.ExecuteNonQuery();
                        }


                        // ------------------------------------------------
                        // 8. 写统一HU OperationLog
                        // ------------------------------------------------
                        const string operationSql = @"
INSERT INTO dbo.WMS_HU_OperationLog
(
    OperationNo,
    OperationType,
    HUId,
    BusinessNo,
    RequestId,
    Operator,
    Remark
)
VALUES
(
    @OperationNo,
    'MOVE',
    @HUId,
    @BusinessNo,
    @RequestId,
    @Operator,
    @Remark
);
";

                        using (SqlCommand operationCmd =
                            new SqlCommand(
                                operationSql,
                                conn,
                                tran))
                        {
                            operationCmd.Parameters.AddWithValue(
                                "@OperationNo",
                                command.MoveNo);

                            operationCmd.Parameters.AddWithValue(
                                "@HUId",
                                command.HuId);

                            operationCmd.Parameters.AddWithValue(
                                "@BusinessNo",
                                DbValue(
                                    command.BusinessNo));

                            operationCmd.Parameters.AddWithValue(
                                "@RequestId",
                                DbValue(
                                    command.RequestId));

                            operationCmd.Parameters.AddWithValue(
                                "@Operator",
                                DbValue(
                                    command.Operator));

                            operationCmd.Parameters.AddWithValue(
                                "@Remark",
                                DbValue(
                                    command.Remark));

                            operationCmd.ExecuteNonQuery();
                        }


                        tran.Commit();


                        HuMoveResultDto result =
                            new HuMoveResultDto();

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

        private static void AddMoveParameters(
    SqlCommand cmd,
    HuMoveCommand command,
    long? fromLocationId)
        {
            cmd.Parameters.AddWithValue(
                "@MoveNo",
                command.MoveNo);

            cmd.Parameters.AddWithValue(
                "@HUId",
                command.HuId);

            cmd.Parameters.AddWithValue(
                "@FromLocationId",
                fromLocationId.HasValue
                    ? (object)fromLocationId.Value
                    : DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@ToLocationId",
                command.ToLocationId);

            cmd.Parameters.AddWithValue(
                "@MoveType",
                command.MoveType);

            cmd.Parameters.AddWithValue(
                "@ScannedLocationCode",
                DbValue(
                    command.ScannedLocationCode));

            cmd.Parameters.AddWithValue(
                "@BusinessNo",
                DbValue(
                    command.BusinessNo));

            cmd.Parameters.AddWithValue(
                "@TaskNo",
                DbValue(
                    command.TaskNo));

            cmd.Parameters.AddWithValue(
                "@RequestId",
                DbValue(
                    command.RequestId));

            cmd.Parameters.AddWithValue(
                "@ReasonCode",
                DbValue(
                    command.ReasonCode));

            cmd.Parameters.AddWithValue(
                "@Operator",
                DbValue(
                    command.Operator));

            cmd.Parameters.AddWithValue(
                "@Remark",
                DbValue(
                    command.Remark));
        }


        private static object DbValue(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? (object)DBNull.Value
                : value;
        }

        public List<HuMoveLogDto> GetMoveLogs(
    long huId,
    int top)
        {
            const string sql = @"
SELECT TOP (@Top)
    m.Id,
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

WHERE m.HUId = @HUId

ORDER BY
    m.MoveTime DESC,
    m.Id DESC;
";

            List<HuMoveLogDto> list =
                new List<HuMoveLogDto>();

            using (SqlConnection conn =
                DbConnectionFactory.Create())

            using (SqlCommand cmd =
                new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(
                    "@Top",
                    top);

                cmd.Parameters.AddWithValue(
                    "@HUId",
                    huId);

                conn.Open();

                using (SqlDataReader reader =
                    cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(
                            MapMoveLog(reader));
                    }
                }
            }

            return list;
        }

        private static HuMoveLogDto MapMoveLog(
    IDataRecord reader)
        {
            HuMoveLogDto dto =
                new HuMoveLogDto();

            dto.Id =
                Convert.ToInt64(
                    reader["Id"]);

            dto.MoveNo =
                Convert.ToString(
                    reader["MoveNo"]);

            dto.HuCode =
                Convert.ToString(
                    reader["HUCode"]);

            dto.FromLocationCode =
                DbString(
                    reader["FromLocationCode"]);

            dto.ToLocationCode =
                Convert.ToString(
                    reader["ToLocationCode"]);

            dto.MoveType =
                Convert.ToString(
                    reader["MoveType"]);

            dto.ScannedLocationCode =
                DbString(
                    reader["ScannedLocationCode"]);

            dto.BusinessNo =
                DbString(
                    reader["BusinessNo"]);

            dto.TaskNo =
                DbString(
                    reader["TaskNo"]);

            dto.RequestId =
                DbString(
                    reader["RequestId"]);

            dto.ReasonCode =
                DbString(
                    reader["ReasonCode"]);

            dto.Operator =
                DbString(
                    reader["Operator"]);

            dto.MoveTime =
                Convert.ToDateTime(
                    reader["MoveTime"]);

            dto.Remark =
                DbString(
                    reader["Remark"]);

            return dto;
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

            using (SqlCommand cmd =
                new SqlCommand(
                    sql,
                    conn,
                    tran))
            {
                cmd.Parameters.Add(
                    "@RequestId",
                    SqlDbType.VarChar,
                    100)
                    .Value = requestId;


                using (SqlDataReader reader =
                    cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }


                    HuMoveResultDto result =
                        new HuMoveResultDto();


                    result.MoveLogId =
                        Convert.ToInt64(
                            reader["Id"]);

                    result.MoveNo =
                        Convert.ToString(
                            reader["MoveNo"]);

                    result.HuCode =
                        Convert.ToString(
                            reader["HUCode"]);


                    if (reader["FromLocationCode"] ==
                        DBNull.Value)
                    {
                        result.FromLocationCode =
                            null;
                    }
                    else
                    {
                        result.FromLocationCode =
                            Convert.ToString(
                                reader["FromLocationCode"]);
                    }


                    result.ToLocationCode =
                        Convert.ToString(
                            reader["ToLocationCode"]);

                    result.MoveType =
                        Convert.ToString(
                            reader["MoveType"]);

                    result.MoveTime =
                        Convert.ToDateTime(
                            reader["MoveTime"]);


                    if (reader["Operator"] ==
                        DBNull.Value)
                    {
                        result.Operator =
                            null;
                    }
                    else
                    {
                        result.Operator =
                            Convert.ToString(
                                reader["Operator"]);
                    }


                    return result;
                }
            }
        }
    }
}