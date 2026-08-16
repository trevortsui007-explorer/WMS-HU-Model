using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Outbound.Formal;

namespace WMS_HU_Model.Repositories
{
    public class FormalOutboundRepository : IFormalOutboundRepository
    {
        public List<OutboundOrderDto> GetOrders(OutboundOrderQuery query)
        {
            const string sql = @"
SELECT TOP (@Top)
    o.Id,
    o.WarehouseId,
    o.OutboundOrderNo,
    o.SAPDeliveryNo,
    w.WarehouseCode,
    o.PlantCode,
    o.SAPStorageLocation,
    o.CustomerCode,
    o.SoldToCode,
    o.PlannedShipDate,
    o.OrderStatus,
    o.ReceivedTime,
    o.StartPickTime,
    o.PickedTime,
    o.ShippedTime
FROM dbo.WMS_OutboundOrder o
INNER JOIN dbo.WMS_Warehouse w ON w.Id = o.WarehouseId
WHERE @Status IS NULL OR o.OrderStatus = @Status
ORDER BY o.ReceivedTime DESC, o.Id DESC;";

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@Top", query.Top, DbType.Int32);
            parameters.Add("@Status", DbParameterValue.StringOrNull(query.Status), DbType.AnsiString, size: 30);
            return DbExecutor.Query<OutboundOrderDto>(sql, parameters);
        }

        public OutboundOrderDto GetOrder(string sapDeliveryNo)
        {
            const string orderSql = @"
SELECT TOP 1
    o.Id,
    o.WarehouseId,
    o.OutboundOrderNo,
    o.SAPDeliveryNo,
    w.WarehouseCode,
    o.PlantCode,
    o.SAPStorageLocation,
    o.CustomerCode,
    o.SoldToCode,
    o.PlannedShipDate,
    o.OrderStatus,
    o.ReceivedTime,
    o.StartPickTime,
    o.PickedTime,
    o.ShippedTime
FROM dbo.WMS_OutboundOrder o
INNER JOIN dbo.WMS_Warehouse w ON w.Id = o.WarehouseId
WHERE o.SAPDeliveryNo = @SAPDeliveryNo;";

            const string lineSql = @"
SELECT
    Id,
    OutboundOrderId,
    SAPItemNo,
    MaterialCode,
    MaterialRevision,
    RequiredQty,
    SpareQty,
    SAPGiftQty,
    GiftQty,
    EffectiveRequiredQty,
    Uom,
    PlantCode,
    SAPStorageLocation,
    AllocatedQty,
    EffectivePickedQty,
    ShippedQty,
    LineStatus
FROM dbo.WMS_OutboundOrderLine
WHERE OutboundOrderId = @OutboundOrderId
ORDER BY SAPItemNo ASC, Id ASC;";

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@SAPDeliveryNo", sapDeliveryNo, DbType.AnsiString, size: 30);
            OutboundOrderDto order = DbExecutor.QueryFirstOrDefault<OutboundOrderDto>(orderSql, parameters);
            if (order == null)
                return null;

            DynamicParameters lineParameters = new DynamicParameters();
            lineParameters.Add("@OutboundOrderId", order.Id, DbType.Int64);
            order.Lines = DbExecutor.Query<OutboundOrderLineDto>(lineSql, lineParameters);
            return order;
        }

        public List<OutboundAllocationCandidateDto> GetAllocationCandidates(
            OutboundOrderDto order,
            string materialCode,
            string uom)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                return GetAllocationCandidatesInternal(conn, null, order, materialCode, uom);
            }
        }

        private static List<OutboundAllocationCandidateDto> GetAllocationCandidatesInternal(
            SqlConnection conn,
            SqlTransaction tran,
            OutboundOrderDto order,
            string materialCode,
            string uom)
        {
            const string boxSql = @"
;WITH Candidate AS
(
    SELECT TOP (101)
        box.Id AS BoxHuId,
        box.HUCode AS BoxHuCode,
        cl.LocationId,
        loc.LocationCode,
        MIN(item.MaterialCode) AS MaterialCode,
        MIN(item.Uom) AS Uom,
        SUM(item.Qty) AS Qty,
        MIN(item.WeekSortKey) AS MinWeekSortKey,
        MAX(item.WeekSortKey) AS MaxWeekSortKey,
        COUNT(DISTINCT item.WeekSortKey) AS DistinctWeekCount
    FROM dbo.WMS_HU box
    INNER JOIN dbo.WMS_HU_CurrentLocation cl ON cl.HUId = box.Id
    INNER JOIN dbo.WMS_Location loc ON loc.Id = cl.LocationId
    INNER JOIN dbo.WMS_Zone z ON z.Id = loc.ZoneId
    INNER JOIN dbo.WMS_Warehouse w ON w.Id = loc.WarehouseId
    INNER JOIN dbo.WMS_WarehousePlant wp
        ON wp.WarehouseId = w.Id
       AND wp.PlantCode = box.PlantCode
       AND wp.IsActive = 1
    INNER JOIN dbo.WMS_HU_Relation rel
        ON rel.ParentHUId = box.Id
       AND rel.IsActive = 1
    INNER JOIN dbo.WMS_HU innerHu
        ON innerHu.Id = rel.ChildHUId
    INNER JOIN dbo.WMS_HU_Item item
        ON item.HUId = innerHu.Id
       AND item.IsActive = 1
    WHERE box.HUType = 'OUTER_BOX'
      AND box.LifecycleStatus = 'ACTIVE'
      AND box.IsHold = 0
      AND w.WarehouseCode = @WarehouseCode
      AND box.PlantCode = @PlantCode
      AND wp.SAPStorageLocation = @SAPStorageLocation
      AND z.ZoneCode = 'STORAGE'
      AND loc.IsVirtual = 0
      AND loc.MasterStatus = 'ACTIVE'
      AND innerHu.HUType = 'INNER_PACK'
      AND innerHu.LifecycleStatus = 'ACTIVE'
      AND innerHu.IsHold = 0
      AND item.StockStatus = 'AVAILABLE'
      AND item.WeekSortKey IS NOT NULL
      AND item.WeekResolvedTime IS NOT NULL
      AND item.MaterialCode = @MaterialCode
      AND item.Uom = @Uom
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.WMS_HU_Reservation r
          WHERE r.HUId = box.Id
            AND r.ReservationStatus = 'ACTIVE'
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.WMS_HU_Relation badRel
          INNER JOIN dbo.WMS_HU badInner ON badInner.Id = badRel.ChildHUId
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
                    FROM dbo.WMS_HU_Item presentItem
                    WHERE presentItem.HUId = badInner.Id
                      AND presentItem.IsActive = 1
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
                          OR badItem.WeekSortKey IS NULL
                          OR badItem.WeekResolvedTime IS NULL
                          OR badItem.MaterialCode <> @MaterialCode
                          OR badItem.Uom <> @Uom
                      )
                )
            )
      )
    GROUP BY box.Id, box.HUCode, cl.LocationId, loc.LocationCode
    HAVING COUNT(DISTINCT item.MaterialCode) = 1
       AND COUNT(DISTINCT item.Uom) = 1
    ORDER BY MIN(item.WeekSortKey), box.Id
), WeekValues AS
(
    SELECT DISTINCT c.BoxHuId, i.WeekSortKey
    FROM Candidate c
    INNER JOIN dbo.WMS_HU_Relation r ON r.ParentHUId = c.BoxHuId AND r.IsActive = 1
    INNER JOIN dbo.WMS_HU_Item i ON i.HUId = r.ChildHUId AND i.IsActive = 1
)
SELECT
    c.BoxHuId,
    c.BoxHuCode,
    c.LocationId,
    c.LocationCode,
    c.MaterialCode,
    c.Uom,
    c.Qty,
    c.MinWeekSortKey,
    c.MaxWeekSortKey,
    c.DistinctWeekCount,
    STUFF
    (
        (
            SELECT ',' + CONVERT(VARCHAR(10), wv.WeekSortKey)
            FROM WeekValues wv
            WHERE wv.BoxHuId = c.BoxHuId
            ORDER BY wv.WeekSortKey
            FOR XML PATH(''), TYPE
        ).value('.', 'VARCHAR(500)'),
        1,
        1,
        ''
    ) AS WeekSummary
FROM Candidate c
ORDER BY c.MinWeekSortKey, c.BoxHuId;";

            DynamicParameters parameters = CreateCandidateParameters(order, materialCode, uom);
            List<OutboundAllocationCandidateDto> candidates = DbExecutor.Query<OutboundAllocationCandidateDto>(
                conn,
                tran,
                boxSql,
                parameters);
            if (candidates.Count == 0)
                return candidates;

            if (candidates.Count > 100)
                throw new InvalidOperationException("单个MATNR+UOM分组的OUTER_BOX候选超过100个，正式分配已阻断。");

            const string innerSql = @"
SELECT
    rel.ParentHUId AS BoxHuId,
    innerHu.Id AS InnerHuId,
    innerHu.HUCode AS InnerHuCode,
    SUM(item.Qty) AS Qty,
    MIN(item.WeekSortKey) AS MinWeekSortKey
FROM dbo.WMS_HU_Relation rel
INNER JOIN dbo.WMS_HU innerHu ON innerHu.Id = rel.ChildHUId
INNER JOIN dbo.WMS_HU_Item item ON item.HUId = innerHu.Id AND item.IsActive = 1
WHERE rel.IsActive = 1
  AND rel.ParentHUId IN @BoxHuIds
GROUP BY rel.ParentHUId, innerHu.Id, innerHu.HUCode
ORDER BY MIN(item.WeekSortKey), innerHu.Id;";

            List<InnerCandidateRow> innerRows = conn.Query<InnerCandidateRow>(
                innerSql,
                new { BoxHuIds = candidates.Select(x => x.BoxHuId).ToArray() },
                tran).AsList();

            Dictionary<long, OutboundAllocationCandidateDto> byId = candidates.ToDictionary(x => x.BoxHuId);
            foreach (InnerCandidateRow row in innerRows)
            {
                OutboundAllocationCandidateDto box;
                if (byId.TryGetValue(row.BoxHuId, out box))
                {
                    box.InnerPacks.Add(new OutboundInnerPackCandidateDto
                    {
                        InnerHuId = row.InnerHuId,
                        InnerHuCode = row.InnerHuCode,
                        Qty = row.Qty,
                        MinWeekSortKey = row.MinWeekSortKey
                    });
                }
            }

            return candidates;
        }

        public StartPickingResultDto GetStartPickingByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;

            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                return ReadStartPickingResult(conn, null, requestId);
            }
        }

        public StartPickingPersistenceResult ExecuteStartPicking(StartPickingCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        StartPickingResultDto duplicate = ReadStartPickingResult(conn, tran, command.RequestId);
                        if (duplicate != null)
                        {
                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return StartPickingPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, command.RequestId))
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        OutboundOrderDto order = ReadOrderForUpdate(conn, tran, command.SAPDeliveryNo);
                        if (order == null)
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("出库订单不存在。");
                        }

                        if (!string.Equals(order.OrderStatus, "WAIT_PICK", StringComparison.Ordinal))
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("只有WAIT_PICK订单允许StartPicking。");
                        }

                        if (string.IsNullOrWhiteSpace(order.SAPStorageLocation))
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("订单SAPStorageLocation为空，无法执行正式分配。");
                        }

                        List<PlannedGroup> groups = new List<PlannedGroup>();
                        foreach (IGrouping<string, OutboundOrderLineDto> lineGroup in order.Lines
                            .OrderBy(x => x.SAPItemNo, StringComparer.Ordinal)
                            .GroupBy(x => BuildGroupKey(x.MaterialCode, x.Uom), StringComparer.Ordinal))
                        {
                            OutboundOrderLineDto first = lineGroup.First();
                            decimal requiredQty = lineGroup.Sum(x => x.EffectiveRequiredQty);
                            if (requiredQty <= 0)
                            {
                                tran.Rollback();
                                return StartPickingPersistenceResult.Fail("订单存在EffectiveRequiredQty不大于0的分组，未定义分配规则。");
                            }

                            List<OutboundAllocationCandidateDto> candidates = GetAllocationCandidatesInternal(
                                conn,
                                tran,
                                order,
                                first.MaterialCode,
                                first.Uom);
                            OutboundAllocationPlanDto plan = WMS_HU_Model.Rules.OutboundAllocationPlanner.Build(candidates, requiredQty);
                            if (!string.Equals(plan.ResultType, "EXACT_WHOLE_BOX", StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return StartPickingPersistenceResult.Fail(
                                    "分组" + lineGroup.Key + "无法整箱精确分配，结果：" + plan.ResultType + "。");
                            }

                            groups.Add(new PlannedGroup
                            {
                                GroupKey = lineGroup.Key,
                                MaterialCode = first.MaterialCode,
                                Uom = first.Uom,
                                RequiredQty = requiredQty,
                                Plan = plan
                            });
                        }

                        if (groups.Count == 0)
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("订单没有可分配的明细行。");
                        }

                        DateTime allocateTime = DateTime.Now;
                        DateTime expireTime = allocateTime.AddHours(3);
                        string token = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
                        string allocationNo = "OA" + allocateTime.ToString("yyyyMMddHHmmss") + token;

                        long allocationId = InsertAllocation(
                            conn,
                            tran,
                            allocationNo,
                            order.Id,
                            command,
                            allocateTime,
                            expireTime);

                        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                        {
                            PlannedGroup group = groups[groupIndex];
                            string pickBatchNo = "PB" + allocateTime.ToString("yyyyMMddHHmmss") + token + (groupIndex + 1).ToString("D2");
                            string batchRequestId = command.RequestId + "/B" + (groupIndex + 1).ToString("D2");
                            long pickBatchId = InsertPickBatch(
                                conn,
                                tran,
                                pickBatchNo,
                                batchRequestId,
                                allocationId,
                                order,
                                group,
                                command,
                                expireTime);

                            for (int boxIndex = 0; boxIndex < group.Plan.SelectedBoxes.Count; boxIndex++)
                            {
                                OutboundAllocationCandidateDto box = group.Plan.SelectedBoxes[boxIndex];
                                string reservationNo = "RS" + allocateTime.ToString("yyyyMMddHHmmss") + token
                                    + (groupIndex + 1).ToString("D2") + (boxIndex + 1).ToString("D3");
                                InsertReservation(
                                    conn,
                                    tran,
                                    reservationNo,
                                    allocationId,
                                    pickBatchId,
                                    order.Id,
                                    box,
                                    group.Uom,
                                    expireTime,
                                    command.Operator);
                            }

                            UpdateAllocatedLines(conn, tran, order.Id, group.MaterialCode, group.Uom);
                        }

                        const string updateOrderSql = @"
UPDATE dbo.WMS_OutboundOrder
SET OrderStatus = 'ALLOCATED',
    StartPickTime = @StartPickTime,
    ModifyUser = @Operator,
    ModifyTime = SYSDATETIME()
WHERE Id = @OutboundOrderId
  AND OrderStatus = 'WAIT_PICK';";

                        int orderRows = conn.Execute(updateOrderSql, new
                        {
                            StartPickTime = allocateTime,
                            Operator = command.Operator,
                            OutboundOrderId = order.Id
                        }, tran);
                        if (orderRows != 1)
                            throw new InvalidOperationException("订单状态在分配事务中发生变化。");

                        StartPickingResultDto result = ReadStartPickingResult(conn, tran, command.RequestId);
                        tran.Commit();
                        return StartPickingPersistenceResult.Success(result);
                    }
                    catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                    {
                        if (tran.Connection != null)
                            tran.Rollback();
                        return StartPickingPersistenceResult.Fail("库存已被并发分配，请重新Preview后再试。");
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

        public OutboundActionPersistenceResult ReleaseAllocation(
            string allocationNo,
            OutboundActionRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        const string duplicateSql = @"
SELECT TOP 1 AllocationNo AS BusinessNo, AllocationStatus AS Status
FROM dbo.WMS_OutboundAllocation
WHERE ReleaseRequestId = @RequestId;";
                        OutboundActionResultDto duplicate = conn.QueryFirstOrDefault<OutboundActionResultDto>(
                            duplicateSql,
                            new { request.RequestId },
                            tran);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.BusinessNo, allocationNo, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OutboundActionPersistenceResult.Fail("RequestId已经用于释放其他Allocation。");
                            }

                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OutboundActionPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        const string readSql = @"
SELECT TOP 1 Id, OutboundOrderId, AllocationStatus, FirstPickTime
FROM dbo.WMS_OutboundAllocation WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE AllocationNo = @AllocationNo;";
                        AllocationActionRow allocation = conn.QueryFirstOrDefault<AllocationActionRow>(
                            readSql,
                            new { AllocationNo = allocationNo },
                            tran);
                        if (allocation == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("Allocation不存在。");
                        }
                        if (!string.Equals(allocation.AllocationStatus, "ACTIVE", StringComparison.Ordinal)
                            || allocation.FirstPickTime.HasValue)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("只有尚未扫描首箱的ACTIVE Allocation允许释放。");
                        }

                        DateTime now = DateTime.Now;
                        conn.Execute(@"
UPDATE dbo.WMS_HU_Reservation
SET ReservationStatus = 'RELEASED', ReleasedTime = @Now, ReleaseReason = @Reason
WHERE AllocationId = @AllocationId AND ReservationStatus = 'ACTIVE';

UPDATE dbo.WMS_OutboundPickBatch
SET BatchStatus = 'RELEASED', CancelTime = @Now, CancelReason = @Reason
WHERE AllocationId = @AllocationId AND BatchStatus = 'ALLOCATED';

UPDATE dbo.WMS_OutboundOrderLine
SET AllocatedQty = 0, LineStatus = 'WAIT_PICK', ModifyTime = @Now
WHERE OutboundOrderId = @OutboundOrderId AND LineStatus = 'ALLOCATED';

UPDATE dbo.WMS_OutboundOrder
SET OrderStatus = 'WAIT_PICK', StartPickTime = NULL,
    ModifyUser = @Operator, ModifyTime = @Now
WHERE Id = @OutboundOrderId AND OrderStatus = 'ALLOCATED';

UPDATE dbo.WMS_OutboundAllocation
SET AllocationStatus = 'RELEASED', ReleaseTime = @Now,
    ReleaseReason = @Reason, ReleaseRequestId = @RequestId, ModifyTime = @Now
WHERE Id = @AllocationId;",
                            new
                            {
                                Now = now,
                                request.Reason,
                                request.Operator,
                                request.RequestId,
                                AllocationId = allocation.Id,
                                allocation.OutboundOrderId
                            },
                            tran);

                        tran.Commit();
                        return OutboundActionPersistenceResult.Success(new OutboundActionResultDto
                        {
                            BusinessNo = allocationNo,
                            Status = "RELEASED"
                        });
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

        public OutboundActionPersistenceResult VoidOrder(
            string sapDeliveryNo,
            OutboundActionRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        const string duplicateSql = @"
SELECT TOP 1 SAPDeliveryNo AS BusinessNo, OrderStatus AS Status
FROM dbo.WMS_OutboundOrder
WHERE VoidRequestId = @RequestId;";
                        OutboundActionResultDto duplicate = conn.QueryFirstOrDefault<OutboundActionResultDto>(
                            duplicateSql,
                            new { request.RequestId },
                            tran);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.BusinessNo, sapDeliveryNo, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OutboundActionPersistenceResult.Fail("RequestId已经用于作废其他订单。");
                            }

                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OutboundActionPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        const string readSql = @"
SELECT TOP 1 Id, OrderStatus
FROM dbo.WMS_OutboundOrder WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE SAPDeliveryNo = @SAPDeliveryNo;";
                        OrderActionRow order = conn.QueryFirstOrDefault<OrderActionRow>(
                            readSql,
                            new { SAPDeliveryNo = sapDeliveryNo },
                            tran);
                        if (order == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("出库订单不存在。");
                        }
                        if (!string.Equals(order.OrderStatus, "WAIT_PICK", StringComparison.Ordinal))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("只有WAIT_PICK订单允许作废。");
                        }

                        bool hasAllocation = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.WMS_OutboundAllocation
    WHERE OutboundOrderId = @OutboundOrderId
      AND AllocationStatus IN ('ACTIVE', 'PICKING')
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { OutboundOrderId = order.Id },
                            tran);
                        if (hasAllocation)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("订单仍有有效Allocation，不能作废。");
                        }

                        DateTime now = DateTime.Now;
                        conn.Execute(@"
UPDATE dbo.WMS_OutboundOrderLine
SET LineStatus = 'VOID', ModifyTime = @Now
WHERE OutboundOrderId = @OutboundOrderId;

UPDATE dbo.WMS_OutboundOrder
SET OrderStatus = 'VOID', VoidTime = @Now, VoidReason = @Reason,
    VoidRequestId = @RequestId, ModifyUser = @Operator, ModifyTime = @Now
WHERE Id = @OutboundOrderId;",
                            new
                            {
                                Now = now,
                                request.Reason,
                                request.RequestId,
                                request.Operator,
                                OutboundOrderId = order.Id
                            },
                            tran);

                        tran.Commit();
                        return OutboundActionPersistenceResult.Success(new OutboundActionResultDto
                        {
                            BusinessNo = sapDeliveryNo,
                            Status = "VOID"
                        });
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

        public string GetPickBatchCustomerCode(string pickBatchNo)
        {
            const string sql = @"
SELECT TOP 1 ISNULL(o.CustomerCode, '')
FROM dbo.WMS_OutboundPickBatch b
INNER JOIN dbo.WMS_OutboundOrder o ON o.Id = b.OutboundOrderId
WHERE b.PickBatchNo = @PickBatchNo;";
            string value = DbExecutor.QueryFirstOrDefault<string>(sql, new { PickBatchNo = pickBatchNo });
            return value == null ? null : value;
        }

        public OutboundScanResultDto GetScanByRequestId(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                return null;
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                return ReadScanResult(conn, null, requestId);
            }
        }

        public OutboundScanPersistenceResult ExecuteScan(OutboundScanCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        OutboundScanResultDto duplicate = ReadScanResult(conn, tran, command.RequestId);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.PickBatchNo, command.PickBatchNo, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OutboundScanPersistenceResult.Fail("RequestId已经被其他PickBatch使用。");
                            }

                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OutboundScanPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, command.RequestId))
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        ScanBatchRow batch = ReadScanBatch(conn, tran, command.PickBatchNo);
                        if (batch == null)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("PickBatch不存在。");
                        }
                        if (batch.BatchStatus != "ALLOCATED" && batch.BatchStatus != "PICKING"
                            && batch.BatchStatus != "EXCEPTION")
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("当前PickBatch状态不允许扫码。");
                        }
                        if (batch.AllocationStatus != "ACTIVE" && batch.AllocationStatus != "PICKING")
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("当前Allocation状态不允许扫码。");
                        }
                        if (batch.OrderStatus != "ALLOCATED" && batch.OrderStatus != "PICKING"
                            && batch.OrderStatus != "EXCEPTION")
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("当前订单状态不允许扫码。");
                        }

                        const string reservationSql = @"
SELECT TOP 1
    r.Id, r.SourceLocationId, r.ReservedQty, r.Uom,
    r.MinWeekSortKey, r.MaxWeekSortKey, r.DistinctWeekCount, r.WeekSummary,
    replacement.Id AS ReplacementLinkId,
    exception.Id AS ReplacementExceptionId,
    exception.PickDetailId AS ReplacementForDetailId,
    hu.HUCode AS BoxHuCode, hu.LifecycleStatus, hu.IsHold
FROM dbo.WMS_HU_Reservation r WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_HU hu WITH (UPDLOCK, HOLDLOCK, ROWLOCK) ON hu.Id = r.HUId
LEFT JOIN dbo.WMS_OutboundExceptionReplacement replacement
    ON replacement.ReservationId = r.Id
LEFT JOIN dbo.WMS_OutboundPickException exception
    ON exception.Id = replacement.ExceptionId
WHERE r.PickBatchId = @PickBatchId
  AND r.HUId = @HUId
  AND r.ReservationStatus = 'ACTIVE';";
                        ScanReservationRow reservation = conn.QueryFirstOrDefault<ScanReservationRow>(
                            reservationSql,
                            new { batch.PickBatchId, command.HuId },
                            tran);
                        if (reservation == null)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("该箱不属于当前PickBatch的有效Reservation。");
                        }
                        if (reservation.LifecycleStatus != "ACTIVE" || reservation.IsHold)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("该箱当前不可用或处于Hold状态。");
                        }

                        const string locationSql = @"
SELECT cl.LocationId, loc.LocationCode
FROM dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_Location loc ON loc.Id = cl.LocationId
WHERE cl.HUId = @HUId;";
                        ScanLocationRow current = conn.QueryFirstOrDefault<ScanLocationRow>(
                            locationSql,
                            new { command.HuId },
                            tran);
                        if (current == null || current.LocationId != reservation.SourceLocationId)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("箱当前位置已变化，与Reservation源货位不一致。");
                        }

                        const string stagingSql = @"
SELECT TOP 1 Id AS LocationId, LocationCode
FROM dbo.WMS_Location
WHERE WarehouseId = @WarehouseId
  AND LocationCode = 'OUTBOUND-STAGING'
  AND MasterStatus = 'ACTIVE';";
                        ScanLocationRow staging = conn.QueryFirstOrDefault<ScanLocationRow>(
                            stagingSql,
                            new { batch.WarehouseId },
                            tran);
                        if (staging == null)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("当前仓库未配置有效OUTBOUND-STAGING货位。");
                        }

                        List<ScanItemRow> items = ReadScanItems(conn, tran, command.HuId, batch.MaterialCode, batch.Uom);
                        decimal itemQty = items.Sum(x => x.Qty);
                        if (items.Count == 0 || itemQty != reservation.ReservedQty)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("箱内有效Item数量与Reservation快照不一致。");
                        }

                        List<ScanLineRow> lines = ReadScanLines(conn, tran, batch.OutboundOrderId, batch.MaterialCode, batch.Uom);
                        List<ScanItemAllocation> allocations = AllocateItemsToLines(items, lines);
                        if (allocations == null)
                        {
                            tran.Rollback();
                            return OutboundScanPersistenceResult.Fail("箱内Item无法按SAPItemNo升序完整分摊到订单行。");
                        }

                        DateTime now = DateTime.Now;
                        string token = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                        string moveNo = "OM" + now.ToString("yyyyMMddHHmmss") + token;
                        string operationNo = "OP" + now.ToString("yyyyMMddHHmmss") + token;

                        HuMoveLogWriter.HuMoveLogWriteResult move = HuMoveLogWriter.InsertWithTime(
                            conn,
                            tran,
                            moveNo,
                            command.HuId,
                            current.LocationId,
                            staging.LocationId,
                            "OUTBOUND_PICK",
                            command.ScannedValue,
                            batch.SAPDeliveryNo,
                            batch.PickBatchNo,
                            command.RequestId,
                            null,
                            command.Operator,
                            command.Remark);

                        int locationRows = conn.Execute(@"
UPDATE dbo.WMS_HU_CurrentLocation
SET LocationId = @ToLocationId,
    LocationSince = @MoveTime,
    LastMoveLogId = @MoveLogId,
    LastMoveNo = @MoveNo,
    LastMoveType = 'OUTBOUND_PICK',
    LastOperator = @Operator,
    ModifyTime = SYSDATETIME()
WHERE HUId = @HUId
  AND LocationId = @FromLocationId;",
                            new
                            {
                                ToLocationId = staging.LocationId,
                                MoveTime = move.MoveTime,
                                MoveLogId = move.Id,
                                MoveNo = moveNo,
                                command.Operator,
                                command.HuId,
                                FromLocationId = current.LocationId
                            },
                            tran);
                        if (locationRows != 1)
                            throw new InvalidOperationException("扫码事务内更新CurrentLocation失败。");

                        HuOperationLogWriter.Insert(
                            conn,
                            tran,
                            operationNo,
                            "OUTBOUND_PICK",
                            command.HuId,
                            null,
                            batch.SAPDeliveryNo,
                            command.RequestId,
                            command.Operator,
                            move.MoveTime,
                            null,
                            command.Remark);

                        int sequenceNo = conn.ExecuteScalar<int>(@"
SELECT ISNULL(MAX(SequenceNo), 0) + 1
FROM dbo.WMS_OutboundPickBatchDetail WITH (UPDLOCK, HOLDLOCK)
WHERE PickBatchId = @PickBatchId;",
                            new { batch.PickBatchId },
                            tran);

                        long detailId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_OutboundPickBatchDetail
(
    PickBatchId, SequenceNo, BoxHUId, FromLocationId, ToLocationId,
    FifoTime, BoxQty, Uom, MoveLogId, OperationNo,
    DetailStatus, EffectiveQty, ReplacementForDetailId, IsReplacement,
    MinWeekSortKey, MaxWeekSortKey,
    DistinctWeekCount, WeekSummary
)
OUTPUT INSERTED.Id
VALUES
(
    @PickBatchId, @SequenceNo, @BoxHUId, @FromLocationId, @ToLocationId,
    NULL, @BoxQty, @Uom, @MoveLogId, @OperationNo,
    'PICKED', @BoxQty, @ReplacementForDetailId, @IsReplacement,
    @MinWeekSortKey, @MaxWeekSortKey,
    @DistinctWeekCount, @WeekSummary
);",
                            new
                            {
                                batch.PickBatchId,
                                SequenceNo = sequenceNo,
                                BoxHUId = command.HuId,
                                FromLocationId = current.LocationId,
                                ToLocationId = staging.LocationId,
                                BoxQty = reservation.ReservedQty,
                                reservation.Uom,
                                MoveLogId = move.Id,
                                OperationNo = operationNo,
                                reservation.ReplacementForDetailId,
                                IsReplacement = reservation.ReplacementLinkId.HasValue,
                                reservation.MinWeekSortKey,
                                reservation.MaxWeekSortKey,
                                reservation.DistinctWeekCount,
                                reservation.WeekSummary
                            },
                            tran);

                        InsertPickDetailItems(conn, tran, detailId, allocations);

                        conn.Execute(@"
UPDATE dbo.WMS_HU_Reservation
SET ReservationStatus = 'CONSUMED', ConsumedTime = @Now,
    Operator = @Operator, RequestId = @RequestId
WHERE Id = @ReservationId AND ReservationStatus = 'ACTIVE';",
                            new
                            {
                                Now = move.MoveTime,
                                command.Operator,
                                command.RequestId,
                                ReservationId = reservation.Id
                            },
                            tran);

                        if (reservation.ReplacementLinkId.HasValue)
                        {
                            conn.Execute(@"
UPDATE dbo.WMS_OutboundExceptionReplacement
SET PickDetailId = @PickDetailId
WHERE Id = @ReplacementLinkId;

UPDATE ex
SET ExceptionStatus = 'RESOLVED',
    ReplacementPickDetailId = @PickDetailId,
    ResolvedTime = @Now,
    ResolvedUser = @Operator,
    ResolutionCode = 'REPLACED'
FROM dbo.WMS_OutboundPickException ex
WHERE ex.Id = @ExceptionId
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.WMS_OutboundExceptionReplacement link
      INNER JOIN dbo.WMS_HU_Reservation rr ON rr.Id = link.ReservationId
      WHERE link.ExceptionId = ex.Id
        AND rr.ReservationStatus <> 'CONSUMED'
  );",
                                new
                                {
                                    PickDetailId = detailId,
                                    ReplacementLinkId = reservation.ReplacementLinkId.Value,
                                    ExceptionId = reservation.ReplacementExceptionId,
                                    Now = move.MoveTime,
                                    command.Operator
                                },
                                tran);
                        }

                        foreach (IGrouping<long, ScanItemAllocation> lineAllocation in allocations.GroupBy(x => x.LineId))
                        {
                            conn.Execute(@"
UPDATE dbo.WMS_OutboundOrderLine
SET EffectivePickedQty = EffectivePickedQty + @Qty,
    LineStatus = CASE
        WHEN EffectivePickedQty + @Qty = EffectiveRequiredQty THEN 'PICKED'
        ELSE 'PICKING'
    END,
    ModifyTime = @Now
WHERE Id = @LineId;",
                                new
                                {
                                    Qty = lineAllocation.Sum(x => x.AllocatedQty),
                                    Now = move.MoveTime,
                                    LineId = lineAllocation.Key
                                },
                                tran);
                        }

                        decimal newBatchQty = batch.EffectivePickedQty + reservation.ReservedQty;
                        string batchStatus = newBatchQty == batch.PlannedQty ? "PICKED" : "PICKING";
                        conn.Execute(@"
UPDATE dbo.WMS_OutboundPickBatch
SET PickedQty = @NewQty,
    DifferenceQty = @NewQty - RequiredQty,
    EffectivePickedQty = @NewQty,
    HistoricalPickedQty = HistoricalPickedQty + @BoxQty,
    BatchStatus = @BatchStatus,
    PickTime = ISNULL(PickTime, @Now),
    StartPickTime = ISNULL(StartPickTime, @Now),
    CompleteTime = CASE WHEN @BatchStatus = 'PICKED' THEN @Now ELSE NULL END
WHERE Id = @PickBatchId;",
                            new
                            {
                                NewQty = newBatchQty,
                                BoxQty = reservation.ReservedQty,
                                BatchStatus = batchStatus,
                                Now = move.MoveTime,
                                batch.PickBatchId
                            },
                            tran);

                        bool allPicked = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN NOT EXISTS
(
    SELECT 1 FROM dbo.WMS_OutboundPickBatch
    WHERE AllocationId = @AllocationId
      AND BatchStatus <> 'PICKED'
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { batch.AllocationId },
                            tran);
                        bool hasOpenException = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.WMS_OutboundPickException
    WHERE OutboundOrderId = @OutboundOrderId
      AND ExceptionStatus NOT IN ('RESOLVED', 'CANCELLED')
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { batch.OutboundOrderId },
                            tran);
                        string orderStatus = hasOpenException
                            ? "EXCEPTION"
                            : (allPicked ? "PICKED" : "PICKING");
                        conn.Execute(@"
UPDATE dbo.WMS_OutboundAllocation
SET AllocationStatus = @AllocationStatus,
    FirstPickTime = ISNULL(FirstPickTime, @Now),
    CompleteTime = CASE WHEN @AllocationStatus = 'COMPLETED' THEN @Now ELSE NULL END,
    ModifyTime = @Now
WHERE Id = @AllocationId;

UPDATE dbo.WMS_OutboundOrder
SET OrderStatus = @OrderStatus,
    PickedTime = CASE WHEN @OrderStatus = 'PICKED' THEN @Now ELSE NULL END,
    ModifyUser = @Operator,
    ModifyTime = @Now
WHERE Id = @OutboundOrderId;",
                            new
                            {
                                AllocationStatus = allPicked && !hasOpenException ? "COMPLETED" : "PICKING",
                                OrderStatus = orderStatus,
                                Now = move.MoveTime,
                                command.Operator,
                                batch.AllocationId,
                                batch.OutboundOrderId
                            },
                            tran);

                        tran.Commit();
                        return OutboundScanPersistenceResult.Success(new OutboundScanResultDto
                        {
                            PickDetailId = detailId,
                            PickBatchNo = batch.PickBatchNo,
                            BoxHuCode = reservation.BoxHuCode,
                            FromLocationCode = current.LocationCode,
                            ToLocationCode = staging.LocationCode,
                            BoxQty = reservation.ReservedQty,
                            Uom = reservation.Uom,
                            BatchStatus = batchStatus,
                            OrderStatus = orderStatus,
                            MoveLogId = move.Id,
                            OperationNo = operationNo
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                    {
                        if (tran.Connection != null)
                            tran.Rollback();
                        OutboundScanResultDto duplicate = GetScanByRequestId(command.RequestId);
                        if (duplicate != null)
                        {
                            duplicate.IsDuplicate = true;
                            return OutboundScanPersistenceResult.Success(duplicate);
                        }
                        return OutboundScanPersistenceResult.Fail("扫码请求与并发库存操作冲突，请重试。");
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

        public OutboundActionPersistenceResult ShipOrder(
            string sapDeliveryNo,
            ShipOutboundRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        OutboundActionResultDto duplicate = conn.QueryFirstOrDefault<OutboundActionResultDto>(@"
SELECT TOP 1 SAPDeliveryNo AS BusinessNo, OrderStatus AS Status
FROM dbo.WMS_OutboundOrder
WHERE ShipRequestId = @RequestId;",
                            new { request.RequestId },
                            tran);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.BusinessNo, sapDeliveryNo, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OutboundActionPersistenceResult.Fail("RequestId已经用于出库其他订单。");
                            }
                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OutboundActionPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        ShipOrderRow order = conn.QueryFirstOrDefault<ShipOrderRow>(@"
SELECT TOP 1 Id, WarehouseId, OrderStatus
FROM dbo.WMS_OutboundOrder WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE SAPDeliveryNo = @SAPDeliveryNo;",
                            new { SAPDeliveryNo = sapDeliveryNo },
                            tran);
                        if (order == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("出库订单不存在。");
                        }
                        if (order.OrderStatus != "PICKED")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("只有PICKED订单允许执行SHIPPED。");
                        }

                        bool invalid = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN
    EXISTS
    (
        SELECT 1 FROM dbo.WMS_OutboundOrderLine
        WHERE OutboundOrderId = @OrderId
          AND (EffectivePickedQty <> EffectiveRequiredQty OR LineStatus <> 'PICKED')
    )
 OR EXISTS
    (
        SELECT 1 FROM dbo.WMS_OutboundPickBatch
        WHERE OutboundOrderId = @OrderId AND BatchStatus <> 'PICKED'
    )
 OR EXISTS
    (
        SELECT 1 FROM dbo.WMS_OutboundPickException
        WHERE OutboundOrderId = @OrderId
          AND ExceptionStatus NOT IN ('RESOLVED', 'CANCELLED')
    )
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { OrderId = order.Id },
                            tran);
                        if (invalid)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("订单数量、Batch状态或Open Exception不满足SHIPPED条件。");
                        }

                        ScanLocationRow shipped = conn.QueryFirstOrDefault<ScanLocationRow>(@"
SELECT TOP 1 Id AS LocationId, LocationCode
FROM dbo.WMS_Location
WHERE WarehouseId = @WarehouseId
  AND LocationCode = 'SHIPPED'
  AND IsVirtual = 1
  AND IsScannable = 0
  AND MasterStatus = 'ACTIVE';",
                            new { order.WarehouseId },
                            tran);
                        if (shipped == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("当前仓库未配置正式SHIPPED虚拟货位。");
                        }

                        List<ShipDetailRow> details = conn.Query<ShipDetailRow>(@"
SELECT
    d.Id AS PickDetailId, d.BoxHUId, d.FromLocationId,
    cl.LocationId AS CurrentLocationId, loc.LocationCode AS CurrentLocationCode
FROM dbo.WMS_OutboundPickBatchDetail d WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON cl.HUId = d.BoxHUId
INNER JOIN dbo.WMS_Location loc ON loc.Id = cl.LocationId
WHERE b.OutboundOrderId = @OrderId
  AND d.DetailStatus = 'PICKED'
  AND d.EffectiveQty > 0
ORDER BY d.Id;",
                            new { OrderId = order.Id },
                            tran).AsList();
                        if (details.Count == 0 || details.Any(x => x.CurrentLocationCode != "OUTBOUND-STAGING"))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("有效拣货箱不全在OUTBOUND-STAGING，拒绝SHIPPED。");
                        }

                        DateTime now = DateTime.Now;
                        string token = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
                        for (int i = 0; i < details.Count; i++)
                        {
                            ShipDetailRow detail = details[i];
                            string childRequestId = request.RequestId + "/S" + (i + 1).ToString("D3");
                            string moveNo = "SM" + now.ToString("yyyyMMddHHmmss") + token + (i + 1).ToString("D3");
                            string operationNo = "SO" + now.ToString("yyyyMMddHHmmss") + token + (i + 1).ToString("D3");
                            HuMoveLogWriter.HuMoveLogWriteResult move = HuMoveLogWriter.InsertWithTime(
                                conn, tran, moveNo, detail.BoxHuId, detail.CurrentLocationId,
                                shipped.LocationId, "OUTBOUND_SHIP", null, sapDeliveryNo, null,
                                childRequestId, null, request.Operator, request.Remark);

                            int rows = conn.Execute(@"
UPDATE dbo.WMS_HU_CurrentLocation
SET LocationId = @ToLocationId, LocationSince = @Now,
    LastMoveLogId = @MoveLogId, LastMoveNo = @MoveNo,
    LastMoveType = 'OUTBOUND_SHIP', LastOperator = @Operator,
    ModifyTime = @Now
WHERE HUId = @HUId AND LocationId = @FromLocationId;

UPDATE dbo.WMS_OutboundPickBatchDetail
SET DetailStatus = 'SHIPPED'
WHERE Id = @PickDetailId AND DetailStatus = 'PICKED';",
                                new
                                {
                                    ToLocationId = shipped.LocationId,
                                    Now = move.MoveTime,
                                    MoveLogId = move.Id,
                                    MoveNo = moveNo,
                                    request.Operator,
                                    HUId = detail.BoxHuId,
                                    FromLocationId = detail.CurrentLocationId,
                                    detail.PickDetailId
                                },
                                tran);
                            if (rows < 2)
                                throw new InvalidOperationException("SHIPPED事务内更新箱位置或Detail失败。");

                            HuOperationLogWriter.Insert(
                                conn, tran, operationNo, "OUTBOUND_SHIP", detail.BoxHuId,
                                null, sapDeliveryNo, childRequestId, request.Operator,
                                move.MoveTime, null, request.Remark);
                        }

                        conn.Execute(@"
UPDATE dbo.WMS_OutboundPickBatch
SET BatchStatus = 'SHIPPED'
WHERE OutboundOrderId = @OrderId AND BatchStatus = 'PICKED';

UPDATE dbo.WMS_OutboundOrderLine
SET ShippedQty = EffectivePickedQty, LineStatus = 'SHIPPED', ModifyTime = @Now
WHERE OutboundOrderId = @OrderId AND LineStatus = 'PICKED';

UPDATE dbo.WMS_OutboundOrder
SET OrderStatus = 'SHIPPED', ShippedTime = @Now,
    ShipRequestId = @RequestId, ModifyUser = @Operator, ModifyTime = @Now
WHERE Id = @OrderId AND OrderStatus = 'PICKED';",
                            new
                            {
                                OrderId = order.Id,
                                Now = now,
                                request.RequestId,
                                request.Operator
                            },
                            tran);

                        tran.Commit();
                        return OutboundActionPersistenceResult.Success(new OutboundActionResultDto
                        {
                            BusinessNo = sapDeliveryNo,
                            Status = "SHIPPED"
                        });
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

        public OutboundActionPersistenceResult ReturnPickDetail(
            long pickDetailId,
            ReturnPickDetailRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        OutboundActionResultDto duplicate = conn.QueryFirstOrDefault<OutboundActionResultDto>(@"
SELECT TOP 1 ExceptionNo AS BusinessNo, ExceptionStatus AS Status
FROM dbo.WMS_OutboundPickException
WHERE RequestId = @RequestId;",
                            new { request.RequestId },
                            tran);
                        if (duplicate != null)
                        {
                            long duplicateDetailId = conn.ExecuteScalar<long>(@"
SELECT PickDetailId FROM dbo.WMS_OutboundPickException WHERE RequestId = @RequestId;",
                                new { request.RequestId },
                                tran);
                            if (duplicateDetailId != pickDetailId)
                            {
                                tran.Rollback();
                                return OutboundActionPersistenceResult.Fail("RequestId已经用于退回其他PickDetail。");
                            }
                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OutboundActionPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        ReturnDetailRow detail = conn.QueryFirstOrDefault<ReturnDetailRow>(@"
SELECT TOP 1
    d.Id, d.PickBatchId, d.BoxHUId, d.EffectiveQty, d.DetailStatus,
    b.AllocationId, b.OutboundOrderId, b.WarehouseId,
    b.EffectivePickedQty AS BatchEffectiveQty, b.PlannedQty,
    o.SAPDeliveryNo, o.OrderStatus,
    cl.LocationId AS CurrentLocationId, loc.LocationCode AS CurrentLocationCode,
    hu.HUCode AS BoxHuCode, hu.IsHold
FROM dbo.WMS_OutboundPickBatchDetail d WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON o.Id = b.OutboundOrderId
INNER JOIN dbo.WMS_HU hu WITH (UPDLOCK, HOLDLOCK, ROWLOCK) ON hu.Id = d.BoxHUId
INNER JOIN dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON cl.HUId = d.BoxHUId
INNER JOIN dbo.WMS_Location loc ON loc.Id = cl.LocationId
WHERE d.Id = @PickDetailId;",
                            new { PickDetailId = pickDetailId },
                            tran);
                        if (detail == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("PickDetail不存在。");
                        }
                        if (detail.DetailStatus != "PICKED" || detail.EffectiveQty <= 0)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("只有当前有效的PICKED明细允许异常退回。");
                        }
                        if (detail.OrderStatus == "SHIPPED")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("SHIPPED订单不允许普通异常退回。");
                        }

                        ScanLocationRow returned = conn.QueryFirstOrDefault<ScanLocationRow>(@"
SELECT TOP 1 Id AS LocationId, LocationCode
FROM dbo.WMS_Location
WHERE WarehouseId = @WarehouseId
  AND LocationCode = 'RETURNED'
  AND IsVirtual = 1
  AND MasterStatus = 'ACTIVE';",
                            new { detail.WarehouseId },
                            tran);
                        if (returned == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("当前仓库未配置RETURNED虚拟货位。");
                        }

                        DateTime now = DateTime.Now;
                        string token = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                        string exceptionNo = "EX" + now.ToString("yyyyMMddHHmmss") + token;
                        string moveNo = "RM" + now.ToString("yyyyMMddHHmmss") + token;
                        string operationNo = "RO" + now.ToString("yyyyMMddHHmmss") + token;

                        HuMoveLogWriter.HuMoveLogWriteResult move = HuMoveLogWriter.InsertWithTime(
                            conn, tran, moveNo, detail.BoxHuId, detail.CurrentLocationId,
                            returned.LocationId, "OUTBOUND_RETURN", null, detail.SAPDeliveryNo,
                            null, request.RequestId, request.ReasonCode, request.Operator, request.Remark);
                        int locationRows = conn.Execute(@"
UPDATE dbo.WMS_HU_CurrentLocation
SET LocationId = @ToLocationId, LocationSince = @Now,
    LastMoveLogId = @MoveLogId, LastMoveNo = @MoveNo,
    LastMoveType = 'OUTBOUND_RETURN', LastOperator = @Operator,
    ModifyTime = @Now
WHERE HUId = @HUId AND LocationId = @FromLocationId;",
                            new
                            {
                                ToLocationId = returned.LocationId,
                                Now = move.MoveTime,
                                MoveLogId = move.Id,
                                MoveNo = moveNo,
                                request.Operator,
                                HUId = detail.BoxHuId,
                                FromLocationId = detail.CurrentLocationId
                            },
                            tran);
                        if (locationRows != 1)
                            throw new InvalidOperationException("异常退回事务内更新CurrentLocation失败。");

                        HuOperationLogWriter.Insert(
                            conn, tran, operationNo, "OUTBOUND_RETURN", detail.BoxHuId,
                            null, detail.SAPDeliveryNo, request.RequestId, request.Operator,
                            move.MoveTime, null, request.Remark);

                        bool quality = request.ReasonCode == "QUALITY_FAILED";
                        long exceptionId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_OutboundPickException
(
    ExceptionNo, OutboundOrderId, PickBatchId, PickDetailId, HUId,
    ExceptionType, ReasonCode, Reason, ExceptionStatus, RequiresHold,
    ReturnedLocationId, RaisedTime, RaisedUser,
    ReturnConfirmedTime, ReturnConfirmedUser, RequestId, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @ExceptionNo, @OutboundOrderId, @PickBatchId, @PickDetailId, @HUId,
    @ExceptionType, @ReasonCode, @Reason, 'RETURN_CONFIRMED', @RequiresHold,
    @ReturnedLocationId, @Now, @Operator,
    @Now, @Operator, @RequestId, @Remark
);",
                            new
                            {
                                ExceptionNo = exceptionNo,
                                detail.OutboundOrderId,
                                detail.PickBatchId,
                                PickDetailId = pickDetailId,
                                HUId = detail.BoxHuId,
                                request.ExceptionType,
                                request.ReasonCode,
                                request.Reason,
                                RequiresHold = quality,
                                ReturnedLocationId = returned.LocationId,
                                Now = move.MoveTime,
                                request.Operator,
                                request.RequestId,
                                request.Remark
                            },
                            tran);

                        long? holdLogId = null;
                        if (quality)
                        {
                            string holdNo = "HD" + now.ToString("yyyyMMddHHmmss") + token;
                            holdLogId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_HU_HoldLog
(
    HoldNo, HUId, HoldType, HoldReasonCode, HoldReason,
    SourceBizType, SourceBizId, SourceBizNo,
    IsActive, HoldTime, HoldUser
)
OUTPUT INSERTED.Id
VALUES
(
    @HoldNo, @HUId, 'QUALITY', 'QUALITY_FAILED', @Reason,
    'OUTBOUND_EXCEPTION', @ExceptionId, @ExceptionNo,
    1, @Now, @Operator
);

UPDATE dbo.WMS_HU
SET IsHold = 1, HoldReason = @Reason, ModifyTime = @Now
WHERE Id = @HUId;
",
                                new
                                {
                                    HoldNo = holdNo,
                                    HUId = detail.BoxHuId,
                                    request.Reason,
                                    ExceptionId = exceptionId,
                                    ExceptionNo = exceptionNo,
                                    Now = move.MoveTime,
                                    request.Operator
                                },
                                tran);
                        }

                        conn.Execute(@"
UPDATE dbo.WMS_OutboundPickException
SET HoldLogId = @HoldLogId
WHERE Id = @ExceptionId;

UPDATE dbo.WMS_OutboundPickBatchDetail
SET DetailStatus = @DetailStatus, EffectiveQty = 0, ReturnedTime = @Now
WHERE Id = @PickDetailId;

UPDATE l
SET l.EffectivePickedQty = l.EffectivePickedQty - x.Qty,
    l.LineStatus = 'EXCEPTION', l.ModifyTime = @Now
FROM dbo.WMS_OutboundOrderLine l
INNER JOIN
(
    SELECT OutboundOrderLineId, SUM(AllocatedQty) AS Qty
    FROM dbo.WMS_OutboundPickDetailItem
    WHERE PickDetailId = @PickDetailId
    GROUP BY OutboundOrderLineId
) x ON x.OutboundOrderLineId = l.Id;

UPDATE dbo.WMS_OutboundPickBatch
SET EffectivePickedQty = EffectivePickedQty - @ReturnedQty,
    PickedQty = EffectivePickedQty - @ReturnedQty,
    DifferenceQty = EffectivePickedQty - @ReturnedQty - RequiredQty,
    BatchStatus = 'EXCEPTION', CompleteTime = NULL
WHERE Id = @PickBatchId;

UPDATE dbo.WMS_OutboundAllocation
SET AllocationStatus = 'PICKING', CompleteTime = NULL, ModifyTime = @Now
WHERE Id = @AllocationId;

UPDATE dbo.WMS_OutboundOrder
SET OrderStatus = 'EXCEPTION', PickedTime = NULL,
    ModifyUser = @Operator, ModifyTime = @Now
WHERE Id = @OutboundOrderId;",
                            new
                            {
                                HoldLogId = holdLogId,
                                ExceptionId = exceptionId,
                                DetailStatus = quality ? "QUALITY_HOLD" : "RETURNED",
                                Now = move.MoveTime,
                                PickDetailId = pickDetailId,
                                ReturnedQty = detail.EffectiveQty,
                                detail.PickBatchId,
                                detail.AllocationId,
                                detail.OutboundOrderId,
                                request.Operator
                            },
                            tran);

                        tran.Commit();
                        return OutboundActionPersistenceResult.Success(new OutboundActionResultDto
                        {
                            BusinessNo = exceptionNo,
                            Status = "RETURN_CONFIRMED"
                        });
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

        public OutboundActionPersistenceResult ReturnToStock(
            string huCode,
            ReturnToStockRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        OutboundActionResultDto duplicate = conn.QueryFirstOrDefault<OutboundActionResultDto>(@"
SELECT TOP 1 hu.HUCode AS BusinessNo, z.ZoneCode AS Status
FROM dbo.WMS_HU_MoveLog ml
INNER JOIN dbo.WMS_HU hu ON hu.Id = ml.HUId
INNER JOIN dbo.WMS_Location loc ON loc.Id = ml.ToLocationId
INNER JOIN dbo.WMS_Zone z ON z.Id = loc.ZoneId
WHERE ml.RequestId = @RequestId
  AND ml.MoveType = 'OUTBOUND_RETURN_TO_STOCK';",
                            new { request.RequestId },
                            tran);
                        if (duplicate != null)
                        {
                            if (!string.Equals(duplicate.BusinessNo, huCode, StringComparison.Ordinal))
                            {
                                tran.Rollback();
                                return OutboundActionPersistenceResult.Fail("RequestId已经用于返库其他HU。");
                            }
                            duplicate.IsDuplicate = true;
                            tran.Commit();
                            return OutboundActionPersistenceResult.Success(duplicate);
                        }

                        if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        ReturnToStockRow box = conn.QueryFirstOrDefault<ReturnToStockRow>(@"
SELECT TOP 1
    hu.Id AS HuId, hu.HUCode, hu.HUType, hu.PlantCode,
    cl.LocationId AS CurrentLocationId, currentLoc.LocationCode AS CurrentLocationCode,
    currentLoc.WarehouseId,
    original.FromLocationId AS OriginalLocationId
FROM dbo.WMS_HU hu WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON cl.HUId = hu.Id
INNER JOIN dbo.WMS_Location currentLoc ON currentLoc.Id = cl.LocationId
OUTER APPLY
(
    SELECT TOP 1 d.FromLocationId
    FROM dbo.WMS_OutboundPickBatchDetail d
    WHERE d.BoxHUId = hu.Id
      AND d.DetailStatus IN ('RETURNED', 'QUALITY_HOLD')
    ORDER BY d.ReturnedTime DESC, d.Id DESC
) original
WHERE hu.HUCode = @HUCode;",
                            new { HUCode = huCode },
                            tran);
                        if (box == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("HU不存在或没有当前位置。");
                        }
                        if (box.HuType != "OUTER_BOX")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("ReturnToStock只支持OUTER_BOX。");
                        }
                        if (box.CurrentLocationCode != "RETURNED")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("只有位于RETURNED的箱允许ReturnToStock。");
                        }
                        if (request.ToLocationCode == null && !box.OriginalLocationId.HasValue)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("没有可用原货位，必须扫描新的STORAGE货位。");
                        }

                        const string targetSql = @"
SELECT TOP 1
    loc.Id AS LocationId, loc.LocationCode, loc.WarehouseId,
    loc.MasterStatus, loc.IsVirtual, loc.PlantPolicy, loc.DedicatedPlantCode,
    z.ZoneCode
FROM dbo.WMS_Location loc WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_Zone z ON z.Id = loc.ZoneId
WHERE loc.WarehouseId = @WarehouseId
  AND
  (
      (@LocationCode IS NOT NULL AND loc.LocationCode = @LocationCode)
      OR (@LocationCode IS NULL AND loc.Id = @OriginalLocationId)
  );";
                        ReturnTargetRow target = conn.QueryFirstOrDefault<ReturnTargetRow>(
                            targetSql,
                            new
                            {
                                box.WarehouseId,
                                LocationCode = request.ToLocationCode,
                                box.OriginalLocationId
                            },
                            tran);
                        if (target == null || target.MasterStatus != "ACTIVE"
                            || target.IsVirtual || target.ZoneCode != "STORAGE")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("目标货位不存在或不是有效STORAGE实体货位。");
                        }

                        bool warehouseAllowsPlant = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.WMS_WarehousePlant
    WHERE WarehouseId = @WarehouseId
      AND PlantCode = @PlantCode
      AND IsActive = 1
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { box.WarehouseId, box.PlantCode },
                            tran);
                        if (!warehouseAllowsPlant)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("当前仓库不允许存储该厂别库存。");
                        }
                        if (target.PlantPolicy == "DEDICATED"
                            && !string.Equals(target.DedicatedPlantCode, box.PlantCode, StringComparison.Ordinal))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("目标货位为其他厂别专用货位。");
                        }
                        if (target.PlantPolicy == "SAME_PLANT")
                        {
                            bool hasOtherPlant = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1
    FROM dbo.WMS_HU_CurrentLocation cl
    INNER JOIN dbo.WMS_HU hu ON hu.Id = cl.HUId
    WHERE cl.LocationId = @LocationId
      AND hu.PlantCode <> @PlantCode
      AND hu.LifecycleStatus = 'ACTIVE'
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                                new { target.LocationId, box.PlantCode },
                                tran);
                            if (hasOtherPlant)
                            {
                                tran.Rollback();
                                return OutboundActionPersistenceResult.Fail("目标货位当前存在其他厂别库存。");
                            }
                        }

                        bool activeReservation = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.WMS_HU_Reservation
    WHERE HUId = @HUId AND ReservationStatus = 'ACTIVE'
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { box.HuId },
                            tran);
                        if (activeReservation)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("HU仍有Active Reservation，不能返库。");
                        }

                        DateTime now = DateTime.Now;
                        string token = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                        string moveNo = "PM" + now.ToString("yyyyMMddHHmmss") + token;
                        string operationNo = "PO" + now.ToString("yyyyMMddHHmmss") + token;
                        HuMoveLogWriter.HuMoveLogWriteResult move = HuMoveLogWriter.InsertWithTime(
                            conn, tran, moveNo, box.HuId, box.CurrentLocationId,
                            target.LocationId, "OUTBOUND_RETURN_TO_STOCK",
                            request.ToLocationCode, null, null, request.RequestId,
                            null, request.Operator, request.Remark);
                        int rows = conn.Execute(@"
UPDATE dbo.WMS_HU_CurrentLocation
SET LocationId = @ToLocationId, LocationSince = @Now,
    LastMoveLogId = @MoveLogId, LastMoveNo = @MoveNo,
    LastMoveType = 'OUTBOUND_RETURN_TO_STOCK', LastOperator = @Operator,
    ModifyTime = @Now
WHERE HUId = @HUId AND LocationId = @FromLocationId;",
                            new
                            {
                                ToLocationId = target.LocationId,
                                Now = move.MoveTime,
                                MoveLogId = move.Id,
                                MoveNo = moveNo,
                                request.Operator,
                                box.HuId,
                                FromLocationId = box.CurrentLocationId
                            },
                            tran);
                        if (rows != 1)
                            throw new InvalidOperationException("ReturnToStock事务内更新CurrentLocation失败。");
                        HuOperationLogWriter.Insert(
                            conn, tran, operationNo, "OUTBOUND_RETURN_TO_STOCK", box.HuId,
                            null, null, request.RequestId, request.Operator,
                            move.MoveTime, null, request.Remark);

                        tran.Commit();
                        return OutboundActionPersistenceResult.Success(new OutboundActionResultDto
                        {
                            BusinessNo = huCode,
                            Status = "STORAGE"
                        });
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

        public StartPickingPersistenceResult ReserveReplacement(
            string pickBatchNo,
            ReplacementRequest request)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        ReplacementContextRow context = conn.QueryFirstOrDefault<ReplacementContextRow>(@"
SELECT TOP 1
    ex.Id AS ExceptionId, ex.ExceptionNo, ex.ExceptionStatus,
    ex.PickDetailId AS OriginalPickDetailId, ex.HUId AS OriginalHuId,
    ex.ReplacementRequestId,
    d.BoxQty AS RequiredReplacementQty,
    b.Id AS PickBatchId, b.PickBatchNo, b.MaterialCode, b.Uom,
    b.AllocationId, b.OutboundOrderId,
    a.RequestId AS AllocationRequestId,
    o.SAPDeliveryNo, o.OrderStatus
FROM dbo.WMS_OutboundPickException ex WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_OutboundPickBatchDetail d ON d.Id = ex.PickDetailId
INNER JOIN dbo.WMS_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON b.Id = ex.PickBatchId
INNER JOIN dbo.WMS_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON a.Id = b.AllocationId
INNER JOIN dbo.WMS_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON o.Id = b.OutboundOrderId
WHERE ex.ExceptionNo = @ExceptionNo;",
                            new { request.ExceptionNo },
                            tran);
                        if (context == null)
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("异常记录不存在。");
                        }
                        if (context.PickBatchNo != pickBatchNo)
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("异常记录不属于指定PickBatch。");
                        }
                        if (context.ReplacementRequestId != null)
                        {
                            if (context.ReplacementRequestId != request.RequestId)
                            {
                                tran.Rollback();
                                return StartPickingPersistenceResult.Fail("该异常已经建立Replacement Reservation。");
                            }
                            StartPickingResultDto existing = ReadStartPickingResult(
                                conn, tran, context.AllocationRequestId);
                            existing.IsDuplicate = true;
                            tran.Commit();
                            return StartPickingPersistenceResult.Success(existing);
                        }
                        if (context.ExceptionStatus != "RETURN_CONFIRMED")
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("只有RETURN_CONFIRMED异常允许建立Replacement。");
                        }
                        if (context.OrderStatus != "EXCEPTION")
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("订单当前不处于EXCEPTION状态。");
                        }
                        if (IsRequestIdUsedByOtherOperation(conn, tran, request.RequestId))
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail("RequestId已经被其他WMS业务使用。");
                        }

                        OutboundOrderDto order = ReadOrderForUpdate(conn, tran, context.SAPDeliveryNo);
                        List<OutboundAllocationCandidateDto> candidates = GetAllocationCandidatesInternal(
                            conn, tran, order, context.MaterialCode, context.Uom);
                        candidates = candidates.Where(x => x.BoxHuId != context.OriginalHuId).ToList();
                        OutboundAllocationPlanDto plan = WMS_HU_Model.Rules.OutboundAllocationPlanner.Build(
                            candidates,
                            context.RequiredReplacementQty);
                        if (plan.ResultType != "EXACT_WHOLE_BOX")
                        {
                            tran.Rollback();
                            return StartPickingPersistenceResult.Fail(
                                "Replacement无法整箱精确分配，结果：" + plan.ResultType + "。");
                        }

                        DateTime now = DateTime.Now;
                        DateTime expireTime = now.AddHours(3);
                        string token = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
                        long? firstReservationId = null;
                        for (int i = 0; i < plan.SelectedBoxes.Count; i++)
                        {
                            OutboundAllocationCandidateDto box = plan.SelectedBoxes[i];
                            string reservationNo = "RR" + now.ToString("yyyyMMddHHmmss") + token + (i + 1).ToString("D3");
                            long reservationId = InsertReservation(
                                conn, tran, reservationNo, context.AllocationId,
                                context.PickBatchId, context.OutboundOrderId,
                                box, context.Uom, expireTime, request.Operator);
                            if (!firstReservationId.HasValue)
                                firstReservationId = reservationId;

                            conn.Execute(@"
INSERT INTO dbo.WMS_OutboundExceptionReplacement
(
    ExceptionId, SequenceNo, ReservationId
)
VALUES
(
    @ExceptionId, @SequenceNo, @ReservationId
);",
                                new
                                {
                                    context.ExceptionId,
                                    SequenceNo = i + 1,
                                    ReservationId = reservationId
                                },
                                tran);
                        }

                        conn.Execute(@"
UPDATE dbo.WMS_OutboundPickException
SET ExceptionStatus = 'REPLACEMENT_RESERVED',
    ReplacementReservationId = @FirstReservationId,
    ReplacementRequestId = @RequestId
WHERE Id = @ExceptionId;

UPDATE dbo.WMS_OutboundPickBatch
SET BatchStatus = 'EXCEPTION', CompleteTime = NULL
WHERE Id = @PickBatchId;

UPDATE dbo.WMS_OutboundAllocation
SET AllocationStatus = 'PICKING', CompleteTime = NULL, ModifyTime = @Now
WHERE Id = @AllocationId;",
                            new
                            {
                                FirstReservationId = firstReservationId,
                                request.RequestId,
                                context.ExceptionId,
                                context.PickBatchId,
                                context.AllocationId,
                                Now = now
                            },
                            tran);

                        StartPickingResultDto result = ReadStartPickingResult(
                            conn, tran, context.AllocationRequestId);
                        tran.Commit();
                        return StartPickingPersistenceResult.Success(result);
                    }
                    catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                    {
                        if (tran.Connection != null)
                            tran.Rollback();
                        return StartPickingPersistenceResult.Fail("Replacement库存已被并发分配，请重试。");
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

        private static ScanBatchRow ReadScanBatch(
            SqlConnection conn,
            SqlTransaction tran,
            string pickBatchNo)
        {
            const string sql = @"
SELECT TOP 1
    b.Id AS PickBatchId, b.PickBatchNo, b.WarehouseId,
    b.MaterialCode, b.Uom, b.PlannedQty, b.EffectivePickedQty,
    b.HistoricalPickedQty, b.BatchStatus,
    b.AllocationId, a.AllocationStatus,
    b.OutboundOrderId, o.SAPDeliveryNo, o.OrderStatus
FROM dbo.WMS_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON a.Id = b.AllocationId
INNER JOIN dbo.WMS_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON o.Id = b.OutboundOrderId
WHERE b.PickBatchNo = @PickBatchNo;";
            return conn.QueryFirstOrDefault<ScanBatchRow>(sql, new { PickBatchNo = pickBatchNo }, tran);
        }

        private static List<ScanItemRow> ReadScanItems(
            SqlConnection conn,
            SqlTransaction tran,
            long boxHuId,
            string materialCode,
            string uom)
        {
            const string sql = @"
SELECT
    innerHu.Id AS InnerHuId,
    item.Id AS HuItemId,
    item.MaterialCode,
    item.MaterialRevision,
    item.DateCode,
    item.WeekSortKey,
    item.BatchNo,
    item.SAPBatchNo,
    item.Qty,
    item.Uom
FROM dbo.WMS_HU_Relation rel WITH (HOLDLOCK)
INNER JOIN dbo.WMS_HU innerHu WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON innerHu.Id = rel.ChildHUId
INNER JOIN dbo.WMS_HU_Item item WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON item.HUId = innerHu.Id
   AND item.IsActive = 1
WHERE rel.ParentHUId = @BoxHuId
  AND rel.IsActive = 1
  AND innerHu.HUType = 'INNER_PACK'
  AND innerHu.LifecycleStatus = 'ACTIVE'
  AND innerHu.IsHold = 0
  AND item.StockStatus = 'AVAILABLE'
  AND item.WeekSortKey IS NOT NULL
  AND item.WeekResolvedTime IS NOT NULL
  AND item.MaterialCode = @MaterialCode
  AND item.Uom = @Uom
ORDER BY item.WeekSortKey, innerHu.Id, item.Id;";
            return conn.Query<ScanItemRow>(sql, new
            {
                BoxHuId = boxHuId,
                MaterialCode = materialCode,
                Uom = uom
            }, tran).AsList();
        }

        private static List<ScanLineRow> ReadScanLines(
            SqlConnection conn,
            SqlTransaction tran,
            long orderId,
            string materialCode,
            string uom)
        {
            const string sql = @"
SELECT Id AS LineId, SAPItemNo, EffectiveRequiredQty, EffectivePickedQty
FROM dbo.WMS_OutboundOrderLine WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE OutboundOrderId = @OutboundOrderId
  AND MaterialCode = @MaterialCode
  AND Uom = @Uom
ORDER BY SAPItemNo, Id;";
            return conn.Query<ScanLineRow>(sql, new
            {
                OutboundOrderId = orderId,
                MaterialCode = materialCode,
                Uom = uom
            }, tran).AsList();
        }

        private static List<ScanItemAllocation> AllocateItemsToLines(
            List<ScanItemRow> items,
            List<ScanLineRow> lines)
        {
            List<ScanItemAllocation> result = new List<ScanItemAllocation>();
            Dictionary<long, decimal> lineRemaining = lines.ToDictionary(
                x => x.LineId,
                x => x.EffectiveRequiredQty - x.EffectivePickedQty);

            foreach (ScanItemRow item in items)
            {
                decimal itemRemaining = item.Qty;
                foreach (ScanLineRow line in lines)
                {
                    decimal remaining = lineRemaining[line.LineId];
                    if (remaining <= 0 || itemRemaining <= 0)
                        continue;

                    decimal allocated = Math.Min(remaining, itemRemaining);
                    result.Add(new ScanItemAllocation
                    {
                        LineId = line.LineId,
                        Item = item,
                        AllocatedQty = allocated
                    });
                    lineRemaining[line.LineId] = remaining - allocated;
                    itemRemaining -= allocated;
                }

                if (itemRemaining != 0)
                    return null;
            }
            return result;
        }

        private static void InsertPickDetailItems(
            SqlConnection conn,
            SqlTransaction tran,
            long detailId,
            List<ScanItemAllocation> allocations)
        {
            const string sql = @"
INSERT INTO dbo.WMS_OutboundPickDetailItem
(
    PickDetailId, OutboundOrderLineId, InnerHUId, HUItemId,
    MaterialCode, MaterialRevision, DateCode, WeekSortKey,
    BatchNo, SAPBatchNo, AllocatedQty, Uom
)
VALUES
(
    @PickDetailId, @OutboundOrderLineId, @InnerHUId, @HUItemId,
    @MaterialCode, @MaterialRevision, @DateCode, @WeekSortKey,
    @BatchNo, @SAPBatchNo, @AllocatedQty, @Uom
);";
            foreach (ScanItemAllocation allocation in allocations)
            {
                conn.Execute(sql, new
                {
                    PickDetailId = detailId,
                    OutboundOrderLineId = allocation.LineId,
                    allocation.Item.InnerHuId,
                    allocation.Item.HuItemId,
                    allocation.Item.MaterialCode,
                    allocation.Item.MaterialRevision,
                    allocation.Item.DateCode,
                    allocation.Item.WeekSortKey,
                    allocation.Item.BatchNo,
                    allocation.Item.SAPBatchNo,
                    allocation.AllocatedQty,
                    allocation.Item.Uom
                }, tran);
            }
        }

        private static OutboundScanResultDto ReadScanResult(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId)
        {
            const string sql = @"
SELECT TOP 1
    d.Id AS PickDetailId,
    b.PickBatchNo,
    hu.HUCode AS BoxHuCode,
    fromLoc.LocationCode AS FromLocationCode,
    toLoc.LocationCode AS ToLocationCode,
    d.BoxQty,
    d.Uom,
    b.BatchStatus,
    o.OrderStatus,
    d.MoveLogId,
    d.OperationNo
FROM dbo.WMS_HU_MoveLog ml
INNER JOIN dbo.WMS_OutboundPickBatchDetail d ON d.MoveLogId = ml.Id
INNER JOIN dbo.WMS_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_OutboundOrder o ON o.Id = b.OutboundOrderId
INNER JOIN dbo.WMS_HU hu ON hu.Id = d.BoxHUId
INNER JOIN dbo.WMS_Location fromLoc ON fromLoc.Id = d.FromLocationId
INNER JOIN dbo.WMS_Location toLoc ON toLoc.Id = d.ToLocationId
WHERE ml.RequestId = @RequestId
  AND ml.MoveType = 'OUTBOUND_PICK';";
            return conn.QueryFirstOrDefault<OutboundScanResultDto>(sql, new { RequestId = requestId }, tran);
        }

        private static OutboundOrderDto ReadOrderForUpdate(
            SqlConnection conn,
            SqlTransaction tran,
            string sapDeliveryNo)
        {
            const string orderSql = @"
SELECT TOP 1
    o.Id,
    o.WarehouseId,
    o.OutboundOrderNo,
    o.SAPDeliveryNo,
    w.WarehouseCode,
    o.PlantCode,
    o.SAPStorageLocation,
    o.CustomerCode,
    o.SoldToCode,
    o.PlannedShipDate,
    o.OrderStatus,
    o.ReceivedTime,
    o.StartPickTime,
    o.PickedTime,
    o.ShippedTime
FROM dbo.WMS_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_Warehouse w ON w.Id = o.WarehouseId
WHERE o.SAPDeliveryNo = @SAPDeliveryNo;";

            OutboundOrderDto order = conn.QueryFirstOrDefault<OutboundOrderDto>(
                orderSql,
                new { SAPDeliveryNo = sapDeliveryNo },
                tran);
            if (order == null)
                return null;

            const string lineSql = @"
SELECT
    Id, OutboundOrderId, SAPItemNo, MaterialCode, MaterialRevision,
    RequiredQty, SpareQty, SAPGiftQty, GiftQty, EffectiveRequiredQty,
    Uom, PlantCode, SAPStorageLocation, AllocatedQty,
    EffectivePickedQty, ShippedQty, LineStatus
FROM dbo.WMS_OutboundOrderLine WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE OutboundOrderId = @OutboundOrderId
ORDER BY SAPItemNo, Id;";
            order.Lines = conn.Query<OutboundOrderLineDto>(
                lineSql,
                new { OutboundOrderId = order.Id },
                tran).AsList();
            return order;
        }

        private static bool IsRequestIdUsedByOtherOperation(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId)
        {
            const string sql = @"
SELECT CASE WHEN
    EXISTS (SELECT 1 FROM dbo.WMS_HU_MoveLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_HU_OperationLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_OutboundPickBatch WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_OutboundOrderReceiveLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_OutboundPickException WHERE RequestId = @RequestId OR ReplacementRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_HU_HoldLog WHERE HoldRequestId = @RequestId OR ReleaseRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_OutboundAllocation WHERE ReleaseRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_OutboundOrder WHERE VoidRequestId = @RequestId OR ShipRequestId = @RequestId)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;";
            return conn.ExecuteScalar<bool>(sql, new { RequestId = requestId }, tran);
        }

        private static long InsertAllocation(
            SqlConnection conn,
            SqlTransaction tran,
            string allocationNo,
            long orderId,
            StartPickingCommand command,
            DateTime allocateTime,
            DateTime expireTime)
        {
            const string sql = @"
INSERT INTO dbo.WMS_OutboundAllocation
(
    AllocationNo, OutboundOrderId, RequestId, AllocationStatus,
    AllocateTime, ExpireTime, Operator, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @AllocationNo, @OutboundOrderId, @RequestId, 'ACTIVE',
    @AllocateTime, @ExpireTime, @Operator, @Remark
);";
            return conn.ExecuteScalar<long>(sql, new
            {
                AllocationNo = allocationNo,
                OutboundOrderId = orderId,
                command.RequestId,
                AllocateTime = allocateTime,
                ExpireTime = expireTime,
                command.Operator,
                command.Remark
            }, tran);
        }

        private static long InsertPickBatch(
            SqlConnection conn,
            SqlTransaction tran,
            string pickBatchNo,
            string batchRequestId,
            long allocationId,
            OutboundOrderDto order,
            PlannedGroup group,
            StartPickingCommand command,
            DateTime expireTime)
        {
            const string sql = @"
INSERT INTO dbo.WMS_OutboundPickBatch
(
    PickBatchNo, RequestId, WarehouseId, PlantCode, MaterialCode,
    MaterialRevision, FilterBatchNo, ProductionOrderNo,
    RequiredQty, PickedQty, DifferenceQty, Uom,
    PickMode, ResultType, BusinessNo, Operator, PickTime, Remark,
    AllocationId, OutboundOrderId, BatchStatus, PlannedQty,
    EffectivePickedQty, HistoricalPickedQty, ReservationExpireTime,
    GroupKey
)
OUTPUT INSERTED.Id
VALUES
(
    @PickBatchNo, @RequestId, @WarehouseId, @PlantCode, @MaterialCode,
    NULL, NULL, NULL,
    @RequiredQty, 0, -@RequiredQty, @Uom,
    'FIFO_WHOLE_BOX', 'EXACT_WHOLE_BOX', @BusinessNo, @Operator, NULL, @Remark,
    @AllocationId, @OutboundOrderId, 'ALLOCATED', @RequiredQty,
    0, 0, @ReservationExpireTime,
    @GroupKey
);";
            return conn.ExecuteScalar<long>(sql, new
            {
                PickBatchNo = pickBatchNo,
                RequestId = batchRequestId,
                order.WarehouseId,
                order.PlantCode,
                group.MaterialCode,
                group.RequiredQty,
                group.Uom,
                BusinessNo = order.SAPDeliveryNo,
                command.Operator,
                command.Remark,
                AllocationId = allocationId,
                OutboundOrderId = order.Id,
                ReservationExpireTime = expireTime,
                group.GroupKey
            }, tran);
        }

        private static long InsertReservation(
            SqlConnection conn,
            SqlTransaction tran,
            string reservationNo,
            long allocationId,
            long pickBatchId,
            long orderId,
            OutboundAllocationCandidateDto box,
            string uom,
            DateTime expireTime,
            string operatorCode)
        {
            const string sql = @"
INSERT INTO dbo.WMS_HU_Reservation
(
    ReservationNo, AllocationId, OutboundOrderId, PickBatchId,
    HUId, SourceLocationId, ReservationScope, ReservedQty, Uom,
    MinWeekSortKey, MaxWeekSortKey, DistinctWeekCount, WeekSummary,
    ReservationStatus, ExpireTime, Operator
)
OUTPUT INSERTED.Id
VALUES
(
    @ReservationNo, @AllocationId, @OutboundOrderId, @PickBatchId,
    @HUId, @SourceLocationId, 'OUTER_BOX', @ReservedQty, @Uom,
    @MinWeekSortKey, @MaxWeekSortKey, @DistinctWeekCount, @WeekSummary,
    'ACTIVE', @ExpireTime, @Operator
);";
            return conn.ExecuteScalar<long>(sql, new
            {
                ReservationNo = reservationNo,
                AllocationId = allocationId,
                OutboundOrderId = orderId,
                PickBatchId = pickBatchId,
                HUId = box.BoxHuId,
                SourceLocationId = box.LocationId,
                ReservedQty = box.Qty,
                Uom = uom,
                box.MinWeekSortKey,
                box.MaxWeekSortKey,
                box.DistinctWeekCount,
                box.WeekSummary,
                ExpireTime = expireTime,
                Operator = operatorCode
            }, tran);
        }

        private static void UpdateAllocatedLines(
            SqlConnection conn,
            SqlTransaction tran,
            long orderId,
            string materialCode,
            string uom)
        {
            const string sql = @"
UPDATE dbo.WMS_OutboundOrderLine
SET AllocatedQty = EffectiveRequiredQty,
    LineStatus = 'ALLOCATED',
    ModifyTime = SYSDATETIME()
WHERE OutboundOrderId = @OutboundOrderId
  AND MaterialCode = @MaterialCode
  AND Uom = @Uom;";
            conn.Execute(sql, new
            {
                OutboundOrderId = orderId,
                MaterialCode = materialCode,
                Uom = uom
            }, tran);
        }

        private static StartPickingResultDto ReadStartPickingResult(
            SqlConnection conn,
            SqlTransaction tran,
            string requestId)
        {
            const string headerSql = @"
SELECT TOP 1
    o.SAPDeliveryNo,
    a.Id AS AllocationId,
    a.AllocationNo,
    a.AllocationStatus,
    a.ExpireTime
FROM dbo.WMS_OutboundAllocation a
INNER JOIN dbo.WMS_OutboundOrder o ON o.Id = a.OutboundOrderId
WHERE a.RequestId = @RequestId;";
            StartPickingHeaderRow header = conn.QueryFirstOrDefault<StartPickingHeaderRow>(
                headerSql,
                new { RequestId = requestId },
                tran);
            if (header == null)
                return null;

            const string batchSql = @"
SELECT Id AS PickBatchId, PickBatchNo, GroupKey, MaterialCode, Uom, PlannedQty
FROM dbo.WMS_OutboundPickBatch
WHERE AllocationId = @AllocationId
ORDER BY Id;";
            List<StartPickingBatchRow> batchRows = conn.Query<StartPickingBatchRow>(
                batchSql,
                new { header.AllocationId },
                tran).AsList();

            const string reservationSql = @"
SELECT
    r.PickBatchId,
    r.ReservationNo,
    r.HUId AS BoxHuId,
    hu.HUCode AS BoxHuCode,
    loc.LocationCode AS SourceLocationCode,
    r.ReservedQty,
    r.MinWeekSortKey,
    r.MaxWeekSortKey,
    r.WeekSummary
FROM dbo.WMS_HU_Reservation r
INNER JOIN dbo.WMS_HU hu ON hu.Id = r.HUId
INNER JOIN dbo.WMS_Location loc ON loc.Id = r.SourceLocationId
WHERE r.AllocationId = @AllocationId
ORDER BY r.PickBatchId, r.MinWeekSortKey, r.HUId;";
            List<StartPickingReservationRow> reservationRows = conn.Query<StartPickingReservationRow>(
                reservationSql,
                new { header.AllocationId },
                tran).AsList();

            StartPickingResultDto result = new StartPickingResultDto
            {
                SAPDeliveryNo = header.SAPDeliveryNo,
                AllocationNo = header.AllocationNo,
                AllocationStatus = header.AllocationStatus,
                ExpireTime = header.ExpireTime
            };
            foreach (StartPickingBatchRow row in batchRows)
            {
                StartPickingBatchDto batch = new StartPickingBatchDto
                {
                    PickBatchNo = row.PickBatchNo,
                    GroupKey = row.GroupKey,
                    MaterialCode = row.MaterialCode,
                    Uom = row.Uom,
                    PlannedQty = row.PlannedQty
                };
                batch.Reservations = reservationRows
                    .Where(x => x.PickBatchId == row.PickBatchId)
                    .Select(x => new StartPickingReservationDto
                    {
                        ReservationNo = x.ReservationNo,
                        BoxHuId = x.BoxHuId,
                        BoxHuCode = x.BoxHuCode,
                        SourceLocationCode = x.SourceLocationCode,
                        ReservedQty = x.ReservedQty,
                        MinWeekSortKey = x.MinWeekSortKey,
                        MaxWeekSortKey = x.MaxWeekSortKey,
                        WeekSummary = x.WeekSummary
                    }).ToList();
                result.Batches.Add(batch);
            }

            return result;
        }

        private static string BuildGroupKey(string materialCode, string uom)
        {
            string material = string.IsNullOrWhiteSpace(materialCode) ? string.Empty : materialCode.Trim().ToUpperInvariant();
            string unit = string.IsNullOrWhiteSpace(uom) ? string.Empty : uom.Trim().ToUpperInvariant();
            return material + "|" + unit;
        }

        private static DynamicParameters CreateCandidateParameters(
            OutboundOrderDto order,
            string materialCode,
            string uom)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@WarehouseCode", order.WarehouseCode, DbType.AnsiString, size: 30);
            parameters.Add("@PlantCode", order.PlantCode, DbType.AnsiString, size: 20);
            parameters.Add("@SAPStorageLocation", order.SAPStorageLocation, DbType.AnsiString, size: 20);
            parameters.Add("@MaterialCode", materialCode, DbType.AnsiString, size: 50);
            parameters.Add("@Uom", uom, DbType.AnsiString, size: 20);
            return parameters;
        }

        private class InnerCandidateRow
        {
            public long BoxHuId { get; set; }
            public long InnerHuId { get; set; }
            public string InnerHuCode { get; set; }
            public decimal Qty { get; set; }
            public int MinWeekSortKey { get; set; }
        }

        private class PlannedGroup
        {
            public string GroupKey { get; set; }
            public string MaterialCode { get; set; }
            public string Uom { get; set; }
            public decimal RequiredQty { get; set; }
            public OutboundAllocationPlanDto Plan { get; set; }
        }

        private class StartPickingHeaderRow
        {
            public long AllocationId { get; set; }
            public string SAPDeliveryNo { get; set; }
            public string AllocationNo { get; set; }
            public string AllocationStatus { get; set; }
            public DateTime ExpireTime { get; set; }
        }

        private class StartPickingBatchRow
        {
            public long PickBatchId { get; set; }
            public string PickBatchNo { get; set; }
            public string GroupKey { get; set; }
            public string MaterialCode { get; set; }
            public string Uom { get; set; }
            public decimal PlannedQty { get; set; }
        }

        private class StartPickingReservationRow
        {
            public long PickBatchId { get; set; }
            public string ReservationNo { get; set; }
            public long BoxHuId { get; set; }
            public string BoxHuCode { get; set; }
            public string SourceLocationCode { get; set; }
            public decimal ReservedQty { get; set; }
            public int MinWeekSortKey { get; set; }
            public int MaxWeekSortKey { get; set; }
            public string WeekSummary { get; set; }
        }

        private class AllocationActionRow
        {
            public long Id { get; set; }
            public long OutboundOrderId { get; set; }
            public string AllocationStatus { get; set; }
            public DateTime? FirstPickTime { get; set; }
        }

        private class OrderActionRow
        {
            public long Id { get; set; }
            public string OrderStatus { get; set; }
        }

        private class ScanBatchRow
        {
            public long PickBatchId { get; set; }
            public string PickBatchNo { get; set; }
            public long WarehouseId { get; set; }
            public string MaterialCode { get; set; }
            public string Uom { get; set; }
            public decimal PlannedQty { get; set; }
            public decimal EffectivePickedQty { get; set; }
            public decimal HistoricalPickedQty { get; set; }
            public string BatchStatus { get; set; }
            public long AllocationId { get; set; }
            public string AllocationStatus { get; set; }
            public long OutboundOrderId { get; set; }
            public string SAPDeliveryNo { get; set; }
            public string OrderStatus { get; set; }
        }

        private class ScanReservationRow
        {
            public long Id { get; set; }
            public long SourceLocationId { get; set; }
            public decimal ReservedQty { get; set; }
            public string Uom { get; set; }
            public int MinWeekSortKey { get; set; }
            public int MaxWeekSortKey { get; set; }
            public int DistinctWeekCount { get; set; }
            public string WeekSummary { get; set; }
            public long? ReplacementLinkId { get; set; }
            public long? ReplacementExceptionId { get; set; }
            public long? ReplacementForDetailId { get; set; }
            public string BoxHuCode { get; set; }
            public string LifecycleStatus { get; set; }
            public bool IsHold { get; set; }
        }

        private class ScanLocationRow
        {
            public long LocationId { get; set; }
            public string LocationCode { get; set; }
        }

        private class ScanItemRow
        {
            public long InnerHuId { get; set; }
            public long HuItemId { get; set; }
            public string MaterialCode { get; set; }
            public string MaterialRevision { get; set; }
            public string DateCode { get; set; }
            public int WeekSortKey { get; set; }
            public string BatchNo { get; set; }
            public string SAPBatchNo { get; set; }
            public decimal Qty { get; set; }
            public string Uom { get; set; }
        }

        private class ScanLineRow
        {
            public long LineId { get; set; }
            public string SAPItemNo { get; set; }
            public decimal EffectiveRequiredQty { get; set; }
            public decimal EffectivePickedQty { get; set; }
        }

        private class ScanItemAllocation
        {
            public long LineId { get; set; }
            public ScanItemRow Item { get; set; }
            public decimal AllocatedQty { get; set; }
        }

        private class ShipOrderRow
        {
            public long Id { get; set; }
            public long WarehouseId { get; set; }
            public string OrderStatus { get; set; }
        }

        private class ShipDetailRow
        {
            public long PickDetailId { get; set; }
            public long BoxHuId { get; set; }
            public long FromLocationId { get; set; }
            public long CurrentLocationId { get; set; }
            public string CurrentLocationCode { get; set; }
        }

        private class ReturnDetailRow
        {
            public long Id { get; set; }
            public long PickBatchId { get; set; }
            public long BoxHuId { get; set; }
            public decimal EffectiveQty { get; set; }
            public string DetailStatus { get; set; }
            public long AllocationId { get; set; }
            public long OutboundOrderId { get; set; }
            public long WarehouseId { get; set; }
            public decimal BatchEffectiveQty { get; set; }
            public decimal PlannedQty { get; set; }
            public string SAPDeliveryNo { get; set; }
            public string OrderStatus { get; set; }
            public long CurrentLocationId { get; set; }
            public string CurrentLocationCode { get; set; }
            public string BoxHuCode { get; set; }
            public bool IsHold { get; set; }
        }

        private class ReturnToStockRow
        {
            public long HuId { get; set; }
            public string HuType { get; set; }
            public string PlantCode { get; set; }
            public long CurrentLocationId { get; set; }
            public string CurrentLocationCode { get; set; }
            public long WarehouseId { get; set; }
            public long? OriginalLocationId { get; set; }
        }

        private class ReturnTargetRow
        {
            public long LocationId { get; set; }
            public string LocationCode { get; set; }
            public long WarehouseId { get; set; }
            public string MasterStatus { get; set; }
            public bool IsVirtual { get; set; }
            public string PlantPolicy { get; set; }
            public string DedicatedPlantCode { get; set; }
            public string ZoneCode { get; set; }
        }

        private class ReplacementContextRow
        {
            public long ExceptionId { get; set; }
            public string ExceptionNo { get; set; }
            public string ExceptionStatus { get; set; }
            public long OriginalPickDetailId { get; set; }
            public long OriginalHuId { get; set; }
            public string ReplacementRequestId { get; set; }
            public decimal RequiredReplacementQty { get; set; }
            public long PickBatchId { get; set; }
            public string PickBatchNo { get; set; }
            public string MaterialCode { get; set; }
            public string Uom { get; set; }
            public long AllocationId { get; set; }
            public long OutboundOrderId { get; set; }
            public string AllocationRequestId { get; set; }
            public string SAPDeliveryNo { get; set; }
            public string OrderStatus { get; set; }
        }
    }
}
