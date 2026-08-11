using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Outbound;

namespace WMS_HU_Model.Repositories
{
    public class OutboundRepository : IOutboundRepository
    {
        public List<OutboundQueueItemDto> GetQueue(OutboundQueueQuery query)
        {
            const string sql = @"
;WITH Candidate AS
(
    SELECT
        box.Id AS BoxHUId,
        box.HUCode AS BoxHUCode,
        box.PlantCode,
        w.WarehouseCode,
        loc.LocationCode,
        MIN(item.CreateTime) AS FifoTime,
        COUNT(DISTINCT innerHU.Id) AS InnerPackCount,
        SUM(item.Qty) AS TotalQty,
        MIN(item.Uom) AS Uom,
        MIN(item.MaterialCode) AS MaterialCode,
        MIN(item.MaterialRevision) AS MaterialRevision
    FROM dbo.WMS_HU box
    INNER JOIN dbo.WMS_HU_CurrentLocation cl ON cl.HUId = box.Id
    INNER JOIN dbo.WMS_Location loc ON loc.Id = cl.LocationId
    INNER JOIN dbo.WMS_Zone z ON z.Id = loc.ZoneId
    INNER JOIN dbo.WMS_Warehouse w ON w.Id = loc.WarehouseId
    INNER JOIN dbo.WMS_HU_Relation rel ON rel.ParentHUId = box.Id AND rel.IsActive = 1
    INNER JOIN dbo.WMS_HU innerHU ON innerHU.Id = rel.ChildHUId
    INNER JOIN dbo.WMS_HU_Item item ON item.HUId = innerHU.Id AND item.IsActive = 1
    WHERE box.HUType = 'OUTER_BOX'
      AND box.PlantCode = @PlantCode
      AND box.LifecycleStatus = 'ACTIVE'
      AND box.IsHold = 0
      AND innerHU.HUType = 'INNER_PACK'
      AND innerHU.LifecycleStatus = 'ACTIVE'
      AND innerHU.IsHold = 0
      AND w.WarehouseCode = @WarehouseCode
      AND z.ZoneCode = 'STORAGE'
      AND loc.IsVirtual = 0
      AND loc.MasterStatus = 'ACTIVE'
      AND item.StockStatus = 'AVAILABLE'
      AND item.MaterialCode = @MaterialCode
      AND (@MaterialRevision IS NULL OR item.MaterialRevision = @MaterialRevision)
      AND (@BatchNo IS NULL OR item.BatchNo = @BatchNo)
      AND (@ProductionOrderNo IS NULL OR item.ProductionOrderNo = @ProductionOrderNo)

    ------------------------------------------------------------
    -- 整箱必须全部符合条件 (排除包含不符合条件的子HU或Item)
    ------------------------------------------------------------
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_HU_Relation badRel
        INNER JOIN dbo.WMS_HU badInner ON badInner.Id = badRel.ChildHUId
        INNER JOIN dbo.WMS_HU_Item badItem ON badItem.HUId = badInner.Id AND badItem.IsActive = 1
        WHERE badRel.ParentHUId = box.Id
          AND badRel.IsActive = 1
          AND (
              badInner.HUType <> 'INNER_PACK'
              OR badInner.LifecycleStatus <> 'ACTIVE'
              OR badInner.IsHold = 1
              OR badItem.StockStatus <> 'AVAILABLE'
              OR badItem.MaterialCode <> @MaterialCode
              OR (@MaterialRevision IS NOT NULL AND ISNULL(badItem.MaterialRevision, '') <> @MaterialRevision)
              OR (@BatchNo IS NOT NULL AND ISNULL(badItem.BatchNo, '') <> @BatchNo)
              OR (@ProductionOrderNo IS NOT NULL AND ISNULL(badItem.ProductionOrderNo, '') <> @ProductionOrderNo)
          )
    )
    GROUP BY
        box.Id,
        box.HUCode,
        box.PlantCode,
        w.WarehouseCode,
        loc.LocationCode
    HAVING COUNT(DISTINCT item.Uom) = 1
)
SELECT TOP (@Top)
    QueueSequence = ROW_NUMBER() OVER (ORDER BY FifoTime ASC, BoxHUId ASC),
    BoxHUId,
    BoxHUCode,
    PlantCode,
    WarehouseCode,
    LocationCode,
    MaterialCode,
    MaterialRevision,
    InnerPackCount,
    TotalQty,
    Uom,
    FifoTime
FROM Candidate
ORDER BY FifoTime ASC, BoxHUId ASC;";

            List<OutboundQueueItemDto> list = new List<OutboundQueueItemDto>();

            using (SqlConnection conn = DbConnectionFactory.Create())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                AddQueueParameters(cmd, query.WarehouseCode, query.PlantCode, query.MaterialCode, query.MaterialRevision, query.BatchNo, query.ProductionOrderNo);
                cmd.Parameters.Add("@Top", SqlDbType.Int).Value = query.Top;

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new OutboundQueueItemDto
                        {
                            QueueSequence = Convert.ToInt32(reader["QueueSequence"]),
                            BoxHuId = Convert.ToInt64(reader["BoxHUId"]),
                            BoxHuCode = Convert.ToString(reader["BoxHUCode"]),
                            PlantCode = Convert.ToString(reader["PlantCode"]),
                            WarehouseCode = Convert.ToString(reader["WarehouseCode"]),
                            LocationCode = Convert.ToString(reader["LocationCode"]),
                            MaterialCode = Convert.ToString(reader["MaterialCode"]),
                            MaterialRevision = DbString(reader["MaterialRevision"]),
                            InnerPackCount = Convert.ToInt32(reader["InnerPackCount"]),
                            TotalQty = Convert.ToDecimal(reader["TotalQty"]),
                            Uom = Convert.ToString(reader["Uom"]),
                            FifoTime = Convert.ToDateTime(reader["FifoTime"])
                        });
                    }
                }
            }

            return list;
        }

        private static void AddQueueParameters(
            SqlCommand cmd,
            string warehouseCode,
            string plantCode,
            string materialCode,
            string materialRevision,
            string batchNo,
            string productionOrderNo)
        {
            cmd.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = warehouseCode;
            cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20).Value = plantCode;
            cmd.Parameters.Add("@MaterialCode", SqlDbType.VarChar, 50).Value = materialCode;
            cmd.Parameters.Add("@MaterialRevision", SqlDbType.VarChar, 30).Value = DbValue(materialRevision);
            cmd.Parameters.Add("@BatchNo", SqlDbType.VarChar, 100).Value = DbValue(batchNo);
            cmd.Parameters.Add("@ProductionOrderNo", SqlDbType.VarChar, 50).Value = DbValue(productionOrderNo);
        }

        public PickResultDto GetPickByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return null;
            }

            const string sql = @"
SELECT TOP 1
    op.OperationNo,
    moveLog.Id AS MoveLogId,
    box.HUCode AS BoxHUCode,
    box.PlantCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    moveLog.MoveTime AS PickTime,
    MIN(item.CreateTime) AS FifoTime,
    MIN(item.MaterialCode) AS MaterialCode,
    COUNT(DISTINCT innerHU.Id) AS InnerPackCount,
    SUM(item.Qty) AS TotalQty,
    MIN(item.Uom) AS Uom
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU box ON box.Id = op.HUId
INNER JOIN dbo.WMS_HU_MoveLog moveLog ON moveLog.MoveNo = op.OperationNo AND moveLog.HUId = box.Id
LEFT JOIN dbo.WMS_Location fromLoc ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc ON toLoc.Id = moveLog.ToLocationId
INNER JOIN dbo.WMS_HU_Relation rel ON rel.ParentHUId = box.Id AND rel.IsActive = 1
INNER JOIN dbo.WMS_HU innerHU ON innerHU.Id = rel.ChildHUId
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = innerHU.Id AND item.IsActive = 1
WHERE op.OperationType = 'PICK'
  AND op.RequestId = @RequestId
GROUP BY
    op.OperationNo,
    moveLog.Id,
    box.HUCode,
    box.PlantCode,
    fromLoc.LocationCode,
    toLoc.LocationCode,
    moveLog.MoveTime;";

            using (SqlConnection conn = DbConnectionFactory.Create())
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    PickResultDto dto = MapPickResult(reader);
                    dto.IsDuplicate = true;

                    return dto;
                }
            }
        }
        public PickPersistenceResult ExecutePickNext(PickNextCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        //------------------------------------------------
                        // 1. RequestId事务内再次检查（幂等性控制）
                        //------------------------------------------------
                        PickResultDto duplicate = GetPickByRequestIdInternal(
                            conn,
                            tran,
                            command.RequestId);

                        if (duplicate != null)
                        {
                            tran.Commit();
                            duplicate.IsDuplicate = true;

                            return PickPersistenceResult.Success(duplicate);
                        }

                        //------------------------------------------------
                        // 2. 找OUTBOUND-STAGING暂存货位
                        //------------------------------------------------
                        long stagingLocationId;
                        string stagingLocationCode;

                        const string stagingSql = @"
SELECT
l.Id,
l.LocationCode
FROM dbo.WMS_Location l
INNER JOIN dbo.WMS_Warehouse w
ON w.Id = l.WarehouseId
INNER JOIN dbo.WMS_Zone z
ON z.Id = l.ZoneId
WHERE
w.WarehouseCode = @WarehouseCode
AND l.LocationCode = 'OUTBOUND-STAGING'
AND z.ZoneCode = 'STAGING'
AND l.MasterStatus = 'ACTIVE';";

                        using (SqlCommand cmd = new SqlCommand(stagingSql, conn, tran))
                        {
                            cmd.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30).Value = command.WarehouseCode;

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return PickPersistenceResult.Fail("未配置可用的OUTBOUND-STAGING货位。");
                                }

                                stagingLocationId = Convert.ToInt64(reader["Id"]);
                                stagingLocationCode = Convert.ToString(reader["LocationCode"]);
                            }
                        }

                        //------------------------------------------------
                        // 3. 锁FIFO队头（带 READPAST, UPDLOCK 跳过已被占用的最老箱）
                        //------------------------------------------------
                        const string candidateSql = @"
SELECT TOP 1
box.Id AS BoxHUId,
box.HUCode AS BoxHUCode,
box.PlantCode,
cl.LocationId AS FromLocationId,
loc.LocationCode AS FromLocationCode,
MIN(item.CreateTime) AS FifoTime,
COUNT(DISTINCT innerHU.Id) AS InnerPackCount,
SUM(item.Qty) AS TotalQty,
MIN(item.Uom) AS Uom,
MIN(item.MaterialCode) AS MaterialCode
FROM dbo.WMS_HU box WITH (UPDLOCK, READPAST, ROWLOCK)
INNER JOIN dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK, READPAST, ROWLOCK)
ON cl.HUId = box.Id
INNER JOIN dbo.WMS_Location loc
ON loc.Id = cl.LocationId
INNER JOIN dbo.WMS_Zone z
ON z.Id = loc.ZoneId
INNER JOIN dbo.WMS_Warehouse w
ON w.Id = loc.WarehouseId
INNER JOIN dbo.WMS_HU_Relation rel
ON rel.ParentHUId = box.Id
AND rel.IsActive = 1
INNER JOIN dbo.WMS_HU innerHU
ON innerHU.Id = rel.ChildHUId
INNER JOIN dbo.WMS_HU_Item item
ON item.HUId = innerHU.Id
AND item.IsActive = 1
WHERE
box.HUType = 'OUTER_BOX'
AND box.PlantCode = @PlantCode
AND box.LifecycleStatus = 'ACTIVE'
AND box.IsHold = 0
AND innerHU.HUType = 'INNER_PACK'
AND innerHU.LifecycleStatus = 'ACTIVE'
AND innerHU.IsHold = 0
AND w.WarehouseCode = @WarehouseCode
AND z.ZoneCode = 'STORAGE'
AND loc.IsVirtual = 0
AND loc.MasterStatus = 'ACTIVE'
AND item.StockStatus = 'AVAILABLE'
AND item.MaterialCode = @MaterialCode
AND (@MaterialRevision IS NULL OR item.MaterialRevision = @MaterialRevision)
AND (@BatchNo IS NULL OR item.BatchNo = @BatchNo)
AND (@ProductionOrderNo IS NULL OR item.ProductionOrderNo = @ProductionOrderNo)
AND NOT EXISTS
(
SELECT 1
FROM dbo.WMS_HU_Relation badRel
INNER JOIN dbo.WMS_HU badInner
    ON badInner.Id = badRel.ChildHUId
INNER JOIN dbo.WMS_HU_Item badItem
    ON badItem.HUId = badInner.Id
    AND badItem.IsActive = 1
WHERE
    badRel.ParentHUId = box.Id
AND badRel.IsActive = 1
AND
(
    badInner.HUType <> 'INNER_PACK'
    OR badInner.LifecycleStatus <> 'ACTIVE'
    OR badInner.IsHold = 1
    OR badItem.StockStatus <> 'AVAILABLE'
    OR badItem.MaterialCode <> @MaterialCode
    OR (@MaterialRevision IS NOT NULL AND ISNULL(badItem.MaterialRevision, '') <> @MaterialRevision)
    OR (@BatchNo IS NOT NULL AND ISNULL(badItem.BatchNo, '') <> @BatchNo)
    OR (@ProductionOrderNo IS NOT NULL AND ISNULL(badItem.ProductionOrderNo, '') <> @ProductionOrderNo)
)
)
GROUP BY
box.Id,
box.HUCode,
box.PlantCode,
cl.LocationId,
loc.LocationCode
HAVING
COUNT(DISTINCT item.Uom) = 1
ORDER BY
MIN(item.CreateTime) ASC,
box.Id ASC;";

                        long boxHuId;
                        string boxHuCode;
                        string plantCode;
                        long fromLocationId;
                        string fromLocationCode;
                        DateTime fifoTime;
                        int innerPackCount;
                        decimal totalQty;
                        string uom;
                        string materialCode;

                        using (SqlCommand cmd = new SqlCommand(candidateSql, conn, tran))
                        {
                            // 调用 SQL 参数绑定方法
                            AddQueueParameters(
                                cmd,
                                command.WarehouseCode,
                                command.PlantCode,
                                command.MaterialCode,
                                command.MaterialRevision,
                                command.BatchNo,
                                command.ProductionOrderNo);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    tran.Rollback();
                                    return PickPersistenceResult.Fail("当前没有符合FIFO条件的可拣外箱。");
                                }

                                boxHuId = Convert.ToInt64(reader["BoxHUId"]);
                                boxHuCode = Convert.ToString(reader["BoxHUCode"]);
                                plantCode = Convert.ToString(reader["PlantCode"]);
                                fromLocationId = Convert.ToInt64(reader["FromLocationId"]);
                                fromLocationCode = Convert.ToString(reader["FromLocationCode"]);
                                fifoTime = Convert.ToDateTime(reader["FifoTime"]);
                                innerPackCount = Convert.ToInt32(reader["InnerPackCount"]);
                                totalQty = Convert.ToDecimal(reader["TotalQty"]);
                                uom = Convert.ToString(reader["Uom"]);
                                materialCode = Convert.ToString(reader["MaterialCode"]);
                            }
                        }

                        DateTime now = DateTime.Now;

                        //------------------------------------------------
                        // 4. 插入移动日志 MoveLog
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
'PICK',
@ScannedLocationCode,
@BusinessNo,
@RequestId,
@Operator,
@MoveTime,
@Remark
);";

                        using (SqlCommand cmd = new SqlCommand(moveSql, conn, tran))
                        {
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = boxHuId;
                            cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt).Value = fromLocationId;
                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt).Value = stagingLocationId;
                            cmd.Parameters.Add("@ScannedLocationCode", SqlDbType.VarChar, 100).Value = stagingLocationCode;
                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbValue(command.BusinessNo);
                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbValue(command.Operator);
                            cmd.Parameters.Add("@MoveTime", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = DbValue(command.Remark);

                            moveLogId = Convert.ToInt64(cmd.ExecuteScalar());
                        }

                        //------------------------------------------------
                        // 5. 更新 CurrentLocation
                        //------------------------------------------------
                        const string currentSql = @"
UPDATE dbo.WMS_HU_CurrentLocation
SET
LocationId = @ToLocationId,
LocationSince = @LocationSince,
LastMoveLogId = @MoveLogId,
LastMoveNo = @MoveNo,
LastMoveType = 'PICK',
LastOperator = @Operator,
ModifyTime = SYSDATETIME()
WHERE
HUId = @HUId
AND LocationId = @ExpectedFromLocationId;";

                        using (SqlCommand cmd = new SqlCommand(currentSql, conn, tran))
                        {
                            cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt).Value = stagingLocationId;
                            cmd.Parameters.Add("@LocationSince", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@MoveLogId", SqlDbType.BigInt).Value = moveLogId;
                            cmd.Parameters.Add("@MoveNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbValue(command.Operator);
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = boxHuId;
                            cmd.Parameters.Add("@ExpectedFromLocationId", SqlDbType.BigInt).Value = fromLocationId;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                tran.Rollback();
                                return PickPersistenceResult.Fail("外箱当前位置已变化，请重新执行拣货。");
                            }
                        }

                        //------------------------------------------------
                        // 6. 插入操作日志 OperationLog
                        //------------------------------------------------
                        const string operationSql = @"
INSERT INTO dbo.WMS_HU_OperationLog
(
OperationNo,
OperationType,
HUId,
BusinessNo,
RequestId,
Operator,
OperationTime,
Remark
)
VALUES
(
@OperationNo,
'PICK',
@HUId,
@BusinessNo,
@RequestId,
@Operator,
@OperationTime,
@Remark
);";

                        using (SqlCommand cmd = new SqlCommand(operationSql, conn, tran))
                        {
                            cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50).Value = command.OperationNo;
                            cmd.Parameters.Add("@HUId", SqlDbType.BigInt).Value = boxHuId;
                            cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100).Value = DbValue(command.BusinessNo);
                            cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = command.RequestId;
                            cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50).Value = DbValue(command.Operator);
                            cmd.Parameters.Add("@OperationTime", SqlDbType.DateTime2).Value = now;
                            cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500).Value = DbValue(command.Remark);

                            cmd.ExecuteNonQuery();
                        }

                        tran.Commit();

                        PickResultDto result = new PickResultDto
                        {
                            OperationNo = command.OperationNo,
                            MoveLogId = moveLogId,
                            BoxHuCode = boxHuCode,
                            PlantCode = plantCode,
                            MaterialCode = materialCode,
                            FromLocationCode = fromLocationCode,
                            ToLocationCode = stagingLocationCode,
                            InnerPackCount = innerPackCount,
                            TotalQty = totalQty,
                            Uom = uom,
                            FifoTime = fifoTime,
                            PickTime = now,
                            IsDuplicate = false
                        };

                        return PickPersistenceResult.Success(result);
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

        private static PickResultDto GetPickByRequestIdInternal(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId)
        {
            const string sql = @"
SELECT TOP 1
op.OperationNo,
moveLog.Id AS MoveLogId,
box.HUCode AS BoxHUCode,
box.PlantCode,
fromLoc.LocationCode AS FromLocationCode,
toLoc.LocationCode AS ToLocationCode,
moveLog.MoveTime AS PickTime,
MIN(item.CreateTime) AS FifoTime,
MIN(item.MaterialCode) AS MaterialCode,
COUNT(DISTINCT innerHU.Id) AS InnerPackCount,
SUM(item.Qty) AS TotalQty,
MIN(item.Uom) AS Uom
FROM dbo.WMS_HU_OperationLog op
INNER JOIN dbo.WMS_HU box
ON box.Id = op.HUId
INNER JOIN dbo.WMS_HU_MoveLog moveLog
ON moveLog.MoveNo = op.OperationNo
AND moveLog.HUId = box.Id
LEFT JOIN dbo.WMS_Location fromLoc
ON fromLoc.Id = moveLog.FromLocationId
INNER JOIN dbo.WMS_Location toLoc
ON toLoc.Id = moveLog.ToLocationId
INNER JOIN dbo.WMS_HU_Relation rel
ON rel.ParentHUId = box.Id
AND rel.IsActive = 1
INNER JOIN dbo.WMS_HU innerHU
ON innerHU.Id = rel.ChildHUId
INNER JOIN dbo.WMS_HU_Item item
ON item.HUId = innerHU.Id
AND item.IsActive = 1
WHERE
op.OperationType = 'PICK'
AND op.RequestId = @RequestId
GROUP BY
op.OperationNo,
moveLog.Id,
box.HUCode,
box.PlantCode,
fromLoc.LocationCode,
toLoc.LocationCode,
moveLog.MoveTime;";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100).Value = requestId;

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;

                    PickResultDto dto = MapPickResult(reader);
                    dto.IsDuplicate = true;

                    return dto;
                }
            }
        }

        /// <summary>
        /// 将 SqlDataReader 数据映射到 PickResultDto 实体
        /// </summary>
        private static PickResultDto MapPickResult(SqlDataReader reader)
        {
            return new PickResultDto
            {
                OperationNo = Convert.ToString(reader["OperationNo"]),
                MoveLogId = Convert.ToInt64(reader["MoveLogId"]),
                BoxHuCode = Convert.ToString(reader["BoxHUCode"]),
                PlantCode = Convert.ToString(reader["PlantCode"]),
                FromLocationCode = DbString(reader["FromLocationCode"]),
                ToLocationCode = Convert.ToString(reader["ToLocationCode"]),
                PickTime = Convert.ToDateTime(reader["PickTime"]),
                FifoTime = Convert.ToDateTime(reader["FifoTime"]),
                MaterialCode = Convert.ToString(reader["MaterialCode"]),
                InnerPackCount = Convert.ToInt32(reader["InnerPackCount"]),
                TotalQty = Convert.ToDecimal(reader["TotalQty"]),
                Uom = Convert.ToString(reader["Uom"])
            };
        }

        /// <summary>
        /// 辅助处理 DBNull (针对非 string 的泛型/对象扩展，保证类型适配)
        /// </summary>
        private static object DbValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
        }

        private static string DbString(object value)
        {
            return value == DBNull.Value ? null : Convert.ToString(value);
        }
    }
}