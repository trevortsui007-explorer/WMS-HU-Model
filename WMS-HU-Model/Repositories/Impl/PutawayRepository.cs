using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
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
    h.Id AS PalletHuId,
    h.HUCode AS PalletHuCode,
    h.PlantCode,
    l.LocationCode,
    z.ZoneCode
FROM dbo.WMS_FG_HU h
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl
    ON cl.HUId = h.Id
LEFT JOIN dbo.WMS_FG_Location l
    ON l.Id = cl.LocationId
LEFT JOIN dbo.WMS_FG_Zone z
    ON z.Id = l.ZoneId
WHERE h.Id = @PalletHUId;
";

            const string boxSql = @"
SELECT
    child.HUCode
FROM dbo.WMS_FG_HU_Relation r
INNER JOIN dbo.WMS_FG_HU child
    ON child.Id = r.ChildHUId
WHERE r.ParentHUId = @PalletHUId
AND r.IsActive = 1
AND child.HUType = 'OUTER_BOX'
AND child.LifecycleStatus = 'ACTIVE'
ORDER BY child.HUCode;
";

            PalletPutawayStatusDto result =
                DbExecutor.QueryFirstOrDefault<PalletPutawayStatusDto>(
                    palletSql,
                    new { PalletHUId = palletHuId });

            if (result == null)
                return null;

            result.RemainingBoxCodes = DbExecutor.Query<string>(
                boxSql,
                new { PalletHUId = palletHuId });

            result.RemainingBoxCount = result.RemainingBoxCodes.Count;
            result.IsEmpty = result.RemainingBoxCount == 0;

            return result;
        }

        public PutawayResultDto GetByRequestId(string requestId)
        {
            return PutawayIdempotencyReader.Read(requestId);
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

                        InboundConfirmRepository.LockPallet(conn, tran, command.PalletHuId);
                        bool confirmedInbound = InboundConfirmRepository.IsRequired(conn, tran, command.PalletHuId);
                        string inboundBlockReason = InboundConfirmRepository.GetPutawayBlockReason(conn, tran, command.PalletHuId);
                        if (inboundBlockReason != null)
                        {
                            tran.Rollback();
                            return PutawayPersistenceResult.Fail(inboundBlockReason);
                        }

                        //------------------------------------------------
                        // 1. 锁定父HU和子HU
                        //------------------------------------------------
                        const string lockHuSql = @"
SELECT Id
FROM dbo.WMS_FG_HU
WITH (UPDLOCK, HOLDLOCK)
WHERE Id IN (@PalletHUId, @BoxHUId) AND LifecycleStatus='ACTIVE' AND IsHold=0;";

                        using (SqlCommand cmd = new SqlCommand(lockHuSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt).Value = command.PalletHuId;
                            cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt).Value = command.BoxHuId;

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
                                return PutawayPersistenceResult.Fail("托盘或外箱不存在、非ACTIVE或已冻结。");
                            }
                        }

                        //------------------------------------------------
                        // 2. 再次确认Active父子关系
                        //------------------------------------------------
                        long relationId;

                        const string relationSql = @"
SELECT Id
FROM dbo.WMS_FG_HU_Relation
WITH (UPDLOCK, HOLDLOCK)
WHERE ParentHUId = @PalletHUId
AND ChildHUId = @BoxHUId
AND IsActive = 1;";

                        using (SqlCommand cmd = new SqlCommand(relationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt).Value = command.PalletHuId;
                            cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt).Value = command.BoxHuId;

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
FROM dbo.WMS_FG_HU_CurrentLocation cl
WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_Location l
    ON l.Id = cl.LocationId
INNER JOIN dbo.WMS_FG_Zone z
    ON z.Id = l.ZoneId
WHERE cl.HUId = @PalletHUId;";

                        using (SqlCommand cmd = new SqlCommand(palletLocationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt).Value = command.PalletHuId;

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

                        long currentSourceLocationId = currentPalletLocationId;
                        string currentSourceLocationCode = currentPalletLocationCode;
                        const string boxLocationSql = @"
SELECT cl.LocationId,l.LocationCode FROM dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK,HOLDLOCK)
INNER JOIN dbo.WMS_FG_Location l ON l.Id=cl.LocationId WHERE cl.HUId=@BoxHUId;";
                        using (SqlCommand cmd = new SqlCommand(boxLocationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt).Value = command.BoxHuId;
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                bool hasDirectLocation = reader.Read();
                                if (confirmedInbound)
                                {
                                    if (!hasDirectLocation || Convert.ToString(reader["LocationCode"]) != "INBOUND-CONFIRMED")
                                        return PutawayPersistenceResult.Fail(InboundConfirmRepository.PendingMessage);
                                    currentSourceLocationId = Convert.ToInt64(reader["LocationId"]);
                                    currentSourceLocationCode = Convert.ToString(reader["LocationCode"]);
                                }
                                else if (hasDirectLocation)
                                    return PutawayPersistenceResult.Fail("历史托盘外箱已经存在直接货位，数据状态异常。");
                            }
                        }
                        if (currentSourceLocationId != command.FromLocationId)
                        {
                            tran.Rollback();
                            return PutawayPersistenceResult.Fail("外箱来源库位已变化，请重新执行上架。");
                        }

                        //------------------------------------------------
                        // 5. 锁定目标货位
                        //------------------------------------------------
                        string targetPlantPolicy;
                        string dedicatedPlantCode;

                        const string targetSql = @"
SELECT
    l.IsActive,
    l.IsVirtual,
    l.PlantPolicy,
    l.DedicatedPlantCode,
    z.ZoneCode,
    w.WarehouseCode
FROM dbo.WMS_FG_Location l
WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_Zone z
    ON z.Id = l.ZoneId
INNER JOIN dbo.WMS_FG_Warehouse w
    ON w.Id = l.WarehouseId
WHERE l.Id = @LocationId;";

                        using (SqlCommand cmd = new SqlCommand(targetSql, conn, tran))
                        {
                            cmd.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = command.ToLocationId;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return PutawayPersistenceResult.Fail("目标货位不存在。");
                                }

                                bool isActive = Convert.ToBoolean(reader["IsActive"]);
                                bool isVirtual = Convert.ToBoolean(reader["IsVirtual"]);
                                string zoneCode = Convert.ToString(reader["ZoneCode"]);
                                string warehouseCode = Convert.ToString(reader["WarehouseCode"]);

                                targetPlantPolicy = Convert.ToString(reader["PlantPolicy"]);
                                dedicatedPlantCode = DbParameterValue.StringOrNull(reader["DedicatedPlantCode"]);

                                if (!isActive)
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
                        // 6. 校验仓库厂别
                        //------------------------------------------------
                        const string plantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_FG_WarehousePlant wp
INNER JOIN dbo.WMS_FG_Warehouse w
    ON w.Id = wp.WarehouseId
WHERE w.WarehouseCode = @WarehouseCode
AND wp.PlantCode = @PlantCode
AND wp.IsActive = 1;";

                        using (SqlCommand cmd = new SqlCommand(plantSql, conn, tran))
                        {
                            cmd.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;
                            cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = command.PlantCode;

                            int count = Convert.ToInt32(cmd.ExecuteScalar());

                            if (count == 0)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("当前仓库不允许存储该厂别。");
                            }
                        }

                        //------------------------------------------------
                        // 7. DEDICATED
                        //------------------------------------------------
                        if (targetPlantPolicy == "DEDICATED" &&
                            !string.Equals(dedicatedPlantCode, command.PlantCode, StringComparison.OrdinalIgnoreCase))
                        {
                            tran.Rollback();
                            return PutawayPersistenceResult.Fail("目标货位为其他厂别专用货位。");
                        }

                        //------------------------------------------------
                        // 8. SAME_PLANT
                        //------------------------------------------------
                        if (targetPlantPolicy == "SAME_PLANT")
                        {
                            const string samePlantSql = @"
SELECT COUNT(1)
FROM dbo.WMS_FG_HU_CurrentLocation cl
INNER JOIN dbo.WMS_FG_HU h
    ON h.Id = cl.HUId
WHERE cl.LocationId = @LocationId
AND h.PlantCode <> @PlantCode
AND h.LifecycleStatus = 'ACTIVE';";

                            using (SqlCommand cmd = new SqlCommand(samePlantSql, conn, tran))
                            {
                                cmd.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = command.ToLocationId;
                                cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = command.PlantCode;

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
UPDATE dbo.WMS_FG_HU_Relation
SET
    EffectiveTo = @Now,
    IsActive = 0,
    CloseOperationNo = @OperationNo,
    CloseUser = @Operator,
    CloseReason = N'PUTAWAY'
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
                                return PutawayPersistenceResult.Fail("HU关系关闭失败。");
                            }
                        }

                        //------------------------------------------------
                        // 11. MoveLog
                        //------------------------------------------------
                        long moveLogId;

                        const string moveSql = @"
INSERT INTO dbo.WMS_FG_HU_MoveLog
(
    MoveNo,
    HUId,
    FromLocationId,
    ToLocationId,
    MoveType,
    BusinessNo,
    RequestId,
    MoveBy,
    MoveDt,
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
    @BusinessNo,
    @RequestId,
    @Operator,
    @MoveTime,
    @Remark
);";

                        using (SqlCommand cmd = new SqlCommand(moveSql, conn, tran))
                        {
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = command.BoxHuId;
                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt).Value = currentSourceLocationId;
                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt).Value = command.ToLocationId;
                            cmd.Parameters.Add("@ScannedLocationCode", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.ScannedLocationCode);
                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.BusinessNo);
                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = BusinessLogText.Move("PUTAWAY", command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 12. 更新已确认外箱库位；历史外箱建立直接库位
                        //------------------------------------------------
                        string currentSql = confirmedInbound
                            ? @"UPDATE dbo.WMS_FG_HU_CurrentLocation SET LocationId=@LocationId,LocationSince=@LocationSince,LastMoveLogId=@LastMoveLogId WHERE HUId=@HUId;"
                            : @"
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
    @CreateBy
);";

                        using (SqlCommand cmd = new SqlCommand(currentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = command.BoxHuId;
                            cmd.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = command.ToLocationId;
                            cmd.Parameters.Add("@LocationSince", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@LastMoveLogId", SqlDbType.BigInt).Value = moveLogId;
                            cmd.Parameters.Add("@CreateBy", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);

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
                            null,
                            now, "上架解绑：解除托盘与外箱关系");

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
                        // 15. 统计剩余外箱
                        //------------------------------------------------
                        int remainingBoxCount;

                        const string remainingSql = @"
SELECT COUNT(1)
FROM dbo.WMS_FG_HU_Relation r
INNER JOIN dbo.WMS_FG_HU child
    ON child.Id = r.ChildHUId
WHERE r.ParentHUId = @PalletHUId
AND r.IsActive = 1
AND child.HUType = 'OUTER_BOX'
AND child.LifecycleStatus = 'ACTIVE';";

                        using (SqlCommand cmd = new SqlCommand(remainingSql, conn, tran))
                        {
                            cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt).Value = command.PalletHuId;
                            remainingBoxCount = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 16. 最后一箱完成后自动关闭一次性运输托
                        //------------------------------------------------
                        bool palletClosed = false;

                        if (remainingBoxCount == 0)
                        {
                            const string closePalletSql = @"
UPDATE dbo.WMS_FG_HU
SET
    LifecycleStatus = 'CLOSED',
    LastEditBy = @Operator,
    LastEditDt = @Now
WHERE Id = @PalletHUId
AND HUType = 'PALLET'
AND LifecycleStatus = 'ACTIVE';";

                            using (SqlCommand cmd = new SqlCommand(closePalletSql, conn, tran))
                            {
                                cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                                cmd.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                                cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt).Value = command.PalletHuId;

                                palletClosed = cmd.ExecuteNonQuery() == 1;
                            }

                            if (!palletClosed)
                            {
                                tran.Rollback();
                                return PutawayPersistenceResult.Fail("运输托HU关闭失败。");
                            }

                            //------------------------------------------------
                            // 17. 一次性运输托关闭后不再保留CurrentLocation
                            //------------------------------------------------
                            const string deletePalletLocationSql = @"
DELETE FROM dbo.WMS_FG_HU_CurrentLocation
WHERE HUId = @PalletHUId;";

                            using (SqlCommand cmd = new SqlCommand(deletePalletLocationSql, conn, tran))
                            {
                                cmd.Parameters.Add("@PalletHUId", SqlDbType.BigInt).Value = command.PalletHuId;
                                cmd.ExecuteNonQuery();
                            }

                            //------------------------------------------------
                            // 18. CLOSE_HU审计
                            //------------------------------------------------
                            InsertOperationLog(
                                conn,
                                tran,
                                command.OperationNo,
                                "CLOSE_HU",
                                command.PalletHuId,
                                command.BoxHuId,
                                command.RequestId,
                                command.BusinessNo,
                                command.Operator,
                                null,
                                now, "上架完成：自动关闭运输托盘");
                        }

                        tran.Commit();

                        PutawayResultDto result = new PutawayResultDto
                        {
                            OperationNo = command.OperationNo,
                            MoveLogId = moveLogId,
                            SourceMode = confirmedInbound ? "PALLET_CONFIRMED" : "PALLET_INHERITED",
                            PalletHuCode = command.PalletHuCode,
                            BoxHuCode = command.BoxHuCode,
                            FromLocationCode = currentSourceLocationCode,
                            ToLocationCode = command.ToLocationCode,
                            RemainingBoxCount = remainingBoxCount,
                            IsPalletEmpty = remainingBoxCount == 0,
                            IsPalletClosed = palletClosed,
                            IsDuplicate = false,
                            PutawayTime = now
                        };

                        return PutawayPersistenceResult.Success(result);
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

        public PutawayPersistenceResult ExecuteDirectReceivePutaway(DirectReceivePutawayCommand command)
        {
            return new DirectReceivePutawayRepository().Execute(command);
        }

        public WholePalletPutawayResultDto GetWholePalletPutawayByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)) return null;
            const string headerSql = @"
SELECT TOP 1 op.OperationNo, pallet.HUCode AS PalletHuCode, op.OperationDt AS PutawayTime
FROM dbo.WMS_FG_HU_OperationLog op
INNER JOIN dbo.WMS_FG_HU pallet ON pallet.Id = op.HUId
WHERE op.OperationType = 'PUTAWAY_PALLET' AND op.RequestId = @RequestId;";
            WholePalletHeaderRow header = DbExecutor.QueryFirstOrDefault<WholePalletHeaderRow>(headerSql, new { RequestId = requestId });
            if (header == null) return null;
            const string itemSql = @"
SELECT box.HUCode AS BoxCode, fromLoc.LocationCode AS FromLocationCode, toLoc.LocationCode AS ToLocationCode
FROM dbo.WMS_FG_HU_OperationLog op
INNER JOIN dbo.WMS_FG_HU box ON box.Id = op.HUId
INNER JOIN dbo.WMS_FG_HU_MoveLog moveLog ON moveLog.MoveNo = op.OperationNo AND moveLog.HUId = box.Id
LEFT JOIN dbo.WMS_FG_Location fromLoc ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_FG_Location toLoc ON toLoc.Id = moveLog.ToLocationId
WHERE op.OperationType = 'PUTAWAY' AND op.RequestId = @RequestId
ORDER BY box.HUCode;";
            List<WholePalletItemRow> items = DbExecutor.Query<WholePalletItemRow>(itemSql, new { RequestId = requestId });
            return new WholePalletPutawayResultDto
            {
                OperationNo = header.OperationNo,
                PalletHuCode = header.PalletHuCode,
                FromLocationCode = items.Select(x => x.FromLocationCode).FirstOrDefault(),
                ToLocationCode = items.Select(x => x.ToLocationCode).FirstOrDefault(),
                BoxCodes = items.Select(x => x.BoxCode).ToList(),
                MovedCount = items.Count,
                IsPalletClosed = true,
                IsDuplicate = true,
                PutawayTime = header.PutawayTime
            };
        }

        public WholePalletPutawayPersistenceResult ExecuteWholePalletPutaway(WholePalletPutawayCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        DateTime now = DateTime.Now;
                        long? inboundPalletId = InboundConfirmRepository.LockPallet(conn, tran, command.PalletHuCode);
                        bool confirmedInbound = inboundPalletId.HasValue && InboundConfirmRepository.IsRequired(conn, tran, inboundPalletId.Value);
                        if (inboundPalletId.HasValue)
                        {
                            string inboundBlockReason = InboundConfirmRepository.GetPutawayBlockReason(conn, tran, inboundPalletId.Value);
                            if (inboundBlockReason != null)
                            {
                                tran.Rollback();
                                return WholePalletPutawayPersistenceResult.Fail(inboundBlockReason);
                            }
                        }
                        const string duplicateSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_OperationLog WITH (UPDLOCK, HOLDLOCK) WHERE RequestId = @RequestId;";
                        using (SqlCommand duplicateCommand = new SqlCommand(duplicateSql, conn, tran))
                        {
                            duplicateCommand.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            if (Convert.ToInt32(duplicateCommand.ExecuteScalar()) > 0)
                            {
                                tran.Rollback();
                                return WholePalletPutawayPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                            }
                        }

                        WholePalletHuRow pallet;
                        const string palletSql = @"
SELECT h.Id, h.HUCode, h.HUType, h.PlantCode, h.LifecycleStatus, h.IsHold,
       cl.LocationId, loc.LocationCode, z.ZoneCode, w.WarehouseCode
FROM dbo.WMS_FG_HU h WITH (UPDLOCK, HOLDLOCK)
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK) ON cl.HUId = h.Id
LEFT JOIN dbo.WMS_FG_Location loc ON loc.Id = cl.LocationId
LEFT JOIN dbo.WMS_FG_Zone z ON z.Id = loc.ZoneId
LEFT JOIN dbo.WMS_FG_Warehouse w ON w.Id = loc.WarehouseId
WHERE h.HUCode = @PalletHuCode;";
                        using (SqlCommand palletCommand = new SqlCommand(palletSql, conn, tran))
                        {
                            palletCommand.Parameters.Add("@PalletHuCode", SqlDbType.VarChar, 100).Value = command.PalletHuCode;
                            using (SqlDataReader reader = palletCommand.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return WholePalletPutawayPersistenceResult.Fail("托盘HU不存在。");
                                }
                                pallet = new WholePalletHuRow
                                {
                                    Id = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]), HuType = Convert.ToString(reader["HUType"]),
                                    PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]), IsHold = Convert.ToBoolean(reader["IsHold"]),
                                    LocationId = reader["LocationId"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["LocationId"]),
                                    LocationCode = reader["LocationCode"] == DBNull.Value ? null : Convert.ToString(reader["LocationCode"]),
                                    ZoneCode = reader["ZoneCode"] == DBNull.Value ? null : Convert.ToString(reader["ZoneCode"]),
                                    WarehouseCode = reader["WarehouseCode"] == DBNull.Value ? null : Convert.ToString(reader["WarehouseCode"])
                                };
                            }
                        }
                        if (pallet.HuType != "PALLET") { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("当前HU不是PALLET。"); }
                        if (pallet.LifecycleStatus != "ACTIVE") { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("只有ACTIVE状态托盘允许整托上架。"); }
                        if (pallet.IsHold) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘当前已冻结。"); }
                        if (!pallet.LocationId.HasValue || pallet.ZoneCode != "RECEIVE") { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘当前不在收货区域。"); }
                        if (!string.Equals(pallet.WarehouseCode, command.WarehouseCode, StringComparison.OrdinalIgnoreCase)) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘当前不属于指定仓库。"); }

                        WholePalletTargetRow target;
                        const string targetSql = @"
SELECT l.Id, l.LocationCode, l.IsActive, l.IsVirtual, l.PlantPolicy, l.DedicatedPlantCode, z.ZoneCode, w.WarehouseCode
FROM dbo.WMS_FG_Location l WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_Zone z ON z.Id = l.ZoneId
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id = l.WarehouseId
WHERE w.WarehouseCode = @WarehouseCode AND l.LocationCode = @LocationCode;";
                        using (SqlCommand targetCommand = new SqlCommand(targetSql, conn, tran))
                        {
                            targetCommand.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;
                            targetCommand.Parameters.Add("@LocationCode", SqlDbType.VarChar, 50).Value = command.TargetLocationCode;
                            using (SqlDataReader reader = targetCommand.ExecuteReader())
                            {
                                if (!reader.Read()) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("目标货位不存在。"); }
                                target = new WholePalletTargetRow
                                {
                                    Id = Convert.ToInt64(reader["Id"]), LocationCode = Convert.ToString(reader["LocationCode"]), IsActive = Convert.ToBoolean(reader["IsActive"]),
                                    IsVirtual = Convert.ToBoolean(reader["IsVirtual"]), PlantPolicy = Convert.ToString(reader["PlantPolicy"]),
                                    DedicatedPlantCode = reader["DedicatedPlantCode"] == DBNull.Value ? null : Convert.ToString(reader["DedicatedPlantCode"]), ZoneCode = Convert.ToString(reader["ZoneCode"])
                                };
                            }
                        }
                        if (!target.IsActive || target.IsVirtual || target.ZoneCode != "STORAGE") { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("目标货位必须是当前仓库可用的实体STORAGE货位。"); }
                        if (target.Id == pallet.LocationId.Value) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("目标货位不能与托盘收货货位相同。"); }

                        List<WholePalletBoxRow> boxes = new List<WholePalletBoxRow>();
                        const string boxesSql = @"
SELECT r.Id AS RelationId, box.Id, box.HUCode, box.PlantCode, box.LifecycleStatus, box.IsHold,
       cl.LocationId AS DirectLocationId,
       CAST(CASE WHEN cl.HUId IS NULL THEN 0 ELSE 1 END AS bit) AS HasDirectLocation
FROM dbo.WMS_FG_HU_Relation r WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU box WITH (UPDLOCK, HOLDLOCK) ON box.Id = r.ChildHUId
LEFT JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK) ON cl.HUId = box.Id
WHERE r.ParentHUId = @PalletHuId AND r.IsActive = 1 AND box.HUType = 'OUTER_BOX'
ORDER BY box.HUCode;";
                        using (SqlCommand boxesCommand = new SqlCommand(boxesSql, conn, tran))
                        {
                            boxesCommand.Parameters.Add("@PalletHuId", SqlDbType.BigInt).Value = pallet.Id;
                            using (SqlDataReader reader = boxesCommand.ExecuteReader())
                            {
                                while (reader.Read()) boxes.Add(new WholePalletBoxRow
                                {
                                    RelationId = Convert.ToInt64(reader["RelationId"]), Id = Convert.ToInt64(reader["Id"]), HuCode = Convert.ToString(reader["HUCode"]),
                                    PlantCode = Convert.ToString(reader["PlantCode"]), LifecycleStatus = Convert.ToString(reader["LifecycleStatus"]),
                                    IsHold = Convert.ToBoolean(reader["IsHold"]), HasDirectLocation = Convert.ToBoolean(reader["HasDirectLocation"]),
                                    DirectLocationId = reader["DirectLocationId"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["DirectLocationId"])
                                });
                            }
                        }
                        if (boxes.Count == 0) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘当前没有可上架外箱。"); }
                        if (boxes.Any(x => x.LifecycleStatus != "ACTIVE")) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘存在非ACTIVE外箱，整托上架已取消。"); }
                        if (boxes.Any(x => x.IsHold)) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘存在冻结外箱，整托上架已取消。"); }
                        if (!confirmedInbound && boxes.Any(x => x.HasDirectLocation)) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘存在已有直接货位的外箱，整托上架已取消。"); }
                        if (boxes.Any(x => !string.Equals(x.PlantCode, pallet.PlantCode, StringComparison.OrdinalIgnoreCase))) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("托盘与外箱厂别不一致。"); }

                        const string warehousePlantSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_WarehousePlant wp INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=wp.WarehouseId WHERE w.WarehouseCode=@WarehouseCode AND wp.PlantCode=@PlantCode AND wp.IsActive=1 AND w.IsActive=1;";
                        using (SqlCommand plantCommand = new SqlCommand(warehousePlantSql, conn, tran))
                        {
                            plantCommand.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;
                            plantCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = pallet.PlantCode;
                            if (Convert.ToInt32(plantCommand.ExecuteScalar()) == 0) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("当前仓库不允许存储该厂别库存。"); }
                        }
                        if (target.PlantPolicy == "DEDICATED" && !string.Equals(target.DedicatedPlantCode, pallet.PlantCode, StringComparison.OrdinalIgnoreCase)) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("目标货位为其他厂别专用货位。"); }
                        if (target.PlantPolicy == "SAME_PLANT")
                        {
                            const string samePlantSql = @"SELECT COUNT(1) FROM dbo.WMS_FG_HU_CurrentLocation cl INNER JOIN dbo.WMS_FG_HU h ON h.Id=cl.HUId WHERE cl.LocationId=@LocationId AND h.PlantCode<>@PlantCode AND h.LifecycleStatus='ACTIVE';";
                            using (SqlCommand samePlantCommand = new SqlCommand(samePlantSql, conn, tran))
                            {
                                samePlantCommand.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                                samePlantCommand.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = pallet.PlantCode;
                                if (Convert.ToInt32(samePlantCommand.ExecuteScalar()) > 0) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("目标货位当前存在其他厂别库存。"); }
                            }
                        }

                        InsertOperationLog(conn, tran, command.OperationNo, "PUTAWAY_PALLET", pallet.Id, null, command.RequestId, command.BusinessNo, command.Operator, command.Remark, now);
                        List<string> movedCodes = new List<string>();
                        for (int index = 0; index < boxes.Count; index++)
                        {
                            WholePalletBoxRow box = boxes[index];
                            string itemOperationNo = command.OperationNo + "-" + (index + 1).ToString("000");
                            string itemRequestId = command.RequestId + ":" + (index + 1).ToString("000");
                            long moveLogId;
                            const string moveSql = @"INSERT INTO dbo.WMS_FG_HU_MoveLog(MoveNo,HUId,FromLocationId,ToLocationId,MoveType,BusinessNo,RequestId,MoveBy,MoveDt,Remark) OUTPUT INSERTED.Id VALUES(@MoveNo,@HUId,@From,@To,'PUTAWAY',@BusinessNo,@RequestId,@Operator,@Now,@Remark);";
                            using (SqlCommand moveCommand = new SqlCommand(moveSql, conn, tran))
                            {
                                moveCommand.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = itemOperationNo;
                                moveCommand.Parameters.Add("@HUId", SqlDbType.BigInt).Value = box.Id;
                                moveCommand.Parameters.Add("@From", SqlDbType.BigInt).Value = confirmedInbound ? box.DirectLocationId.Value : pallet.LocationId.Value;
                                moveCommand.Parameters.Add("@To", SqlDbType.BigInt).Value = target.Id;
                                moveCommand.Parameters.Add("@Scan", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.ScannedLocationCode);
                                moveCommand.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbParameterValue.StringOrDbNull(command.BusinessNo);
                                moveCommand.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = itemRequestId;
                                moveCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                                moveCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                                moveCommand.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = BusinessLogText.Move("PUTAWAY", command.Remark);
                                moveLogId = Convert.ToInt64(moveCommand.ExecuteScalar());
                            }
                            string currentSql = confirmedInbound
                                ? @"UPDATE dbo.WMS_FG_HU_CurrentLocation SET LocationId=@LocationId,LocationSince=@Now,LastMoveLogId=@MoveLogId WHERE HUId=@HUId;"
                                : @"INSERT INTO dbo.WMS_FG_HU_CurrentLocation(HUId,LocationId,LocationSince,LastMoveLogId,CreateBy) VALUES(@HUId,@LocationId,@Now,@MoveLogId,@Operator);";
                            using (SqlCommand currentCommand = new SqlCommand(currentSql, conn, tran))
                            {
                                currentCommand.Parameters.Add("@HUId", SqlDbType.BigInt).Value = box.Id;
                                currentCommand.Parameters.Add("@LocationId", SqlDbType.BigInt).Value = target.Id;
                                currentCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                                currentCommand.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                                currentCommand.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = itemOperationNo;
                                currentCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                                currentCommand.ExecuteNonQuery();
                            }
                            const string closeSql = @"UPDATE dbo.WMS_FG_HU_Relation SET EffectiveTo=@Now,IsActive=0,CloseOperationNo=@OperationNo,CloseUser=@Operator,CloseReason=N'PUTAWAY' WHERE Id=@RelationId AND IsActive=1;";
                            using (SqlCommand closeCommand = new SqlCommand(closeSql, conn, tran))
                            {
                                closeCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                                closeCommand.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50).Value = itemOperationNo;
                                closeCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                                closeCommand.Parameters.Add("@RelationId", SqlDbType.BigInt).Value = box.RelationId;
                                if (closeCommand.ExecuteNonQuery() != 1) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("外箱关系已变化，整托上架已取消。"); }
                            }
                            InsertOperationLog(conn, tran, itemOperationNo, "DETACH_HU", pallet.Id, box.Id, command.RequestId, command.BusinessNo, command.Operator, null, now, "上架解绑：解除托盘与外箱关系");
                            InsertOperationLog(conn, tran, itemOperationNo, "PUTAWAY", box.Id, pallet.Id, command.RequestId, command.BusinessNo, command.Operator, command.Remark, now, "整托上架：外箱上架完成");
                            movedCodes.Add(box.HuCode);
                        }
                        const string closePalletSql = @"UPDATE dbo.WMS_FG_HU SET LifecycleStatus='CLOSED',LastEditBy=@Operator,LastEditDt=@Now WHERE Id=@PalletHuId AND HUType='PALLET' AND LifecycleStatus='ACTIVE';";
                        using (SqlCommand closePalletCommand = new SqlCommand(closePalletSql, conn, tran))
                        {
                            closePalletCommand.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbParameterValue.StringOrDbNull(command.Operator);
                            closePalletCommand.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                            closePalletCommand.Parameters.Add("@PalletHuId", SqlDbType.BigInt).Value = pallet.Id;
                            if (closePalletCommand.ExecuteNonQuery() != 1) { tran.Rollback(); return WholePalletPutawayPersistenceResult.Fail("运输托HU关闭失败。"); }
                        }
                        const string deletePalletLocationSql = @"DELETE FROM dbo.WMS_FG_HU_CurrentLocation WHERE HUId=@PalletHuId;";
                        using (SqlCommand deletePalletLocationCommand = new SqlCommand(deletePalletLocationSql, conn, tran))
                        {
                            deletePalletLocationCommand.Parameters.Add("@PalletHuId", SqlDbType.BigInt).Value = pallet.Id;
                            deletePalletLocationCommand.ExecuteNonQuery();
                        }
                        InsertOperationLog(conn, tran, command.OperationNo, "CLOSE_HU", pallet.Id, null, command.RequestId, command.BusinessNo, command.Operator, null, now, "上架完成：自动关闭运输托盘");
                        tran.Commit();
                        return WholePalletPutawayPersistenceResult.Success(new WholePalletPutawayResultDto
                        {
                            OperationNo = command.OperationNo, PalletHuCode = pallet.HuCode, FromLocationCode = confirmedInbound ? "INBOUND-CONFIRMED" : pallet.LocationCode,
                            ToLocationCode = target.LocationCode, BoxCodes = movedCodes, MovedCount = movedCodes.Count,
                            IsPalletClosed = true, IsDuplicate = false, PutawayTime = now
                        });
                    }
                    catch
                    {
                        if (tran.Connection != null) tran.Rollback();
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
            long? relatedHuId,
            string requestId,
            string businessNo,
            string operatorCode,
            string remark,
            DateTime operationTime, string summary = null)
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
                remark: remark, summary: summary);
        }

        private class WholePalletHeaderRow
        {
            public string OperationNo { get; set; }
            public string PalletHuCode { get; set; }
            public DateTime PutawayTime { get; set; }
        }

        private class WholePalletItemRow
        {
            public string BoxCode { get; set; }
            public string FromLocationCode { get; set; }
            public string ToLocationCode { get; set; }
        }

        private class WholePalletHuRow
        {
            public long Id { get; set; }
            public string HuCode { get; set; }
            public string HuType { get; set; }
            public string PlantCode { get; set; }
            public string LifecycleStatus { get; set; }
            public bool IsHold { get; set; }
            public long? LocationId { get; set; }
            public string LocationCode { get; set; }
            public string ZoneCode { get; set; }
            public string WarehouseCode { get; set; }
        }

        private class WholePalletTargetRow
        {
            public long Id { get; set; }
            public string LocationCode { get; set; }
            public bool IsActive { get; set; }
            public bool IsVirtual { get; set; }
            public string PlantPolicy { get; set; }
            public string DedicatedPlantCode { get; set; }
            public string ZoneCode { get; set; }
        }

        private class WholePalletBoxRow
        {
            public long RelationId { get; set; }
            public long Id { get; set; }
            public string HuCode { get; set; }
            public string PlantCode { get; set; }
            public string LifecycleStatus { get; set; }
            public bool IsHold { get; set; }
            public bool HasDirectLocation { get; set; }
            public long? DirectLocationId { get; set; }
        }
    }
}
