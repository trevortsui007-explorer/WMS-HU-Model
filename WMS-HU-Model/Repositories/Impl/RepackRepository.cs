using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Repack;

namespace WMS_HU_Model.Repositories
{
    public class RepackRepository : IRepackRepository
    {
        public BoxRepackStatusDto GetBoxStatus(long boxHuId)
        {
            const string boxSql = @"
SELECT
    h.Id AS BoxHuId,
    h.HUCode AS BoxHuCode,
    h.PlantCode,
    h.LifecycleStatus,
    h.IsHold,
    l.LocationCode,
    w.WarehouseCode,
    z.ZoneCode
FROM dbo.WMS_HU h
LEFT JOIN dbo.WMS_HU_CurrentLocation cl
    ON cl.HUId = h.Id
LEFT JOIN dbo.WMS_Location l
    ON l.Id = cl.LocationId
LEFT JOIN dbo.WMS_Warehouse w
    ON w.Id = l.WarehouseId
LEFT JOIN dbo.WMS_Zone z
    ON z.Id = l.ZoneId
WHERE h.Id = @BoxHUId;
";

            const string innerSql = @"
SELECT
    innerHU.HUCode,
    Qty =
    ISNULL
    (
        (
            SELECT SUM(item.Qty)
            FROM dbo.WMS_HU_Item item
            WHERE item.HUId = innerHU.Id
            AND item.IsActive = 1
        ),
        0
    )
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU innerHU
    ON innerHU.Id = r.ChildHUId
WHERE r.ParentHUId = @BoxHUId
AND r.IsActive = 1
AND innerHU.HUType = 'INNER_PACK'
AND innerHU.LifecycleStatus = 'ACTIVE'
ORDER BY innerHU.HUCode;
";

            BoxRepackStatusDto result =
                DbExecutor.QueryFirstOrDefault<BoxRepackStatusDto>(
                    boxSql,
                    new { BoxHUId = boxHuId });

            if (result == null)
                return null;

            List<InnerPackRow> innerPacks = DbExecutor.Query<InnerPackRow>(
                innerSql,
                new { BoxHUId = boxHuId });

            foreach (InnerPackRow innerPack in innerPacks)
            {
                result.InnerPackCodes.Add(innerPack.HuCode);
                result.TotalQty += innerPack.Qty;
            }

            result.InnerPackCount = result.InnerPackCodes.Count;

            return result;
        }

        public UnpackInnerResultDto GetUnpackByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;

            const string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    sourceBox.HUCode AS SourceBoxHUCode,
    innerHU.HUCode AS InnerHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveTime AS OperationTime,
    SourceRemainingInnerCount = (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation r2
        INNER JOIN dbo.WMS_HU h2 ON h2.Id = r2.ChildHUId
        WHERE r2.ParentHUId = sourceBox.Id
          AND r2.IsActive = 1
          AND h2.HUType = 'INNER_PACK'
          AND h2.LifecycleStatus = 'ACTIVE'
    )
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU innerHU ON innerHU.Id = op.HUId
INNER JOIN dbo.WMS_HU sourceBox ON sourceBox.Id = op.RelatedHUId
INNER JOIN dbo.WMS_HU_MoveLog moveLog ON moveLog.MoveNo = op.OperationNo AND moveLog.HUId = innerHU.Id
LEFT JOIN dbo.WMS_Location fromLoc ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc ON toLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'UNPACK_INNER'
  AND op.RequestId = @RequestId;
";

            UnpackInnerResultDto result =
                DbExecutor.QueryFirstOrDefault<UnpackInnerResultDto>(
                    sql,
                    new { RequestId = requestId });

            if (result == null)
                return null;

            result.IsDuplicate = true;
            result.IsSourceEmpty = result.SourceRemainingInnerCount == 0;

            return result;
        }

        public UnpackInnerPersistenceResult ExecuteUnpackInner(UnpackInnerCommand command)
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
                        // 1. 锁 SourceBox + Inner
                        //------------------------------------------------
                        const string lockHuSql = @"
SELECT Id, HUType, PlantCode, LifecycleStatus, IsHold
FROM dbo.WMS_HU WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN (@SourceBoxHUId, @InnerHUId);";

                        int huCount = 0;
                        using (SqlCommand cmd = new SqlCommand(lockHuSql, conn, tran))
                        {
                            cmd.Parameters.Add("@SourceBoxHUId", SqlDbType.BigInt).Value = command.SourceBoxHuId;
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt).Value = command.InnerHuId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    huCount++;
                                    string status = Convert.ToString(reader["LifecycleStatus"]);
                                    bool isHold = Convert.ToBoolean(reader["IsHold"]);

                                    if (status != "ACTIVE")
                                    {
                                        tran.Rollback();
                                        return UnpackInnerPersistenceResult.Fail("参与拆箱的HU状态已发生变化。");
                                    }

                                    if (isHold)
                                    {
                                        tran.Rollback();
                                        return UnpackInnerPersistenceResult.Fail("参与拆箱的HU已被冻结。");
                                    }
                                }
                            }
                        }

                        if (huCount != 2)
                        {
                            tran.Rollback();
                            return UnpackInnerPersistenceResult.Fail("拆箱HU数据不完整。");
                        }

                        //------------------------------------------------
                        // 2. SourceBox → Inner Relation
                        //------------------------------------------------
                        long relationId;
                        const string relationSql = @"
SELECT Id
FROM dbo.WMS_HU_Relation WITH (UPDLOCK, HOLDLOCK)
WHERE ParentHUId = @SourceBoxHUId
  AND ChildHUId = @InnerHUId
  AND IsActive = 1;";

                        using (SqlCommand cmd = new SqlCommand(relationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@SourceBoxHUId", SqlDbType.BigInt).Value = command.SourceBoxHuId;
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt).Value = command.InnerHuId;

                            object value = cmd.ExecuteScalar();
                            if (value == null || value == DBNull.Value)
                            {
                                tran.Rollback();
                                return UnpackInnerPersistenceResult.Fail("内包当前已不属于指定外箱。");
                            }

                            relationId = Convert.ToInt64(value);
                        }

                        //------------------------------------------------
                        // 3. Inner不能已有DirectLocation
                        //------------------------------------------------
                        const string innerCurrentSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_CurrentLocation WITH (UPDLOCK, HOLDLOCK)
WHERE HUId = @InnerHUId;";

                        using (SqlCommand cmd = new SqlCommand(innerCurrentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt).Value = command.InnerHuId;

                            int count = Convert.ToInt32(cmd.ExecuteScalar());
                            if (count > 0)
                            {
                                tran.Rollback();
                                return UnpackInnerPersistenceResult.Fail("内包已经存在直接货位，数据状态异常。");
                            }
                        }

                        //------------------------------------------------
                        // 4. 锁源箱CurrentLocation
                        //------------------------------------------------
                        long sourceLocationId;
                        string sourceLocationCode;

                        ReadDirectLocation(conn, tran, command.SourceBoxHuId, out sourceLocationId, out sourceLocationCode);

                        if (sourceLocationId != command.FromLocationId)
                        {
                            tran.Rollback();
                            return UnpackInnerPersistenceResult.Fail("源外箱位置已经发生变化。");
                        }

                        //------------------------------------------------
                        // 5. 锁目标货位
                        //------------------------------------------------
                        string targetWarehouseCode, targetZoneCode, targetPlantPolicy, dedicatedPlantCode;
                        const string targetSql = @"
SELECT l.MasterStatus, l.IsVirtual, l.PlantPolicy, l.DedicatedPlantCode, w.WarehouseCode, z.ZoneCode
FROM dbo.WMS_Location l WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Warehouse w ON w.Id = l.WarehouseId
INNER JOIN dbo.WMS_Zone z ON z.Id = l.ZoneId
WHERE l.Id = @LocationId;";

                        using (SqlCommand cmd = new SqlCommand(targetSql, conn, tran))
                        {
                            cmd.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = command.ToLocationId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return UnpackInnerPersistenceResult.Fail("目标货位不存在。");
                                }

                                string status = Convert.ToString(reader["MasterStatus"]);
                                bool isVirtual = Convert.ToBoolean(reader["IsVirtual"]);

                                targetWarehouseCode = Convert.ToString(reader["WarehouseCode"]);
                                targetZoneCode = Convert.ToString(reader["ZoneCode"]);
                                targetPlantPolicy = Convert.ToString(reader["PlantPolicy"]);
                                dedicatedPlantCode = DbParameterValue.StringOrNull(reader["DedicatedPlantCode"]);

                                if (status != "ACTIVE")
                                {
                                    tran.Rollback();
                                    return UnpackInnerPersistenceResult.Fail("目标货位当前不可用。");
                                }

                                if (isVirtual)
                                {
                                    tran.Rollback();
                                    return UnpackInnerPersistenceResult.Fail("拆出的内包必须进入实体货位。");
                                }
                            }
                        }

                        if (!string.Equals(targetWarehouseCode, command.WarehouseCode, StringComparison.OrdinalIgnoreCase))
                        {
                            tran.Rollback();
                            return UnpackInnerPersistenceResult.Fail("目标货位不属于指定仓库。");
                        }

                        if (targetZoneCode != "STORAGE")
                        {
                            tran.Rollback();
                            return UnpackInnerPersistenceResult.Fail("拆出的内包必须进入STORAGE区域。");
                        }

                        //------------------------------------------------
                        // 6. WarehousePlant
                        //------------------------------------------------
                        const string plantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_WarehousePlant wp
INNER JOIN dbo.WMS_Warehouse w ON w.Id = wp.WarehouseId
WHERE w.WarehouseCode = @WarehouseCode
  AND wp.PlantCode = @PlantCode
  AND wp.IsActive = 1
  AND w.IsActive = 1;";

                        using (SqlCommand cmd = new SqlCommand(plantSql, conn, tran))
                        {
                            cmd.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;
                            cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = command.PlantCode;

                            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                            {
                                tran.Rollback();
                                return UnpackInnerPersistenceResult.Fail("目标仓库不允许存储该厂别库存。");
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
                                return UnpackInnerPersistenceResult.Fail("目标货位属于其他厂别专用货位。");
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
INNER JOIN dbo.WMS_HU h ON h.Id = cl.HUId
WHERE cl.LocationId = @LocationId
  AND h.PlantCode <> @PlantCode
  AND h.LifecycleStatus = 'ACTIVE';";

                            using (SqlCommand cmd = new SqlCommand(samePlantSql, conn, tran))
                            {
                                cmd.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = command.ToLocationId;
                                cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = command.PlantCode;

                                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                {
                                    tran.Rollback();
                                    return UnpackInnerPersistenceResult.Fail("目标货位存在其他厂别库存。");
                                }
                            }
                        }

                        //------------------------------------------------
                        // 9. 关闭Box → Inner Relation
                        //------------------------------------------------
                        const string closeRelationSql = @"
UPDATE dbo.WMS_HU_Relation
SET EffectiveTo = @Now,
    IsActive = 0,
    CloseOperationNo = @OperationNo,
    CloseUser = @Operator,
    CloseReason = N'UNPACK_INNER'
WHERE Id = @RelationId
  AND IsActive = 1;";

                        using (SqlCommand cmd = new SqlCommand(closeRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                            cmd.Parameters.Add("@RelationId", SqlDbType.BigInt).Value = relationId;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                tran.Rollback();
                                return UnpackInnerPersistenceResult.Fail("关闭外箱内包关系失败。");
                            }
                        }

                        //------------------------------------------------
                        // 10. MoveLog
                        //------------------------------------------------
                        long moveLogId;
                        const string moveSql = @"
INSERT INTO dbo.WMS_HU_MoveLog
(
    MoveNo, HUId, FromLocationId, ToLocationId, MoveType,
    ScannedLocationCode, BusinessNo, RequestId, Operator, MoveTime, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @MoveNo, @HUId, @FromLocationId, @ToLocationId, 'UNPACK_INNER',
    @ScannedLocationCode, @BusinessNo, @RequestId, @Operator, @MoveTime, @Remark
);";

                        using (SqlCommand cmd = new SqlCommand(moveSql, conn, tran))
                        {
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = command.InnerHuId;
                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt).Value = sourceLocationId;
                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt).Value = command.ToLocationId;
                            cmd.Parameters.Add("@ScannedLocationCode", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.ScannedLocationCode);
                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.BusinessNo);
                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = DbParameterValue.StringOrDbNull(command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 11. Inner建立DirectLocation
                        //------------------------------------------------
                        const string insertCurrentSql = @"
INSERT INTO dbo.WMS_HU_CurrentLocation
(
    HUId, LocationId, LocationSince, LastMoveLogId, LastMoveNo, LastMoveType, LastOperator
)
VALUES
(
    @HUId, @LocationId, @LocationSince, @MoveLogId, @MoveNo, 'UNPACK_INNER', @Operator
);";

                        using (SqlCommand cmd = new SqlCommand(insertCurrentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = command.InnerHuId;
                            cmd.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = command.ToLocationId;
                            cmd.Parameters.Add("@LocationSince", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 12. OperationLog
                        //------------------------------------------------
                        InsertRepackOperationLog(conn, tran, command.OperationNo, "DETACH_HU", command.SourceBoxHuId, command.InnerHuId, command.BusinessNo, command.RequestId, command.Operator, "拆箱：解除外箱与内包关系", now);
                        InsertRepackOperationLog(conn, tran, command.OperationNo, "UNPACK_INNER", command.InnerHuId, command.SourceBoxHuId, command.BusinessNo, command.RequestId, command.Operator, command.Remark, now);

                        int remaining = CountInner(conn, tran, command.SourceBoxHuId);

                        tran.Commit();

                        UnpackInnerResultDto result = new UnpackInnerResultDto
                        {
                            OperationNo = command.OperationNo,
                            MoveLogId = moveLogId,
                            SourceBoxHuCode = command.SourceBoxHuCode,
                            InnerHuCode = command.InnerHuCode,
                            FromLocationCode = sourceLocationCode,
                            ToLocationCode = command.ToLocationCode,
                            SourceRemainingInnerCount = remaining,
                            IsSourceEmpty = remaining == 0,
                            IsDuplicate = false,
                            OperationTime = now
                        };

                        return UnpackInnerPersistenceResult.Success(result);
                    }
                    catch
                    {
                        if (tran.Connection != null)
                        {
                            tran.Rollback();
                        }
                        throw;
                    }
                }
            }
        }

        public PackInnerResultDto GetPackByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return null;
            }

            const string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    targetBox.HUCode AS TargetBoxHUCode,
    innerHU.HUCode AS InnerHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveTime AS OperationTime,
    TargetInnerCount = (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation r
        INNER JOIN dbo.WMS_HU h ON h.Id = r.ChildHUId
        WHERE r.ParentHUId = targetBox.Id
          AND r.IsActive = 1
          AND h.HUType = 'INNER_PACK'
          AND h.LifecycleStatus = 'ACTIVE'
    )
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU innerHU ON innerHU.Id = op.HUId
INNER JOIN dbo.WMS_HU targetBox ON targetBox.Id = op.RelatedHUId
INNER JOIN dbo.WMS_HU_MoveLog moveLog ON moveLog.MoveNo = op.OperationNo AND moveLog.HUId = innerHU.Id
LEFT JOIN dbo.WMS_Location fromLoc ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc ON toLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'PACK_INNER'
  AND op.RequestId = @RequestId;";

            PackInnerResultDto result =
                DbExecutor.QueryFirstOrDefault<PackInnerResultDto>(
                    sql,
                    new { RequestId = requestId });

            if (result != null)
                result.IsDuplicate = true;

            return result;
        }

        public PackInnerPersistenceResult ExecutePackInner(PackInnerCommand command)
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
                        // 1. 锁 TargetBox + Inner (强制按 ID 顺序加锁，彻底规避并发死锁)
                        //------------------------------------------------
                        long firstLockId = Math.Min(command.TargetBoxHuId, command.InnerHuId);
                        long secondLockId = Math.Max(command.TargetBoxHuId, command.InnerHuId);

                        const string lockHuSql = @"
SELECT Id, HUType, PlantCode, LifecycleStatus, IsHold
FROM dbo.WMS_HU WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN (@FirstId, @SecondId)
ORDER BY Id ASC;";

                        int huCount = 0;
                        using (SqlCommand cmd = new SqlCommand(lockHuSql, conn, tran))
                        {
                            cmd.Parameters.Add("@FirstId", SqlDbType.BigInt).Value = firstLockId;
                            cmd.Parameters.Add("@SecondId", SqlDbType.BigInt).Value = secondLockId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    huCount++;
                                    string status = Convert.ToString(reader["LifecycleStatus"]);
                                    bool hold = Convert.ToBoolean(reader["IsHold"]);

                                    if (status != "ACTIVE")
                                    {
                                        return PackInnerPersistenceResult.Fail("参与装箱的HU状态已发生变化。");
                                    }

                                    if (hold)
                                    {
                                        return PackInnerPersistenceResult.Fail("参与装箱的HU已被冻结。");
                                    }
                                }
                            }
                        }

                        if (huCount != 2)
                        {
                            return PackInnerPersistenceResult.Fail("装箱HU数据不完整。");
                        }

                        //------------------------------------------------
                        // 2. Inner 不能存在 Active Parent
                        //------------------------------------------------
                        const string parentSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation WITH (UPDLOCK, HOLDLOCK)
WHERE ChildHUId = @InnerHUId
  AND IsActive = 1;";

                        using (SqlCommand cmd = new SqlCommand(parentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt).Value = command.InnerHuId;
                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count > 0)
                            {
                                return PackInnerPersistenceResult.Fail("内包当前已经属于其他父HU。");
                            }
                        }

                        //------------------------------------------------
                        // 3. Inner 必须有 DirectLocation
                        //------------------------------------------------
                        ReadDirectLocation(conn, tran, command.InnerHuId, out long innerLocationId, out string innerLocationCode);

                        if (innerLocationId != command.FromLocationId)
                        {
                            return PackInnerPersistenceResult.Fail("内包当前位置已经发生变化。");
                        }

                        //------------------------------------------------
                        // 4. TargetBox 必须有 DirectLocation
                        //------------------------------------------------
                        ReadDirectLocation(conn, tran, command.TargetBoxHuId, out long boxLocationId, out string boxLocationCode);

                        if (boxLocationId != command.ToLocationId)
                        {
                            return PackInnerPersistenceResult.Fail("目标外箱位置已经发生变化。");
                        }

                        //------------------------------------------------
                        // 5. 建立 Box → Inner Relation
                        //------------------------------------------------
                        const string relationSql = @"
INSERT INTO dbo.WMS_HU_Relation
(
    ParentHUId, ChildHUId, RelationType, EffectiveFrom, IsActive, OperationNo, CreateUser
)
VALUES
(
    @TargetBoxHUId, @InnerHUId, 'CONTAIN', @Now, 1, @OperationNo, @Operator
);";

                        using (SqlCommand cmd = new SqlCommand(relationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt).Value = command.TargetBoxHuId;
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt).Value = command.InnerHuId;
                            cmd.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 6. MoveLog
                        //------------------------------------------------
                        const string moveSql = @"
INSERT INTO dbo.WMS_HU_MoveLog
(
    MoveNo, HUId, FromLocationId, ToLocationId, MoveType,
    ScannedLocationCode, BusinessNo, RequestId, Operator, MoveTime, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @MoveNo, @HUId, @FromLocationId, @ToLocationId, 'PACK_INNER',
    @ToLocationCode, @BusinessNo, @RequestId, @Operator, @MoveTime, @Remark
);";

                        long moveLogId;
                        using (SqlCommand cmd = new SqlCommand(moveSql, conn, tran))
                        {
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = command.InnerHuId;
                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt).Value = innerLocationId;
                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt).Value = boxLocationId;
                            cmd.Parameters.Add("@ToLocationCode", SqlDbType.VarChar, 100).Value = boxLocationCode;
                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.BusinessNo);
                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = DbParameterValue.StringOrDbNull(command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 7. Inner 不再有 DirectLocation
                        //------------------------------------------------
                        const string deleteCurrentSql = @"
DELETE FROM dbo.WMS_HU_CurrentLocation
WHERE HUId = @InnerHUId
  AND LocationId = @ExpectedLocationId;";

                        using (SqlCommand cmd = new SqlCommand(deleteCurrentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt).Value = command.InnerHuId;
                            cmd.Parameters.Add("@ExpectedLocationId", SqlDbType.BigInt).Value = innerLocationId;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                return PackInnerPersistenceResult.Fail("清除内包独立货位失败。");
                            }
                        }

                        //------------------------------------------------
                        // 8. OperationLog & Commit
                        //------------------------------------------------
                        InsertRepackOperationLog(conn, tran, command.OperationNo, "ATTACH_HU", command.TargetBoxHuId, command.InnerHuId, command.BusinessNo, command.RequestId, command.Operator, "装箱：建立外箱与内包关系", now);
                        InsertRepackOperationLog(conn, tran, command.OperationNo, "PACK_INNER", command.InnerHuId, command.TargetBoxHuId, command.BusinessNo, command.RequestId, command.Operator, command.Remark, now);

                        int targetCount = CountInner(conn, tran, command.TargetBoxHuId);

                        // 显式提交事务
                        tran.Commit();

                        PackInnerResultDto result = new PackInnerResultDto
                        {
                            OperationNo = command.OperationNo,
                            MoveLogId = moveLogId,
                            TargetBoxHuCode = command.TargetBoxHuCode,
                            InnerHuCode = command.InnerHuCode,
                            FromLocationCode = innerLocationCode,
                            ToLocationCode = boxLocationCode,
                            TargetInnerCount = targetCount,
                            IsDuplicate = false,
                            OperationTime = now
                        };

                        return PackInnerPersistenceResult.Success(result);
                    }
                    catch
                    {
                        // 发生未捕获异常时回滚
                        if (tran.Connection != null)
                        {
                            tran.Rollback();
                        }
                        throw;
                    }
                }
            }
        }

        public UnpackBoxResultDto GetUnpackBoxByRequestId(string requestId)
        {
            return new WholeBoxUnpackRepository().GetByRequestId(requestId);
        }

        public UnpackBoxPersistenceResult ExecuteUnpackBox(UnpackBoxCommand command)
        {
            return new WholeBoxUnpackRepository().Execute(command);
        }

        private static void ReadDirectLocation(
    SqlConnection conn,
    SqlTransaction tran,
    long huId,
    out long locationId,
    out string locationCode)
        {
            const string sql = @"
SELECT TOP 1
    cl.LocationId,
    l.LocationCode
FROM dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Location l ON l.Id = cl.LocationId
WHERE cl.HUId = @HUId;";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = huId;

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException($"HU [{huId}] 没有直接货位。");
                    }

                    locationId = Convert.ToInt64(reader["LocationId"]);
                    locationCode = Convert.ToString(reader["LocationCode"]);
                }
            }
        }

        private static void InsertRepackOperationLog(
    SqlConnection conn,
    SqlTransaction tran,
    string operationNo,
    string operationType,
    long huId,
    long relatedHuId,
    string businessNo,
    string requestId,
    string operatorCode,
    string remark,
    DateTime operationTime)
        {
            HuOperationLogWriter.Insert(
                conn,
                tran,
                operationNo,
                operationType,
                huId,
                relatedHuId,
                businessNo,
                requestId,
                operatorCode,
                operationTime,
                detailJson: null,
                remark: remark);
        }

        public TransferInnerResultDto GetTransferByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;

            const string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    innerHU.HUCode AS InnerHUCode,
    sourceBox.HUCode AS SourceBoxHUCode,
    targetBox.HUCode AS TargetBoxHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveTime AS OperationTime,

    SourceRemainingInnerCount =
    (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation r1
        INNER JOIN dbo.WMS_HU h1
            ON h1.Id = r1.ChildHUId
        WHERE r1.ParentHUId = sourceBox.Id
        AND r1.IsActive = 1
        AND h1.HUType = 'INNER_PACK'
        AND h1.LifecycleStatus = 'ACTIVE'
    ),

    TargetInnerCount =
    (
        SELECT COUNT(1)
        FROM dbo.WMS_HU_Relation r2
        INNER JOIN dbo.WMS_HU h2
            ON h2.Id = r2.ChildHUId
        WHERE r2.ParentHUId = targetBox.Id
        AND r2.IsActive = 1
        AND h2.HUType = 'INNER_PACK'
        AND h2.LifecycleStatus = 'ACTIVE'
    )
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU innerHU
    ON innerHU.Id = op.HUId
INNER JOIN dbo.WMS_HU targetBox
    ON targetBox.Id = op.RelatedHUId
INNER JOIN dbo.WMS_HU_Relation sourceRel
    ON sourceRel.ChildHUId = innerHU.Id
   AND sourceRel.CloseOperationNo = op.OperationNo
INNER JOIN dbo.WMS_HU sourceBox
    ON sourceBox.Id = sourceRel.ParentHUId
INNER JOIN dbo.WMS_HU_Relation targetRel
    ON targetRel.ChildHUId = innerHU.Id
   AND targetRel.OperationNo = op.OperationNo
INNER JOIN dbo.WMS_HU_MoveLog moveLog
    ON moveLog.MoveNo = op.OperationNo
   AND moveLog.HUId = innerHU.Id
LEFT JOIN dbo.WMS_Location fromLoc
    ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc
    ON toLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'REPACK_TRANSFER'
AND op.RequestId = @RequestId;
";

            TransferInnerResultDto result =
                DbExecutor.QueryFirstOrDefault<TransferInnerResultDto>(
                    sql,
                    new { RequestId = requestId });

            if (result == null)
                return null;

            result.IsSourceEmpty = result.SourceRemainingInnerCount == 0;
            result.IsDuplicate = true;

            return result;
        }

        public TransferInnerPersistenceResult ExecuteTransferInner(TransferInnerCommand command)
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
                        // 1. 锁三个HU
                        //------------------------------------------------

                        const string lockHuSql = @"
SELECT
    Id,
    HUCode,
    HUType,
    PlantCode,
    LifecycleStatus,
    IsHold
FROM dbo.WMS_HU
WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN
(
    @SourceBoxHUId,
    @TargetBoxHUId,
    @InnerHUId
);
";

                        int huCount = 0;

                        using (SqlCommand cmd = new SqlCommand(lockHuSql, conn, tran))
                        {
                            cmd.Parameters.Add("@SourceBoxHUId", SqlDbType.BigInt)
                                .Value = command.SourceBoxHuId;

                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt)
                                .Value = command.TargetBoxHuId;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    huCount++;

                                    string status = Convert.ToString(reader["LifecycleStatus"]);
                                    bool isHold = Convert.ToBoolean(reader["IsHold"]);

                                    if (status != "ACTIVE")
                                    {
                                        tran.Rollback();
                                        return TransferInnerPersistenceResult.Fail("参与Repack的HU状态已变化。");
                                    }

                                    if (isHold)
                                    {
                                        tran.Rollback();
                                        return TransferInnerPersistenceResult.Fail("参与Repack的HU已被冻结。");
                                    }
                                }
                            }
                        }

                        if (huCount != 3)
                        {
                            tran.Rollback();
                            return TransferInnerPersistenceResult.Fail("Repack HU数据不完整。");
                        }

                        //------------------------------------------------
                        // 2. Source → Inner Active Relation
                        //------------------------------------------------

                        long sourceRelationId;

                        const string sourceRelationSql = @"
SELECT Id
FROM dbo.WMS_HU_Relation
WITH (UPDLOCK, HOLDLOCK)
WHERE ParentHUId = @SourceBoxHUId
AND ChildHUId = @InnerHUId
AND IsActive = 1;
";

                        using (SqlCommand cmd = new SqlCommand(sourceRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@SourceBoxHUId", SqlDbType.BigInt)
                                .Value = command.SourceBoxHuId;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            object value = cmd.ExecuteScalar();

                            if (value == null || value == DBNull.Value)
                            {
                                tran.Rollback();
                                return TransferInnerPersistenceResult.Fail("内包当前已不属于源外箱，请刷新数据。");
                            }

                            sourceRelationId = Convert.ToInt64(value);
                        }

                        //------------------------------------------------
                        // 3. Inner不能已有DirectLocation
                        //------------------------------------------------

                        const string innerLocationSql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_CurrentLocation
WITH (UPDLOCK, HOLDLOCK)
WHERE HUId = @InnerHUId;
";

                        using (SqlCommand cmd = new SqlCommand(innerLocationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count > 0)
                            {
                                tran.Rollback();
                                return TransferInnerPersistenceResult.Fail("内包已经存在直接货位，不能执行箱到箱Transfer。");
                            }
                        }

                        //------------------------------------------------
                        // 4. Source/Target直接位置重新锁定
                        //------------------------------------------------

                        long sourceLocationId;
                        string sourceLocationCode;
                        long targetLocationId;
                        string targetLocationCode;

                        ReadBoxLocation(
                            conn,
                            tran,
                            command.SourceBoxHuId,
                            out sourceLocationId,
                            out sourceLocationCode);

                        ReadBoxLocation(
                            conn,
                            tran,
                            command.TargetBoxHuId,
                            out targetLocationId,
                            out targetLocationCode);

                        if (sourceLocationId != command.FromLocationId)
                        {
                            tran.Rollback();
                            return TransferInnerPersistenceResult.Fail("源外箱位置已经发生变化。");
                        }

                        if (targetLocationId != command.ToLocationId)
                        {
                            tran.Rollback();
                            return TransferInnerPersistenceResult.Fail("目标外箱位置已经发生变化。");
                        }

                        //------------------------------------------------
                        // 5. 关闭旧Relation
                        //------------------------------------------------

                        const string closeRelationSql = @"
UPDATE dbo.WMS_HU_Relation
SET
    EffectiveTo = @Now,
    IsActive = 0,
    CloseOperationNo = @OperationNo,
    CloseUser = @Operator,
    CloseReason = N'REPACK_TRANSFER'
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
                                .Value = DbParameterValue.StringOrDbNull(command.Operator);

                            cmd.Parameters.Add("@RelationId", SqlDbType.BigInt)
                                .Value = sourceRelationId;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                tran.Rollback();
                                return TransferInnerPersistenceResult.Fail("关闭源包装关系失败。");
                            }
                        }

                        //------------------------------------------------
                        // 6. 建立 Target → Inner Relation
                        //------------------------------------------------

                        const string createRelationSql = @"
INSERT INTO dbo.WMS_HU_Relation
(
    ParentHUId,
    ChildHUId,
    RelationType,
    EffectiveFrom,
    IsActive,
    OperationNo,
    CreateUser
)
VALUES
(
    @TargetBoxHUId,
    @InnerHUId,
    'CONTAIN',
    @Now,
    1,
    @OperationNo,
    @Operator
);
";

                        using (SqlCommand cmd = new SqlCommand(createRelationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt)
                                .Value = command.TargetBoxHuId;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            cmd.Parameters.Add("@Now", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbParameterValue.StringOrDbNull(command.Operator);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 7. Inner MoveLog
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
    'REPACK_TRANSFER',
    @ToLocationCode,
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
                                .Value = command.InnerHuId;

                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt)
                                .Value = sourceLocationId;

                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt)
                                .Value = targetLocationId;

                            cmd.Parameters.Add("@ToLocationCode", SqlDbType.VarChar, 100)
                                .Value = targetLocationCode;

                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                                .Value = DbParameterValue.StringOrDbNull(command.BusinessNo);

                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                                .Value = command.RequestId;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbParameterValue.StringOrDbNull(command.Operator);

                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                                .Value = DbParameterValue.StringOrDbNull(command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 8. OperationLog
                        //------------------------------------------------

                        const string operationSql = @"
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
    'REPACK_TRANSFER',
    @InnerHUId,
    @TargetBoxHUId,
    @BusinessNo,
    @RequestId,
    @Operator,
    @OperationTime,
    @Remark
);
";

                        using (SqlCommand cmd = new SqlCommand(operationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                                .Value = command.OperationNo;

                            cmd.Parameters.Add("@InnerHUId", SqlDbType.BigInt)
                                .Value = command.InnerHuId;

                            cmd.Parameters.Add("@TargetBoxHUId", SqlDbType.BigInt)
                                .Value = command.TargetBoxHuId;

                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                                .Value = DbParameterValue.StringOrDbNull(command.BusinessNo);

                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                                .Value = command.RequestId;

                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                                .Value = DbParameterValue.StringOrDbNull(command.Operator);

                            cmd.Parameters.Add("@OperationTime", SqlDbType.DateTime2)
                                .Value = now;

                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                                .Value = DbParameterValue.StringOrDbNull(command.Remark);

                            cmd.ExecuteNonQuery();
                        }

                        //------------------------------------------------
                        // 9. 统计
                        //------------------------------------------------

                        int sourceCount = CountInner(conn, tran, command.SourceBoxHuId);
                        int targetCount = CountInner(conn, tran, command.TargetBoxHuId);

                        tran.Commit();

                        TransferInnerResultDto result = new TransferInnerResultDto();

                        result.OperationNo = command.OperationNo;
                        result.MoveLogId = moveLogId;
                        result.InnerHuCode = command.InnerHuCode;
                        result.SourceBoxHuCode = command.SourceBoxHuCode;
                        result.TargetBoxHuCode = command.TargetBoxHuCode;
                        result.FromLocationCode = sourceLocationCode;
                        result.ToLocationCode = targetLocationCode;
                        result.SourceRemainingInnerCount = sourceCount;
                        result.TargetInnerCount = targetCount;
                        result.IsSourceEmpty = sourceCount == 0;
                        result.IsDuplicate = false;
                        result.OperationTime = now;

                        return TransferInnerPersistenceResult.Success(result);
                    }
                    catch (SqlException ex)
                    {
                        if (tran.Connection != null)
                            tran.Rollback();

                        //------------------------------------------------
                        // RequestId唯一索引命中
                        //------------------------------------------------

                        if (ex.Number == 2601 || ex.Number == 2627)
                        {
                            TransferInnerResultDto duplicate = GetTransferByRequestId(command.RequestId);

                            if (duplicate != null)
                            {
                                bool sameRequest =
                                    string.Equals(
                                        duplicate.InnerHuCode,
                                        command.InnerHuCode,
                                        StringComparison.OrdinalIgnoreCase)
                                    &&
                                    string.Equals(
                                        duplicate.SourceBoxHuCode,
                                        command.SourceBoxHuCode,
                                        StringComparison.OrdinalIgnoreCase)
                                    &&
                                    string.Equals(
                                        duplicate.TargetBoxHuCode,
                                        command.TargetBoxHuCode,
                                        StringComparison.OrdinalIgnoreCase);

                                if (sameRequest)
                                {
                                    duplicate.IsDuplicate = true;
                                    return TransferInnerPersistenceResult.Success(duplicate);
                                }
                            }
                        }

                        throw;
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

        private static void ReadBoxLocation(
            SqlConnection conn,
            SqlTransaction tran,
            long boxHuId,
            out long locationId,
            out string locationCode)
        {
            const string sql = @"
SELECT
    cl.LocationId,
    l.LocationCode
FROM dbo.WMS_HU_CurrentLocation cl
WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Location l
    ON l.Id = cl.LocationId
WHERE cl.HUId = @BoxHUId;
";

            BoxLocationRow row = DbExecutor.QueryFirstOrDefault<BoxLocationRow>(
                conn,
                tran,
                sql,
                new { BoxHUId = boxHuId });

            if (row == null)
                throw new InvalidOperationException("外箱没有直接货位，无法执行Repack。");

            locationId = row.LocationId;
            locationCode = row.LocationCode;
        }

        private static int CountInner(SqlConnection conn, SqlTransaction tran, long boxHuId)
        {
            const string sql = @"
SELECT COUNT(1)
FROM dbo.WMS_HU_Relation r
INNER JOIN dbo.WMS_HU h
    ON h.Id = r.ChildHUId
WHERE r.ParentHUId = @BoxHUId
AND r.IsActive = 1
AND h.HUType = 'INNER_PACK'
AND h.LifecycleStatus = 'ACTIVE';
";

            return DbExecutor.ExecuteScalar<int>(
                conn,
                tran,
                sql,
                new { BoxHUId = boxHuId });
        }

        private class InnerPackRow
        {
            public string HuCode { get; set; }
            public decimal Qty { get; set; }
        }

        private class BoxLocationRow
        {
            public long LocationId { get; set; }
            public string LocationCode { get; set; }
        }
    }
}
