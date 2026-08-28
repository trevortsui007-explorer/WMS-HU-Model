using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Infrastructure.Sap;
using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Repositories
{
    public class SapOutboundPostingRepository : ISapOutboundPostingRepository
    {
        public SapPostingSnapshotBuildResult BuildPreview(string sapDeliveryNo)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                OrderSnapshot order = ReadOrderSnapshot(connection, null, sapDeliveryNo, false);
                if (order == null)
                    return BuildFailure("出库订单不存在。");
                string error = ValidateSnapshot(connection, null, order);
                if (error != null)
                    return BuildFailure(error);
                List<PickedItemRow> source = ReadPickedItems(connection, null, order.AllocationId);
                SapOutboundPostingPreviewDto preview;
                error = TryBuildPreview(order, source, out preview);
                return error == null
                    ? new SapPostingSnapshotBuildResult { IsSuccess = true, OutboundOrderId = order.Id, OutboundStatus = order.OrderStatus, Preview = preview }
                    : BuildFailure(error);
            }
        }

        public SapPostingClaimResult ClaimInitial(string sapDeliveryNo, SapOutboundPostingControlRequest request, SapOutboundPostingPreviewDto preview)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        OrderSnapshot order = ReadOrderSnapshot(connection, transaction, sapDeliveryNo, true);
                        if (order == null)
                            return RollbackClaim(transaction, "出库订单不存在。");
                        PostingRow existing = ReadPosting(connection, transaction, sapDeliveryNo, true);
                        if (existing != null)
                        {
                            if (string.Equals(existing.RequestId, request.RequestId, StringComparison.Ordinal))
                            {
                                OutboundShippingStateDto duplicateState = ReadShippingState(connection, transaction, sapDeliveryNo);
                                transaction.Commit();
                                return new SapPostingClaimResult { IsSuccess = true, ShouldInvokeSap = false, Message = "重复请求，返回当前过账状态。", State = duplicateState };
                            }
                            transaction.Rollback();
                            return new SapPostingClaimResult { IsSuccess = false, Message = existing.PostingStatus == SapOutboundPostingStatuses.Success
                                ? "该SAP交货单已经过账成功。"
                                : "该SAP交货单已经存在过账快照，不能通过普通确认再次发送。" };
                        }
                        string validation = ValidateSnapshot(connection, transaction, order);
                        if (validation != null)
                            return RollbackClaim(transaction, validation);
                        if (IsRequestIdUsed(connection, transaction, request.RequestId))
                            return RollbackClaim(transaction, "RequestId已经被其他WMS业务使用。");
                        long postingId = InsertPosting(connection, transaction, order, request, preview);
                        InsertDetails(connection, transaction, postingId, preview.Data);
                        int attemptNo = 1;
                        DateTime now = DateTime.Now;
                        UpdateSendingClaim(connection, transaction, postingId, request, attemptNo, false, now);
                        long logId = InsertAttemptLog(connection, transaction, sapDeliveryNo, request.RequestId, attemptNo, preview.PayloadJson, preview.PayloadHash, now);
                        transaction.Commit();
                        return new SapPostingClaimResult
                        {
                            IsSuccess = true,
                            ShouldInvokeSap = true,
                            PostingId = postingId,
                            IntegrationLogId = logId,
                            AttemptNo = attemptNo,
                            PayloadJson = preview.PayloadJson,
                            PayloadHash = preview.PayloadHash
                        };
                    }
                    catch
                    {
                        if (transaction.Connection != null)
                            transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public SapPostingClaimResult ClaimRetry(string sapDeliveryNo, SapOutboundPostingControlRequest request)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        PostingRow posting = ReadPosting(connection, transaction, sapDeliveryNo, true);
                        if (posting == null)
                            return RollbackClaim(transaction, "SAP过账快照不存在。");
                        if (posting.PostingStatus != SapOutboundPostingStatuses.Failed
                            || (string.IsNullOrWhiteSpace(posting.ManualConfirmRemark) && posting.SapCode != "LOCAL_NOT_INVOKED"))
                            return RollbackClaim(transaction, "只有经IT确认SAP未过账的FAILED记录允许重试。");
                        if (string.Equals(posting.RequestId, request.RequestId, StringComparison.Ordinal))
                        {
                            OutboundShippingStateDto duplicate = ReadShippingState(connection, transaction, sapDeliveryNo);
                            transaction.Commit();
                            return new SapPostingClaimResult { IsSuccess = true, ShouldInvokeSap = false, Message = "重复重试请求，返回当前状态。", State = duplicate };
                        }
                        if (IsRequestIdUsed(connection, transaction, request.RequestId))
                            return RollbackClaim(transaction, "RequestId已经被其他WMS业务使用。");
                        int attemptNo = posting.CurrentAttemptNo + 1;
                        DateTime now = DateTime.Now;
                        UpdateSendingClaim(connection, transaction, posting.Id, request, attemptNo, true, now);
                        long logId = InsertAttemptLog(connection, transaction, sapDeliveryNo, request.RequestId, attemptNo, posting.PayloadJson, posting.PayloadHash, now);
                        transaction.Commit();
                        return new SapPostingClaimResult
                        {
                            IsSuccess = true,
                            ShouldInvokeSap = true,
                            PostingId = posting.Id,
                            IntegrationLogId = logId,
                            AttemptNo = attemptNo,
                            PayloadJson = posting.PayloadJson,
                            PayloadHash = posting.PayloadHash
                        };
                    }
                    catch
                    {
                        if (transaction.Connection != null)
                            transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public OutboundShippingStateDto FinalizeAttempt(long postingId, int attemptNo, string requestId, SapOutboundTransportResult transport, string operatorValue, string remark)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        PostingRow posting = ReadPostingById(connection, transaction, postingId, true);
                        if (posting == null || posting.PostingStatus != SapOutboundPostingStatuses.Sending
                            || posting.CurrentAttemptNo != attemptNo || !string.Equals(posting.RequestId, requestId, StringComparison.Ordinal))
                            throw new InvalidOperationException("SAP过账Attempt已失去当前SENDING状态所有权。");
                        if (transport == null)
                            throw new InvalidOperationException("SAP transport结果不能为空。");

                        string status = transport.InvocationStarted
                            ? NormalizeTransportStatus(transport.ResultStatus)
                            : SapOutboundPostingStatuses.Failed;
                        if (!transport.InvocationStarted)
                        {
                            transport.SapCode = "LOCAL_NOT_INVOKED";
                            transport.SapMessage = transport.ExceptionMessage ?? "SAP外部调用未开始。";
                        }
                        DateTime now = DateTime.Now;
                        if (status == SapOutboundPostingStatuses.Success)
                            FinalizeShipment(connection, transaction, posting.SAPDeliveryNo, requestId, operatorValue, remark, now);
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.WMS_OutboundSapPosting
SET PostingStatus=@PostingStatus, SapCode=@SapCode, SapMessage=@SapMessage,
    ResponseJson=@ResponseJson, SuccessTime=CASE WHEN @PostingStatus='SUCCESS' THEN @Now ELSE SuccessTime END,
    SuccessConfirmSource=CASE WHEN @PostingStatus='SUCCESS' THEN 'SAP_RESPONSE' ELSE SuccessConfirmSource END,
    ModifyUser=@Operator, ModifyTime=@Now
WHERE Id=@Id AND PostingStatus='SENDING' AND CurrentAttemptNo=@AttemptNo AND RequestId=@RequestId;

UPDATE dbo.WMS_SapIntegrationLog
SET ResponseTime=@ResponseTime, DurationMs=@DurationMs, ResponseJson=@ResponseJson,
    ResultStatus=@PostingStatus, ExternalCode=@SapCode, ExternalMessage=@SapMessage,
    HttpStatusCode=@HttpStatusCode, ExceptionType=@ExceptionType, ExceptionMessage=@ExceptionMessage
WHERE InterfaceCode='S0089' AND Direction='OUTBOUND' AND RequestId=@RequestId AND AttemptNo=@AttemptNo AND ResultStatus='SENDING';"))
                        {
                            AddString(command, "@PostingStatus", 20, status);
                            AddString(command, "@SapCode", 20, transport.SapCode);
                            AddNVarChar(command, "@SapMessage", 1000, transport.SapMessage);
                            AddNVarCharMax(command, "@ResponseJson", transport.ResponseJson);
                            AddDateTime(command, "@Now", now);
                            AddString(command, "@Operator", 50, operatorValue);
                            AddLong(command, "@Id", postingId);
                            AddInt(command, "@AttemptNo", attemptNo);
                            AddString(command, "@RequestId", 100, requestId);
                            AddDateTimeNullable(command, "@ResponseTime", transport.ResponseTime);
                            AddLong(command, "@DurationMs", transport.DurationMs);
                            AddIntNullable(command, "@HttpStatusCode", transport.HttpStatusCode);
                            AddString(command, "@ExceptionType", 200, transport.ExceptionType);
                            AddNVarChar(command, "@ExceptionMessage", 2000, transport.ExceptionMessage);
                            if (command.ExecuteNonQuery() < 2)
                                throw new InvalidOperationException("SAP过账Attempt完成状态写入失败。");
                        }
                        OutboundShippingStateDto state = ReadShippingState(connection, transaction, posting.SAPDeliveryNo);
                        transaction.Commit();
                        return state;
                    }
                    catch
                    {
                        if (transaction.Connection != null)
                            transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public OutboundShippingStateDto GetShippingState(string sapDeliveryNo)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                return ReadShippingState(connection, null, sapDeliveryNo);
            }
        }

        public OutboundShippingStateDto ManualConfirmPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request)
        {
            return ManualConfirm(sapDeliveryNo, request, true);
        }

        public OutboundShippingStateDto ManualConfirmNotPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request)
        {
            return ManualConfirm(sapDeliveryNo, request, false);
        }

        private OutboundShippingStateDto ManualConfirm(string sapDeliveryNo, SapOutboundManualConfirmRequest request, bool posted)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        PostingRow posting = ReadPosting(connection, transaction, sapDeliveryNo, true);
                        if (posting == null || posting.PostingStatus != SapOutboundPostingStatuses.Unknown)
                            throw new InvalidOperationException("只有UNKNOWN过账记录允许IT人工确认。");
                        if (IsRequestIdUsed(connection, transaction, request.RequestId))
                            throw new InvalidOperationException("RequestId已经被其他WMS业务使用。");
                        DateTime now = DateTime.Now;
                        if (posted)
                            FinalizeShipment(connection, transaction, sapDeliveryNo, request.RequestId, request.Operator, request.Remark, now);
                        string newStatus = posted ? SapOutboundPostingStatuses.Success : SapOutboundPostingStatuses.Failed;
                        using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.WMS_OutboundSapPosting
SET PostingStatus=@PostingStatus, RequestId=@RequestId,
    SuccessTime=CASE WHEN @PostingStatus='SUCCESS' THEN @Now ELSE SuccessTime END,
    SuccessConfirmSource=CASE WHEN @PostingStatus='SUCCESS' THEN 'MANUAL_CONFIRM' ELSE NULL END,
    ManualConfirmUser=@Operator, ManualConfirmTime=@Now, ManualConfirmRemark=@Remark,
    ModifyUser=@Operator, ModifyTime=@Now
WHERE Id=@Id AND PostingStatus='UNKNOWN';"))
                        {
                            AddString(command, "@PostingStatus", 20, newStatus);
                            AddString(command, "@RequestId", 100, request.RequestId);
                            AddDateTime(command, "@Now", now);
                            AddString(command, "@Operator", 50, request.Operator);
                            AddNVarChar(command, "@Remark", 500, request.Remark);
                            AddLong(command, "@Id", posting.Id);
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("UNKNOWN人工确认发生并发冲突。");
                        }
                        OutboundShippingStateDto state = ReadShippingState(connection, transaction, sapDeliveryNo);
                        transaction.Commit();
                        return state;
                    }
                    catch
                    {
                        if (transaction.Connection != null)
                            transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public PagedResult<OutboundShippingStateDto> QueryShippingOrders(string sapDeliveryNo, string outboundStatus, string postingStatus, int page, int pageSize)
        {
            PagedResult<OutboundShippingStateDto> result = new PagedResult<OutboundShippingStateDto> { Page = page, PageSize = pageSize };
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                const string where = @"
WHERE (@SAPDeliveryNo IS NULL OR o.SAPDeliveryNo LIKE '%' + @SAPDeliveryNo + '%')
  AND (@OutboundStatus IS NULL OR o.OrderStatus=@OutboundStatus)
  AND (@PostingStatus IS NULL OR ISNULL(p.PostingStatus,'READY')=@PostingStatus)";
                using (SqlCommand count = CreateCommand(connection, null, "SELECT COUNT(1) FROM dbo.WMS_OutboundOrder o LEFT JOIN dbo.WMS_OutboundSapPosting p ON p.OutboundOrderId=o.Id " + where))
                {
                    AddString(count, "@SAPDeliveryNo", 30, sapDeliveryNo);
                    AddString(count, "@OutboundStatus", 30, outboundStatus);
                    AddString(count, "@PostingStatus", 20, postingStatus);
                    result.Total = Convert.ToInt32(count.ExecuteScalar());
                }
                using (SqlCommand command = CreateCommand(connection, null, @"
SELECT o.SAPDeliveryNo,o.OrderStatus,ISNULL(p.PostingStatus,'READY') PostingStatus,
       p.SapCode,p.SapMessage,p.SuccessConfirmSource,p.RequestId,
       COUNT(DISTINCT CASE WHEN d.DetailStatus IN ('PICKED','SHIPPED') AND d.EffectiveQty>0 THEN d.BoxHUId END) PickedBoxCount,
       ISNULL(SUM(CASE WHEN d.DetailStatus IN ('PICKED','SHIPPED') AND d.EffectiveQty>0 THEN d.EffectiveQty ELSE 0 END),0) PickedQty,
       CASE WHEN p.PostingStatus='FAILED' AND (p.ManualConfirmRemark IS NOT NULL OR p.SapCode='LOCAL_NOT_INVOKED') THEN CONVERT(bit,1) ELSE CONVERT(bit,0) END CanRetry
FROM dbo.WMS_OutboundOrder o
LEFT JOIN dbo.WMS_OutboundSapPosting p ON p.OutboundOrderId=o.Id
LEFT JOIN dbo.WMS_OutboundAllocation a ON a.OutboundOrderId=o.Id AND a.AllocationStatus='COMPLETED'
LEFT JOIN dbo.WMS_OutboundPickBatch b ON b.AllocationId=a.Id
LEFT JOIN dbo.WMS_OutboundPickBatchDetail d ON d.PickBatchId=b.Id
" + where + @"
GROUP BY o.SAPDeliveryNo,o.OrderStatus,p.PostingStatus,p.SapCode,p.SapMessage,p.SuccessConfirmSource,p.RequestId,p.ManualConfirmRemark,o.Id
ORDER BY o.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;"))
                {
                    AddString(command, "@SAPDeliveryNo", 30, sapDeliveryNo);
                    AddString(command, "@OutboundStatus", 30, outboundStatus);
                    AddString(command, "@PostingStatus", 20, postingStatus);
                    AddInt(command, "@Offset", (page - 1) * pageSize);
                    AddInt(command, "@PageSize", pageSize);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            result.Items.Add(MapState(reader));
                    }
                }
            }
            return result;
        }

        public PagedResult<SapIntegrationLogDto> QueryLogs(SapIntegrationLogQuery query)
        {
            PagedResult<SapIntegrationLogDto> result = new PagedResult<SapIntegrationLogDto> { Page = query.Page, PageSize = query.PageSize };
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                const string where = @" WHERE (@BusinessKey IS NULL OR BusinessKey LIKE '%' + @BusinessKey + '%') AND (@InterfaceCode IS NULL OR InterfaceCode=@InterfaceCode) AND (@Direction IS NULL OR Direction=@Direction) AND (@ResultStatus IS NULL OR ResultStatus=@ResultStatus) ";
                using (SqlCommand count = CreateCommand(connection, null, "SELECT COUNT(1) FROM dbo.WMS_SapIntegrationLog" + where))
                {
                    AddLogFilters(count, query);
                    result.Total = Convert.ToInt32(count.ExecuteScalar());
                }
                using (SqlCommand command = CreateCommand(connection, null, "SELECT Id,InterfaceCode,Direction,BusinessType,BusinessKey,RequestId,CorrelationId,AttemptNo,RequestTime,ResponseTime,DurationMs,NULL RequestJson,NULL ResponseJson,ResultStatus,ExternalCode,ExternalMessage,HttpStatusCode,ExceptionType,ExceptionMessage,PayloadHash FROM dbo.WMS_SapIntegrationLog" + where + " ORDER BY RequestTime DESC,Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY"))
                {
                    AddLogFilters(command, query);
                    AddInt(command, "@Offset", (query.Page - 1) * query.PageSize);
                    AddInt(command, "@PageSize", query.PageSize);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            result.Items.Add(MapLog(reader));
                    }
                }
            }
            return result;
        }

        public SapIntegrationLogDto GetLog(long id)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand command = CreateCommand(connection, null, "SELECT Id,InterfaceCode,Direction,BusinessType,BusinessKey,RequestId,CorrelationId,AttemptNo,RequestTime,ResponseTime,DurationMs,RequestJson,ResponseJson,ResultStatus,ExternalCode,ExternalMessage,HttpStatusCode,ExceptionType,ExceptionMessage,PayloadHash FROM dbo.WMS_SapIntegrationLog WHERE Id=@Id"))
                {
                    AddLong(command, "@Id", id);
                    using (SqlDataReader reader = command.ExecuteReader())
                        return reader.Read() ? MapLog(reader) : null;
                }
            }
        }

        public void RecordReceiveCommunication(string businessKey, string requestJson, string resultStatus, string externalMessage, string payloadHash)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand command = CreateCommand(connection, null, @"
INSERT INTO dbo.WMS_SapIntegrationLog(InterfaceCode,Direction,BusinessType,BusinessKey,RequestId,CorrelationId,AttemptNo,RequestTime,ResponseTime,DurationMs,RequestJson,ResponseJson,ResultStatus,ExternalCode,ExternalMessage,HttpStatusCode,ExceptionType,ExceptionMessage,PayloadHash)
VALUES('SAP_DELIVERY_RECEIVE','INBOUND','OUTBOUND_ORDER_RECEIVE',@BusinessKey,NULL,NULL,0,@Now,@Now,0,@RequestJson,NULL,@ResultStatus,NULL,@ExternalMessage,NULL,NULL,NULL,@PayloadHash);"))
                {
                    AddString(command, "@BusinessKey", 100, businessKey);
                    AddDateTime(command, "@Now", DateTime.Now);
                    AddNVarCharMax(command, "@RequestJson", requestJson);
                    AddString(command, "@ResultStatus", 20, NormalizeLogStatus(resultStatus));
                    AddNVarChar(command, "@ExternalMessage", 1000, externalMessage);
                    AddString(command, "@PayloadHash", 64, payloadHash);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static OrderSnapshot ReadOrderSnapshot(SqlConnection connection, SqlTransaction transaction, string sapDeliveryNo, bool lockRow)
        {
            string lockHint = lockRow ? " WITH (UPDLOCK,HOLDLOCK,ROWLOCK)" : string.Empty;
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT TOP 1 o.Id,o.SAPDeliveryNo,o.WarehouseId,o.OrderStatus,
       (SELECT TOP 1 a.Id FROM dbo.WMS_OutboundAllocation a" + lockHint + @"
        WHERE a.OutboundOrderId=o.Id AND a.AllocationStatus='COMPLETED' ORDER BY a.Id DESC) AllocationId
FROM dbo.WMS_OutboundOrder o" + lockHint + @" WHERE o.SAPDeliveryNo=@SAPDeliveryNo;"))
            {
                AddString(command, "@SAPDeliveryNo", 30, sapDeliveryNo);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        return null;
                    return new OrderSnapshot
                    {
                        Id = reader.GetInt64(0),
                        SAPDeliveryNo = reader.GetString(1),
                        WarehouseId = reader.GetInt64(2),
                        OrderStatus = reader.GetString(3),
                        AllocationId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4)
                    };
                }
            }
        }

        private static string ValidateSnapshot(SqlConnection connection, SqlTransaction transaction, OrderSnapshot order)
        {
            if (order.OrderStatus != "PICKED")
                return "只有PICKED订单允许生成SAP过账。";
            if (order.AllocationId <= 0)
                return "订单不存在已完成的当前Allocation。";
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT
 CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_HU_Reservation WHERE OutboundOrderId=@OrderId AND ReservationStatus='ACTIVE') THEN 1 ELSE 0 END HasReservation,
 CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_OutboundPickException WHERE OutboundOrderId=@OrderId AND ExceptionStatus NOT IN ('RESOLVED','CANCELLED')) THEN 1 ELSE 0 END HasException,
 CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_OutboundOrderLine WHERE OutboundOrderId=@OrderId AND (LineStatus<>'PICKED' OR EffectivePickedQty<>EffectiveRequiredQty)) THEN 1 ELSE 0 END InvalidLine,
 CASE WHEN EXISTS(SELECT 1 FROM dbo.WMS_OutboundPickBatch WHERE AllocationId=@AllocationId AND BatchStatus<>'PICKED') THEN 1 ELSE 0 END InvalidBatch;"))
            {
                AddLong(command, "@OrderId", order.Id);
                AddLong(command, "@AllocationId", order.AllocationId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    if (reader.GetInt32(0) != 0) return "订单仍存在ACTIVE Reservation。";
                    if (reader.GetInt32(1) != 0) return "订单仍存在未关闭的出库异常。";
                    if (reader.GetInt32(2) != 0) return "订单行数量或状态不满足SAP过账条件。";
                    if (reader.GetInt32(3) != 0) return "当前Allocation存在非PICKED PickBatch。";
                }
            }
            return null;
        }

        private static List<PickedItemRow> ReadPickedItems(SqlConnection connection, SqlTransaction transaction, long allocationId)
        {
            List<PickedItemRow> rows = new List<PickedItemRow>();
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT d.Id PickDetailId,d.BoxHUId,d.EffectiveQty,box.HUCode,l.Id OutboundOrderLineId,l.SAPItemNo,
       l.MaterialCode LineMaterialCode,l.SAPStorageLocation,pdi.MaterialCode ItemMaterialCode,
       pdi.SAPBatchNo,pdi.AllocatedQty
FROM dbo.WMS_OutboundPickBatch b
INNER JOIN dbo.WMS_OutboundPickBatchDetail d ON d.PickBatchId=b.Id
INNER JOIN dbo.WMS_OutboundPickDetailItem pdi ON pdi.PickDetailId=d.Id
INNER JOIN dbo.WMS_OutboundOrderLine l ON l.Id=pdi.OutboundOrderLineId
INNER JOIN dbo.WMS_HU box ON box.Id=d.BoxHUId AND box.HUType='OUTER_BOX'
WHERE b.AllocationId=@AllocationId AND b.BatchStatus='PICKED'
  AND d.DetailStatus='PICKED' AND d.EffectiveQty>0 AND l.LineStatus='PICKED'
ORDER BY l.SAPItemNo,d.Id,pdi.SAPBatchNo,pdi.Id;"))
            {
                AddLong(command, "@AllocationId", allocationId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new PickedItemRow
                        {
                            PickDetailId = reader.GetInt64(0),
                            BoxHuId = reader.GetInt64(1),
                            EffectiveQty = reader.GetDecimal(2),
                            BoxHuCode = reader.GetString(3),
                            OrderLineId = reader.GetInt64(4),
                            SAPItemNo = reader.GetString(5),
                            LineMaterialCode = reader.GetString(6),
                            SAPStorageLocation = reader.IsDBNull(7) ? null : reader.GetString(7),
                            ItemMaterialCode = reader.GetString(8),
                            SAPBatchNo = reader.IsDBNull(9) ? null : reader.GetString(9),
                            AllocatedQty = reader.GetDecimal(10)
                        });
                    }
                }
            }
            return rows;
        }

        private static string TryBuildPreview(OrderSnapshot order, List<PickedItemRow> source, out SapOutboundPostingPreviewDto preview)
        {
            preview = null;
            if (source.Count == 0)
                return "没有可用于SAP过账的PICKED PickDetailItem。";
            foreach (PickedItemRow row in source)
            {
                if (!string.Equals(row.LineMaterialCode, row.ItemMaterialCode, StringComparison.Ordinal))
                    return "SAP_POSTING_MATERIAL_CONFLICT: POSNR[" + row.SAPItemNo + "]物料与拣货快照不一致。";
                if (string.IsNullOrWhiteSpace(row.SAPStorageLocation))
                    return "SAP_POSTING_LGORT_MISSING: POSNR[" + row.SAPItemNo + "]SAPStorageLocation为空。";
                if (string.IsNullOrWhiteSpace(row.SAPBatchNo))
                    return "SAP_POSTING_CHARG_MISSING: 箱[" + row.BoxHuCode + "]缺少SAPBatchNo。";
            }
            foreach (IGrouping<long, PickedItemRow> detail in source.GroupBy(x => x.PickDetailId))
            {
                PickedItemRow first = detail.First();
                decimal itemTotal = detail.Sum(x => x.AllocatedQty);
                if (itemTotal != first.EffectiveQty)
                    return "SAP_POSTING_BOX_QTY_CONFLICT: 箱[" + first.BoxHuCode + "]LFIMG_BOX与PickDetailItem总数不一致。";
            }
            List<SapOutboundPostingRow> data = source
                .GroupBy(x => new { x.PickDetailId, x.OrderLineId, x.SAPBatchNo })
                .Select(group =>
                {
                    PickedItemRow first = group.First();
                    decimal lineQuantity = group.Sum(x => x.AllocatedQty);
                    return new SapOutboundPostingRow
                    {
                        VBELN = order.SAPDeliveryNo,
                        POSNR = first.SAPItemNo,
                        LGORT = first.SAPStorageLocation,
                        BOXNO = first.BoxHuCode,
                        MATNR = first.LineMaterialCode,
                        CHARG = first.SAPBatchNo,
                        LFIMG = lineQuantity,
                        LFIMG_BOX = lineQuantity,
                        NTGEW = 0m,
                        BRGEW = 0m,
                        GEWEI = "KG",
                        VRKME = string.Empty,
                        BOXSIZE = string.Empty
                    };
                })
                .OrderBy(x => x.POSNR, StringComparer.Ordinal)
                .ThenBy(x => x.BOXNO, StringComparer.Ordinal)
                .ThenBy(x => x.CHARG, StringComparer.Ordinal)
                .ToList();
            SapOutboundPostingPayload payload = new SapOutboundPostingPayload { Data = data };
            string json = SapOutboundPayloadSerializer.Serialize(payload);
            preview = new SapOutboundPostingPreviewDto
            {
                SAPDeliveryNo = order.SAPDeliveryNo,
                PayloadJson = json,
                PayloadHash = SapOutboundPayloadSerializer.ComputeHash(json),
                RowCount = data.Count,
                BoxCount = data.Select(x => x.BOXNO).Distinct(StringComparer.Ordinal).Count(),
                TotalQty = data.Sum(x => x.LFIMG),
                Data = data,
                Validations = new List<string> { "DETAIL_TOTAL_VALID", "BOX_TOTAL_VALID", "VBELN_TOTAL_VALID" }
            };
            return null;
        }

        private static long InsertPosting(SqlConnection connection, SqlTransaction transaction, OrderSnapshot order, SapOutboundPostingControlRequest request, SapOutboundPostingPreviewDto preview)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT INTO dbo.WMS_OutboundSapPosting(OutboundOrderId,SAPDeliveryNo,RequestId,CurrentAttemptNo,PostingStatus,PayloadJson,PayloadHash,RetryCount,CreateUser,CreateTime)
OUTPUT INSERTED.Id VALUES(@OrderId,@SAPDeliveryNo,@RequestId,0,'READY',@PayloadJson,@PayloadHash,0,@Operator,@Now);"))
            {
                AddLong(command, "@OrderId", order.Id);
                AddString(command, "@SAPDeliveryNo", 30, order.SAPDeliveryNo);
                AddString(command, "@RequestId", 100, request.RequestId);
                AddNVarCharMax(command, "@PayloadJson", preview.PayloadJson);
                AddString(command, "@PayloadHash", 64, preview.PayloadHash);
                AddString(command, "@Operator", 50, request.Operator);
                AddDateTime(command, "@Now", DateTime.Now);
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static void InsertDetails(SqlConnection connection, SqlTransaction transaction, long postingId, List<SapOutboundPostingRow> rows)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                SapOutboundPostingRow row = rows[index];
                using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT INTO dbo.WMS_OutboundSapPostingDetail(PostingId,LineNo,VBELN,POSNR,LGORT,MATNR,BOXNO,CHARG,LFIMG,LFIMG_BOX,NTGEW,BRGEW,GEWEI,VRKME,BOXSIZE)
VALUES(@PostingId,@LineNo,@VBELN,@POSNR,@LGORT,@MATNR,@BOXNO,@CHARG,@LFIMG,@LFIMG_BOX,@NTGEW,@BRGEW,@GEWEI,@VRKME,@BOXSIZE);"))
                {
                    AddLong(command, "@PostingId", postingId);
                    AddInt(command, "@LineNo", index + 1);
                    AddString(command, "@VBELN", 30, row.VBELN);
                    AddString(command, "@POSNR", 20, row.POSNR);
                    AddString(command, "@LGORT", 20, row.LGORT);
                    AddString(command, "@MATNR", 50, row.MATNR);
                    AddString(command, "@BOXNO", 100, row.BOXNO);
                    AddString(command, "@CHARG", 100, row.CHARG);
                    AddDecimal(command, "@LFIMG", row.LFIMG);
                    AddDecimal(command, "@LFIMG_BOX", row.LFIMG_BOX);
                    AddDecimal(command, "@NTGEW", row.NTGEW);
                    AddDecimal(command, "@BRGEW", row.BRGEW);
                    AddString(command, "@GEWEI", 10, row.GEWEI);
                    AddString(command, "@VRKME", 20, row.VRKME);
                    AddString(command, "@BOXSIZE", 100, row.BOXSIZE);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void UpdateSendingClaim(SqlConnection connection, SqlTransaction transaction, long postingId, SapOutboundPostingControlRequest request, int attemptNo, bool retry, DateTime now)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.WMS_OutboundSapPosting
SET PostingStatus='SENDING',RequestId=@RequestId,CurrentAttemptNo=@AttemptNo,
    FirstSendTime=ISNULL(FirstSendTime,@Now),LastSendTime=@Now,
    RetryCount=RetryCount+@RetryIncrement,ModifyUser=@Operator,ModifyTime=@Now
WHERE Id=@Id AND PostingStatus=@RequiredStatus;"))
            {
                AddString(command, "@RequestId", 100, request.RequestId);
                AddInt(command, "@AttemptNo", attemptNo);
                AddDateTime(command, "@Now", now);
                AddInt(command, "@RetryIncrement", retry ? 1 : 0);
                AddString(command, "@Operator", 50, request.Operator);
                AddLong(command, "@Id", postingId);
                AddString(command, "@RequiredStatus", 20, retry ? "FAILED" : "READY");
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("SAP过账发送claim发生并发冲突。");
            }
        }

        private static long InsertAttemptLog(SqlConnection connection, SqlTransaction transaction, string deliveryNo, string requestId, int attemptNo, string payloadJson, string payloadHash, DateTime now)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
INSERT INTO dbo.WMS_SapIntegrationLog(InterfaceCode,Direction,BusinessType,BusinessKey,RequestId,CorrelationId,AttemptNo,RequestTime,RequestJson,ResultStatus,PayloadHash)
OUTPUT INSERTED.Id VALUES('S0089','OUTBOUND','OUTBOUND_SHIPMENT',@BusinessKey,@RequestId,@BusinessKey,@AttemptNo,@Now,@RequestJson,'SENDING',@PayloadHash);"))
            {
                AddString(command, "@BusinessKey", 100, deliveryNo);
                AddString(command, "@RequestId", 100, requestId);
                AddInt(command, "@AttemptNo", attemptNo);
                AddDateTime(command, "@Now", now);
                AddNVarCharMax(command, "@RequestJson", payloadJson);
                AddString(command, "@PayloadHash", 64, payloadHash);
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }

        private static void FinalizeShipment(SqlConnection connection, SqlTransaction transaction, string deliveryNo, string requestId, string operatorValue, string remark, DateTime now)
        {
            OrderSnapshot order = ReadOrderSnapshot(connection, transaction, deliveryNo, true);
            if (order == null || order.OrderStatus != "PICKED" || order.AllocationId <= 0)
                throw new InvalidOperationException("订单不再满足PICKED出货完成条件。");
            string validation = ValidateSnapshot(connection, transaction, order);
            if (validation != null)
                throw new InvalidOperationException(validation);
            long shippedLocationId;
            using (SqlCommand location = CreateCommand(connection, transaction, @"
SELECT TOP 1 Id FROM dbo.WMS_Location WHERE WarehouseId=@WarehouseId AND LocationCode='SHIPPED' AND IsVirtual=1 AND IsScannable=0 AND MasterStatus='ACTIVE';"))
            {
                AddLong(location, "@WarehouseId", order.WarehouseId);
                object value = location.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("当前仓库未配置正式SHIPPED虚拟货位。");
                shippedLocationId = Convert.ToInt64(value);
            }
            List<ShipDetail> details = new List<ShipDetail>();
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT d.Id,d.BoxHUId,cl.LocationId,loc.LocationCode
FROM dbo.WMS_OutboundPickBatchDetail d WITH(UPDLOCK,HOLDLOCK,ROWLOCK)
INNER JOIN dbo.WMS_OutboundPickBatch b ON b.Id=d.PickBatchId
INNER JOIN dbo.WMS_HU_CurrentLocation cl WITH(UPDLOCK,HOLDLOCK,ROWLOCK) ON cl.HUId=d.BoxHUId
INNER JOIN dbo.WMS_Location loc ON loc.Id=cl.LocationId
WHERE b.AllocationId=@AllocationId AND d.DetailStatus='PICKED' AND d.EffectiveQty>0 ORDER BY d.Id;"))
            {
                AddLong(command, "@AllocationId", order.AllocationId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        details.Add(new ShipDetail { Id = reader.GetInt64(0), BoxHuId = reader.GetInt64(1), LocationId = reader.GetInt64(2), LocationCode = reader.GetString(3) });
                }
            }
            if (details.Count == 0 || details.Any(x => x.LocationCode != "OUTBOUND-STAGING"))
                throw new InvalidOperationException("有效拣货箱不全在OUTBOUND-STAGING，拒绝SHIPPED。");
            string token = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            for (int i = 0; i < details.Count; i++)
            {
                ShipDetail detail = details[i];
                string childRequestId = requestId + "/S" + (i + 1).ToString("D3");
                string moveNo = "SM" + now.ToString("yyyyMMddHHmmss") + token + (i + 1).ToString("D3");
                string operationNo = "SO" + now.ToString("yyyyMMddHHmmss") + token + (i + 1).ToString("D3");
                HuMoveLogWriter.HuMoveLogWriteResult move = HuMoveLogWriter.InsertWithTime(
                    connection, transaction, moveNo, detail.BoxHuId, detail.LocationId, shippedLocationId,
                    "OUTBOUND_SHIP", null, deliveryNo, null, childRequestId, null, operatorValue, remark);
                using (SqlCommand update = CreateCommand(connection, transaction, @"
UPDATE dbo.WMS_HU_CurrentLocation SET LocationId=@ToLocationId,LocationSince=@Now,LastMoveLogId=@MoveLogId,LastMoveNo=@MoveNo,LastMoveType='OUTBOUND_SHIP',LastOperator=@Operator,ModifyTime=@Now WHERE HUId=@HUId AND LocationId=@FromLocationId;
UPDATE dbo.WMS_OutboundPickBatchDetail SET DetailStatus='SHIPPED' WHERE Id=@DetailId AND DetailStatus='PICKED';"))
                {
                    AddLong(update, "@ToLocationId", shippedLocationId);
                    AddDateTime(update, "@Now", move.MoveTime);
                    AddLong(update, "@MoveLogId", move.Id);
                    AddString(update, "@MoveNo", 50, moveNo);
                    AddString(update, "@Operator", 50, operatorValue);
                    AddLong(update, "@HUId", detail.BoxHuId);
                    AddLong(update, "@FromLocationId", detail.LocationId);
                    AddLong(update, "@DetailId", detail.Id);
                    if (update.ExecuteNonQuery() < 2)
                        throw new InvalidOperationException("SHIPPED事务内更新箱位置或Detail失败。");
                }
                HuOperationLogWriter.Insert(connection, transaction, operationNo, "OUTBOUND_SHIP", detail.BoxHuId,
                    null, deliveryNo, childRequestId, operatorValue, move.MoveTime, null, remark);
            }
            using (SqlCommand command = CreateCommand(connection, transaction, @"
UPDATE dbo.WMS_OutboundPickBatch SET BatchStatus='SHIPPED' WHERE AllocationId=@AllocationId AND BatchStatus='PICKED';
UPDATE dbo.WMS_OutboundOrderLine SET ShippedQty=EffectivePickedQty,LineStatus='SHIPPED',ModifyTime=@Now WHERE OutboundOrderId=@OrderId AND LineStatus='PICKED';
UPDATE dbo.WMS_OutboundOrder SET OrderStatus='SHIPPED',ShippedTime=@Now,ShipRequestId=@RequestId,ModifyUser=@Operator,ModifyTime=@Now WHERE Id=@OrderId AND OrderStatus='PICKED';"))
            {
                AddLong(command, "@AllocationId", order.AllocationId);
                AddLong(command, "@OrderId", order.Id);
                AddDateTime(command, "@Now", now);
                AddString(command, "@RequestId", 100, requestId);
                AddString(command, "@Operator", 50, operatorValue);
                command.ExecuteNonQuery();
            }
        }

        private static PostingRow ReadPosting(SqlConnection connection, SqlTransaction transaction, string deliveryNo, bool lockRow)
        {
            string lockHint = lockRow ? " WITH(UPDLOCK,HOLDLOCK,ROWLOCK)" : string.Empty;
            using (SqlCommand command = CreateCommand(connection, transaction, "SELECT TOP 1 Id,SAPDeliveryNo,RequestId,CurrentAttemptNo,PostingStatus,PayloadJson,PayloadHash,ManualConfirmRemark,SapCode FROM dbo.WMS_OutboundSapPosting" + lockHint + " WHERE SAPDeliveryNo=@SAPDeliveryNo"))
            {
                AddString(command, "@SAPDeliveryNo", 30, deliveryNo);
                using (SqlDataReader reader = command.ExecuteReader())
                    return reader.Read() ? MapPosting(reader) : null;
            }
        }

        private static PostingRow ReadPostingById(SqlConnection connection, SqlTransaction transaction, long id, bool lockRow)
        {
            string lockHint = lockRow ? " WITH(UPDLOCK,HOLDLOCK,ROWLOCK)" : string.Empty;
            using (SqlCommand command = CreateCommand(connection, transaction, "SELECT TOP 1 Id,SAPDeliveryNo,RequestId,CurrentAttemptNo,PostingStatus,PayloadJson,PayloadHash,ManualConfirmRemark,SapCode FROM dbo.WMS_OutboundSapPosting" + lockHint + " WHERE Id=@Id"))
            {
                AddLong(command, "@Id", id);
                using (SqlDataReader reader = command.ExecuteReader())
                    return reader.Read() ? MapPosting(reader) : null;
            }
        }

        private static PostingRow MapPosting(SqlDataReader reader)
        {
            return new PostingRow
            {
                Id = reader.GetInt64(0), SAPDeliveryNo = reader.GetString(1), RequestId = reader.GetString(2),
                CurrentAttemptNo = reader.GetInt32(3), PostingStatus = reader.GetString(4), PayloadJson = reader.GetString(5),
                PayloadHash = reader.GetString(6), ManualConfirmRemark = reader.IsDBNull(7) ? null : reader.GetString(7),
                SapCode = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
        }

        private static OutboundShippingStateDto ReadShippingState(SqlConnection connection, SqlTransaction transaction, string deliveryNo)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT o.SAPDeliveryNo,o.OrderStatus,ISNULL(p.PostingStatus,'READY') PostingStatus,p.SapCode,p.SapMessage,p.SuccessConfirmSource,p.RequestId,
 COUNT(DISTINCT CASE WHEN d.DetailStatus IN('PICKED','SHIPPED') AND d.EffectiveQty>0 THEN d.BoxHUId END) PickedBoxCount,
 ISNULL(SUM(CASE WHEN d.DetailStatus IN('PICKED','SHIPPED') AND d.EffectiveQty>0 THEN d.EffectiveQty ELSE 0 END),0) PickedQty,
 CASE WHEN p.PostingStatus='FAILED' AND (p.ManualConfirmRemark IS NOT NULL OR p.SapCode='LOCAL_NOT_INVOKED') THEN CONVERT(bit,1) ELSE CONVERT(bit,0) END CanRetry
FROM dbo.WMS_OutboundOrder o
LEFT JOIN dbo.WMS_OutboundSapPosting p ON p.OutboundOrderId=o.Id
LEFT JOIN dbo.WMS_OutboundAllocation a ON a.OutboundOrderId=o.Id AND a.AllocationStatus='COMPLETED'
LEFT JOIN dbo.WMS_OutboundPickBatch b ON b.AllocationId=a.Id
LEFT JOIN dbo.WMS_OutboundPickBatchDetail d ON d.PickBatchId=b.Id
WHERE o.SAPDeliveryNo=@SAPDeliveryNo
GROUP BY o.SAPDeliveryNo,o.OrderStatus,p.PostingStatus,p.SapCode,p.SapMessage,p.SuccessConfirmSource,p.RequestId,p.ManualConfirmRemark;"))
            {
                AddString(command, "@SAPDeliveryNo", 30, deliveryNo);
                using (SqlDataReader reader = command.ExecuteReader())
                    return reader.Read() ? MapState(reader) : null;
            }
        }

        private static OutboundShippingStateDto MapState(SqlDataReader reader)
        {
            string outboundStatus = reader.GetString(1);
            string postingStatus = reader.GetString(2);
            return new OutboundShippingStateDto
            {
                SAPDeliveryNo = reader.GetString(0), OutboundStatus = outboundStatus, SapPostingStatus = postingStatus,
                LastSapCode = reader.IsDBNull(3) ? null : reader.GetString(3), LastSapMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                SuccessConfirmSource = reader.IsDBNull(5) ? null : reader.GetString(5), RequestId = reader.IsDBNull(6) ? null : reader.GetString(6),
                PickedBoxCount = reader.GetInt32(7), PickedQty = reader.GetDecimal(8),
                CanConfirmShipment = outboundStatus == "PICKED" && postingStatus == SapOutboundPostingStatuses.Ready,
                CanRetry = outboundStatus == "PICKED" && reader.GetBoolean(9),
                RequiresITReview = postingStatus == SapOutboundPostingStatuses.Unknown
            };
        }

        private static bool IsRequestIdUsed(SqlConnection connection, SqlTransaction transaction, string requestId)
        {
            using (SqlCommand command = CreateCommand(connection, transaction, @"
SELECT CASE WHEN
 EXISTS(SELECT 1 FROM dbo.WMS_HU_MoveLog WHERE RequestId=@RequestId) OR EXISTS(SELECT 1 FROM dbo.WMS_HU_OperationLog WHERE RequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_OutboundPickBatch WHERE RequestId=@RequestId) OR EXISTS(SELECT 1 FROM dbo.WMS_OutboundOrderReceiveLog WHERE RequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_OutboundPickException WHERE RequestId=@RequestId OR ReplacementRequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_HU_HoldLog WHERE HoldRequestId=@RequestId OR ReleaseRequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_OutboundAllocation WHERE RequestId=@RequestId OR ReleaseRequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_OutboundOrder WHERE ReceivedRequestId=@RequestId OR VoidRequestId=@RequestId OR ShipRequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_OutboundSapPosting WHERE RequestId=@RequestId)
 OR EXISTS(SELECT 1 FROM dbo.WMS_SapIntegrationLog WHERE RequestId=@RequestId)
 THEN 1 ELSE 0 END;"))
            {
                AddString(command, "@RequestId", 100, requestId);
                return Convert.ToInt32(command.ExecuteScalar()) != 0;
            }
        }

        private static SapIntegrationLogDto MapLog(SqlDataReader reader)
        {
            return new SapIntegrationLogDto
            {
                Id=reader.GetInt64(0),InterfaceCode=reader.GetString(1),Direction=reader.GetString(2),BusinessType=reader.GetString(3),BusinessKey=reader.IsDBNull(4)?null:reader.GetString(4),
                RequestId=reader.IsDBNull(5)?null:reader.GetString(5),CorrelationId=reader.IsDBNull(6)?null:reader.GetString(6),AttemptNo=reader.GetInt32(7),RequestTime=reader.GetDateTime(8),
                ResponseTime=reader.IsDBNull(9)?(DateTime?)null:reader.GetDateTime(9),DurationMs=reader.IsDBNull(10)?(long?)null:reader.GetInt64(10),RequestJson=reader.IsDBNull(11)?null:reader.GetString(11),
                ResponseJson=reader.IsDBNull(12)?null:reader.GetString(12),ResultStatus=reader.GetString(13),ExternalCode=reader.IsDBNull(14)?null:reader.GetString(14),ExternalMessage=reader.IsDBNull(15)?null:reader.GetString(15),
                HttpStatusCode=reader.IsDBNull(16)?(int?)null:reader.GetInt32(16),ExceptionType=reader.IsDBNull(17)?null:reader.GetString(17),ExceptionMessage=reader.IsDBNull(18)?null:reader.GetString(18),PayloadHash=reader.IsDBNull(19)?null:reader.GetString(19)
            };
        }

        private static void AddLogFilters(SqlCommand command, SapIntegrationLogQuery query)
        {
            AddString(command,"@BusinessKey",100,query.BusinessKey);AddString(command,"@InterfaceCode",50,query.InterfaceCode);
            AddString(command,"@Direction",20,query.Direction);AddString(command,"@ResultStatus",20,query.ResultStatus);
        }

        private static string NormalizeTransportStatus(string status)
        {
            if (status == SapOutboundPostingStatuses.Success || status == SapOutboundPostingStatuses.Failed)
                return status;
            return SapOutboundPostingStatuses.Unknown;
        }

        private static string NormalizeLogStatus(string status)
        {
            if (status == "SUCCESS" || status == "FAILED" || status == "DUPLICATE" || status == "REJECTED") return status;
            return "REJECTED";
        }

        private static SapPostingSnapshotBuildResult BuildFailure(string message) { return new SapPostingSnapshotBuildResult { IsSuccess=false,Message=message }; }
        private static SapPostingClaimResult RollbackClaim(SqlTransaction transaction, string message) { transaction.Rollback(); return new SapPostingClaimResult { IsSuccess=false,Message=message }; }
        private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string sql) { return new SqlCommand(sql, connection, transaction); }
        private static void AddString(SqlCommand c,string n,int s,string v){c.Parameters.Add(n,SqlDbType.VarChar,s).Value=(object)v??DBNull.Value;}
        private static void AddNVarChar(SqlCommand c,string n,int s,string v){c.Parameters.Add(n,SqlDbType.NVarChar,s).Value=(object)v??DBNull.Value;}
        private static void AddNVarCharMax(SqlCommand c,string n,string v){c.Parameters.Add(n,SqlDbType.NVarChar,-1).Value=(object)v??DBNull.Value;}
        private static void AddLong(SqlCommand c,string n,long v){c.Parameters.Add(n,SqlDbType.BigInt).Value=v;}
        private static void AddInt(SqlCommand c,string n,int v){c.Parameters.Add(n,SqlDbType.Int).Value=v;}
        private static void AddIntNullable(SqlCommand c,string n,int? v){c.Parameters.Add(n,SqlDbType.Int).Value=v.HasValue?(object)v.Value:DBNull.Value;}
        private static void AddDateTime(SqlCommand c,string n,DateTime v){c.Parameters.Add(n,SqlDbType.DateTime2).Value=v;}
        private static void AddDateTimeNullable(SqlCommand c,string n,DateTime? v){c.Parameters.Add(n,SqlDbType.DateTime2).Value=v.HasValue?(object)v.Value:DBNull.Value;}
        private static void AddDecimal(SqlCommand c,string n,decimal v){SqlParameter p=c.Parameters.Add(n,SqlDbType.Decimal);p.Precision=18;p.Scale=4;p.Value=v;}

        private class OrderSnapshot { public long Id; public string SAPDeliveryNo; public long WarehouseId; public string OrderStatus; public long AllocationId; }
        private class PickedItemRow { public long PickDetailId; public long BoxHuId; public decimal EffectiveQty; public string BoxHuCode; public long OrderLineId; public string SAPItemNo; public string LineMaterialCode; public string SAPStorageLocation; public string ItemMaterialCode; public string SAPBatchNo; public decimal AllocatedQty; }
        private class PostingRow { public long Id; public string SAPDeliveryNo; public string RequestId; public int CurrentAttemptNo; public string PostingStatus; public string PayloadJson; public string PayloadHash; public string ManualConfirmRemark; public string SapCode; }
        private class ShipDetail { public long Id; public long BoxHuId; public long LocationId; public string LocationCode; }
    }
}
