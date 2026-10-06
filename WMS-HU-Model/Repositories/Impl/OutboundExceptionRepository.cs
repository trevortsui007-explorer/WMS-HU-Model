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
    public class OutboundExceptionRepository : IOutboundExceptionRepository
    {


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
FROM dbo.WMS_FG_OutboundPickException
WHERE RequestId = @RequestId;",
                            new { request.RequestId },
                            tran);
                        if (duplicate != null)
                        {
                            long duplicateDetailId = conn.ExecuteScalar<long>(@"
SELECT PickDetailId FROM dbo.WMS_FG_OutboundPickException WHERE RequestId = @RequestId;",
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

                        if(conn.ExecuteScalar<int>(@"SELECT COUNT(*) FROM dbo.WMS_FG_OutboundPickBatchDetail d
JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id=d.PickBatchId JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id=b.AllocationId
WHERE d.Id=@Id AND a.AllocationMode='COMBINED'",new { Id=pickDetailId },tran)>0)
                            return OutboundActionPersistenceResult.Fail("联合任务不支持局部退回或换箱，请联系IT核查整组任务。");
                        ReturnDetailRow detail = conn.QueryFirstOrDefault<ReturnDetailRow>(@"
SELECT TOP 1
    d.Id, d.PickBatchId, d.BoxHUId, d.EffectiveQty, d.DetailStatus,
    b.AllocationId, a.OutboundOrderId, o.WarehouseId,
    b.EffectivePickedQty AS BatchEffectiveQty, b.PlannedQty,
    o.SAPDeliveryNo, o.OrderStatus,
    cl.LocationId AS CurrentLocationId, loc.LocationCode AS CurrentLocationCode,
    hu.HUCode AS BoxHuCode, hu.IsHold
FROM dbo.WMS_FG_OutboundPickBatchDetail d WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON a.Id = b.AllocationId
INNER JOIN dbo.WMS_FG_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON o.Id = a.OutboundOrderId
INNER JOIN dbo.WMS_FG_HU hu WITH (UPDLOCK, HOLDLOCK, ROWLOCK) ON hu.Id = d.BoxHUId
INNER JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON cl.HUId = d.BoxHUId
INNER JOIN dbo.WMS_FG_Location loc ON loc.Id = cl.LocationId
WHERE d.Id = @PickDetailId;",
                            new { PickDetailId = pickDetailId },
                            tran);
                        if (detail == null)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("PickDetail不存在。");
                        }
                        if (detail.OrderStatus == "SHIPPED")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("SHIPPED订单不允许普通异常退回。");
                        }
                        if (detail.DetailStatus != "PICKED" || detail.EffectiveQty <= 0)
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("只有当前有效的PICKED明细允许异常退回。");
                        }

                        ScanLocationRow returned = conn.QueryFirstOrDefault<ScanLocationRow>(@"
SELECT TOP 1 Id AS LocationId, LocationCode
FROM dbo.WMS_FG_Location
WHERE WarehouseId = @WarehouseId
  AND LocationCode = 'RETURNED'
  AND IsVirtual = 1
  AND IsActive = 1;",
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
UPDATE dbo.WMS_FG_HU_CurrentLocation
SET LocationId = @ToLocationId, LocationSince = @Now,
    LastMoveLogId = @MoveLogId, LastEditBy = @Operator,
    LastEditDt = @Now
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
                            move.MoveTime, null, request.Remark, BusinessLogText.Reason("出库退回", request.Reason));

                        bool quality = request.ReasonCode == "QUALITY_FAILED";
                        long exceptionId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_FG_OutboundPickException
(
    ExceptionNo, PickDetailId, ReasonCode, Reason, ExceptionStatus,
    ReturnMoveLogId, RequestId, ExceptionDt, ExceptionBy, Remark
)
OUTPUT INSERTED.Id
VALUES
(
    @ExceptionNo, @PickDetailId, @ReasonCode, @Reason, 'RETURN_CONFIRMED',
    @ReturnMoveLogId, @RequestId, @Now, @Operator, @Remark
);",
                            new
                            {
                                ExceptionNo = exceptionNo,
                                PickDetailId = pickDetailId,
                                request.ReasonCode,
                                request.Reason,
                                ReturnMoveLogId = move.Id,
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
INSERT INTO dbo.WMS_FG_HU_HoldLog
(
    HoldNo, HUId, HoldType, HoldReasonCode, HoldReason,
    SourceBizType, SourceBizId, SourceBizNo,
    IsActive, HoldDt, HoldBy
)
OUTPUT INSERTED.Id
VALUES
(
    @HoldNo, @HUId, 'QUALITY', 'QUALITY_FAILED', @Reason,
    'OUTBOUND_EXCEPTION', @ExceptionId, @ExceptionNo,
    1, @Now, @Operator
);

UPDATE dbo.WMS_FG_HU
SET IsHold = 1, LastEditBy = @Operator, LastEditDt = @Now
WHERE Id = @HUId;
",
                                new
                                {
                                    HoldNo = holdNo,
                                    HUId = detail.BoxHuId,
                                    Reason = BusinessLogText.Reason("冻结", request.Reason),
                                    ExceptionId = exceptionId,
                                    ExceptionNo = exceptionNo,
                                    Now = move.MoveTime,
                                    request.Operator
                                },
                                tran);
                        }

                        conn.Execute(@"
UPDATE dbo.WMS_FG_OutboundPickException
SET HoldLogId = @HoldLogId
WHERE Id = @ExceptionId;

UPDATE dbo.WMS_FG_OutboundPickBatchDetail
SET DetailStatus = @DetailStatus, EffectiveQty = 0, ReturnDt = @Now
WHERE Id = @PickDetailId;

UPDATE l
SET l.EffectivePickedQty = l.EffectivePickedQty - x.Qty,
    l.LineStatus = 'EXCEPTION', l.LastEditDt = @Now
FROM dbo.WMS_FG_OutboundOrderLine l
INNER JOIN
(
    SELECT OutboundOrderLineId, SUM(AllocatedQty) AS Qty
    FROM dbo.WMS_FG_OutboundPickDetailItem
    WHERE PickDetailId = @PickDetailId
    GROUP BY OutboundOrderLineId
) x ON x.OutboundOrderLineId = l.Id;

UPDATE dbo.WMS_FG_OutboundPickBatch
SET EffectivePickedQty = EffectivePickedQty - @ReturnedQty,
    BatchStatus = 'EXCEPTION', CompleteDt = NULL
WHERE Id = @PickBatchId;

UPDATE dbo.WMS_FG_OutboundAllocation
SET AllocationStatus = 'PICKING', CompleteDt = NULL, LastEditDt = @Now
WHERE Id = @AllocationId;

UPDATE dbo.WMS_FG_OutboundOrder
SET OrderStatus = 'EXCEPTION', PickedDt = NULL,
    LastEditBy = @Operator, LastEditDt = @Now
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
FROM dbo.WMS_FG_HU_MoveLog ml
INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = ml.HUId
INNER JOIN dbo.WMS_FG_Location loc ON loc.Id = ml.ToLocationId
INNER JOIN dbo.WMS_FG_Zone z ON z.Id = loc.ZoneId
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
FROM dbo.WMS_FG_HU hu WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_HU_CurrentLocation cl WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON cl.HUId = hu.Id
INNER JOIN dbo.WMS_FG_Location currentLoc ON currentLoc.Id = cl.LocationId
OUTER APPLY
(
    SELECT TOP 1 move.FromLocationId
    FROM dbo.WMS_FG_OutboundPickBatchDetail d
    INNER JOIN dbo.WMS_FG_HU_MoveLog move ON move.Id = d.MoveLogId
    WHERE d.BoxHUId = hu.Id
      AND d.DetailStatus IN ('RETURNED', 'QUALITY_HOLD')
    ORDER BY d.ReturnDt DESC, d.Id DESC
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
    loc.IsActive, loc.IsVirtual, loc.PlantPolicy, loc.DedicatedPlantCode,
    z.ZoneCode
FROM dbo.WMS_FG_Location loc WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_Zone z ON z.Id = loc.ZoneId
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
                        if (target == null || !target.IsActive
                            || target.IsVirtual || target.ZoneCode != "STORAGE")
                        {
                            tran.Rollback();
                            return OutboundActionPersistenceResult.Fail("目标货位不存在或不是有效STORAGE实体货位。");
                        }

                        bool warehouseAllowsPlant = conn.ExecuteScalar<bool>(@"
SELECT CASE WHEN EXISTS
(
    SELECT 1 FROM dbo.WMS_FG_WarehousePlant
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
    FROM dbo.WMS_FG_HU_CurrentLocation cl
    INNER JOIN dbo.WMS_FG_HU hu ON hu.Id = cl.HUId
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
    SELECT 1 FROM dbo.WMS_FG_HU_Reservation
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
UPDATE dbo.WMS_FG_HU_CurrentLocation
SET LocationId = @ToLocationId, LocationSince = @Now,
    LastMoveLogId = @MoveLogId, LastEditBy = @Operator,
    LastEditDt = @Now
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

                        conn.Execute(@"
UPDATE ex
SET ExceptionStatus = 'RESOLVED', ResolvedDt = @Now, ResolvedBy = @Operator
FROM dbo.WMS_FG_OutboundPickException ex
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail detail ON detail.Id = ex.PickDetailId
WHERE detail.BoxHUId = @HUId
  AND ex.ExceptionStatus = 'RETURN_CONFIRMED';",
                            new { Now = move.MoveTime, request.Operator, HUId = box.HuId }, tran);

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
    ex.PickDetailId AS OriginalPickDetailId, d.BoxHUId AS OriginalHuId,
    ex.ReplacementRequestId,
    d.BoxQty AS RequiredReplacementQty,
    b.Id AS PickBatchId, b.PickBatchNo, b.ProdNo AS MaterialCode, b.Uom,
    b.AllocationId, a.OutboundOrderId,
    a.RequestId AS AllocationRequestId,
    o.SAPDeliveryNo, o.OrderStatus
FROM dbo.WMS_FG_OutboundPickException ex WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.Id = ex.PickDetailId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON a.Id = b.AllocationId
INNER JOIN dbo.WMS_FG_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON o.Id = a.OutboundOrderId
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
                        DateTime? expireTime = null; // Replacement reservations also require manual handling.
                        string token = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
                        for (int i = 0; i < plan.SelectedBoxes.Count; i++)
                        {
                            OutboundAllocationCandidateDto box = plan.SelectedBoxes[i];
                            string reservationNo = "RR" + now.ToString("yyyyMMddHHmmss") + token + (i + 1).ToString("D3");
                            long reservationId = InsertReservation(
                                conn, tran, reservationNo, context.AllocationId,
                                context.PickBatchId,
                                box, context.Uom, expireTime, request.Operator);
                            conn.Execute(@"
INSERT INTO dbo.WMS_FG_OutboundExceptionReplacement
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
UPDATE dbo.WMS_FG_OutboundPickException
SET ExceptionStatus = 'REPLACEMENT_RESERVED',
    ReplacementRequestId = @RequestId
WHERE Id = @ExceptionId;

UPDATE dbo.WMS_FG_OutboundPickBatch
SET BatchStatus = 'EXCEPTION', CompleteDt = NULL
WHERE Id = @PickBatchId;

UPDATE dbo.WMS_FG_OutboundAllocation
SET AllocationStatus = 'PICKING', CompleteDt = NULL, LastEditDt = @Now
WHERE Id = @AllocationId;",
                            new
                            {
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



        private static long InsertReservation(
            SqlConnection conn,
            SqlTransaction tran,
            string reservationNo,
            long allocationId,
            long pickBatchId,
            OutboundAllocationCandidateDto box,
            string uom,
            DateTime? expireTime,
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
            public DateTime? ExpireTime { get; set; }
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



        private class ScanLocationRow
        {
            public long LocationId { get; set; }
            public string LocationCode { get; set; }
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
            public bool IsActive { get; set; }
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

        private class PickingAllocationRow
        {
            public long Id { get; set; }
            public string AllocationNo { get; set; }
            public string AllocationStatus { get; set; }
        }
    }
}
