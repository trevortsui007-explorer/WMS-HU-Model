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
                OrderRow order = ReadOrder(connection, null, sapDeliveryNo, false);
                if (order == null) return Fail("出库订单不存在。");
                string error = Validate(connection, null, order);
                if (error != null) return Fail(error);
                return Build(order, ReadItems(connection, null, order.AllocationId));
            }
        }

        public SapPostingClaimResult ClaimInitial(string sapDeliveryNo, SapOutboundPostingControlRequest request, SapOutboundPostingPreviewDto ignoredPreview)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        OrderRow order = ReadOrder(connection, transaction, sapDeliveryNo, true);
                        if (order == null) return Rollback(transaction, "出库订单不存在。");
                        PostingRow existing = ReadPosting(connection, transaction, order.Id, true);
                        if (existing != null)
                        {
                            if (existing.Status == SapOutboundPostingStatuses.Sending) return Rollback(transaction, "SAP过账正在发送，禁止创建第二个Attempt。");
                            return Rollback(transaction, existing.Status == SapOutboundPostingStatuses.Success ? "该SAP交货单已经过账成功。" : "该SAP交货单已经存在过账记录，请使用Retry流程。");
                        }
                        string error = Validate(connection, transaction, order);
                        if (error != null) return Rollback(transaction, error);
                        SapPostingSnapshotBuildResult snapshot = Build(order, ReadItems(connection, transaction, order.AllocationId));
                        if (!snapshot.IsSuccess) return Rollback(transaction, snapshot.Message);
                        DateTime now = DateTime.Now;
                        long postingId = InsertPosting(connection, transaction, order.Id, snapshot.Preview.PayloadHash, now);
                        InsertDetails(connection, transaction, postingId, ReadItems(connection, transaction, order.AllocationId));
                        long logId = InsertAttempt(connection, transaction, postingId, order.SAPDeliveryNo, 1, snapshot.Preview.PayloadJson, now);
                        SetSending(connection, transaction, postingId, logId, now, SapOutboundPostingStatuses.Sending);
                        transaction.Commit();
                        return Claim(postingId, logId, 1, snapshot.Preview);
                    }
                    catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { if (transaction.Connection != null) transaction.Rollback(); return new SapPostingClaimResult { IsSuccess = false, Message = "SAP过账Attempt发生并发冲突，请查询当前状态。" }; }
                    catch { if (transaction.Connection != null) transaction.Rollback(); throw; }
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
                        OrderRow order = ReadOrder(connection, transaction, sapDeliveryNo, true);
                        if (order == null) return Rollback(transaction, "出库订单不存在。");
                        PostingRow posting = ReadPosting(connection, transaction, order.Id, true);
                        if (posting == null || posting.Status != SapOutboundPostingStatuses.Failed) return Rollback(transaction, "只有FAILED过账记录允许Retry；UNKNOWN禁止盲目重试。");
                        SapPostingSnapshotBuildResult current = Build(order, ReadItems(connection, transaction, order.AllocationId));
                        if (!current.IsSuccess) return Rollback(transaction, current.Message);
                        if (!string.Equals(current.Preview.PayloadHash, posting.PayloadHash, StringComparison.Ordinal)) return Rollback(transaction, "PAYLOAD_CHANGED：当前Payload与第一次过账快照不一致。");
                        SapOutboundPostingPreviewDto frozen = ReadFrozenPayload(connection, transaction, posting.Id, order.SAPDeliveryNo);
                        if (frozen == null || frozen.PayloadHash != posting.PayloadHash) return Rollback(transaction, "PAYLOAD_CHANGED：当前冻结快照Hash不匹配。");
                        DateTime now = DateTime.Now;
                        int attemptNo = NextAttempt(connection, transaction, posting.Id);
                        long logId = InsertAttempt(connection, transaction, posting.Id, order.SAPDeliveryNo, attemptNo, frozen.PayloadJson, now);
                        SetSending(connection, transaction, posting.Id, logId, now, SapOutboundPostingStatuses.Failed);
                        transaction.Commit();
                        return Claim(posting.Id, logId, attemptNo, frozen);
                    }
                    catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) { if (transaction.Connection != null) transaction.Rollback(); return new SapPostingClaimResult { IsSuccess = false, Message = "SAP过账Attempt发生并发冲突，请查询当前状态。" }; }
                    catch { if (transaction.Connection != null) transaction.Rollback(); throw; }
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
                        AttemptRow attempt = ReadAttempt(connection, transaction, postingId, attemptNo, true);
                        if (posting == null || attempt == null || posting.Status != SapOutboundPostingStatuses.Sending || posting.LastLogId != attempt.Id) throw new InvalidOperationException("SAP过账Attempt已失去SENDING所有权。");
                        string status = transport != null && transport.InvocationStarted ? Normalize(transport.ResultStatus) : SapOutboundPostingStatuses.Failed;
                        DateTime now = DateTime.Now;
                        UpdateAttempt(connection, transaction, attempt.Id, status, transport, now);
                        UpdatePosting(connection, transaction, posting.Id, status, attempt.Id, transport, now);
                        if (status == SapOutboundPostingStatuses.Success) FinalizeShipment(connection, transaction, posting.OrderId, operatorValue, remark, now);
                        OutboundShippingStateDto state = ReadState(connection, transaction, posting.OrderId);
                        transaction.Commit();
                        return state;
                    }
                    catch { if (transaction.Connection != null) transaction.Rollback(); throw; }
                }
            }
        }

        public OutboundShippingStateDto GetShippingState(string sapDeliveryNo)
        {
            using (SqlConnection connection = DbConnectionFactory.Create()) { connection.Open(); OrderRow order = ReadOrder(connection, null, sapDeliveryNo, false); return order == null ? null : ReadState(connection, null, order.Id); }
        }
        public OutboundShippingStateDto ManualConfirmPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request) { throw new InvalidOperationException("UNKNOWN必须经SAP权威状态查询确认；人工确认接口已弃用。"); }
        public OutboundShippingStateDto ManualConfirmNotPosted(string sapDeliveryNo, SapOutboundManualConfirmRequest request) { throw new InvalidOperationException("UNKNOWN必须经SAP权威状态查询确认；人工确认接口已弃用。"); }
        public PagedResult<OutboundShippingStateDto> QueryShippingOrders(string sapDeliveryNo, string outboundStatus, string postingStatus, int page, int pageSize) { return new PagedResult<OutboundShippingStateDto> { Page = page, PageSize = pageSize }; }
        public ShipmentManagementResultDto QueryShipmentManagement(ShipmentManagementQuery query)
        {
            const string fromSql = @"
FROM dbo.WMS_FG_OutboundOrder o
INNER JOIN dbo.WMS_FG_Warehouse w ON w.Id=o.WarehouseId
LEFT JOIN dbo.WMS_FG_OutboundSapPosting p ON p.OutboundOrderId=o.Id";
            const string filterSql = @"
WHERE (@Delivery IS NULL OR o.SAPDeliveryNo LIKE '%' + @Delivery + '%')
  AND (@Customer IS NULL OR o.CustomerCode LIKE '%' + @Customer + '%')
  AND (@Material IS NULL OR EXISTS
      (SELECT 1 FROM dbo.WMS_FG_OutboundOrderLine fl WHERE fl.OutboundOrderId=o.Id AND fl.ProdNo LIKE '%' + @Material + '%'))
  AND (@Plant IS NULL OR o.PlantCode=@Plant)
  AND (@Warehouse IS NULL OR w.WarehouseCode=@Warehouse)
  AND (@SapPosting IS NULL OR p.PostingStatus=@SapPosting)
  AND (@DateFrom IS NULL OR o.PlannedShipDate>=@DateFrom)
  AND (@DateTo IS NULL OR o.PlannedShipDate<DATEADD(day,1,@DateTo))";
            const string businessFilterSql = @"
  AND (@Business IS NULL
    OR (@Business='WAIT_PICK' AND o.OrderStatus='WAIT_PICK')
    OR (@Business='PICKING' AND o.OrderStatus IN ('ALLOCATED','PICKING'))
    OR (@Business='WAIT_CONFIRM' AND o.OrderStatus='PICKED' AND (p.PostingStatus IS NULL OR p.PostingStatus='READY'))
    OR (@Business='SAP_EXCEPTION' AND p.PostingStatus IN ('FAILED','UNKNOWN'))
    OR (@Business='SHIPPED' AND (o.OrderStatus='SHIPPED' OR p.PostingStatus='SUCCESS')))";
            const string selectSql = @"
SELECT o.Id,o.SAPDeliveryNo,o.PlantCode,w.WarehouseCode,o.CustomerCode,
       ISNULL(lineFacts.LineCount,0),ISNULL(lineFacts.RequiredQty,0),
       ISNULL(pickFacts.PickedQty,0),ISNULL(pickFacts.PickedBoxCount,0),
       o.OrderStatus,p.PostingStatus,p.ResultCode,p.ResultMessage,
       COALESCE(p.SuccessDt,p.LastAttemptDt,o.ShippedDt,o.PickedDt,o.StartPickDt,o.LastEditDt,o.CreateDt)
";
            const string factsSql = @"
OUTER APPLY
(
    SELECT COUNT(1) AS LineCount,SUM(l.EffectiveRequiredQty) AS RequiredQty
    FROM dbo.WMS_FG_OutboundOrderLine l
    WHERE l.OutboundOrderId=o.Id
) lineFacts
OUTER APPLY
(
    SELECT SUM(item.AllocatedQty) AS PickedQty,COUNT(DISTINCT detail.BoxHUId) AS PickedBoxCount
    FROM dbo.WMS_FG_OutboundAllocation allocation
    INNER JOIN dbo.WMS_FG_OutboundPickBatch batch ON batch.AllocationId=allocation.Id
    INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail detail ON detail.PickBatchId=batch.Id
    INNER JOIN dbo.WMS_FG_OutboundPickDetailItem item ON item.PickDetailId=detail.Id
    WHERE allocation.OutboundOrderId=o.Id
      AND detail.DetailStatus IN ('PICKED','SHIPPED')
      AND detail.EffectiveQty>0
) pickFacts";
            const string summarySql = @"
SELECT COUNT(1),
       SUM(CASE WHEN o.OrderStatus='WAIT_PICK' THEN 1 ELSE 0 END),
       SUM(CASE WHEN o.OrderStatus IN ('ALLOCATED','PICKING') THEN 1 ELSE 0 END),
       SUM(CASE WHEN o.OrderStatus='PICKED' AND (p.PostingStatus IS NULL OR p.PostingStatus='READY') THEN 1 ELSE 0 END),
       SUM(CASE WHEN p.PostingStatus IN ('FAILED','UNKNOWN') THEN 1 ELSE 0 END),
       SUM(CASE WHEN o.OrderStatus='SHIPPED' OR p.PostingStatus='SUCCESS' THEN 1 ELSE 0 END)
";

            ShipmentManagementResultDto result = new ShipmentManagementResultDto { Page = query.Page, PageSize = query.PageSize };
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                using (SqlCommand summary = new SqlCommand(summarySql + fromSql + filterSql, connection))
                {
                    AddShipmentQuery(summary, query);
                    using (SqlDataReader reader = summary.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result.Summary.All = reader.GetInt32(0);
                            result.Summary.WaitPick = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            result.Summary.Picking = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                            result.Summary.WaitConfirm = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                            result.Summary.SapException = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                            result.Summary.Shipped = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                        }
                    }
                }
                using (SqlCommand count = new SqlCommand("SELECT COUNT(1)" + fromSql + filterSql + businessFilterSql, connection))
                {
                    AddShipmentQuery(count, query);
                    result.Total = Convert.ToInt32(count.ExecuteScalar());
                }
                string rowsSql = selectSql + fromSql + factsSql + filterSql + businessFilterSql
                    + " ORDER BY COALESCE(p.SuccessDt,p.LastAttemptDt,o.ShippedDt,o.PickedDt,o.StartPickDt,o.LastEditDt,o.CreateDt) DESC,o.SAPDeliveryNo DESC"
                    + " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
                using (SqlCommand command = new SqlCommand(rowsSql, connection))
                {
                    AddShipmentQuery(command, query);
                    I(command, "@Offset", (query.Page - 1) * query.PageSize);
                    I(command, "@PageSize", query.PageSize);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Items.Add(new ShipmentManagementRowDto
                            {
                                OutboundOrderId = reader.GetInt64(0),
                                SapDeliveryNo = reader.GetString(1),
                                PlantCode = reader.GetString(2),
                                WarehouseCode = reader.GetString(3),
                                CustomerCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                                LineCount = reader.GetInt32(5),
                                RequiredQty = reader.GetDecimal(6),
                                PickedQty = reader.GetDecimal(7),
                                PickedBoxCount = reader.GetInt32(8),
                                OutboundStatus = reader.GetString(9),
                                SapPostingStatus = reader.IsDBNull(10) ? null : reader.GetString(10),
                                LastSapCode = reader.IsDBNull(11) ? null : reader.GetString(11),
                                LastSapMessage = reader.IsDBNull(12) ? null : reader.GetString(12),
                                LastOperationTime = reader.IsDBNull(13) ? (DateTime?)null : reader.GetDateTime(13)
                            });
                        }
                    }
                }
            }
            return result;
        }

        public ShipmentManagementDetailDto GetShipmentManagementDetail(string sapDeliveryNo)
        {
            using (SqlConnection connection = DbConnectionFactory.Create())
            {
                connection.Open();
                long orderId;
                string orderStatus;
                using (SqlCommand header = new SqlCommand("SELECT Id,OrderStatus FROM dbo.WMS_FG_OutboundOrder WHERE SAPDeliveryNo=@No", connection))
                {
                    S(header, "@No", 50, sapDeliveryNo);
                    using (SqlDataReader reader = header.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        orderId = reader.GetInt64(0);
                        orderStatus = reader.GetString(1);
                    }
                }

                ShipmentManagementDetailDto detail = new ShipmentManagementDetailDto { SapDeliveryNo = sapDeliveryNo };
                const string lineSql = @"
SELECT line.SAPItemNo,line.ProdNo,CAST(NULL AS VARCHAR(30)) AS MaterialRevision,
       line.RequiredQty,line.GiftQty,line.EffectiveRequiredQty,line.AllocatedQty,
       ISNULL((SELECT SUM(item.AllocatedQty)
               FROM dbo.WMS_FG_OutboundPickDetailItem item
               INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail pickedDetail ON pickedDetail.Id=item.PickDetailId
               WHERE item.OutboundOrderLineId=line.Id
                 AND pickedDetail.DetailStatus IN ('PICKED','SHIPPED')),0) AS PickedQty,
       line.Uom,line.LineStatus
FROM dbo.WMS_FG_OutboundOrderLine line
WHERE line.OutboundOrderId=@Id
ORDER BY line.SAPItemNo,line.Id;";
                using (SqlCommand lines = new SqlCommand(lineSql, connection))
                {
                    L(lines, "@Id", orderId);
                    using (SqlDataReader reader = lines.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            detail.DeliveryLines.Add(new ShipmentDeliveryLineDto
                            {
                                SapItemNo = reader.GetString(0),
                                MaterialCode = reader.GetString(1),
                                MaterialRevision = reader.IsDBNull(2) ? null : reader.GetString(2),
                                RequiredQty = reader.GetDecimal(3),
                                GiftQty = reader.GetDecimal(4),
                                EffectiveRequiredQty = reader.GetDecimal(5),
                                AllocatedQty = reader.GetDecimal(6),
                                PickedQty = reader.GetDecimal(7),
                                Uom = reader.GetString(8),
                                LineStatus = reader.GetString(9)
                            });
                        }
                    }
                }

                const string pickedSql = @"
SELECT detail.Id,batch.PickBatchNo,box.HUCode,source.LocationCode,
       detail.BoxQty,detail.EffectiveQty,detail.Uom,detail.DetailStatus,moveLog.MoveNo,moveLog.MoveDt,
       item.Id,innerHu.HUCode,line.SAPItemNo,item.ProdNo,item.ProdRev,item.DateCode,item.WeekSortKey,
       item.LotNo,item.SAPBatchNo,item.AllocatedQty,item.Uom
FROM dbo.WMS_FG_OutboundPickBatchDetail detail
INNER JOIN dbo.WMS_FG_OutboundPickBatch batch ON batch.Id=detail.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation allocation ON allocation.Id=batch.AllocationId
INNER JOIN dbo.WMS_FG_HU box ON box.Id=detail.BoxHUId
INNER JOIN dbo.WMS_FG_HU_MoveLog moveLog ON moveLog.Id=detail.MoveLogId
LEFT JOIN dbo.WMS_FG_Location source ON source.Id=moveLog.FromLocationId
LEFT JOIN dbo.WMS_FG_OutboundPickDetailItem item ON item.PickDetailId=detail.Id
LEFT JOIN dbo.WMS_FG_HU innerHu ON innerHu.Id=item.InnerHUId
LEFT JOIN dbo.WMS_FG_OutboundOrderLine line ON line.Id=item.OutboundOrderLineId
WHERE allocation.OutboundOrderId=@Id
  AND detail.DetailStatus IN ('PICKED','SHIPPED')
  AND detail.EffectiveQty>0
ORDER BY detail.Id,item.Id;";
                Dictionary<long, ShipmentPickedBoxDto> boxes = new Dictionary<long, ShipmentPickedBoxDto>();
                using (SqlCommand picked = new SqlCommand(pickedSql, connection))
                {
                    L(picked, "@Id", orderId);
                    using (SqlDataReader reader = picked.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long pickDetailId = reader.GetInt64(0);
                            ShipmentPickedBoxDto box;
                            if (!boxes.TryGetValue(pickDetailId, out box))
                            {
                                box = new ShipmentPickedBoxDto
                                {
                                    PickDetailId = pickDetailId,
                                    PickBatchNo = reader.GetString(1),
                                    BoxCode = reader.GetString(2),
                                    SourceLocationCode = reader.IsDBNull(3) ? null : reader.GetString(3),
                                    BoxQty = reader.GetDecimal(4),
                                    EffectiveQty = reader.GetDecimal(5),
                                    Uom = reader.GetString(6),
                                    DetailStatus = reader.GetString(7),
                                    OperationNo = reader.GetString(8),
                                    PickTime = reader.GetDateTime(9)
                                };
                                boxes.Add(pickDetailId, box);
                                detail.PickedBoxes.Add(box);
                            }
                            if (!reader.IsDBNull(10))
                            {
                                box.Items.Add(new ShipmentPickedItemDto
                                {
                                    Id = reader.GetInt64(10),
                                    InnerPackCode = reader.IsDBNull(11) ? null : reader.GetString(11),
                                    SapItemNo = reader.IsDBNull(12) ? null : reader.GetString(12),
                                    MaterialCode = reader.GetString(13),
                                    MaterialRevision = reader.IsDBNull(14) ? null : reader.GetString(14),
                                    DateCode = reader.IsDBNull(15) ? null : reader.GetString(15),
                                    WeekSortKey = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                                    BatchNo = reader.IsDBNull(17) ? null : reader.GetString(17),
                                    SapBatchNo = reader.IsDBNull(18) ? null : reader.GetString(18),
                                    AllocatedQty = reader.GetDecimal(19),
                                    Uom = reader.GetString(20)
                                });
                            }
                        }
                    }
                }

                const string postingSql = @"
SELECT posting.PostingStatus,posting.ResultCode,posting.ResultMessage,
       (SELECT COUNT(1) FROM dbo.WMS_FG_SapIntegrationLog log
        WHERE log.InterfaceCode='S0089' AND log.BizType='OUTBOUND_SAP_POSTING' AND log.BizId=posting.Id),
       posting.LastAttemptDt,posting.SuccessDt
FROM dbo.WMS_FG_OutboundSapPosting posting
WHERE posting.OutboundOrderId=@Id;";
                using (SqlCommand posting = new SqlCommand(postingSql, connection))
                {
                    L(posting, "@Id", orderId);
                    using (SqlDataReader reader = posting.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            detail.Posting = new ShipmentPostingBusinessDto
                            {
                                PostingStatus = reader.GetString(0),
                                ResultCode = reader.IsDBNull(1) ? null : reader.GetString(1),
                                ResultMessage = reader.IsDBNull(2) ? null : reader.GetString(2),
                                AttemptCount = reader.GetInt32(3),
                                LastAttemptTime = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                SuccessTime = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5)
                            };
                        }
                    }
                }
                detail.Posting = detail.Posting ?? new ShipmentPostingBusinessDto();
                detail.Posting.RequiresITReview = detail.Posting.PostingStatus == SapOutboundPostingStatuses.Failed
                    || detail.Posting.PostingStatus == SapOutboundPostingStatuses.Unknown;
                detail.Posting.CanConfirmShipment = orderStatus == "PICKED"
                    && (string.IsNullOrEmpty(detail.Posting.PostingStatus) || detail.Posting.PostingStatus == SapOutboundPostingStatuses.Ready);
                return detail;
            }
        }

        private static void AddShipmentQuery(SqlCommand command, ShipmentManagementQuery query)
        {
            S(command, "@Delivery", 50, query.SapDeliveryNo);
            S(command, "@Customer", 50, query.CustomerCode);
            S(command, "@Material", 50, query.MaterialCode);
            S(command, "@Plant", 20, query.PlantCode);
            S(command, "@Warehouse", 30, query.WarehouseCode);
            S(command, "@Business", 30, query.BusinessStatus);
            S(command, "@SapPosting", 20, query.SapPostingStatus);
            DN(command, "@DateFrom", query.DateFrom);
            DN(command, "@DateTo", query.DateTo);
        }
        public PagedResult<SapIntegrationLogDto> QueryLogs(SapIntegrationLogQuery query)
        {
            PagedResult<SapIntegrationLogDto> result = new PagedResult<SapIntegrationLogDto> { Page = query.Page, PageSize = query.PageSize };
            using (SqlConnection c = DbConnectionFactory.Create())
            {
                c.Open();
                const string where = " WHERE (@B IS NULL OR BizNo LIKE '%' + @B + '%') AND (@I IS NULL OR InterfaceCode=@I) AND (@S IS NULL OR IntegrationStatus=@S) ";
                using (SqlCommand x = new SqlCommand("SELECT COUNT(1) FROM dbo.WMS_FG_SapIntegrationLog" + where, c)) { S(x,"@B",100,query.BusinessKey);S(x,"@I",50,query.InterfaceCode);S(x,"@S",20,query.ResultStatus);result.Total=Convert.ToInt32(x.ExecuteScalar()); }
                using (SqlCommand x = new SqlCommand("SELECT Id,InterfaceCode,BizType,BizId,BizNo,AttemptNo,IntegrationStatus,StartDt,EndDt,DurationMs,NULL,NULL,ResultCode,ResultMessage,HttpStatusCode FROM dbo.WMS_FG_SapIntegrationLog" + where + " ORDER BY StartDt DESC,Id DESC OFFSET @O ROWS FETCH NEXT @P ROWS ONLY", c))
                { S(x,"@B",100,query.BusinessKey);S(x,"@I",50,query.InterfaceCode);S(x,"@S",20,query.ResultStatus);I(x,"@O",(query.Page-1)*query.PageSize);I(x,"@P",query.PageSize);using(SqlDataReader r=x.ExecuteReader())while(r.Read())result.Items.Add(MapLog(r)); }
            }
            return result;
        }
        public SapIntegrationLogDto GetLog(long id)
        {
            using (SqlConnection c=DbConnectionFactory.Create()) { c.Open(); using(SqlCommand x=new SqlCommand("SELECT Id,InterfaceCode,BizType,BizId,BizNo,AttemptNo,IntegrationStatus,StartDt,EndDt,DurationMs,RequestJson,ResponseJson,ResultCode,ResultMessage,HttpStatusCode FROM dbo.WMS_FG_SapIntegrationLog WHERE Id=@I",c)){L(x,"@I",id);using(SqlDataReader r=x.ExecuteReader())return r.Read()?MapLog(r):null;} }
        }
        public void RecordReceiveCommunication(string businessKey, string requestJson, string resultStatus, string externalMessage, string payloadHash) { }

        private static SapIntegrationLogDto MapLog(SqlDataReader r)
        {
            return new SapIntegrationLogDto { Id=r.GetInt64(0),InterfaceCode=r.GetString(1),BusinessType=r.GetString(2),BusinessKey=r.IsDBNull(4)?null:r.GetString(4),AttemptNo=r.GetInt32(5),ResultStatus=r.GetString(6),RequestTime=r.GetDateTime(7),ResponseTime=r.IsDBNull(8)?(DateTime?)null:r.GetDateTime(8),DurationMs=r.IsDBNull(9)?(long?)null:Convert.ToInt64(r.GetInt32(9)),RequestJson=r.IsDBNull(10)?null:r.GetString(10),ResponseJson=r.IsDBNull(11)?null:r.GetString(11),ExternalCode=r.IsDBNull(12)?null:r.GetString(12),ExternalMessage=r.IsDBNull(13)?null:r.GetString(13),HttpStatusCode=r.IsDBNull(14)?(int?)null:r.GetInt32(14) };
        }

        private static SapPostingSnapshotBuildResult Build(OrderRow order, List<ItemRow> items)
        {
            if (items.Count == 0) return Fail("没有可用于SAP过账的PICKED PickDetailItem。");
            if (items.Any(x => string.IsNullOrWhiteSpace(x.SapBatchNo))) return Fail("SAP_POSTING_CHARG_MISSING：PickDetailItem缺少SAPBatchNo。");
            List<SapOutboundPostingRow> rows = items.GroupBy(x => new { x.PickDetailId, x.LineId, x.SapBatchNo }).Select(g => new SapOutboundPostingRow { VBELN = order.SAPDeliveryNo, POSNR = g.First().SapItemNo, LGORT = order.SapStorageLocation, BOXNO = g.First().BoxNo, MATNR = g.First().ProdNo, CHARG = g.Key.SapBatchNo, LFIMG = g.Sum(x => x.Qty), LFIMG_BOX = g.Sum(x => x.Qty), NTGEW = 0m, BRGEW = 0m, GEWEI = "KG", VRKME = string.Empty, BOXSIZE = string.Empty }).OrderBy(x => x.POSNR).ThenBy(x => x.BOXNO).ThenBy(x => x.CHARG).ToList();
            string json = SapOutboundPayloadSerializer.Serialize(new SapOutboundPostingPayload { Data = rows });
            SapOutboundPostingPreviewDto preview = new SapOutboundPostingPreviewDto { SAPDeliveryNo = order.SAPDeliveryNo, Data = rows, PayloadJson = json, PayloadHash = SapOutboundPayloadSerializer.ComputeHash(json), RowCount = rows.Count, BoxCount = rows.Select(x => x.BOXNO).Distinct().Count(), TotalQty = rows.Sum(x => x.LFIMG) };
            return new SapPostingSnapshotBuildResult { IsSuccess = true, OutboundOrderId = order.Id, OutboundStatus = order.Status, Preview = preview };
        }
        private static string Validate(SqlConnection c, SqlTransaction t, OrderRow o) { return o.Status == "PICKED" && o.AllocationId > 0 ? null : "只有已完成PICKED订单允许SAP过账。"; }
        private static OrderRow ReadOrder(SqlConnection c, SqlTransaction t, string no, bool l)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT o.Id,o.SAPDeliveryNo,o.WarehouseId,o.OrderStatus,o.SAPStorageLocation,(SELECT TOP 1 Id FROM dbo.WMS_FG_OutboundAllocation WHERE OutboundOrderId=o.Id AND AllocationStatus='COMPLETED' ORDER BY Id DESC) FROM dbo.WMS_FG_OutboundOrder o" + (l ? " WITH(UPDLOCK,HOLDLOCK,ROWLOCK)" : "") + " WHERE o.SAPDeliveryNo=@No", c, t)) { S(cmd,"@No",50,no); using (SqlDataReader r=cmd.ExecuteReader()) return r.Read()?new OrderRow{Id=r.GetInt64(0),SAPDeliveryNo=r.GetString(1),WarehouseId=r.GetInt64(2),Status=r.GetString(3),SapStorageLocation=r.GetString(4),AllocationId=r.IsDBNull(5)?0:r.GetInt64(5)}:null; }
        }
        private static List<ItemRow> ReadItems(SqlConnection c, SqlTransaction t, long allocationId)
        {
            List<ItemRow> x=new List<ItemRow>(); using(SqlCommand cmd=new SqlCommand(@"SELECT pdi.OutboundOrderLineId,pdi.PickDetailId,d.BoxHUId,pdi.ProdNo,pdi.SAPBatchNo,pdi.AllocatedQty,pdi.Uom,l.SAPItemNo,hu.HUCode FROM dbo.WMS_FG_OutboundPickBatch b INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.PickBatchId=b.Id INNER JOIN dbo.WMS_FG_OutboundPickDetailItem pdi ON pdi.PickDetailId=d.Id INNER JOIN dbo.WMS_FG_OutboundOrderLine l ON l.Id=pdi.OutboundOrderLineId INNER JOIN dbo.WMS_FG_HU hu ON hu.Id=d.BoxHUId WHERE b.AllocationId=@Id AND b.BatchStatus='PICKED' AND d.DetailStatus='PICKED'",c,t)){L(cmd,"@Id",allocationId);using(SqlDataReader r=cmd.ExecuteReader())while(r.Read())x.Add(new ItemRow{LineId=r.GetInt64(0),PickDetailId=r.GetInt64(1),BoxHuId=r.GetInt64(2),ProdNo=r.GetString(3),SapBatchNo=r.IsDBNull(4)?null:r.GetString(4),Qty=r.GetDecimal(5),Uom=r.GetString(6),SapItemNo=r.GetString(7),BoxNo=r.GetString(8)});}return x;
        }
        private static long InsertPosting(SqlConnection c,SqlTransaction t,long orderId,string hash,DateTime now){using(SqlCommand x=new SqlCommand("INSERT dbo.WMS_FG_OutboundSapPosting(OutboundOrderId,PostingStatus,PayloadHash,FirstSendDt,LastAttemptDt) OUTPUT INSERTED.Id VALUES(@O,'SENDING',@H,@N,@N)",c,t)){L(x,"@O",orderId);S(x,"@H",64,hash);D(x,"@N",now);return Convert.ToInt64(x.ExecuteScalar());}}
        private static void InsertDetails(SqlConnection c,SqlTransaction t,long postingId,List<ItemRow> items){int n=0;foreach(var g in items.GroupBy(x=>new{x.PickDetailId,x.LineId,x.SapBatchNo})){ItemRow first=g.First();using(SqlCommand x=new SqlCommand("INSERT dbo.WMS_FG_OutboundSapPostingDetail(PostingId,SequenceNo,OutboundOrderLineId,PickDetailId,BoxHUId,SAPItemNo,ProdNo,BoxNo,SAPBatchNo,PostingQty,Uom,SAPStorageLocation) SELECT @P,@N,@L,@D,@H,@I,@M,@B,@C,@Q,@U,o.SAPStorageLocation FROM dbo.WMS_FG_OutboundPickBatch b INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id=b.AllocationId INNER JOIN dbo.WMS_FG_OutboundOrder o ON o.Id=a.OutboundOrderId WHERE b.Id=(SELECT PickBatchId FROM dbo.WMS_FG_OutboundPickBatchDetail WHERE Id=@D)",c,t)){L(x,"@P",postingId);I(x,"@N",++n);L(x,"@L",g.Key.LineId);L(x,"@D",g.Key.PickDetailId);L(x,"@H",first.BoxHuId);S(x,"@I",20,first.SapItemNo);S(x,"@M",50,first.ProdNo);S(x,"@B",100,first.BoxNo);S(x,"@C",50,g.Key.SapBatchNo);Q(x,"@Q",g.Sum(a=>a.Qty));S(x,"@U",20,first.Uom);if(x.ExecuteNonQuery()!=1)throw new InvalidOperationException("PostingDetail快照失败。");}}}
        private static long InsertAttempt(SqlConnection c,SqlTransaction t,long p,string no,int n,string json,DateTime now){using(SqlCommand x=new SqlCommand("INSERT dbo.WMS_FG_SapIntegrationLog(InterfaceCode,BizType,BizId,BizNo,AttemptNo,IntegrationStatus,RequestJson,StartDt) OUTPUT INSERTED.Id VALUES('S0089','OUTBOUND_SAP_POSTING',@P,@B,@N,'SENDING',@J,@D)",c,t)){L(x,"@P",p);S(x,"@B",100,no);I(x,"@N",n);NV(x,"@J",json);D(x,"@D",now);return Convert.ToInt64(x.ExecuteScalar());}}
        private static void SetSending(SqlConnection c,SqlTransaction t,long p,long l,DateTime n,string expected){using(SqlCommand x=new SqlCommand("UPDATE dbo.WMS_FG_OutboundSapPosting SET PostingStatus='SENDING',LastAttemptDt=@N,LastIntegrationLogId=@L WHERE Id=@P AND PostingStatus=@E",c,t)){D(x,"@N",n);L(x,"@L",l);L(x,"@P",p);S(x,"@E",20,expected);if(x.ExecuteNonQuery()!=1)throw new InvalidOperationException("Posting claim并发冲突。");}}
        private static int NextAttempt(SqlConnection c,SqlTransaction t,long p){using(SqlCommand x=new SqlCommand("SELECT ISNULL(MAX(AttemptNo),0)+1 FROM dbo.WMS_FG_SapIntegrationLog WITH(UPDLOCK,HOLDLOCK) WHERE InterfaceCode='S0089' AND BizType='OUTBOUND_SAP_POSTING' AND BizId=@P",c,t)){L(x,"@P",p);return Convert.ToInt32(x.ExecuteScalar());}}
        private static PostingRow ReadPosting(SqlConnection c,SqlTransaction t,long o,bool l){return ReadPosting(c,t,"OutboundOrderId",o,l);} private static PostingRow ReadPostingById(SqlConnection c,SqlTransaction t,long p,bool l){return ReadPosting(c,t,"Id",p,l);} private static PostingRow ReadPosting(SqlConnection c,SqlTransaction t,string col,long v,bool l){using(SqlCommand x=new SqlCommand("SELECT Id,OutboundOrderId,PostingStatus,PayloadHash,LastIntegrationLogId FROM dbo.WMS_FG_OutboundSapPosting"+(l?" WITH(UPDLOCK,HOLDLOCK,ROWLOCK)":"")+" WHERE "+col+"=@V",c,t)){L(x,"@V",v);using(SqlDataReader r=x.ExecuteReader())return r.Read()?new PostingRow{Id=r.GetInt64(0),OrderId=r.GetInt64(1),Status=r.GetString(2),PayloadHash=r.GetString(3),LastLogId=r.IsDBNull(4)?0:r.GetInt64(4)}:null;}}
        private static AttemptRow ReadAttempt(SqlConnection c,SqlTransaction t,long p,int n,bool l){using(SqlCommand x=new SqlCommand("SELECT Id FROM dbo.WMS_FG_SapIntegrationLog"+(l?" WITH(UPDLOCK,HOLDLOCK,ROWLOCK)":"")+" WHERE InterfaceCode='S0089' AND BizType='OUTBOUND_SAP_POSTING' AND BizId=@P AND AttemptNo=@N",c,t)){L(x,"@P",p);I(x,"@N",n);object id=x.ExecuteScalar();return id==null?null:new AttemptRow{Id=Convert.ToInt64(id)};}}
        private static SapOutboundPostingPreviewDto ReadFrozenPayload(SqlConnection c,SqlTransaction t,long p,string no){using(SqlCommand x=new SqlCommand("SELECT SAPItemNo,ProdNo,BoxNo,SAPBatchNo,PostingQty,Uom,SAPStorageLocation FROM dbo.WMS_FG_OutboundSapPostingDetail WHERE PostingId=@P ORDER BY SequenceNo",c,t)){L(x,"@P",p);List<SapOutboundPostingRow> rows=new List<SapOutboundPostingRow>();using(SqlDataReader r=x.ExecuteReader())while(r.Read())rows.Add(new SapOutboundPostingRow{VBELN=no,POSNR=r.GetString(0),MATNR=r.GetString(1),BOXNO=r.GetString(2),CHARG=r.IsDBNull(3)?null:r.GetString(3),LFIMG=r.GetDecimal(4),LFIMG_BOX=r.GetDecimal(4),VRKME=string.Empty,BOXSIZE=string.Empty,GEWEI="KG",LGORT=r.GetString(6)});if(rows.Count==0)return null;string j=SapOutboundPayloadSerializer.Serialize(new SapOutboundPostingPayload{Data=rows});return new SapOutboundPostingPreviewDto{SAPDeliveryNo=no,Data=rows,PayloadJson=j,PayloadHash=SapOutboundPayloadSerializer.ComputeHash(j)};}}
        private static void UpdateAttempt(SqlConnection c,SqlTransaction t,long id,string s,SapOutboundTransportResult r,DateTime n){using(SqlCommand x=new SqlCommand("UPDATE dbo.WMS_FG_SapIntegrationLog SET IntegrationStatus=@S,ResponseJson=@R,HttpStatusCode=@H,ResultCode=@C,ResultMessage=@M,EndDt=@N,DurationMs=@U WHERE Id=@I AND IntegrationStatus='SENDING'",c,t)){S(x,"@S",20,s);NV(x,"@R",r==null?null:r.ResponseJson);IN(x,"@H",r==null?null:r.HttpStatusCode);S(x,"@C",50,r==null?"LOCAL_NOT_INVOKED":r.SapCode);NS(x,"@M",1000,r==null?"SAP调用未开始":r.SapMessage??r.ExceptionMessage);D(x,"@N",n);IN(x,"@U",r==null?null:(int?)r.DurationMs);L(x,"@I",id);if(x.ExecuteNonQuery()!=1)throw new InvalidOperationException("Attempt完成失败。");}}
        private static void UpdatePosting(SqlConnection c,SqlTransaction t,long id,string s,long log,SapOutboundTransportResult r,DateTime n){using(SqlCommand x=new SqlCommand("UPDATE dbo.WMS_FG_OutboundSapPosting SET PostingStatus=@S,LastAttemptDt=@N,SuccessDt=CASE WHEN @S='SUCCESS' THEN @N ELSE SuccessDt END,ResultCode=@C,ResultMessage=@M,LastIntegrationLogId=@L WHERE Id=@I AND PostingStatus='SENDING'",c,t)){S(x,"@S",20,s);D(x,"@N",n);S(x,"@C",50,r==null?"LOCAL_NOT_INVOKED":r.SapCode);NS(x,"@M",1000,r==null?"SAP调用未开始":r.SapMessage??r.ExceptionMessage);L(x,"@L",log);L(x,"@I",id);if(x.ExecuteNonQuery()!=1)throw new InvalidOperationException("Posting完成失败。");}}
        private static void FinalizeShipment(SqlConnection c, SqlTransaction t, long orderId, string op, string remark, DateTime now)
        {
            long allocationId;
            long shippedId;
            string deliveryNo;
            using (SqlCommand command = new SqlCommand(@"
SELECT TOP 1 o.SAPDeliveryNo, a.Id, shipped.Id
FROM dbo.WMS_FG_OutboundOrder o WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_OutboundAllocation a WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
    ON a.OutboundOrderId = o.Id AND a.AllocationStatus = 'COMPLETED'
INNER JOIN dbo.WMS_FG_Location shipped
    ON shipped.WarehouseId = o.WarehouseId
   AND shipped.LocationCode = 'SHIPPED'
   AND shipped.IsVirtual = 1
   AND shipped.IsActive = 1
WHERE o.Id = @OrderId AND o.OrderStatus = 'PICKED'
ORDER BY a.Id DESC;", c, t))
            {
                L(command, "@OrderId", orderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("订单不满足PICKED、完成Allocation或SHIPPED虚拟货位条件。");
                    deliveryNo = reader.GetString(0);
                    allocationId = reader.GetInt64(1);
                    shippedId = reader.GetInt64(2);
                }
            }

            int expectedBatchCount;
            int expectedLineCount;
            using (SqlCommand command = new SqlCommand(@"
SELECT
    CASE WHEN EXISTS
    (
        SELECT 1 FROM dbo.WMS_FG_HU_Reservation
        WHERE AllocationId = @AllocationId AND ReservationStatus = 'ACTIVE'
    ) THEN 1 ELSE 0 END AS HasActiveReservation,
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.WMS_FG_OutboundPickException ex
        INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.Id = ex.PickDetailId
        INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
        WHERE b.AllocationId = @AllocationId AND ex.ExceptionStatus <> 'RESOLVED'
    ) THEN 1 ELSE 0 END AS HasOpenException,
    CASE WHEN EXISTS
    (
        SELECT 1 FROM dbo.WMS_FG_OutboundOrderLine
        WHERE OutboundOrderId = @OrderId
          AND (LineStatus <> 'PICKED' OR EffectivePickedQty <> EffectiveRequiredQty)
    ) THEN 1 ELSE 0 END AS HasInvalidLine,
    CASE WHEN EXISTS
    (
        SELECT 1 FROM dbo.WMS_FG_OutboundPickBatch
        WHERE AllocationId = @AllocationId
          AND (BatchStatus <> 'PICKED' OR EffectivePickedQty <> PlannedQty)
    ) THEN 1 ELSE 0 END AS HasInvalidBatch,
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.WMS_FG_OutboundPickBatchDetail d
        INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
        INNER JOIN dbo.WMS_FG_HU_Reservation reservation ON reservation.Id = d.ReservationId
        WHERE b.AllocationId = @AllocationId
          AND d.EffectiveQty > 0
          AND
          (
              d.DetailStatus <> 'PICKED'
              OR reservation.ReservationStatus <> 'CONSUMED'
              OR d.EffectiveQty <>
                 ISNULL((SELECT SUM(item.AllocatedQty) FROM dbo.WMS_FG_OutboundPickDetailItem item WHERE item.PickDetailId = d.Id), 0)
          )
    ) THEN 1 ELSE 0 END AS HasInvalidDetail,
    (SELECT COUNT(1) FROM dbo.WMS_FG_OutboundPickBatch WHERE AllocationId = @AllocationId AND BatchStatus = 'PICKED') AS BatchCount,
    (SELECT COUNT(1) FROM dbo.WMS_FG_OutboundOrderLine WHERE OutboundOrderId = @OrderId AND LineStatus = 'PICKED') AS LineCount;", c, t))
            {
                L(command, "@OrderId", orderId);
                L(command, "@AllocationId", allocationId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    if (reader.GetInt32(0) != 0) throw new InvalidOperationException("订单仍存在ACTIVE Reservation。");
                    if (reader.GetInt32(1) != 0) throw new InvalidOperationException("订单仍存在未解决的出库异常。");
                    if (reader.GetInt32(2) != 0) throw new InvalidOperationException("订单行数量或状态不满足SHIPPED条件。");
                    if (reader.GetInt32(3) != 0) throw new InvalidOperationException("PickBatch数量或状态不满足SHIPPED条件。");
                    if (reader.GetInt32(4) != 0) throw new InvalidOperationException("有效PickDetail不满足SHIPPED条件。");
                    expectedBatchCount = reader.GetInt32(5);
                    expectedLineCount = reader.GetInt32(6);
                }
            }
            if (expectedBatchCount == 0 || expectedLineCount == 0)
                throw new InvalidOperationException("订单没有可完成SHIPPED的PickBatch或订单行。");

            List<ShipRow> rows = new List<ShipRow>();
            using (SqlCommand command = new SqlCommand(@"
SELECT d.Id, d.BoxHUId, currentLocation.LocationId, location.LocationCode
FROM dbo.WMS_FG_OutboundPickBatchDetail d WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN dbo.WMS_FG_OutboundPickBatch b WITH (UPDLOCK, HOLDLOCK, ROWLOCK) ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_HU_CurrentLocation currentLocation WITH (UPDLOCK, HOLDLOCK, ROWLOCK) ON currentLocation.HUId = d.BoxHUId
INNER JOIN dbo.WMS_FG_Location location ON location.Id = currentLocation.LocationId
WHERE b.AllocationId = @AllocationId AND d.DetailStatus = 'PICKED' AND d.EffectiveQty > 0
ORDER BY d.Id;", c, t))
            {
                L(command, "@AllocationId", allocationId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!string.Equals(reader.GetString(3), "OUTBOUND-STAGING", StringComparison.Ordinal))
                            throw new InvalidOperationException("有效拣货箱不全在OUTBOUND-STAGING，拒绝SHIPPED。");
                        rows.Add(new ShipRow { DetailId = reader.GetInt64(0), HuId = reader.GetInt64(1), FromId = reader.GetInt64(2) });
                    }
                }
            }
            if (rows.Count == 0)
                throw new InvalidOperationException("没有可完成SHIPPED的有效拣货明细。");

            string token = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            for (int i = 0; i < rows.Count; i++)
            {
                ShipRow row = rows[i];
                string requestId = "SAP-SHIP-" + orderId + "-" + (i + 1);
                string suffix = token + (i + 1).ToString("D3");
                HuMoveLogWriter.HuMoveLogWriteResult move = HuMoveLogWriter.InsertWithTime(
                    c, t, "SM" + now.ToString("yyyyMMddHHmmss") + suffix, row.HuId, row.FromId, shippedId,
                    "OUTBOUND_SHIP", null, deliveryNo, null, requestId, null, op, remark);

                using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.WMS_FG_HU_CurrentLocation
SET LocationId = @ToLocationId, LocationSince = @Now, LastMoveLogId = @MoveLogId,
    LastEditBy = @Operator, LastEditDt = @Now
WHERE HUId = @HUId AND LocationId = @FromLocationId;", c, t))
                {
                    L(command, "@ToLocationId", shippedId);
                    D(command, "@Now", move.MoveTime);
                    L(command, "@MoveLogId", move.Id);
                    S(command, "@Operator", 50, op);
                    L(command, "@HUId", row.HuId);
                    L(command, "@FromLocationId", row.FromId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("SHIPPED当前位置并发更新失败。");
                }
                using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.WMS_FG_OutboundPickBatchDetail
SET DetailStatus = 'SHIPPED'
WHERE Id = @DetailId AND DetailStatus = 'PICKED';", c, t))
                {
                    L(command, "@DetailId", row.DetailId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("PickDetail SHIPPED状态更新失败。");
                }
                HuOperationLogWriter.Insert(
                    c, t, "SO" + now.ToString("yyyyMMddHHmmss") + suffix, "OUTBOUND_SHIP", row.HuId,
                    null, deliveryNo, requestId, op, move.MoveTime, null, remark);
            }

            using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.WMS_FG_OutboundPickBatch
SET BatchStatus = 'SHIPPED'
WHERE AllocationId = @AllocationId AND BatchStatus = 'PICKED';", c, t))
            {
                L(command, "@AllocationId", allocationId);
                if (command.ExecuteNonQuery() != expectedBatchCount)
                    throw new InvalidOperationException("PickBatch SHIPPED状态更新数量不一致。");
            }
            using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.WMS_FG_OutboundOrderLine
SET LineStatus = 'SHIPPED', LastEditDt = @Now
WHERE OutboundOrderId = @OrderId AND LineStatus = 'PICKED';", c, t))
            {
                L(command, "@OrderId", orderId);
                D(command, "@Now", now);
                if (command.ExecuteNonQuery() != expectedLineCount)
                    throw new InvalidOperationException("订单行SHIPPED状态更新数量不一致。");
            }
            using (SqlCommand command = new SqlCommand(@"
UPDATE dbo.WMS_FG_OutboundOrder
SET OrderStatus = 'SHIPPED', ShippedDt = @Now, LastEditBy = @Operator, LastEditDt = @Now
WHERE Id = @OrderId AND OrderStatus = 'PICKED';", c, t))
            {
                L(command, "@OrderId", orderId);
                D(command, "@Now", now);
                S(command, "@Operator", 50, op);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("订单SHIPPED状态更新失败。");
            }
        }
        private static OutboundShippingStateDto ReadState(SqlConnection c,SqlTransaction t,long id){using(SqlCommand x=new SqlCommand("SELECT o.SAPDeliveryNo,o.OrderStatus,ISNULL(p.PostingStatus,'READY'),p.ResultCode,p.ResultMessage FROM dbo.WMS_FG_OutboundOrder o LEFT JOIN dbo.WMS_FG_OutboundSapPosting p ON p.OutboundOrderId=o.Id WHERE o.Id=@I",c,t)){L(x,"@I",id);using(SqlDataReader r=x.ExecuteReader())return r.Read()?new OutboundShippingStateDto{SAPDeliveryNo=r.GetString(0),OutboundStatus=r.GetString(1),SapPostingStatus=r.GetString(2),LastSapCode=r.IsDBNull(3)?null:r.GetString(3),LastSapMessage=r.IsDBNull(4)?null:r.GetString(4),CanConfirmShipment=r.GetString(1)=="PICKED"&&r.GetString(2)=="READY",CanRetry=r.GetString(2)=="FAILED",RequiresITReview=r.GetString(2)=="UNKNOWN"}:null;}}
        private static string Normalize(string s){return s==SapOutboundPostingStatuses.Success||s==SapOutboundPostingStatuses.Failed?s:SapOutboundPostingStatuses.Unknown;} private static SapPostingSnapshotBuildResult Fail(string m){return new SapPostingSnapshotBuildResult{IsSuccess=false,Message=m};} private static SapPostingClaimResult Rollback(SqlTransaction t,string m){t.Rollback();return new SapPostingClaimResult{IsSuccess=false,Message=m};} private static SapPostingClaimResult Claim(long p,long l,int n,SapOutboundPostingPreviewDto x){return new SapPostingClaimResult{IsSuccess=true,ShouldInvokeSap=true,PostingId=p,IntegrationLogId=l,AttemptNo=n,PayloadJson=x.PayloadJson,PayloadHash=x.PayloadHash};}
        private static void S(SqlCommand c,string n,int z,string v){c.Parameters.Add(n,SqlDbType.VarChar,z).Value=(object)v??DBNull.Value;} private static void NS(SqlCommand c,string n,int z,string v){c.Parameters.Add(n,SqlDbType.NVarChar,z).Value=(object)v??DBNull.Value;} private static void NV(SqlCommand c,string n,string v){c.Parameters.Add(n,SqlDbType.NVarChar,-1).Value=(object)v??DBNull.Value;} private static void L(SqlCommand c,string n,long v){c.Parameters.Add(n,SqlDbType.BigInt).Value=v;} private static void I(SqlCommand c,string n,int v){c.Parameters.Add(n,SqlDbType.Int).Value=v;} private static void IN(SqlCommand c,string n,int? v){c.Parameters.Add(n,SqlDbType.Int).Value=v.HasValue?(object)v.Value:DBNull.Value;} private static void D(SqlCommand c,string n,DateTime v){c.Parameters.Add(n,SqlDbType.DateTime2).Value=v;} private static void DN(SqlCommand c,string n,DateTime? v){c.Parameters.Add(n,SqlDbType.Date).Value=v.HasValue?(object)v.Value.Date:DBNull.Value;} private static void Q(SqlCommand c,string n,decimal v){SqlParameter p=c.Parameters.Add(n,SqlDbType.Decimal);p.Precision=18;p.Scale=4;p.Value=v;}
        private class OrderRow{public long Id;public string SAPDeliveryNo;public long WarehouseId;public string Status;public string SapStorageLocation;public long AllocationId;} private class ItemRow{public long LineId;public long PickDetailId;public long BoxHuId;public string ProdNo;public string SapBatchNo;public decimal Qty;public string Uom;public string SapItemNo;public string BoxNo;} private class PostingRow{public long Id;public long OrderId;public string Status;public string PayloadHash;public long LastLogId;} private class AttemptRow{public long Id;} private class ShipRow{public long DetailId;public long HuId;public long FromId;}
    }
}
