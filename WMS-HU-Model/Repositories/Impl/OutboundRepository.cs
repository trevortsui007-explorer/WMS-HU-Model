using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Outbound;
using WMS_HU_Model.Rules;

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
        CASE
            WHEN COUNT(DISTINCT ISNULL(item.MaterialRevision, '')) = 1
            THEN MIN(item.MaterialRevision)
            ELSE NULL
        END AS MaterialRevision
    FROM dbo.WMS_HU box
    INNER JOIN dbo.WMS_HU_CurrentLocation cl
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
    WHERE box.HUType = 'OUTER_BOX'
    AND box.PlantCode = @PlantCode
    AND box.LifecycleStatus = 'ACTIVE'
    AND box.IsHold = 0
    AND w.WarehouseCode = @WarehouseCode
    AND z.ZoneCode = 'STORAGE'
    AND loc.IsVirtual = 0
    AND loc.MasterStatus = 'ACTIVE'
    AND innerHU.HUType = 'INNER_PACK'
    AND innerHU.LifecycleStatus = 'ACTIVE'
    AND innerHU.IsHold = 0
    AND item.StockStatus = 'AVAILABLE'
    AND item.MaterialCode = @MaterialCode
    AND
    (
        @MaterialRevision IS NULL
        OR item.MaterialRevision = @MaterialRevision
    )
    AND
    (
        @BatchNo IS NULL
        OR item.BatchNo = @BatchNo
    )
    AND
    (
        @ProductionOrderNo IS NULL
        OR item.ProductionOrderNo = @ProductionOrderNo
    )

    ------------------------------------------------------------
    -- 整箱所有Active子HU必须符合出库要求
    ------------------------------------------------------------
    AND NOT EXISTS
    (
        SELECT 1
        FROM dbo.WMS_HU_Relation badRel
        INNER JOIN dbo.WMS_HU badInner
            ON badInner.Id = badRel.ChildHUId
        WHERE badRel.ParentHUId = box.Id
        AND badRel.IsActive = 1
        AND
        (
            badInner.HUType <> 'INNER_PACK'
            OR badInner.LifecycleStatus <> 'ACTIVE'
            OR badInner.IsHold = 1

            ----------------------------------------------------
            -- 内包没有有效库存
            ----------------------------------------------------
            OR NOT EXISTS
            (
                SELECT 1
                FROM dbo.WMS_HU_Item checkItem
                WHERE checkItem.HUId = badInner.Id
                AND checkItem.IsActive = 1
            )

            ----------------------------------------------------
            -- 内包存在任意不符合条件的Item
            ----------------------------------------------------
            OR EXISTS
            (
                SELECT 1
                FROM dbo.WMS_HU_Item badItem
                WHERE badItem.HUId = badInner.Id
                AND badItem.IsActive = 1
                AND
                (
                    badItem.StockStatus <> 'AVAILABLE'
                    OR badItem.MaterialCode <> @MaterialCode
                    OR
                    (
                        @MaterialRevision IS NOT NULL
                        AND ISNULL(badItem.MaterialRevision, '') <> @MaterialRevision
                    )
                    OR
                    (
                        @BatchNo IS NOT NULL
                        AND ISNULL(badItem.BatchNo, '') <> @BatchNo
                    )
                    OR
                    (
                        @ProductionOrderNo IS NOT NULL
                        AND ISNULL(badItem.ProductionOrderNo, '') <> @ProductionOrderNo
                    )
                )
            )
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
    ROW_NUMBER() OVER
    (
        ORDER BY
            FifoTime ASC,
            BoxHUId ASC
    ) AS QueueSequence,
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
ORDER BY
    FifoTime ASC,
    BoxHUId ASC;
";

            return DbExecutor.Query<OutboundQueueItemDto>(
                sql,
                new
                {
                    query.WarehouseCode,
                    query.PlantCode,
                    query.MaterialCode,
                    MaterialRevision = DbParameterValue.StringOrNull(query.MaterialRevision),
                    BatchNo = DbParameterValue.StringOrNull(query.BatchNo),
                    ProductionOrderNo = DbParameterValue.StringOrNull(query.ProductionOrderNo),
                    query.Top
                });
        }

        public PickResultDto GetPickByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;

            return GetPickByRequestIdInternal(null, null, requestId);
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
                        /*
                         * 事务内再次检查幂等。
                         */
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

                        /*
                         * 防止 RequestId 已经被其他业务占用。
                         */
                        if (IsRequestIdUsedByOtherOperationInternal(conn, tran, command.RequestId))
                        {
                            tran.Rollback();
                            return PickPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        long warehouseId;
                        long stagingLocationId;
                        string stagingLocationCode;

                        ReadOutboundStaging(
                            conn,
                            tran,
                            command.WarehouseCode,
                            out warehouseId,
                            out stagingLocationId,
                            out stagingLocationCode);

                        OutboundPickCandidateDto candidate = ReadSinglePickCandidate(
                            conn,
                            tran,
                            command);

                        if (candidate == null)
                        {
                            tran.Rollback();
                            return PickPersistenceResult.Fail("当前没有符合FIFO条件的可拣外箱。");
                        }

                        DateTime now = DateTime.Now;

                        long moveLogId = InsertMoveLog(
                            conn,
                            tran,
                            command.OperationNo,
                            candidate.BoxHuId,
                            candidate.LocationId,
                            stagingLocationId,
                            "PICK",
                            stagingLocationCode,
                            command.BusinessNo,
                            command.RequestId,
                            command.Operator,
                            now,
                            command.Remark);

                        bool locationUpdated = UpdateCurrentLocation(
                            conn,
                            tran,
                            candidate.BoxHuId,
                            candidate.LocationId,
                            stagingLocationId,
                            moveLogId,
                            command.OperationNo,
                            "PICK",
                            command.Operator,
                            now);

                        if (!locationUpdated)
                        {
                            tran.Rollback();
                            return PickPersistenceResult.Fail("外箱当前位置已变化，请重新执行拣货。");
                        }

                        InsertOperationLog(
                            conn,
                            tran,
                            command.OperationNo,
                            "PICK",
                            candidate.BoxHuId,
                            command.BusinessNo,
                            command.RequestId,
                            command.Operator,
                            now,
                            command.Remark);

                        tran.Commit();

                        PickResultDto result = new PickResultDto
                        {
                            OperationNo = command.OperationNo,
                            MoveLogId = moveLogId,
                            BoxHuCode = candidate.BoxHuCode,
                            PlantCode = candidate.PlantCode,
                            MaterialCode = command.MaterialCode,
                            FromLocationCode = candidate.LocationCode,
                            ToLocationCode = stagingLocationCode,
                            InnerPackCount = candidate.InnerPackCount,
                            TotalQty = candidate.Qty,
                            Uom = candidate.Uom,
                            FifoTime = candidate.FifoTime,
                            PickTime = now,
                            IsDuplicate = false
                        };

                        return PickPersistenceResult.Success(result);
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

        public List<OutboundPickCandidateDto> GetCandidates(OutboundBatchPlanRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return ReadCandidates(
                null,
                null,
                request.WarehouseCode,
                request.PlantCode,
                request.MaterialCode,
                request.MaterialRevision,
                request.BatchNo,
                request.ProductionOrderNo,
                request.Uom,
                false);
        }

        public PickBatchResultDto GetPickBatchByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;

            return GetPickBatchByRequestIdInternal(null, null, requestId);
        }

        public bool IsRequestIdUsedByOtherOperation(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return false;

            return IsRequestIdUsedByOtherOperationInternal(null, null, requestId);
        }

        public PickBatchPersistenceResult ExecutePickBatch(PickBatchCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();

                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        /*
                         * 1. 事务内再次做批量拣货幂等检查。
                         */
                        PickBatchResultDto duplicate = GetPickBatchByRequestIdInternal(
                            conn,
                            tran,
                            command.RequestId);

                        if (duplicate != null)
                        {
                            tran.Commit();
                            duplicate.IsDuplicate = true;

                            return PickBatchPersistenceResult.Success(duplicate);
                        }

                        /*
                         * 2. RequestId跨业务保护。
                         */
                        if (IsRequestIdUsedByOtherOperationInternal(conn, tran, command.RequestId))
                        {
                            tran.Rollback();
                            return PickBatchPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        /*
                         * 3. 查询出库暂存区。
                         */
                        long warehouseId;
                        long stagingLocationId;
                        string stagingLocationCode;

                        ReadOutboundStaging(
                            conn,
                            tran,
                            command.WarehouseCode,
                            out warehouseId,
                            out stagingLocationId,
                            out stagingLocationCode);

                        /*
                         * 4. 在同一事务内重新读取并锁定FIFO候选。
                         */
                        List<OutboundPickCandidateDto> candidates = ReadCandidates(
                            conn,
                            tran,
                            command.WarehouseCode,
                            command.PlantCode,
                            command.MaterialCode,
                            command.MaterialRevision,
                            command.BatchNo,
                            command.ProductionOrderNo,
                            command.Uom,
                            true);

                        /*
                         * 5. 重新计算FIFO计划。
                         */
                        OutboundBatchPlanDto plan = OutboundFifoPlanner.Build(
                            candidates,
                            command.RequiredQty,
                            command.Uom,
                            command.AllowOverPick);

                        if (plan.ResultType == "SHORTAGE")
                        {
                            tran.Rollback();

                            return PickBatchPersistenceResult.Fail(
                                "可用FIFO库存不足。" +
                                "需求：" +
                                command.RequiredQty +
                                " " +
                                command.Uom +
                                "，当前可用：" +
                                plan.PlannedPickQty +
                                " " +
                                command.Uom +
                                "，缺少：" +
                                plan.RemainingQty +
                                " " +
                                command.Uom +
                                "。");
                        }

                        if (plan.ResultType == "NEED_SPLIT")
                        {
                            tran.Rollback();

                            return PickBatchPersistenceResult.Fail(
                                "FIFO队头需要拆零，本次未执行拣货。" +
                                "还需：" +
                                plan.RemainingQty +
                                " " +
                                command.Uom +
                                "；建议拆零外箱：" +
                                plan.SuggestedSplitBoxHuCode +
                                "，箱内数量：" +
                                plan.SuggestedSplitBoxQty +
                                "。");
                        }

                        DateTime now = DateTime.Now;

                        /*
                         * 6. 创建批量拣货头。
                         */
                        long pickBatchId = InsertPickBatchHeader(
                            conn,
                            tran,
                            command,
                            plan,
                            warehouseId,
                            now);

                        PickBatchResultDto result = new PickBatchResultDto
                        {
                            PickBatchNo = command.PickBatchNo,
                            RequestId = command.RequestId,
                            WarehouseCode = command.WarehouseCode,
                            PlantCode = command.PlantCode,
                            MaterialCode = command.MaterialCode,
                            RequiredQty = command.RequiredQty,
                            PickedQty = plan.PlannedPickQty,
                            DifferenceQty = plan.DifferenceQty,
                            Uom = command.Uom,
                            ResultType = plan.ResultType,
                            PickTime = now,
                            IsDuplicate = false
                        };

                        /*
                         * 7. 逐箱拣货。
                         */
                        foreach (OutboundBatchPlanItemDto planItem in plan.SelectedBoxes)
                        {
                            OutboundPickCandidateDto candidate = candidates.Find(
                                x => x.BoxHuId == planItem.BoxHuId);

                            if (candidate == null)
                            {
                                tran.Rollback();
                                return PickBatchPersistenceResult.Fail("FIFO候选数据发生变化，整个批量拣货已回滚。");
                            }

                            string operationNo = Guid.NewGuid()
                                .ToString("N")
                                .ToUpperInvariant();

                            PickBatchItemResultDto detail;

                            string processError = ProcessBatchBox(
                                conn,
                                tran,
                                command,
                                candidate,
                                planItem,
                                pickBatchId,
                                stagingLocationId,
                                stagingLocationCode,
                                operationNo,
                                now,
                                out detail);

                            if (!string.IsNullOrEmpty(processError))
                            {
                                tran.Rollback();
                                return PickBatchPersistenceResult.Fail(processError);
                            }

                            result.Boxes.Add(detail);
                        }

                        result.BoxCount = result.Boxes.Count;

                        tran.Commit();

                        return PickBatchPersistenceResult.Success(result);
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

        private static OutboundPickCandidateDto ReadSinglePickCandidate(
            SqlConnection conn,
            SqlTransaction tran,
            PickNextCommand command)
        {
            const string sql = @"
SELECT TOP 1
    box.Id AS BoxHUId,
    box.HUCode AS BoxHUCode,
    box.PlantCode,
    cl.LocationId,
    loc.LocationCode,
    MIN(item.CreateTime) AS FifoTime,
    COUNT(DISTINCT innerHU.Id) AS InnerPackCount,
    SUM(item.Qty) AS Qty,
    MIN(item.Uom) AS Uom
FROM dbo.WMS_HU box
WITH
(
    UPDLOCK,
    READPAST,
    ROWLOCK
)
INNER JOIN dbo.WMS_HU_CurrentLocation cl
WITH
(
    UPDLOCK,
    READPAST,
    ROWLOCK
)
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
WHERE box.HUType = 'OUTER_BOX'
AND box.PlantCode = @PlantCode
AND box.LifecycleStatus = 'ACTIVE'
AND box.IsHold = 0
AND w.WarehouseCode = @WarehouseCode
AND z.ZoneCode = 'STORAGE'
AND loc.IsVirtual = 0
AND loc.MasterStatus = 'ACTIVE'
AND innerHU.HUType = 'INNER_PACK'
AND innerHU.LifecycleStatus = 'ACTIVE'
AND innerHU.IsHold = 0
AND item.StockStatus = 'AVAILABLE'
AND item.MaterialCode = @MaterialCode
AND
(
    @MaterialRevision IS NULL
    OR item.MaterialRevision = @MaterialRevision
)
AND
(
    @BatchNo IS NULL
    OR item.BatchNo = @BatchNo
)
AND
(
    @ProductionOrderNo IS NULL
    OR item.ProductionOrderNo = @ProductionOrderNo
)

AND NOT EXISTS
(
    SELECT 1
    FROM dbo.WMS_HU_Relation badRel
    INNER JOIN dbo.WMS_HU badInner
        ON badInner.Id = badRel.ChildHUId
    WHERE badRel.ParentHUId = box.Id
    AND badRel.IsActive = 1
    AND
    (
        badInner.HUType <> 'INNER_PACK'
        OR badInner.LifecycleStatus <> 'ACTIVE'
        OR badInner.IsHold = 1

        OR NOT EXISTS
        (
            SELECT 1
            FROM dbo.WMS_HU_Item checkItem
            WHERE checkItem.HUId = badInner.Id
            AND checkItem.IsActive = 1
        )

        OR EXISTS
        (
            SELECT 1
            FROM dbo.WMS_HU_Item badItem
            WHERE badItem.HUId = badInner.Id
            AND badItem.IsActive = 1
            AND
            (
                badItem.StockStatus <> 'AVAILABLE'
                OR badItem.MaterialCode <> @MaterialCode
                OR
                (
                    @MaterialRevision IS NOT NULL
                    AND ISNULL(badItem.MaterialRevision, '') <> @MaterialRevision
                )
                OR
                (
                    @BatchNo IS NOT NULL
                    AND ISNULL(badItem.BatchNo, '') <> @BatchNo
                )
                OR
                (
                    @ProductionOrderNo IS NOT NULL
                    AND ISNULL(badItem.ProductionOrderNo, '') <> @ProductionOrderNo
                )
            )
        )
    )
)

GROUP BY
    box.Id,
    box.HUCode,
    box.PlantCode,
    cl.LocationId,
    loc.LocationCode

HAVING COUNT(DISTINCT item.Uom) = 1

ORDER BY
    MIN(item.CreateTime) ASC,
    box.Id ASC;
";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
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
                        return null;

                    return new OutboundPickCandidateDto
                    {
                        BoxHuId = Convert.ToInt64(reader["BoxHUId"]),
                        BoxHuCode = Convert.ToString(reader["BoxHUCode"]),
                        PlantCode = Convert.ToString(reader["PlantCode"]),
                        LocationId = Convert.ToInt64(reader["LocationId"]),
                        LocationCode = Convert.ToString(reader["LocationCode"]),
                        FifoTime = Convert.ToDateTime(reader["FifoTime"]),
                        InnerPackCount = Convert.ToInt32(reader["InnerPackCount"]),
                        Qty = Convert.ToDecimal(reader["Qty"]),
                        Uom = Convert.ToString(reader["Uom"])
                    };
                }
            }
        }

        private static List<OutboundPickCandidateDto> ReadCandidates(
            SqlConnection conn,
            SqlTransaction tran,
            string warehouseCode,
            string plantCode,
            string materialCode,
            string materialRevision,
            string batchNo,
            string productionOrderNo,
            string uom,
            bool forUpdate)
        {
            string boxLockHint = forUpdate
                ? "WITH (UPDLOCK, HOLDLOCK, ROWLOCK)"
                : string.Empty;

            string locationLockHint = forUpdate
                ? "WITH (UPDLOCK, HOLDLOCK, ROWLOCK)"
                : string.Empty;

            string sql = @"
SELECT
    box.Id AS BoxHUId,
    box.HUCode AS BoxHUCode,
    box.PlantCode,
    cl.LocationId,
    loc.LocationCode,
    MIN(item.CreateTime) AS FifoTime,
    COUNT(DISTINCT innerHU.Id) AS InnerPackCount,
    SUM(item.Qty) AS Qty,
    MIN(item.Uom) AS Uom
FROM dbo.WMS_HU box
" + boxLockHint + @"
INNER JOIN dbo.WMS_HU_CurrentLocation cl
" + locationLockHint + @"
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
WHERE box.HUType = 'OUTER_BOX'
AND box.PlantCode = @PlantCode
AND box.LifecycleStatus = 'ACTIVE'
AND box.IsHold = 0
AND w.WarehouseCode = @WarehouseCode
AND z.ZoneCode = 'STORAGE'
AND loc.IsVirtual = 0
AND loc.MasterStatus = 'ACTIVE'
AND innerHU.HUType = 'INNER_PACK'
AND innerHU.LifecycleStatus = 'ACTIVE'
AND innerHU.IsHold = 0
AND item.StockStatus = 'AVAILABLE'
AND item.MaterialCode = @MaterialCode
AND item.Uom = @Uom
AND
(
    @MaterialRevision IS NULL
    OR item.MaterialRevision = @MaterialRevision
)
AND
(
    @BatchNo IS NULL
    OR item.BatchNo = @BatchNo
)
AND
(
    @ProductionOrderNo IS NULL
    OR item.ProductionOrderNo = @ProductionOrderNo
)

------------------------------------------------------------
-- 保证整箱纯净且完整
------------------------------------------------------------
AND NOT EXISTS
(
    SELECT 1
    FROM dbo.WMS_HU_Relation badRel
    INNER JOIN dbo.WMS_HU badInner
        ON badInner.Id = badRel.ChildHUId
    WHERE badRel.ParentHUId = box.Id
    AND badRel.IsActive = 1
    AND
    (
        badInner.HUType <> 'INNER_PACK'
        OR badInner.LifecycleStatus <> 'ACTIVE'
        OR badInner.IsHold = 1

        OR NOT EXISTS
        (
            SELECT 1
            FROM dbo.WMS_HU_Item checkItem
            WHERE checkItem.HUId = badInner.Id
            AND checkItem.IsActive = 1
        )

        OR EXISTS
        (
            SELECT 1
            FROM dbo.WMS_HU_Item badItem
            WHERE badItem.HUId = badInner.Id
            AND badItem.IsActive = 1
            AND
            (
                badItem.StockStatus <> 'AVAILABLE'
                OR badItem.MaterialCode <> @MaterialCode
                OR badItem.Uom <> @Uom
                OR
                (
                    @MaterialRevision IS NOT NULL
                    AND ISNULL(badItem.MaterialRevision, '') <> @MaterialRevision
                )
                OR
                (
                    @BatchNo IS NOT NULL
                    AND ISNULL(badItem.BatchNo, '') <> @BatchNo
                )
                OR
                (
                    @ProductionOrderNo IS NOT NULL
                    AND ISNULL(badItem.ProductionOrderNo, '') <> @ProductionOrderNo
                )
            )
        )
    )
)

GROUP BY
    box.Id,
    box.HUCode,
    box.PlantCode,
    cl.LocationId,
    loc.LocationCode

ORDER BY
    MIN(item.CreateTime) ASC,
    box.Id ASC;
";

            return Query<OutboundPickCandidateDto>(
                conn,
                tran,
                sql,
                CreateBatchFilterParameters(
                    warehouseCode,
                    plantCode,
                    materialCode,
                    materialRevision,
                    batchNo,
                    productionOrderNo,
                    uom));
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
WHERE op.OperationType = 'PICK'
AND op.RequestId = @RequestId
GROUP BY
    op.OperationNo,
    moveLog.Id,
    box.HUCode,
    box.PlantCode,
    fromLoc.LocationCode,
    toLoc.LocationCode,
    moveLog.MoveTime;
";

            PickResultDto result = QueryFirstOrDefault<PickResultDto>(
                conn,
                tran,
                sql,
                new { RequestId = requestId });

            if (result != null)
                result.IsDuplicate = true;

            return result;
        }

        private static PickBatchResultDto GetPickBatchByRequestIdInternal(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId)
        {
            const string headerSql = @"
SELECT
    b.Id,
    b.PickBatchNo,
    b.RequestId,
    w.WarehouseCode,
    b.PlantCode,
    b.MaterialCode,
    b.RequiredQty,
    b.PickedQty,
    b.DifferenceQty,
    b.Uom,
    b.ResultType,
    b.PickTime
FROM dbo.WMS_OutboundPickBatch b
INNER JOIN dbo.WMS_Warehouse w
    ON w.Id = b.WarehouseId
WHERE b.RequestId = @RequestId;
";

            PickBatchHeaderRow header = QueryFirstOrDefault<PickBatchHeaderRow>(
                conn,
                tran,
                headerSql,
                new { RequestId = requestId });

            if (header == null)
                return null;

            PickBatchResultDto result = new PickBatchResultDto
            {
                PickBatchNo = header.PickBatchNo,
                RequestId = header.RequestId,
                WarehouseCode = header.WarehouseCode,
                PlantCode = header.PlantCode,
                MaterialCode = header.MaterialCode,
                RequiredQty = header.RequiredQty,
                PickedQty = header.PickedQty,
                DifferenceQty = header.DifferenceQty,
                Uom = header.Uom,
                ResultType = header.ResultType,
                PickTime = header.PickTime,
                IsDuplicate = true
            };

            const string detailSql = @"
SELECT
    d.SequenceNo,
    h.HUCode AS BoxHUCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    d.BoxQty,
    d.Uom,
    d.FifoTime,
    d.MoveLogId,
    d.OperationNo
FROM dbo.WMS_OutboundPickBatchDetail d
INNER JOIN dbo.WMS_HU h
    ON h.Id = d.BoxHUId
INNER JOIN dbo.WMS_Location fromLoc
    ON fromLoc.Id = d.FromLocationId
INNER JOIN dbo.WMS_Location toLoc
    ON toLoc.Id = d.ToLocationId
WHERE d.PickBatchId = @PickBatchId
ORDER BY d.SequenceNo;
";

            result.Boxes = Query<PickBatchItemResultDto>(
                conn,
                tran,
                detailSql,
                new { PickBatchId = header.Id });

            result.BoxCount = result.Boxes.Count;

            return result;
        }

        private static bool IsRequestIdUsedByOtherOperationInternal(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId)
        {
            const string sql = @"
SELECT
    CASE
        WHEN EXISTS
        (
            SELECT 1
            FROM dbo.WMS_HU_MoveLog
            WHERE RequestId = @RequestId
        )
        OR EXISTS
        (
            SELECT 1
            FROM dbo.WMS_HU_OperationLog
            WHERE RequestId = @RequestId
            AND OperationType <> 'PICK_BATCH'
        )
        THEN 1
        ELSE 0
    END;
";

            return ExecuteScalar<int>(
                conn,
                tran,
                sql,
                new { RequestId = requestId }) == 1;
        }

        private static void ReadOutboundStaging(
            SqlConnection conn,
            SqlTransaction tran,
            string warehouseCode,
            out long warehouseId,
            out long locationId,
            out string locationCode)
        {
            const string sql = @"
SELECT
    w.Id AS WarehouseId,
    l.Id AS LocationId,
    l.LocationCode
FROM dbo.WMS_Location l
INNER JOIN dbo.WMS_Warehouse w
    ON w.Id = l.WarehouseId
INNER JOIN dbo.WMS_Zone z
    ON z.Id = l.ZoneId
WHERE w.WarehouseCode = @WarehouseCode
AND l.LocationCode = 'OUTBOUND-STAGING'
AND z.ZoneCode = 'STAGING'
AND l.MasterStatus = 'ACTIVE';
";

            OutboundStagingRow row = QueryFirstOrDefault<OutboundStagingRow>(
                conn,
                tran,
                sql,
                new { WarehouseCode = warehouseCode });

            if (row == null)
            {
                throw new InvalidOperationException(
                    "仓库[" +
                    warehouseCode +
                    "]未配置可用的OUTBOUND-STAGING货位。");
            }

            warehouseId = row.WarehouseId;
            locationId = row.LocationId;
            locationCode = row.LocationCode;
        }

        private static long InsertMoveLog(
            SqlConnection conn,
            SqlTransaction tran,
            string moveNo,
            long huId,
            long fromLocationId,
            long toLocationId,
            string moveType,
            string scannedLocationCode,
            string businessNo,
            string requestId,
            string operatorCode,
            DateTime moveTime,
            string remark)
        {
            return HuMoveLogWriter.Insert(
                conn,
                tran,
                moveNo,
                huId,
                fromLocationId,
                toLocationId,
                moveType,
                scannedLocationCode,
                businessNo,
                taskNo: null,
                requestId: requestId,
                reasonCode: null,
                operatorCode: operatorCode,
                moveTime: moveTime,
                remark: remark);
        }

        private static bool UpdateCurrentLocation(
            SqlConnection conn,
            SqlTransaction tran,
            long huId,
            long expectedFromLocationId,
            long toLocationId,
            long moveLogId,
            string moveNo,
            string moveType,
            string operatorCode,
            DateTime moveTime)
        {
            const string sql = @"
UPDATE dbo.WMS_HU_CurrentLocation
SET
    LocationId = @ToLocationId,
    LocationSince = @LocationSince,
    LastMoveLogId = @MoveLogId,
    LastMoveNo = @MoveNo,
    LastMoveType = @MoveType,
    LastOperator = @Operator,
    ModifyTime = SYSDATETIME()
WHERE HUId = @HUId
AND LocationId = @ExpectedFromLocationId;
";

            return DbExecutor.Execute(
                conn,
                tran,
                sql,
                new
                {
                    ToLocationId = toLocationId,
                    LocationSince = moveTime,
                    MoveLogId = moveLogId,
                    MoveNo = moveNo,
                    MoveType = moveType,
                    Operator = DbParameterValue.StringOrNull(operatorCode),
                    HUId = huId,
                    ExpectedFromLocationId = expectedFromLocationId
                }) == 1;
        }

        private static void InsertOperationLog(
            SqlConnection conn,
            SqlTransaction tran,
            string operationNo,
            string operationType,
            long huId,
            string businessNo,
            string requestId,
            string operatorCode,
            DateTime operationTime,
            string remark)
        {
            HuOperationLogWriter.Insert(
                conn,
                tran,
                operationNo,
                operationType,
                huId,
                relatedHuId: null,
                businessNo: businessNo,
                requestId: requestId,
                operatorCode: operatorCode,
                operationTime: operationTime,
                detailJson: null,
                remark: remark);
        }

        private static long InsertPickBatchHeader(
            SqlConnection conn,
            SqlTransaction tran,
            PickBatchCommand command,
            OutboundBatchPlanDto plan,
            long warehouseId,
            DateTime pickTime)
        {
            const string sql = @"
INSERT INTO dbo.WMS_OutboundPickBatch
(
    PickBatchNo,
    RequestId,
    WarehouseId,
    PlantCode,
    MaterialCode,
    MaterialRevision,
    FilterBatchNo,
    ProductionOrderNo,
    RequiredQty,
    PickedQty,
    DifferenceQty,
    Uom,
    PickMode,
    ResultType,
    BusinessNo,
    Operator,
    PickTime,
    Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @PickBatchNo,
    @RequestId,
    @WarehouseId,
    @PlantCode,
    @MaterialCode,
    @MaterialRevision,
    @FilterBatchNo,
    @ProductionOrderNo,
    @RequiredQty,
    @PickedQty,
    @DifferenceQty,
    @Uom,
    'FIFO_WHOLE_BOX',
    @ResultType,
    @BusinessNo,
    @Operator,
    @PickTime,
    @Remark
);
";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@PickBatchNo", SqlDbType.VarChar, 50)
                    .Value = command.PickBatchNo;

                cmd.Parameters.Add("@RequestId", SqlDbType.VarChar, 100)
                    .Value = command.RequestId;

                cmd.Parameters.Add("@WarehouseId", SqlDbType.BigInt)
                    .Value = warehouseId;

                cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20)
                    .Value = command.PlantCode;

                cmd.Parameters.Add("@MaterialCode", SqlDbType.VarChar, 50)
                    .Value = command.MaterialCode;

                cmd.Parameters.Add("@MaterialRevision", SqlDbType.VarChar, 30)
                    .Value = DbParameterValue.StringOrDbNull(command.MaterialRevision);

                cmd.Parameters.Add("@FilterBatchNo", SqlDbType.VarChar, 100)
                    .Value = DbParameterValue.StringOrDbNull(command.BatchNo);

                cmd.Parameters.Add("@ProductionOrderNo", SqlDbType.VarChar, 50)
                    .Value = DbParameterValue.StringOrDbNull(command.ProductionOrderNo);

                cmd.Parameters.Add("@RequiredQty", SqlDbType.Decimal)
                    .Value = command.RequiredQty;

                cmd.Parameters["@RequiredQty"].Precision = 18;
                cmd.Parameters["@RequiredQty"].Scale = 4;

                cmd.Parameters.Add("@PickedQty", SqlDbType.Decimal)
                    .Value = plan.PlannedPickQty;

                cmd.Parameters["@PickedQty"].Precision = 18;
                cmd.Parameters["@PickedQty"].Scale = 4;

                cmd.Parameters.Add("@DifferenceQty", SqlDbType.Decimal)
                    .Value = plan.DifferenceQty;

                cmd.Parameters["@DifferenceQty"].Precision = 18;
                cmd.Parameters["@DifferenceQty"].Scale = 4;

                cmd.Parameters.Add("@Uom", SqlDbType.VarChar, 20)
                    .Value = command.Uom;

                cmd.Parameters.Add("@ResultType", SqlDbType.VarChar, 20)
                    .Value = plan.ResultType;

                cmd.Parameters.Add("@BusinessNo", SqlDbType.VarChar, 100)
                    .Value = DbParameterValue.StringOrDbNull(command.BusinessNo);

                cmd.Parameters.Add("@Operator", SqlDbType.VarChar, 50)
                    .Value = DbParameterValue.StringOrDbNull(command.Operator);

                cmd.Parameters.Add("@PickTime", SqlDbType.DateTime2)
                    .Value = pickTime;

                cmd.Parameters.Add("@Remark", SqlDbType.NVarChar, 500)
                    .Value = DbParameterValue.StringOrDbNull(command.Remark);

                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        private static string ProcessBatchBox(
            SqlConnection conn,
            SqlTransaction tran,
            PickBatchCommand command,
            OutboundPickCandidateDto candidate,
            OutboundBatchPlanItemDto planItem,
            long pickBatchId,
            long stagingLocationId,
            string stagingLocationCode,
            string operationNo,
            DateTime pickTime,
            out PickBatchItemResultDto detail)
        {
            detail = null;

            long moveLogId = InsertMoveLog(
                conn,
                tran,
                operationNo,
                candidate.BoxHuId,
                candidate.LocationId,
                stagingLocationId,
                "PICK_BATCH",
                stagingLocationCode,
                command.BusinessNo,
                null,
                command.Operator,
                pickTime,
                command.Remark);

            bool locationUpdated = UpdateCurrentLocation(
                conn,
                tran,
                candidate.BoxHuId,
                candidate.LocationId,
                stagingLocationId,
                moveLogId,
                operationNo,
                "PICK_BATCH",
                command.Operator,
                pickTime);

            if (!locationUpdated)
            {
                return "外箱[" +
                       candidate.BoxHuCode +
                       "]当前位置已变化，整个批量拣货已回滚。";
            }

            InsertOperationLog(
                conn,
                tran,
                operationNo,
                "PICK_BATCH",
                candidate.BoxHuId,
                command.BusinessNo,
                command.RequestId,
                command.Operator,
                pickTime,
                command.Remark);

            InsertPickBatchDetail(
                conn,
                tran,
                pickBatchId,
                planItem.SequenceNo,
                candidate,
                stagingLocationId,
                moveLogId,
                operationNo);

            detail = new PickBatchItemResultDto
            {
                SequenceNo = planItem.SequenceNo,
                BoxHuCode = candidate.BoxHuCode,
                FromLocationCode = candidate.LocationCode,
                ToLocationCode = stagingLocationCode,
                BoxQty = candidate.Qty,
                Uom = candidate.Uom,
                FifoTime = candidate.FifoTime,
                MoveLogId = moveLogId,
                OperationNo = operationNo
            };

            return null;
        }

        private static void InsertPickBatchDetail(
            SqlConnection conn,
            SqlTransaction tran,
            long pickBatchId,
            int sequenceNo,
            OutboundPickCandidateDto candidate,
            long stagingLocationId,
            long moveLogId,
            string operationNo)
        {
            const string sql = @"
INSERT INTO dbo.WMS_OutboundPickBatchDetail
(
    PickBatchId,
    SequenceNo,
    BoxHUId,
    FromLocationId,
    ToLocationId,
    FifoTime,
    BoxQty,
    Uom,
    MoveLogId,
    OperationNo
)
VALUES
(
    @PickBatchId,
    @SequenceNo,
    @BoxHUId,
    @FromLocationId,
    @ToLocationId,
    @FifoTime,
    @BoxQty,
    @Uom,
    @MoveLogId,
    @OperationNo
);
";

            using (SqlCommand cmd = new SqlCommand(sql, conn, tran))
            {
                cmd.Parameters.Add("@PickBatchId", SqlDbType.BigInt)
                    .Value = pickBatchId;

                cmd.Parameters.Add("@SequenceNo", SqlDbType.Int)
                    .Value = sequenceNo;

                cmd.Parameters.Add("@BoxHUId", SqlDbType.BigInt)
                    .Value = candidate.BoxHuId;

                cmd.Parameters.Add("@FromLocationId", SqlDbType.BigInt)
                    .Value = candidate.LocationId;

                cmd.Parameters.Add("@ToLocationId", SqlDbType.BigInt)
                    .Value = stagingLocationId;

                cmd.Parameters.Add("@FifoTime", SqlDbType.DateTime2)
                    .Value = candidate.FifoTime;

                cmd.Parameters.Add("@BoxQty", SqlDbType.Decimal)
                    .Value = candidate.Qty;

                cmd.Parameters["@BoxQty"].Precision = 18;
                cmd.Parameters["@BoxQty"].Scale = 4;

                cmd.Parameters.Add("@Uom", SqlDbType.VarChar, 20)
                    .Value = candidate.Uom;

                cmd.Parameters.Add("@MoveLogId", SqlDbType.BigInt)
                    .Value = moveLogId;

                cmd.Parameters.Add("@OperationNo", SqlDbType.VarChar, 50)
                    .Value = operationNo;

                cmd.ExecuteNonQuery();
            }
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
            cmd.Parameters.Add("@WarehouseCode", SqlDbType.VarChar, 30)
                .Value = warehouseCode;

            cmd.Parameters.Add("@PlantCode", SqlDbType.VarChar, 20)
                .Value = plantCode;

            cmd.Parameters.Add("@MaterialCode", SqlDbType.VarChar, 50)
                .Value = materialCode;

            cmd.Parameters.Add("@MaterialRevision", SqlDbType.VarChar, 30)
                .Value = DbParameterValue.StringOrDbNull(materialRevision);

            cmd.Parameters.Add("@BatchNo", SqlDbType.VarChar, 100)
                .Value = DbParameterValue.StringOrDbNull(batchNo);

            cmd.Parameters.Add("@ProductionOrderNo", SqlDbType.VarChar, 50)
                .Value = DbParameterValue.StringOrDbNull(productionOrderNo);
        }

        private static DynamicParameters CreateBatchFilterParameters(
            string warehouseCode,
            string plantCode,
            string materialCode,
            string materialRevision,
            string batchNo,
            string productionOrderNo,
            string uom)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@WarehouseCode", warehouseCode, DbType.AnsiString, size: 30);
            parameters.Add("@PlantCode", plantCode, DbType.AnsiString, size: 20);
            parameters.Add("@MaterialCode", materialCode, DbType.AnsiString, size: 50);
            parameters.Add("@MaterialRevision", DbParameterValue.StringOrNull(materialRevision), DbType.AnsiString, size: 30);
            parameters.Add("@BatchNo", DbParameterValue.StringOrNull(batchNo), DbType.AnsiString, size: 100);
            parameters.Add("@ProductionOrderNo", DbParameterValue.StringOrNull(productionOrderNo), DbType.AnsiString, size: 50);
            parameters.Add("@Uom", uom, DbType.AnsiString, size: 20);

            return parameters;
        }

        private static List<T> Query<T>(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn == null
                ? DbExecutor.Query<T>(sql, param)
                : DbExecutor.Query<T>(conn, tran, sql, param);
        }

        private static T QueryFirstOrDefault<T>(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn == null
                ? DbExecutor.QueryFirstOrDefault<T>(sql, param)
                : DbExecutor.QueryFirstOrDefault<T>(conn, tran, sql, param);
        }

        private static T ExecuteScalar<T>(
            SqlConnection conn,
            SqlTransaction tran,
            string sql,
            object param = null)
        {
            return conn == null
                ? DbExecutor.ExecuteScalar<T>(sql, param)
                : DbExecutor.ExecuteScalar<T>(conn, tran, sql, param);
        }

        private class PickBatchHeaderRow : PickBatchResultDto
        {
            public long Id { get; set; }
        }

        private class OutboundStagingRow
        {
            public long WarehouseId { get; set; }
            public long LocationId { get; set; }
            public string LocationCode { get; set; }
        }
    }
}
