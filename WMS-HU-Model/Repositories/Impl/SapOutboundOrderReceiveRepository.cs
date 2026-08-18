using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using WMS_HU_Model.Infrastructure.Database;
using WMS_HU_Model.Models.Outbound.Integration;

namespace WMS_HU_Model.Repositories
{
    public class SapOutboundOrderReceiveRepository : ISapOutboundOrderReceiveRepository
    {
        public SapOutboundOrderReceiveAttemptResult TryHandleExisting(string sapDeliveryNo, string payloadHash, string rawJson)
        {
            if (sapDeliveryNo == null)
                return new SapOutboundOrderReceiveAttemptResult();

            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    ExistingOrderRow existing = ReadExisting(conn, tran, sapDeliveryNo);
                    if (existing == null)
                    {
                        tran.Commit();
                        return new SapOutboundOrderReceiveAttemptResult();
                    }
                    bool sameHash = string.Equals(existing.SourcePayloadHash, payloadHash, StringComparison.Ordinal);
                    InsertLog(conn, tran, sapDeliveryNo, existing.Id, payloadHash, rawJson,
                        sameHash ? "DUPLICATE" : "REJECTED",
                        sameHash ? "DUPLICATE" : "PAYLOAD_CHANGED",
                        sameHash ? "重复请求，返回原接收结果。" : "同一VBELN已存在，但SAP报文内容发生变化，请使用新交货单号。");
                    tran.Commit();
                    return new SapOutboundOrderReceiveAttemptResult
                    {
                        Exists = true,
                        IsSuccess = sameHash,
                        Message = sameHash ? "重复请求，返回原接收结果。" : "同一VBELN已存在，但SAP报文内容发生变化，请使用新交货单号。",
                        Data = sameHash ? ToResult(existing, true) : null
                    };
                }
            }
        }

        public void RecordRejected(string sapDeliveryNo, string payloadHash, string rawJson, string message)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction())
                {
                    InsertLog(conn, tran, sapDeliveryNo, null, payloadHash, rawJson, "REJECTED", "VALIDATION_FAILED", message);
                    tran.Commit();
                }
            }
        }

        public SapOutboundOrderReceiveAttemptResult CreateFirst(SapOutboundOrderReceiveCommand command)
        {
            using (SqlConnection conn = DbConnectionFactory.Create())
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction(IsolationLevel.Serializable))
                {
                    ExistingOrderRow existing = ReadExisting(conn, tran, command.SAPDeliveryNo);
                    if (existing != null)
                    {
                        bool sameHash = string.Equals(existing.SourcePayloadHash, command.PayloadHash, StringComparison.Ordinal);
                        InsertLog(conn, tran, command.SAPDeliveryNo, existing.Id, command.PayloadHash, command.RawJson,
                            sameHash ? "DUPLICATE" : "REJECTED", sameHash ? "DUPLICATE" : "PAYLOAD_CHANGED",
                            sameHash ? "重复请求，返回原接收结果。" : "同一VBELN已存在，但SAP报文内容发生变化，请使用新交货单号。");
                        tran.Commit();
                        return new SapOutboundOrderReceiveAttemptResult
                        {
                            Exists = true,
                            IsSuccess = sameHash,
                            Message = sameHash ? "重复请求，返回原接收结果。" : "同一VBELN已存在，但SAP报文内容发生变化，请使用新交货单号。",
                            Data = sameHash ? ToResult(existing, true) : null
                        };
                    }

                    List<WarehouseMappingRow> mappings = conn.Query<WarehouseMappingRow>(@"
SELECT wp.WarehouseId, w.WarehouseCode, wp.SAPStorageLocation
FROM dbo.WMS_WarehousePlant wp WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Warehouse w WITH (UPDLOCK, HOLDLOCK) ON w.Id = wp.WarehouseId
WHERE wp.PlantCode = @PlantCode
  AND wp.IsActive = 1
  AND w.IsActive = 1
  AND w.WarehouseType = 'FINISHED_GOODS'
  AND wp.SAPStorageLocation IS NOT NULL
  AND LTRIM(RTRIM(wp.SAPStorageLocation)) <> '';",
                        new { command.PlantCode }, tran).AsList();
                    if (mappings.Count != 1)
                    {
                        string message = mappings.Count == 0 ? "未找到有效仓库/SAP库存地点映射。" : "存在多条有效仓库/SAP库存地点映射。";
                        InsertLog(conn, tran, command.SAPDeliveryNo, null, command.PayloadHash, command.RawJson, "REJECTED", "WAREHOUSE_MAPPING", message);
                        tran.Commit();
                        return new SapOutboundOrderReceiveAttemptResult { IsSuccess = false, Message = message };
                    }

                    WarehouseMappingRow mapping = mappings.Single();
                    DateTime now = DateTime.Now;
                    long orderId = conn.ExecuteScalar<long>(@"
INSERT INTO dbo.WMS_OutboundOrder
(
    OutboundOrderNo, SAPDeliveryNo, WarehouseId, PlantCode, SAPStorageLocation,
    DeliveryType, CustomerCode, SoldToCode, SalesOrgCode, PlannedShipDate,
    PlannedShipTime, ShippingType, SpecialHandlingCode, SAPCreatedBy, SAPCreateDate,
    OrderStatus, SourceSystem, ReceivedRequestId, SourcePayloadHash, SourcePayloadJson,
    ReceivedTime, CreateTime
)
OUTPUT INSERTED.Id
VALUES
(
    @OutboundOrderNo, @SAPDeliveryNo, @WarehouseId, @PlantCode, @SAPStorageLocation,
    @DeliveryType, @CustomerCode, @SoldToCode, @SalesOrgCode, @PlannedShipDate,
    @PlannedShipTime, @ShippingType, @SpecialHandlingCode, @SAPCreatedBy, @SAPCreateDate,
    'WAIT_PICK', 'SAP', NULL, @PayloadHash, @RawJson, @Now, @Now
);",
                        new
                        {
                            OutboundOrderNo = command.SAPDeliveryNo,
                            command.SAPDeliveryNo,
                            mapping.WarehouseId,
                            command.PlantCode,
                            mapping.SAPStorageLocation,
                            command.DeliveryType,
                            command.CustomerCode,
                            command.SoldToCode,
                            command.SalesOrgCode,
                            command.PlannedShipDate,
                            command.PlannedShipTime,
                            command.ShippingType,
                            command.SpecialHandlingCode,
                            command.SAPCreatedBy,
                            command.SAPCreateDate,
                            command.PayloadHash,
                            command.RawJson,
                            Now = now
                        }, tran);
                    foreach (SapOutboundOrderReceiveLineCommand line in command.Lines)
                    {
                        conn.Execute(@"
INSERT INTO dbo.WMS_OutboundOrderLine
(
    OutboundOrderId, SAPItemNo, MaterialCode, MaterialRevision, RequiredQty, SpareQty,
    SAPGiftQty, GiftQty, EffectiveRequiredQty, Uom, PlantCode, SAPStorageLocation,
    ItemCategory, SourceOrderNo, SourceOrderItem, CustomerPO, AllocatedQty,
    EffectivePickedQty, ShippedQty, LineStatus, CreateTime
)
VALUES
(
    @OutboundOrderId, @SAPItemNo, @MaterialCode, NULL, @RequiredQty, 0,
    0, 0, @RequiredQty, @Uom, @PlantCode, @SAPStorageLocation,
    @ItemCategory, @SourceOrderNo, @SourceOrderItem, @CustomerPO, 0,
    0, 0, 'WAIT_PICK', @Now
);",
                            new
                            {
                                OutboundOrderId = orderId,
                                line.SAPItemNo,
                                line.MaterialCode,
                                line.RequiredQty,
                                line.Uom,
                                command.PlantCode,
                                mapping.SAPStorageLocation,
                                line.ItemCategory,
                                line.SourceOrderNo,
                                line.SourceOrderItem,
                                line.CustomerPO,
                                Now = now
                            }, tran);
                    }
                    InsertLog(conn, tran, command.SAPDeliveryNo, orderId, command.PayloadHash, command.RawJson, "SUCCESS", "CREATED", "success");
                    tran.Commit();
                    return new SapOutboundOrderReceiveAttemptResult
                    {
                        IsSuccess = true,
                        Message = "success",
                        Data = new SapOutboundOrderReceiveResultDto
                        {
                            SAPDeliveryNo = command.SAPDeliveryNo,
                            OutboundOrderNo = command.SAPDeliveryNo,
                            OrderStatus = "WAIT_PICK",
                            WarehouseCode = mapping.WarehouseCode,
                            PlantCode = command.PlantCode,
                            SAPStorageLocation = mapping.SAPStorageLocation,
                            LineCount = command.Lines.Count
                        }
                    };
                }
            }
        }

        private static ExistingOrderRow ReadExisting(SqlConnection conn, SqlTransaction tran, string sapDeliveryNo)
        {
            return conn.QueryFirstOrDefault<ExistingOrderRow>(@"
SELECT TOP 1 o.Id, o.SAPDeliveryNo, o.OutboundOrderNo, o.OrderStatus, w.WarehouseCode,
    o.PlantCode, o.SAPStorageLocation, o.SourcePayloadHash,
    (SELECT COUNT(1) FROM dbo.WMS_OutboundOrderLine l WHERE l.OutboundOrderId = o.Id) AS LineCount
FROM dbo.WMS_OutboundOrder o WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.WMS_Warehouse w ON w.Id = o.WarehouseId
WHERE o.SAPDeliveryNo = @SAPDeliveryNo;", new { SAPDeliveryNo = sapDeliveryNo }, tran);
        }

        private static SapOutboundOrderReceiveResultDto ToResult(ExistingOrderRow row, bool duplicate)
        {
            // WarehouseCode is a current master-data display value resolved by the historical WarehouseId.
            // Duplicate business acceptance and all business fields come from the existing order snapshot.
            return new SapOutboundOrderReceiveResultDto
            {
                SAPDeliveryNo = row.SAPDeliveryNo,
                OutboundOrderNo = row.OutboundOrderNo,
                OrderStatus = row.OrderStatus,
                WarehouseCode = row.WarehouseCode,
                PlantCode = row.PlantCode,
                SAPStorageLocation = row.SAPStorageLocation,
                LineCount = row.LineCount,
                IsDuplicate = duplicate
            };
        }

        private static void InsertLog(SqlConnection conn, SqlTransaction tran, string deliveryNo, long? orderId, string hash, string rawJson, string status, string resultCode, string message)
        {
            conn.Execute(@"
INSERT INTO dbo.WMS_OutboundOrderReceiveLog
(
    RequestId, InterfaceCode, SourceSystem, SAPDeliveryNo, OutboundOrderId,
    RequestHash, RequestJson, ReceiveTime, ProcessStatus, ResultCode, ResultMessage, CreateTime
)
VALUES
(
    NULL, 'SAP_OUTBOUND_ORDER', 'SAP', @SAPDeliveryNo, @OutboundOrderId,
    @RequestHash, @RequestJson, @Now, @ProcessStatus, @ResultCode, @ResultMessage, @Now
);",
                new
                {
                    SAPDeliveryNo = deliveryNo,
                    OutboundOrderId = orderId,
                    RequestHash = hash,
                    RequestJson = rawJson,
                    Now = DateTime.Now,
                    ProcessStatus = status,
                    ResultCode = resultCode,
                    ResultMessage = message
                }, tran);
        }

        private class ExistingOrderRow
        {
            public long Id { get; set; }
            public string SAPDeliveryNo { get; set; }
            public string OutboundOrderNo { get; set; }
            public string OrderStatus { get; set; }
            public string WarehouseCode { get; set; }
            public string PlantCode { get; set; }
            public string SAPStorageLocation { get; set; }
            public string SourcePayloadHash { get; set; }
            public int LineCount { get; set; }
        }

        private class WarehouseMappingRow
        {
            public long WarehouseId { get; set; }
            public string WarehouseCode { get; set; }
            public string SAPStorageLocation { get; set; }
        }
    }
}
