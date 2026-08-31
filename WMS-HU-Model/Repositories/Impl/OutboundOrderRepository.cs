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
    public class OutboundOrderRepository : IOutboundOrderRepository
    {
        public List<OutboundOrderDto> GetOrders(OutboundOrderQuery query)
        {
            const string sql = @"
SELECT TOP (@Top)
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
FROM dbo.WMS_FG_OutboundOrder o
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id = o.WarehouseId
WHERE @Status IS NULL OR o.OrderStatus = @Status
ORDER BY o.CreateDt DESC, o.Id DESC;";

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
FROM dbo.WMS_FG_OutboundOrder o
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id = o.WarehouseId
WHERE o.SAPDeliveryNo = @SAPDeliveryNo;";

            const string lineSql = @"
SELECT
    Id,
    OutboundOrderId,
    SAPItemNo,
    ProdNo AS MaterialCode,
    NULL AS MaterialRevision,
    RequiredQty,
    CAST(0 AS DECIMAL(18,4)) AS SpareQty,
    CAST(0 AS DECIMAL(18,4)) AS SAPGiftQty,
    GiftQty,
    EffectiveRequiredQty,
    Uom,
    NULL AS PlantCode,
    NULL AS SAPStorageLocation,
    AllocatedQty,
    EffectivePickedQty,
    CAST(0 AS DECIMAL(18,4)) AS ShippedQty,
    LineStatus
FROM dbo.WMS_FG_OutboundOrderLine
WHERE OutboundOrderId = @OutboundOrderId
ORDER BY SAPItemNo ASC, Id ASC;";

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@SAPDeliveryNo", sapDeliveryNo, DbType.AnsiString, size: 50);
            OutboundOrderDto order = DbExecutor.QueryFirstOrDefault<OutboundOrderDto>(orderSql, parameters);
            if (order == null)
                return null;

            DynamicParameters lineParameters = new DynamicParameters();
            lineParameters.Add("@OutboundOrderId", order.Id, DbType.Int64);
            order.Lines = DbExecutor.Query<OutboundOrderLineDto>(lineSql, lineParameters);
            return order;
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
FROM dbo.WMS_FG_OutboundOrder
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
FROM dbo.WMS_FG_OutboundOrder WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
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
    SELECT 1 FROM dbo.WMS_FG_OutboundAllocation
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
UPDATE dbo.WMS_FG_OutboundOrderLine
SET LineStatus = 'VOID', LastEditDt = @Now
WHERE OutboundOrderId = @OutboundOrderId;

UPDATE dbo.WMS_FG_OutboundOrder
SET OrderStatus = 'VOID', VoidTime = @Now, VoidReason = @Reason,
    VoidRequestId = @RequestId, LastEditBy = @Operator, LastEditDt = @Now
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

        public OutboundPickingStateReadResult GetPickingState(string sapDeliveryNo)
        {
            OutboundOrderDto order = GetOrder(sapDeliveryNo);
            if (order == null) return null;
            OutboundPickingStateDto result = new OutboundPickingStateDto
            {
                SAPDeliveryNo = order.SAPDeliveryNo,
                OrderStatus = order.OrderStatus,
                Lines = order.Lines
            };
            if (order.OrderStatus == "WAIT_PICK") return OutboundPickingStateReadResult.Success(result);

            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                const string activeAllocationSql = @"
SELECT Id, AllocationNo, AllocationStatus
FROM dbo.WMS_FG_OutboundAllocation
WHERE OutboundOrderId=@OutboundOrderId AND AllocationStatus IN ('ACTIVE','PICKING')
ORDER BY Id;";
                List<PickingAllocationRow> active = connection.Query<PickingAllocationRow>(activeAllocationSql, new { OutboundOrderId = order.Id }).AsList();
                if (active.Count > 1)
                {
                    return OutboundPickingStateReadResult.Fail("PICKING_STATE_CONFLICT: 出库单存在多个有效Allocation，无法确定当前拣货状态。");
                }

                PickingAllocationRow allocation = active.SingleOrDefault();
                if (allocation == null && (order.OrderStatus == "PICKED" || order.OrderStatus == "EXCEPTION"))
                {
                    const string completedSql = @"
SELECT TOP 2 Id, AllocationNo, AllocationStatus
FROM dbo.WMS_FG_OutboundAllocation
WHERE OutboundOrderId=@OutboundOrderId AND AllocationStatus='COMPLETED'
ORDER BY CompleteDt DESC, Id DESC;";
                    List<PickingAllocationRow> completed = connection.Query<PickingAllocationRow>(completedSql, new { OutboundOrderId = order.Id }).AsList();
                    if (completed.Count > 1)
                    {
                        return OutboundPickingStateReadResult.Fail("PICKING_STATE_CONFLICT: 出库单存在多个已完成Allocation，无法确定当前拣货状态。");
                    }
                    allocation = completed.SingleOrDefault();
                }
                if (allocation == null) return OutboundPickingStateReadResult.Success(result);

                result.HasActivePicking = active.Count == 1;
                result.AllocationNo = allocation.AllocationNo;
                result.AllocationStatus = allocation.AllocationStatus;
                const string duplicateBatchSql = @"
SELECT COUNT(1)
FROM
(
    SELECT ProdNo, Uom
    FROM dbo.WMS_FG_OutboundPickBatch
    WHERE AllocationId=@AllocationId AND BatchStatus IN ('ALLOCATED','PICKING','EXCEPTION')
    GROUP BY ProdNo, Uom
    HAVING COUNT(1)>1
) duplicateGroup;";
                if (connection.ExecuteScalar<int>(duplicateBatchSql, new { AllocationId = allocation.Id }) > 0)
                {
                    return OutboundPickingStateReadResult.Fail("PICKING_STATE_CONFLICT: 同一拣货分组存在多个有效PickBatch，无法确定当前任务。");
                }
                const string batchSql = @"
SELECT Id AS PickBatchId,PickBatchNo,BatchStatus,ProdNo AS MaterialCode,Uom,PlannedQty,
       EffectivePickedQty AS PickedQty,PlannedQty-EffectivePickedQty AS RemainingQty
FROM dbo.WMS_FG_OutboundPickBatch
WHERE AllocationId=@AllocationId
ORDER BY Id;";
                result.ActivePickBatches = connection.Query<PickingStateBatchDto>(batchSql, new { AllocationId = allocation.Id }).AsList();

                const string taskSql = @"
SELECT b.Id AS PickBatchId,b.PickBatchNo,b.ProdNo AS MaterialCode,NULL AS MaterialRevision,b.Uom,
       r.ReservationNo,box.HUCode AS BoxCode,location.LocationCode,r.ReservedQty,
       CAST(0 AS DECIMAL(18,4)) AS PickedQty,r.ReservedQty AS RemainingQty,
       r.ReservationStatus AS PickStatus,ISNULL(r.MinWeekSortKey,2147483647) AS MinWeekSortKey
FROM dbo.WMS_FG_HU_Reservation r
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id=r.PickBatchId
INNER JOIN dbo.WMS_FG_HU box ON box.Id=r.HUId
INNER JOIN dbo.WMS_FG_Location location ON location.Id=r.SourceLocationId
WHERE r.AllocationId=@AllocationId AND r.ReservationStatus='ACTIVE'
ORDER BY b.Id,ISNULL(r.MinWeekSortKey,2147483647),r.HUId;";
                result.RemainingTasks = connection.Query<PickingStateTaskDto>(taskSql, new { AllocationId = allocation.Id }).AsList();
                foreach (PickingStateTaskDto task in result.RemainingTasks)
                {
                    task.SAPItemNos = order.Lines.Where(x => string.Equals(x.MaterialCode, task.MaterialCode, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.Uom, task.Uom, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.SAPItemNo).Select(x => x.SAPItemNo).ToList();
                    task.SAPItemNo = task.SAPItemNos.Count == 1 ? task.SAPItemNos[0] : null;
                }

                const string completedDetailSql = @"
SELECT d.Id AS PickDetailId,b.PickBatchNo,box.HUCode AS BoxCode,source.LocationCode,
       d.EffectiveQty AS PickedQty,d.DetailStatus AS PickStatus
FROM dbo.WMS_FG_OutboundPickBatchDetail d
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id=d.PickBatchId
INNER JOIN dbo.WMS_FG_HU box ON box.Id=d.BoxHUId
INNER JOIN dbo.WMS_FG_HU_MoveLog moveLog ON moveLog.Id=d.MoveLogId
INNER JOIN dbo.WMS_FG_Location source ON source.Id=moveLog.FromLocationId
WHERE b.AllocationId=@AllocationId
ORDER BY d.Id;";
                result.CompletedPickDetails = connection.Query<PickingStateCompletedDetailDto>(completedDetailSql, new { AllocationId = allocation.Id }).AsList();
                result.IsPickingComplete = result.RemainingTasks.Count == 0
                    && result.ActivePickBatches.Count > 0
                    && result.ActivePickBatches.All(x => x.BatchStatus == "PICKED" || x.BatchStatus == "SHIPPED");
            }
            return OutboundPickingStateReadResult.Success(result);
        }

        private class OrderActionRow
        {
            public long Id { get; set; }
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
