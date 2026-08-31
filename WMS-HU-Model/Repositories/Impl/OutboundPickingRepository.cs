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
    public class OutboundPickingRepository : IOutboundPickingRepository
    {        public List<OutboundAllocationCandidateDto> GetAllocationCandidates(
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
        MIN(item.ProdNo) AS MaterialCode,
        MIN(item.Uom) AS Uom,
        SUM(item.Qty) AS Qty,
        MIN(item.WeekSortKey) AS MinWeekSortKey,
        MAX(item.WeekSortKey) AS MaxWeekSortKey,
        COUNT(DISTINCT item.WeekSortKey) AS DistinctWeekCount
    FROM dbo.WMS_FG_HU box
    INNER JOIN dbo.WMS_FG_HU_CurrentLocation cl ON cl.HUId = box.Id
    INNER JOIN dbo.WMS_FG_Location loc ON loc.Id = cl.LocationId
    INNER JOIN dbo.WMS_FG_Zone z ON z.Id = loc.ZoneId
    INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id = loc.WarehouseId
    INNER JOIN dbo.WMS_FG_WarehousePlant wp
        ON wp.WarehouseId = w.Id
       AND wp.PlantCode = box.PlantCode
       AND wp.IsActive = 1
    INNER JOIN dbo.WMS_FG_HU_Relation rel
        ON rel.ParentHUId = box.Id
       AND rel.IsActive = 1
    INNER JOIN dbo.WMS_FG_HU innerHu
        ON innerHu.Id = rel.ChildHUId
    INNER JOIN dbo.WMS_FG_HU_Item item
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
      AND loc.IsActive = 1
      AND innerHu.HUType = 'INNER_PACK'
      AND innerHu.LifecycleStatus = 'ACTIVE'
      AND innerHu.IsHold = 0
      AND item.StockStatus = 'AVAILABLE'
      AND item.WeekSortKey IS NOT NULL
      AND item.WeekResolvedTime IS NOT NULL
      AND item.ProdNo = @MaterialCode
      AND item.Uom = @Uom
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.WMS_FG_HU_Reservation r
          WHERE r.HUId = box.Id
            AND r.ReservationStatus = 'ACTIVE'
      )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.WMS_FG_HU_Relation badRel
          INNER JOIN dbo.WMS_FG_HU badInner ON badInner.Id = badRel.ChildHUId
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
                    FROM dbo.WMS_FG_HU_Item presentItem
                    WHERE presentItem.HUId = badInner.Id
                      AND presentItem.IsActive = 1
                )
                OR EXISTS
                (
                    SELECT 1
                    FROM dbo.WMS_FG_HU_Item badItem
                    WHERE badItem.HUId = badInner.Id
                      AND badItem.IsActive = 1
                      AND
                      (
                          badItem.StockStatus <> 'AVAILABLE'
                          OR badItem.WeekSortKey IS NULL
                          OR badItem.WeekResolvedTime IS NULL
                          OR badItem.ProdNo <> @MaterialCode
                          OR badItem.Uom <> @Uom
                      )
                )
            )
      )
    GROUP BY box.Id, box.HUCode, cl.LocationId, loc.LocationCode
    HAVING COUNT(DISTINCT item.ProdNo) = 1
       AND COUNT(DISTINCT item.Uom) = 1
    ORDER BY MIN(item.WeekSortKey), box.Id
), WeekValues AS
(
    SELECT DISTINCT c.BoxHuId, i.WeekSortKey
    FROM Candidate c
    INNER JOIN dbo.WMS_FG_HU_Relation r ON r.ParentHUId = c.BoxHuId AND r.IsActive = 1
    INNER JOIN dbo.WMS_FG_HU_Item i ON i.HUId = r.ChildHUId AND i.IsActive = 1
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
FROM dbo.WMS_FG_HU_Relation rel
INNER JOIN dbo.WMS_FG_HU innerHu ON innerHu.Id = rel.ChildHUId
INNER JOIN dbo.WMS_FG_HU_Item item ON item.HUId = innerHu.Id AND item.IsActive = 1
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
                            long pickBatchId = InsertPickBatch(
                                conn,
                                tran,
                                pickBatchNo,
                                allocationId,
                                group,
                                command);

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
                                    box,
                                    group.Uom,
                                    expireTime,
                                    command.Operator);
                            }

                            UpdateAllocatedLines(conn, tran, order.Id, group.MaterialCode, group.Uom);
                        }

                        const string updateOrderSql = @"
UPDATE dbo.WMS_FG_OutboundOrder
SET OrderStatus = 'ALLOCATED',
    StartPickDt = @StartPickDt,
    LastEditBy = @Operator,
    LastEditDt = SYSDATETIME()
WHERE Id = @OutboundOrderId
  AND OrderStatus = 'WAIT_PICK';";

                        int orderRows = conn.Execute(updateOrderSql, new
                        {
                            StartPickDt = allocateTime,
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
FROM dbo.WMS_FG_OutboundAllocation
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
SELECT TOP 1 Id, OutboundOrderId, AllocationStatus, FirstPickDt AS FirstPickTime
FROM dbo.WMS_FG_OutboundAllocation WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
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

                        if (allocation.FirstPickTime.HasValue)
                        {
                            tran.Rollback();

                            return OutboundActionPersistenceResult.Fail("当前已扫描出货箱，禁止释放分配，请按拣货异常流程处理。");
                        }

                        if (!string.Equals(allocation.AllocationStatus, "ACTIVE", StringComparison.Ordinal))
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("当前Allocation状态不允许释放。");
                        }

                        DateTime now = DateTime.Now;
                        conn.Execute(@"
UPDATE dbo.WMS_FG_HU_Reservation
SET ReservationStatus = 'RELEASED', ReleaseDt = @Now, ReleaseBy = @Operator, ReleaseReason = @Reason
WHERE AllocationId = @AllocationId AND ReservationStatus = 'ACTIVE';

UPDATE dbo.WMS_FG_OutboundPickBatch
SET BatchStatus = 'RELEASED', ReleaseDt = @Now, ReleaseReason = @Reason
WHERE AllocationId = @AllocationId AND BatchStatus = 'ALLOCATED';

UPDATE dbo.WMS_FG_OutboundOrderLine
SET AllocatedQty = 0, LineStatus = 'WAIT_PICK', LastEditDt = @Now
WHERE OutboundOrderId = @OutboundOrderId AND LineStatus = 'ALLOCATED';

UPDATE dbo.WMS_FG_OutboundOrder
SET OrderStatus = 'WAIT_PICK', StartPickDt = NULL,
    LastEditBy = @Operator, LastEditDt = @Now
WHERE Id = @OutboundOrderId AND OrderStatus = 'ALLOCATED';

UPDATE dbo.WMS_FG_OutboundAllocation
SET AllocationStatus = 'RELEASED', ReleaseDt = @Now, ReleaseBy = @Operator,
    ReleaseReason = @Reason, ReleaseRequestId = @RequestId
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


        public string GetPickBatchCustomerCode(string pickBatchNo)
        {
            const string sql = @"
SELECT TOP 1 ISNULL(o.CustomerCode, '')
FROM dbo.WMS_FG_OutboundPickBatch b
INNER JOIN dbo.WMS_FG_OutboundOrder o ON o.Id = b.OutboundOrderId
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
FROM dbo.WMS_FG_HU_Reservation r WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_HU hu WITH (UPDLOCK, HOLDLOCK, ROWLOCK) ON hu.Id = r.HUId
LEFT JOIN dbo.WMS_FG_OutboundExceptionReplacement replacement
    ON replacement.ReservationId = r.Id
LEFT JOIN dbo.WMS_FG_OutboundPickException exception
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
FROM dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_Location loc ON loc.Id = cl.LocationId
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
FROM dbo.WMS_FG_Location
WHERE WarehouseId = @WarehouseId
  AND LocationCode = 'OUTBOUND-STAGING'
  AND IsActive = 1;";
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
UPDATE dbo.WMS_FG_HU_CurrentLocation
SET LocationId = @ToLocationId,
    LocationSince = @MoveTime,
    LastMoveLogId = @MoveLogId,
    LastEditBy = @Operator,
    LastEditDt = SYSDATETIME()
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
FROM dbo.WMS_FG_OutboundPickBatchDetail WITH (UPDLOCK, HOLDLOCK)
WHERE PickBatchId = @PickBatchId;",
                            new { batch.PickBatchId },
                            tran);

                        long detailId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_FG_OutboundPickBatchDetail
(
    PickBatchId, ReservationId, SequenceNo, BoxHUId,
    BoxQty, EffectiveQty, Uom, MoveLogId,
    DetailStatus, ReplacementForDetailId, MinWeekSortKey
)
OUTPUT INSERTED.Id
VALUES
(
    @PickBatchId, @ReservationId, @SequenceNo, @BoxHUId,
    @BoxQty, @BoxQty, @Uom, @MoveLogId,
    'PICKED', @ReplacementForDetailId, @MinWeekSortKey
);",
                            new
                            {
                                batch.PickBatchId,
                                ReservationId = reservation.Id,
                                SequenceNo = sequenceNo,
                                BoxHUId = command.HuId,
                                BoxQty = reservation.ReservedQty,
                                reservation.Uom,
                                MoveLogId = move.Id,
                                reservation.ReplacementForDetailId,
                                reservation.MinWeekSortKey
                            },
                            tran);

                        InsertPickDetailItems(conn, tran, detailId, allocations);

                        conn.Execute(@"
UPDATE dbo.WMS_FG_HU_Reservation
SET ReservationStatus = 'CONSUMED', ConsumeDt = @Now,
    ConsumeBy = @Operator
WHERE Id = @ReservationId AND ReservationStatus = 'ACTIVE';",
                            new
                            {
                                Now = move.MoveTime,
                                command.Operator,
                                ReservationId = reservation.Id
                            },
                            tran);

                        if (reservation.ReplacementLinkId.HasValue)
                        {
                            conn.Execute(@"
UPDATE dbo.WMS_FG_OutboundExceptionReplacement
SET PickDetailId = @PickDetailId
WHERE Id = @ReplacementLinkId;

UPDATE ex
SET ExceptionStatus = 'RESOLVED',
    ResolvedDt = @Now,
    ResolvedBy = @Operator
FROM dbo.WMS_FG_OutboundPickException ex
WHERE ex.Id = @ExceptionId
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.WMS_FG_OutboundExceptionReplacement link
      INNER JOIN dbo.WMS_FG_HU_Reservation rr ON rr.Id = link.ReservationId
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
UPDATE dbo.WMS_FG_OutboundOrderLine
SET EffectivePickedQty = EffectivePickedQty + @Qty,
    LineStatus = CASE
        WHEN EffectivePickedQty + @Qty = EffectiveRequiredQty THEN 'PICKED'
        ELSE 'PICKING'
    END,
    LastEditDt = @Now
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
UPDATE dbo.WMS_FG_OutboundPickBatch
SET EffectivePickedQty = @NewQty,
    HistoricalPickedQty = HistoricalPickedQty + @BoxQty,
    BatchStatus = @BatchStatus,
    CompleteDt = CASE WHEN @BatchStatus = 'PICKED' THEN @Now ELSE NULL END
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
    SELECT 1 FROM dbo.WMS_FG_OutboundPickBatch
    WHERE AllocationId = @AllocationId
      AND BatchStatus <> 'PICKED'
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { batch.AllocationId },
                            tran);
                        bool hasOpenException = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.WMS_FG_OutboundPickException ex
    INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail detail ON detail.Id = ex.PickDetailId
    INNER JOIN dbo.WMS_FG_OutboundPickBatch exceptionBatch ON exceptionBatch.Id = detail.PickBatchId
    WHERE exceptionBatch.AllocationId = @AllocationId
      AND ex.ExceptionStatus <> 'RESOLVED'
)
THEN CONVERT(bit, 1) ELSE CONVERT(bit, 0) END;",
                            new { batch.AllocationId },
                            tran);
                        string orderStatus = hasOpenException
                            ? "EXCEPTION"
                            : (allPicked ? "PICKED" : "PICKING");
                        conn.Execute(@"
UPDATE dbo.WMS_FG_OutboundAllocation
SET AllocationStatus = @AllocationStatus,
    FirstPickDt = ISNULL(FirstPickDt, @Now),
    CompleteDt = CASE WHEN @AllocationStatus = 'COMPLETED' THEN @Now ELSE NULL END
WHERE Id = @AllocationId;

UPDATE dbo.WMS_FG_OutboundOrder
SET OrderStatus = @OrderStatus,
    PickedDt = CASE WHEN @OrderStatus = 'PICKED' THEN @Now ELSE NULL END,
    LastEditBy = @Operator,
    LastEditDt = @Now
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


        private static ScanBatchRow ReadScanBatch(
            SqlConnection conn,
            SqlTransaction tran,
            string pickBatchNo)
        {
            const string sql = @"
SELECT TOP 1
    b.Id AS PickBatchId, b.PickBatchNo, o.WarehouseId,
    b.ProdNo AS MaterialCode, b.Uom, b.PlannedQty, b.EffectivePickedQty,
    b.HistoricalPickedQty, b.BatchStatus,
    b.AllocationId, a.AllocationStatus,
    a.OutboundOrderId, o.SAPDeliveryNo, o.OrderStatus
FROM dbo.WMS_FG_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON a.Id = b.AllocationId
INNER JOIN dbo.WMS_FG_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON o.Id = a.OutboundOrderId
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
    item.ProdNo AS MaterialCode,
    item.ProdRev AS MaterialRevision,
    item.DateCode,
    item.WeekSortKey,
    item.LotNo AS BatchNo,
    item.SAPBatchNo,
    item.Qty,
    item.Uom
FROM dbo.WMS_FG_HU_Relation rel WITH (HOLDLOCK)
INNER JOIN dbo.WMS_FG_HU innerHu WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON innerHu.Id = rel.ChildHUId
INNER JOIN dbo.WMS_FG_HU_Item item WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
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
  AND item.ProdNo = @MaterialCode
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
FROM dbo.WMS_FG_OutboundOrderLine WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE OutboundOrderId = @OutboundOrderId
  AND ProdNo = @MaterialCode
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
INSERT INTO dbo.WMS_FG_OutboundPickDetailItem
(
    PickDetailId, OutboundOrderLineId, InnerHUId, HUItemId,
    ProdNo, ProdRev, DateCode, WeekSortKey,
    LotNo, SAPBatchNo, AllocatedQty, Uom
)
VALUES
(
    @PickDetailId, @OutboundOrderLineId, @InnerHUId, @HUItemId,
    @ProdNo, @ProdRev, @DateCode, @WeekSortKey,
    @LotNo, @SAPBatchNo, @AllocatedQty, @Uom
);";
            foreach (ScanItemAllocation allocation in allocations)
            {
                conn.Execute(sql, new
                {
                    PickDetailId = detailId,
                    OutboundOrderLineId = allocation.LineId,
                    allocation.Item.InnerHuId,
                    allocation.Item.HuItemId,
                    ProdNo = allocation.Item.MaterialCode,
                    ProdRev = allocation.Item.MaterialRevision,
                    allocation.Item.DateCode,
                    allocation.Item.WeekSortKey,
                    LotNo = allocation.Item.BatchNo,
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
    ml.MoveNo AS OperationNo
FROM dbo.WMS_FG_HU_MoveLog ml
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.MoveLogId = ml.Id
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
INNER JOIN dbo.WMS_FG_OutboundOrder o ON o.Id = a.OutboundOrderId
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = d.BoxHUId
LEFT JOIN dbo.WMS_FG_Location fromLoc ON fromLoc.Id = ml.FromLocationId
INNER JOIN dbo.WMS_FG_Location toLoc ON toLoc.Id = ml.ToLocationId
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
    o.SAPDeliveryNo AS OutboundOrderNo,
    o.SAPDeliveryNo,
    w.WarehouseCode,
    o.PlantCode,
    o.SAPStorageLocation,
    o.CustomerCode,
    o.SoldToCode,
    o.PlannedShipDate,
    o.OrderStatus,
    o.CreateDt AS ReceivedTime,
    o.StartPickDt AS StartPickTime,
    o.PickedDt AS PickedTime,
    o.ShippedDt AS ShippedTime
FROM dbo.WMS_FG_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id = o.WarehouseId
WHERE o.SAPDeliveryNo = @SAPDeliveryNo;";

            OutboundOrderDto order = conn.QueryFirstOrDefault<OutboundOrderDto>(
                orderSql,
                new { SAPDeliveryNo = sapDeliveryNo },
                tran);
            if (order == null)
                return null;

            const string lineSql = @"
SELECT
    Id, OutboundOrderId, SAPItemNo, ProdNo AS MaterialCode, NULL AS MaterialRevision,
    RequiredQty, CAST(0 AS DECIMAL(18,4)) AS SpareQty, CAST(0 AS DECIMAL(18,4)) AS SAPGiftQty, GiftQty, EffectiveRequiredQty,
    Uom, NULL AS PlantCode, NULL AS SAPStorageLocation, AllocatedQty,
    EffectivePickedQty, CAST(0 AS DECIMAL(18,4)) AS ShippedQty, LineStatus
FROM dbo.WMS_FG_OutboundOrderLine WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
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
    EXISTS (SELECT 1 FROM dbo.WMS_FG_HU_MoveLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_HU_OperationLog WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundAllocation WHERE RequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundPickException WHERE RequestId = @RequestId OR ReplacementRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_HU_HoldLog WHERE HoldRequestId = @RequestId OR ReleaseRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundAllocation WHERE ReleaseRequestId = @RequestId)
 OR EXISTS (SELECT 1 FROM dbo.WMS_FG_OutboundOrder WHERE VoidRequestId = @RequestId)
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
INSERT INTO dbo.WMS_FG_OutboundAllocation
(
    AllocationNo, OutboundOrderId, RequestId, AllocationStatus,
    AllocateDt, AllocateBy, ExpireDt, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @AllocationNo, @OutboundOrderId, @RequestId, 'ACTIVE',
    @AllocateTime, @Operator, @ExpireTime, @Remark
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
            long allocationId,
            PlannedGroup group,
            StartPickingCommand command)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_OutboundPickBatch
(
    PickBatchNo, AllocationId, ProdNo, Uom, PlannedQty,
    EffectivePickedQty, HistoricalPickedQty, BatchStatus, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @PickBatchNo, @AllocationId, @ProdNo, @Uom, @PlannedQty,
    0, 0, 'ALLOCATED', @Remark
);";
            return conn.ExecuteScalar<long>(sql, new
            {
                PickBatchNo = pickBatchNo,
                ProdNo = group.MaterialCode,
                PlannedQty = group.RequiredQty,
                group.Uom,
                command.Remark,
                AllocationId = allocationId
            }, tran);
        }


        private static long InsertReservation(
            SqlConnection conn,
            SqlTransaction tran,
            string reservationNo,
            long allocationId,
            long pickBatchId,
            OutboundAllocationCandidateDto box,
            string uom,
            DateTime expireTime,
            string operatorCode)
        {
            const string sql = @"
INSERT INTO dbo.WMS_FG_HU_Reservation
(
    ReservationNo, AllocationId, PickBatchId,
    HUId, SourceLocationId, ReservedQty, Uom,
    MinWeekSortKey, MaxWeekSortKey, DistinctWeekCount, WeekSummary,
    ReservationStatus, ReserveDt, ReserveBy, ExpireDt
)
OUTPUT INSERTED.Id
VALUES
(
    @ReservationNo, @AllocationId, @PickBatchId,
    @HUId, @SourceLocationId, @ReservedQty, @Uom,
    @MinWeekSortKey, @MaxWeekSortKey, @DistinctWeekCount, @WeekSummary,
    'ACTIVE', @ReserveDt, @Operator, @ExpireTime
);";
            return conn.ExecuteScalar<long>(sql, new
            {
                ReservationNo = reservationNo,
                AllocationId = allocationId,
                PickBatchId = pickBatchId,
                HUId = box.BoxHuId,
                SourceLocationId = box.LocationId,
                ReservedQty = box.Qty,
                Uom = uom,
                box.MinWeekSortKey,
                box.MaxWeekSortKey,
                box.DistinctWeekCount,
                box.WeekSummary,
                ReserveDt = DateTime.Now,
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
UPDATE dbo.WMS_FG_OutboundOrderLine
SET AllocatedQty = EffectiveRequiredQty,
    LineStatus = 'ALLOCATED',
    LastEditDt = SYSDATETIME()
WHERE OutboundOrderId = @OutboundOrderId
  AND ProdNo = @MaterialCode
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
    a.ExpireDt AS ExpireTime
FROM dbo.WMS_FG_OutboundAllocation a
INNER JOIN dbo.WMS_FG_OutboundOrder o ON o.Id = a.OutboundOrderId
WHERE a.RequestId = @RequestId;";
            StartPickingHeaderRow header = conn.QueryFirstOrDefault<StartPickingHeaderRow>(
                headerSql,
                new { RequestId = requestId },
                tran);
            if (header == null)
                return null;

            const string batchSql = @"
SELECT Id AS PickBatchId, PickBatchNo, ProdNo AS MaterialCode, Uom, PlannedQty
FROM dbo.WMS_FG_OutboundPickBatch
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
FROM dbo.WMS_FG_HU_Reservation r
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = r.HUId
INNER JOIN dbo.WMS_FG_Location loc ON loc.Id = r.SourceLocationId
WHERE r.AllocationId = @AllocationId
  AND r.ReservationStatus = 'ACTIVE'
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
                    GroupKey = BuildGroupKey(row.MaterialCode, row.Uom),
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
    }
}
