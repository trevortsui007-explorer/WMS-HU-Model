using System;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Putaway;

namespace WMS_HU_Model.Repositories
{
    public class PutawayRepository : IPutawayRepository
    {
        public PalletPutawayStatusDto GetPalletStatus(long palletHuId)
        {
            const string palletSql = @"
SELECT
    h.Id,
    h.HUCode,
    h.PlantCode,
    l.LocationCode,
    z.ZoneCode
FROM dbo.WMS_HU h
LEFT JOIN dbo.WMS_HU_CurrentLocation cl
    ON cl.HUId = h.Id
LEFT JOIN dbo.WMS_Location l
    ON l.Id = cl.LocationId
LEFT JOIN dbo.WMS_Zone z
    ON z.Id = l.ZoneId
WHERE h.Id = @PalletHUId;
";

            const string boxSql = @"
SELECT
    child.HUCode
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU child
    ON child.Id = r.ChildHUId
WHERE r.ParentHUId = @PalletHUId
AND r.IsActive = 1
AND child.HUType = 'OUTER_BOX'
AND child.LifecycleStatus = 'ACTIVE'
ORDER BY child.HUCode;
";

            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                PalletPutawayStatusDto result = null;

                using (SqlCommand cmd = new SqlCommand(palletSql, conn))
                {
                    cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt)
                        .Value = palletHuId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;

                        result = new PalletPutawayStatusDto();

                        result.PalletHuId = Convert.ToInt64(reader["Id"]);
                        result.PalletHuCode = Convert.ToString(reader["HUCode"]);
                        result.PlantCode = Convert.ToString(reader["PlantCode"]);
                        result.CurrentLocationCode = DbString(reader["LocationCode"]);
                        result.CurrentZoneCode = DbString(reader["ZoneCode"]);
                    }
                }

                using (SqlCommand cmd = new SqlCommand(boxSql, conn))
                {
                    cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt)
                        .Value = palletHuId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string boxCode = Convert.ToString(reader["HUCode"]);
                            result.RemainingBoxCodes.Add(boxCode);
                        }
                    }
                }

                result.RemainingBoxCount = result.RemainingBoxCodes.Count;
                result.IsEmpty = result.RemainingBoxCount == 0;

                return result;
            }
        }

        public PutawayResultDto GetByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;

            const string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    pallet.HUCode AS PalletHUCode,
    box.HUCode AS BoxHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveTime AS PutawayTime,
    RemainingBoxCount =
    (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation remainRel
        INNER JOIN dbo.WMS_HU remainHU
            ON remainHU.Id = remainRel.ChildHUId
        WHERE remainRel.ParentHUId = pallet.Id
        AND remainRel.IsActive = 1
        AND remainHU.HUType = 'OUTER_BOX'
        AND remainHU.LifecycleStatus = 'ACTIVE'
    )
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU box
    ON box.Id = op.HUId
INNER JOIN dbo.WMS_HU pallet
    ON pallet.Id = op.RelatedHUId
INNER JOIN dbo.WMS_HU_MoveLog moveLog
    ON moveLog.MoveNo = op.OperationNo
   AND moveLog.HUId = box.Id
LEFT JOIN dbo.WMS_Location fromLoc
    ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc
    ON toLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'PUTAWAY'
AND op.RequestId = @RequestId;
";

            using (SqlConnection conn = DbConnectionFactory.Create())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                    .Value = requestId;

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    PutawayResultDto result = new PutawayResultDto();

                    result.OperationNo = Convert.ToString(reader["OperationNo"]);
                    result.MoveLogId = Convert.ToInt64(reader["MoveLogId"]);
                    result.PalletHuCode = Convert.ToString(reader["PalletHUCode"]);
                    result.BoxHuCode = Convert.ToString(reader["BoxHUCode"]);
                    result.FromLocationCode = DbString(reader["FromLocationCode"]);
                    result.ToLocationCode = Convert.ToString(reader["ToLocationCode"]);
                    result.RemainingBoxCount = Convert.ToInt32(reader["RemainingBoxCount"]);
                    result.IsPalletEmpty = result.RemainingBoxCount == 0;
                    result.PutawayTime = Convert.ToDateTime(reader["PutawayTime"]);
                    result.IsDuplicate = true;

                    return result;
                }
            }
        }

        public PutawayPersistenceResult ExecutePutaway(PutawayCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        DateTime now = DateTime.Now;

                        //------------------------------------------------
                        // 1. 锁定父HU和子HU
                        //------------------------------------------------

                        const string lockHuSql = @"
SELECT Id
FROM dbo.WMS_HU
WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN
(
    @PalletHUId,
    @BoxHUId
);
";

                        using (SqlCommand cmd = new SqlCommand(lockHuSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt)
                                .Value = command.PalletHuId;

                            cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                                .Value = command.BoxHuId;

                            int huCount = 0;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    huCount++;
                                }
                            }

                            if (huCount != 2)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("托盘或外箱HU不存在。");
                            }
                        }

                        //------------------------------------------------
                        // 2. 再次确认Active父子关系
                        //------------------------------------------------

                        long relationId;

                        const string relationSql = @"
SELECT
    Id
FROM dbo.WMS_HU_Relation
WITH (UPDLOCK, HOLDLOCK)
WHERE ParentHUId = @PalletHUId
AND ChildHUId = @BoxHUId
AND IsActive = 1;
";

                        using (SqlCommand cmd = new SqlCommand(relationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt)
                                .Value = command.PalletHuId;

                            cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                                .Value = command.BoxHuId;

                            object value = cmd.ExecuteScalar();

                            if (value == null || value == DBNull.Value)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("外箱当前已不属于指定托盘，请刷新托盘数据。");
                            }

                            relationId = Convert.ToInt64(value);
                        }

                        //------------------------------------------------
                        // 3. 锁定并确认托盘当前位置
                        //------------------------------------------------

                        long currentPalletLocationId;
                        string currentPalletLocationCode;

                        const string palletLocationSql = @"
SELECT
    cl.LocationId,
    l.LocationCode,
    z.ZoneCode
FROM dbo.WMS_HU_CurrentLocation cl
WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Location l
    ON l.Id = cl.LocationId
INNER JOIN dbo.WMS_Zone z
    ON z.Id = l.ZoneId
WHERE cl.HUId = @PalletHUId;
";

                        using (SqlCommand cmd = new SqlCommand(palletLocationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt)
                                .Value = command.PalletHuId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("托盘没有直接当前位置。");
                                }

                                currentPalletLocationId = Convert.ToInt64(reader["LocationId"]);
                                currentPalletLocationCode = Convert.ToString(reader["LocationCode"]);

                                string zoneCode = Convert.ToString(reader["ZoneCode"]);

                                if (zoneCode != "RECEIVE")
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("托盘当前不在收货区域，不能执行正常拆托上架。");
                                }
                            }
                        }

                        if (currentPalletLocationId != command.FromLocationId)
                        {
                            tran.Rollback();
                            return PutawayPersistenceResult.Fail("托盘当前位置已变化，请重新扫描托盘。");
                        }

                        //------------------------------------------------
                        // 4. 外箱不允许已经有DirectLocation
                        //------------------------------------------------

                        const string boxLocationSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_CurrentLocation
WITH (UPDLOCK, HOLDLOCK)
WHERE HUId = @BoxHUId;
";

                        using (SqlCommand cmd = new SqlCommand(boxLocationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                                .Value = command.BoxHuId;

                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count > 0)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("外箱已经存在直接货位，数据状态异常。");
                            }
                        }

                        //------------------------------------------------
                        // 5. 锁定目标货位
                        //------------------------------------------------

                        string targetPlantPolicy;
                        string dedicatedPlantCode;

                        const string targetSql = @"
SELECT
    l.MasterStatus,
    l.IsVirtual,
    l.PlantPolicy,
    l.DedicatedPlantCode,
    z.ZoneCode,
    w.WarehouseCode
FROM dbo.WMS_Location l
WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Zone z
    ON z.Id = l.ZoneId
INNER JOIN dbo.WMS_Warehouse w
    ON w.Id = l.WarehouseId
WHERE l.Id = @LocationId;
";

                        using (SqlCommand cmd = new SqlCommand(targetSql, conn, tran))
                        {
                            cmd.Parameters.Add("@LocationId", SqlDbType.BigInt)
                                .Value = command.ToLocationId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("目标货位不存在。");
                                }

                                string masterStatus = Convert.ToString(reader["MasterStatus"]);
                                bool isVirtual = Convert.ToBoolean(reader["IsVirtual"]);
                                string zoneCode = Convert.ToString(reader["ZoneCode"]);
                                string warehouseCode = Convert.ToString(reader["WarehouseCode"]);
                                targetPlantPolicy = Convert.ToString(reader["PlantPolicy"]);
                                dedicatedPlantCode = DbString(reader["DedicatedPlantCode"]);

                                if (masterStatus != "ACTIVE")
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("目标货位不可用。");
                                }

                                if (isVirtual)
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("正常上架不能使用虚拟货位。");
                                }

                                if (zoneCode != "STORAGE")
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("目标货位不属于正式储存区。");
                                }

                                if (!string.Equals(warehouseCode, command.WarehouseCode, StringComparison.OrdinalIgnoreCase))
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("目标货位不属于指定仓库。");
                                }
                            }
                        }

                        //------------------------------------------------
                        // 6. 仓库厂别
                        //------------------------------------------------

                        const string plantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_WarehousePlant wp
INNER JOIN dbo.WMS_Warehouse w
    ON w.Id = wp.WarehouseId
WHERE w.WarehouseCode = @WarehouseCode
AND wp.PlantCode = @PlantCode
AND wp.IsActive = 1;
";

                        using (SqlCommand cmd = new SqlCommand(plantSql, conn, tran))
                        {
                            cmd.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30)
                                .Value = command.WarehouseCode;

                            cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20)
                                .Value = command.PlantCode;

                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count == 0)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("当前仓库不允许存储该厂别。");
                            }
                        }

                        //------------------------------------------------
                        // 7. Dedicated
                        //------------------------------------------------

                        if (targetPlantPolicy == "DEDICATED")
                        {
                            if (!string.Equals(dedicatedPlantCode, command.PlantCode, StringComparison.OrdinalIgnoreCase))
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("目标货位为其他厂别专用货位。");
                            }
                        }

                        //------------------------------------------------
                        // 8. SAME_PLANT
                        //------------------------------------------------

                        if (targetPlantPolicy == "SAME_PLANT")
                        {
                            const string samePlantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_CurrentLocation cl
INNER JOIN dbo.WMS_HU h
    ON h.Id = cl.HUId
WHERE cl.LocationId = @LocationId
AND h.PlantCode <> @PlantCode
AND h.LifecycleStatus = 'ACTIVE';
";

                            using (SqlCommand cmd = new SqlCommand(samePlantSql, conn, tran))
                            {
                                cmd.Parameters.Add("@LocationId", SqlDbType.BigInt)
                                    .Value = command.ToLocationId;

                                cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20)
                                    .Value = command.PlantCode;

                                int count = Convert.ToInt32(cmd.ExecuteScalar());

                                if (count > 0)
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("目标货位当前存在其他厂别库存。");
                                }
                            }
                        }

                        //------------------------------------------------
                        // 9. 不允许原地拆托
                        //------------------------------------------------

                        if (currentPalletLocationId == command.ToLocationId)
                        {
                            tran.Rollback();
                            return PutawayPersistenceResult.Fail("目标货位不能与托盘收货货位相同。");
                        }

                        //------------------------------------------------
                        // 10. 关闭托盘 → 外箱Relation
                        //------------------------------------------------

                        const string closeRelationSql = @"
UPDATE dbo.WMS_HU_Relation
SET
    EffectiveTo = @Now,
    IsActive = 0,
    CloseOperationNo = @OperationNo,
    CloseUser = @Operator,
    CloseReason = N'PUTAWAY'
WHERE Id = @RelationId
AND IsActive = 1;
";

                        using (SqlCommand cmd = new SqlCommand(closeRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@Now", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.Parameters.Add("@RelationId", SqlDbType.BigInt)
                                .Value = relationId;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("HU关系关闭失败。");
                            }
                        }

                        //------------------------------------------------
                        // 11. MoveLog
                        //------------------------------------------------

                        long moveLogId;

                        const string moveSql = @"
INSERT INTO dbo.WMS_HU_MoveLog
(
    MoveNo,
    HUId,
    FromLocationId,
    ToLocationId,
    MoveType,
    ScannedLocationCode,
    BusinessNo,
    RequestId,
    Operator,
    MoveTime,
    Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @MoveNo,
    @HUId,
    @FromLocationId,
    @ToLocationId,
    'PUTAWAY',
    @ScannedLocationCode,
    @BusinessNo,
    @RequestId,
    @Operator,
    @MoveTime,
    @Remark
);
";

                        using (SqlCommand cmd = new SqlCommand(moveSql, conn, tran))
                        {
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt)
                                .Value = command.BoxHuId;

                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt)
                                .Value = currentPalletLocationId;

                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt)
                                .Value = command.ToLocationId;

                            cmd.Parameters.Add("@ScannedLocationCode", SqlDbType.VarChar, 100)
                                .Value = DbValue(command.ScannedLocationCode);

                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                                .Value = DbValue(command.BusinessNo);

                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                                .Value = command.RequestId;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                                .Value = DbValue(command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 12. 建立Box CurrentLocation
                        //------------------------------------------------

                        const string currentSql = @"
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
    'PUTAWAY',
    @LastOperator
);
";

                        using (SqlCommand cmd = new SqlCommand(currentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt)
                                .Value = command.BoxHuId;

                            cmd.Parameters.Add("@LocationId", SqlDbType.BigInt)
                                .Value = command.ToLocationId;

                            cmd.Parameters.Add("@LocationSince", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@LastMoveLogId", SqlDbType.BigInt)
                                .Value = moveLogId;

                            cmd.Parameters.Add("@LastMoveNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@LastOperator", SqlDbType.VarChar, 50)
                                .Value = DbValue(command.Operator);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 13. DETACH_HU审计
                        //------------------------------------------------

                        InsertOperationLog(
                            conn,
                            tran,
                            command.OperationNo,
                            "DETACH_HU",
                            command.PalletHuId,
                            command.BoxHuId,
                            command.RequestId,
                            command.BusinessNo,
                            command.Operator,
                            "拆托上架：解除托盘与外箱关系",
                            now);

                        //------------------------------------------------
                        // 14. PUTAWAY审计
                        //------------------------------------------------

                        InsertOperationLog(
                            conn,
                            tran,
                            command.OperationNo,
                            "PUTAWAY",
                            command.BoxHuId,
                            command.PalletHuId,
                            command.RequestId,
                            command.BusinessNo,
                            command.Operator,
                            command.Remark,
                            now);

                        //------------------------------------------------
                        // 15. 剩余箱数
                        //------------------------------------------------

                        int remainingBoxCount;

                        const string remainingSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU child
    ON child.Id = r.ChildHUId
WHERE r.ParentHUId = @PalletHUId
AND r.IsActive = 1
AND child.HUType = 'OUTER_BOX'
AND child.LifecycleStatus = 'ACTIVE';
";

                        using (SqlCommand cmd = new SqlCommand(remainingSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt)
                                .Value = command.PalletHuId;

                            remainingBoxCount = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        tran.Commit();

                        PutawayResultDto result = new PutawayResultDto();

                        result.OperationNo = command.OperationNo;
                        result.MoveLogId = moveLogId;
                        result.PalletHuCode = command.PalletHuCode;
                        result.BoxHuCode = command.BoxHuCode;
                        result.FromLocationCode = currentPalletLocationCode;
                        result.ToLocationCode = command.ToLocationCode;
                        result.RemainingBoxCount = remainingBoxCount;
                        result.IsPalletEmpty = remainingBoxCount == 0;
                        result.IsDuplicate = false;
                        result.PutawayTime = now;

                        return PutawayPersistenceResult.Success(result);
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

        private static void InsertOperationLog(
            SqlConnection conn,
            SqlTransaction tran,
            string operationNo,
            string operationType,
            long huId,
            long relatedHuId,
            string requestId,
            string businessNo,
            string operatorCode,
            string remark,
            DateTime operationTime)
        {
            const string sql = @"
INSERT INTO dbo.WMS_HU_OperationLog
(
    OperationNo,
    OperationType,
    HUId,
    RelatedHUId,
    BusinessNo,
    RequestId,
    Operator,
    OperationTime,
    Remark
)
VALUES
(
    @OperationNo,
    @OperationType,
    @HUId,
    @RelatedHUId,
    @BusinessNo,
    @RequestId,
    @Operator,
    @OperationTime,
    @Remark
);
";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                    .Value = operationNo;

                cmd.Parameters.Add("@OperationType", SqlDbType.VarChar, 30)
                    .Value = operationType;

                cmd.Parameters.Add("@HUId", SqlDbType.BigInt)
                    .Value = huId;

                cmd.Parameters.Add("@RelatedHUId", SqlDbType.BigInt)
                    .Value = relatedHuId;

                cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                    .Value = DbValue(businessNo);

                cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                    .Value = DbValue(requestId);

                cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                    .Value = DbValue(operatorCode);

                cmd.Parameters.Add("@OperationTime", SqlDbType.DateTime2)
                    .Value = operationTime;

                cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                    .Value = DbValue(remark);

                cmd.ExecuteNonQuery();
            }
        }

        private static string DbString(object value)
        {
            return value == DBNull.Value ? null : Convert.ToString(value);
        }

        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }
    }
}