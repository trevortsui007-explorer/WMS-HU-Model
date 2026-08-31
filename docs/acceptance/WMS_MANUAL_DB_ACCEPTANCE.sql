/*
WMS manual database acceptance pack
Database: adms705
Execution: Human-operated SSMS only

SAFETY: This file is read-only. Its default sections contain no data or schema
mutation statement. Replace the example
@SAPDeliveryNo only after selecting an approved, isolated acceptance fixture.
*/

USE [adms705];
GO

SET NOCOUNT ON;
GO

/* A. Required table and physical-column metadata. */
WITH RequiredTables AS
(
    SELECT TableName FROM (VALUES
        (N'WMS_FG_OutboundOrder'), (N'WMS_FG_OutboundOrderLine'), (N'WMS_FG_OutboundAllocation'),
        (N'WMS_FG_OutboundPickBatch'), (N'WMS_FG_HU_Reservation'), (N'WMS_FG_OutboundPickBatchDetail'),
        (N'WMS_FG_OutboundPickDetailItem'), (N'WMS_FG_OutboundPickException'), (N'WMS_FG_OutboundExceptionReplacement'),
        (N'WMS_FG_OutboundSapPosting'), (N'WMS_FG_OutboundSapPostingDetail'), (N'WMS_FG_SapIntegrationLog'),
        (N'WMS_FG_HU_CurrentLocation'), (N'WMS_FG_HU_MoveLog'), (N'WMS_FG_HU_OperationLog')) v(TableName)
)
SELECT
    t.TableName,
    CASE WHEN st.object_id IS NULL THEN 'MISSING' ELSE 'PRESENT' END AS TableStatus
FROM @Tables t
LEFT JOIN sys.tables st ON st.name = t.TableName AND SCHEMA_NAME(st.schema_id) = N'dbo'
ORDER BY t.TableName;

WITH RequiredTables AS
(
    SELECT TableName FROM (VALUES
        (N'WMS_FG_OutboundOrder'), (N'WMS_FG_OutboundOrderLine'), (N'WMS_FG_OutboundAllocation'),
        (N'WMS_FG_OutboundPickBatch'), (N'WMS_FG_HU_Reservation'), (N'WMS_FG_OutboundPickBatchDetail'),
        (N'WMS_FG_OutboundPickDetailItem'), (N'WMS_FG_OutboundPickException'), (N'WMS_FG_OutboundExceptionReplacement'),
        (N'WMS_FG_OutboundSapPosting'), (N'WMS_FG_OutboundSapPostingDetail'), (N'WMS_FG_SapIntegrationLog'),
        (N'WMS_FG_HU_CurrentLocation'), (N'WMS_FG_HU_MoveLog'), (N'WMS_FG_HU_OperationLog')) v(TableName)
)
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnOrdinal,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE
        WHEN ty.name IN (N'nvarchar', N'nchar') AND c.max_length <> -1 THEN c.max_length / 2
        ELSE c.max_length
    END AS MaxLength,
    c.precision AS [Precision],
    c.scale AS Scale,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    dc.definition AS DefaultDefinition
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
INNER JOIN sys.columns c ON c.object_id = t.object_id
INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
INNER JOIN RequiredTables required ON required.TableName = t.name
WHERE s.name = N'dbo'
ORDER BY t.name, c.column_id;
GO

/* B. Targeted current Runtime-contract columns. Posting attempt number belongs to
   WMS_FG_SapIntegrationLog, not WMS_FG_OutboundSapPosting. */
WITH ExpectedColumns AS
(
    SELECT TableName, ColumnName, Purpose FROM (VALUES
    (N'WMS_FG_OutboundOrder', N'SAPDeliveryNo', N'Outbound business identifier'), (N'WMS_FG_OutboundOrder', N'OrderStatus', N'Outbound terminal state'), (N'WMS_FG_OutboundOrder', N'ShippedDt', N'Final shipment timestamp'),
    (N'WMS_FG_OutboundSapPosting', N'OutboundOrderId', N'Posting owner'), (N'WMS_FG_OutboundSapPosting', N'PostingStatus', N'Current posting state'), (N'WMS_FG_OutboundSapPosting', N'PayloadHash', N'Frozen payload guard'), (N'WMS_FG_OutboundSapPosting', N'FirstSendDt', N'First claim time'), (N'WMS_FG_OutboundSapPosting', N'LastAttemptDt', N'Latest claim time'), (N'WMS_FG_OutboundSapPosting', N'SuccessDt', N'Success timestamp'), (N'WMS_FG_OutboundSapPosting', N'LastIntegrationLogId', N'Current attempt link'),
    (N'WMS_FG_OutboundSapPostingDetail', N'PostingId', N'Immutable snapshot parent'), (N'WMS_FG_OutboundSapPostingDetail', N'SequenceNo', N'Snapshot order'), (N'WMS_FG_OutboundSapPostingDetail', N'OutboundOrderLineId', N'Order-line mapping'), (N'WMS_FG_OutboundSapPostingDetail', N'PickDetailId', N'Picked inventory mapping'), (N'WMS_FG_OutboundSapPostingDetail', N'BoxHUId', N'Box mapping'), (N'WMS_FG_OutboundSapPostingDetail', N'SAPItemNo', N'POSNR snapshot'), (N'WMS_FG_OutboundSapPostingDetail', N'ProdNo', N'Material snapshot'), (N'WMS_FG_OutboundSapPostingDetail', N'BoxNo', N'Box snapshot'), (N'WMS_FG_OutboundSapPostingDetail', N'SAPBatchNo', N'CHARG snapshot'), (N'WMS_FG_OutboundSapPostingDetail', N'PostingQty', N'Quantity snapshot'), (N'WMS_FG_OutboundSapPostingDetail', N'Uom', N'UOM snapshot'), (N'WMS_FG_OutboundSapPostingDetail', N'SAPStorageLocation', N'LGORT snapshot'),
    (N'WMS_FG_SapIntegrationLog', N'BizType', N'Integration business type'), (N'WMS_FG_SapIntegrationLog', N'BizId', N'Posting identifier'), (N'WMS_FG_SapIntegrationLog', N'BizNo', N'SAP delivery number'), (N'WMS_FG_SapIntegrationLog', N'AttemptNo', N'Attempt sequence'), (N'WMS_FG_SapIntegrationLog', N'IntegrationStatus', N'Attempt result'), (N'WMS_FG_SapIntegrationLog', N'RequestJson', N'Actual request snapshot'), (N'WMS_FG_SapIntegrationLog', N'ResponseJson', N'Actual response'), (N'WMS_FG_SapIntegrationLog', N'ResultCode', N'SAP result code'), (N'WMS_FG_SapIntegrationLog', N'ResultMessage', N'SAP result message'), (N'WMS_FG_SapIntegrationLog', N'HttpStatusCode', N'HTTP result'), (N'WMS_FG_SapIntegrationLog', N'StartDt', N'Attempt start'), (N'WMS_FG_SapIntegrationLog', N'EndDt', N'Attempt end'),
    (N'WMS_FG_HU_CurrentLocation', N'HUId', N'HU key'), (N'WMS_FG_HU_CurrentLocation', N'LocationId', N'Direct current location'), (N'WMS_FG_HU_CurrentLocation', N'LastMoveLogId', N'Current-location movement audit')) v(TableName, ColumnName, Purpose)
)
SELECT
    e.TableName,
    e.ColumnName,
    e.Purpose,
    CASE WHEN c.column_id IS NULL THEN 'MISSING' ELSE 'PRESENT' END AS ColumnStatus,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM @ExpectedColumns e
LEFT JOIN sys.tables t ON t.name = e.TableName AND SCHEMA_NAME(t.schema_id) = N'dbo'
LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = e.ColumnName
LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
ORDER BY e.TableName, e.ColumnName;
GO

/* C. Legacy physical fields must be absent. Any PRESENT row requires Human Review;
   it does not authorize a schema change. */
WITH LegacyColumns AS (SELECT ColumnName FROM (VALUES (N'ShipRequestId'), (N'ShippedQty'), (N'ShippedTime'), (N'ReviewDt'), (N'ReviewBy'), (N'ReviewRemark')) v(ColumnName))
SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    c.name AS LegacyColumn,
    'PRESENT_REQUIRES_HUMAN_REVIEW' AS Result
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
INNER JOIN LegacyColumns legacy ON legacy.ColumnName = c.name
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND t.name LIKE N'WMS_FG_%'
UNION ALL
SELECT
    SCHEMA_NAME(t.schema_id),
    t.name,
    c.name,
    'PRESENT_REQUIRES_HUMAN_REVIEW'
FROM sys.tables t
INNER JOIN sys.columns c ON c.object_id = t.object_id
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
  AND t.name LIKE N'WMS_FG_%'
  AND c.name LIKE N'ManualConfirm%'
ORDER BY TableName, LegacyColumn;
GO

/* D. Index and key-constraint metadata. Review unique/filter results against the
   Runtime's claim, idempotency, and request handling expectations. */
WITH IndexTables AS (SELECT TableName FROM (VALUES (N'WMS_FG_OutboundSapPosting'), (N'WMS_FG_OutboundSapPostingDetail'), (N'WMS_FG_SapIntegrationLog'), (N'WMS_FG_OutboundAllocation'), (N'WMS_FG_OutboundPickBatch'), (N'WMS_FG_HU_Reservation')) v(TableName))
SELECT
    t.name AS TableName,
    i.name AS IndexOrConstraintName,
    CASE WHEN kc.name IS NULL THEN 'INDEX' ELSE kc.type_desc END AS ObjectType,
    i.is_unique AS IsUnique,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    STUFF((
        SELECT N', ' + QUOTENAME(c.name) + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
        ORDER BY ic.key_ordinal
        FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 2, N'') AS KeyColumns
FROM sys.tables t
INNER JOIN IndexTables required ON required.TableName = t.name
INNER JOIN sys.indexes i ON i.object_id = t.object_id AND i.index_id > 0
LEFT JOIN sys.key_constraints kc ON kc.parent_object_id = i.object_id AND kc.unique_index_id = i.index_id
WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
ORDER BY t.name, i.is_unique DESC, i.name;
GO

/* E. Reusable single-delivery state inspection. Replace only after Human approval. */
DECLARE @SAPDeliveryNo varchar(50) = 'REPLACE_WITH_APPROVED_ACCEPTANCE_DELIVERY';
DECLARE @OrderId bigint;
DECLARE @PostingId bigint;

SELECT @OrderId = o.Id
FROM dbo.WMS_FG_OutboundOrder o
WHERE o.SAPDeliveryNo = @SAPDeliveryNo;

SELECT N'OutboundOrder' AS ResultSet, o.*
FROM dbo.WMS_FG_OutboundOrder o
WHERE o.Id = @OrderId;

SELECT N'OutboundOrderLine' AS ResultSet, l.*
FROM dbo.WMS_FG_OutboundOrderLine l
WHERE l.OutboundOrderId = @OrderId
ORDER BY l.Id;

SELECT N'OutboundAllocation' AS ResultSet, a.*
FROM dbo.WMS_FG_OutboundAllocation a
WHERE a.OutboundOrderId = @OrderId
ORDER BY a.Id;

SELECT N'OutboundPickBatch' AS ResultSet, b.*
FROM dbo.WMS_FG_OutboundPickBatch b
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY b.Id;

SELECT N'HU_Reservation' AS ResultSet, r.*
FROM dbo.WMS_FG_HU_Reservation r
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = r.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY r.Id;

SELECT N'OutboundPickBatchDetail' AS ResultSet, d.*
FROM dbo.WMS_FG_OutboundPickBatchDetail d
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY d.Id;

SELECT N'OutboundPickDetailItem' AS ResultSet, i.*
FROM dbo.WMS_FG_OutboundPickDetailItem i
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.Id = i.PickDetailId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY i.Id;

SELECT N'OutboundPickException' AS ResultSet, ex.*
FROM dbo.WMS_FG_OutboundPickException ex
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.Id = ex.PickDetailId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY ex.Id;

SELECT N'OutboundExceptionReplacement' AS ResultSet, er.*
FROM dbo.WMS_FG_OutboundExceptionReplacement er
INNER JOIN dbo.WMS_FG_OutboundPickException ex ON ex.Id = er.ExceptionId
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.Id = ex.PickDetailId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY er.Id;

SELECT @PostingId = p.Id
FROM dbo.WMS_FG_OutboundSapPosting p
WHERE p.OutboundOrderId = @OrderId;

SELECT N'OutboundSapPosting' AS ResultSet, p.*
FROM dbo.WMS_FG_OutboundSapPosting p
WHERE p.OutboundOrderId = @OrderId;

SELECT N'OutboundSapPostingDetail' AS ResultSet, pd.*
FROM dbo.WMS_FG_OutboundSapPostingDetail pd
WHERE pd.PostingId = @PostingId
ORDER BY pd.SequenceNo, pd.Id;

SELECT N'SapIntegrationLog' AS ResultSet, log.*
FROM dbo.WMS_FG_SapIntegrationLog log
WHERE log.InterfaceCode = 'S0089'
  AND log.BizType = 'OUTBOUND_SAP_POSTING'
  AND log.BizId = @PostingId
ORDER BY log.AttemptNo, log.Id;

SELECT N'HU_CurrentLocation' AS ResultSet, cl.*
FROM dbo.WMS_FG_HU_CurrentLocation cl
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.BoxHUId = cl.HUId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY cl.HUId;

SELECT N'HU_MoveLog' AS ResultSet, ml.*
FROM dbo.WMS_FG_HU_MoveLog ml
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.BoxHUId = ml.HUId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY ml.Id;

SELECT N'HU_OperationLog' AS ResultSet, ol.*
FROM dbo.WMS_FG_HU_OperationLog ol
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.BoxHUId = ol.HUId
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.Id = d.PickBatchId
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.Id = b.AllocationId
WHERE a.OutboundOrderId = @OrderId
ORDER BY ol.Id;
GO

/* F. SHIPPED integrity. Every result row is an anomaly requiring Human Review. */
SELECT N'SHIPPED_ORDER_WITHOUT_SUCCESS_POSTING' AS Anomaly, o.Id AS OrderId, o.SAPDeliveryNo, o.OrderStatus, p.PostingStatus
FROM dbo.WMS_FG_OutboundOrder o
LEFT JOIN dbo.WMS_FG_OutboundSapPosting p ON p.OutboundOrderId = o.Id
WHERE o.OrderStatus = 'SHIPPED' AND ISNULL(p.PostingStatus, '') <> 'SUCCESS';

SELECT N'SUCCESS_POSTING_WITHOUT_SHIPPED_ORDER' AS Anomaly, p.Id AS PostingId, o.SAPDeliveryNo, p.PostingStatus, o.OrderStatus
FROM dbo.WMS_FG_OutboundSapPosting p
INNER JOIN dbo.WMS_FG_OutboundOrder o ON o.Id = p.OutboundOrderId
WHERE p.PostingStatus = 'SUCCESS' AND o.OrderStatus <> 'SHIPPED';

SELECT N'SHIPPED_ORDER_WITH_ACTIVE_RESERVATION' AS Anomaly, o.Id AS OrderId, o.SAPDeliveryNo, r.Id AS ReservationId
FROM dbo.WMS_FG_OutboundOrder o
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.OutboundOrderId = o.Id
INNER JOIN dbo.WMS_FG_HU_Reservation r ON r.AllocationId = a.Id AND r.ReservationStatus = 'ACTIVE'
WHERE o.OrderStatus = 'SHIPPED';

SELECT N'SHIPPED_ORDER_WITH_UNRESOLVED_EXCEPTION' AS Anomaly, o.Id AS OrderId, o.SAPDeliveryNo, ex.Id AS ExceptionId, ex.ExceptionStatus
FROM dbo.WMS_FG_OutboundOrder o
INNER JOIN dbo.WMS_FG_OutboundAllocation a ON a.OutboundOrderId = o.Id
INNER JOIN dbo.WMS_FG_OutboundPickBatch b ON b.AllocationId = a.Id
INNER JOIN dbo.WMS_FG_OutboundPickBatchDetail d ON d.PickBatchId = b.Id
INNER JOIN dbo.WMS_FG_OutboundPickException ex ON ex.PickDetailId = d.Id AND ex.ExceptionStatus <> 'RESOLVED'
WHERE o.OrderStatus = 'SHIPPED';

SELECT N'SHIPPED_DETAIL_STILL_AT_OUTBOUND_STAGING' AS Anomaly, d.Id AS PickDetailId, d.BoxHUId, location.LocationCode
FROM dbo.WMS_FG_OutboundPickBatchDetail d
INNER JOIN dbo.WMS_FG_HU_CurrentLocation cl ON cl.HUId = d.BoxHUId
INNER JOIN dbo.WMS_FG_Location location ON location.Id = cl.LocationId
WHERE d.DetailStatus = 'SHIPPED' AND location.LocationCode = 'OUTBOUND-STAGING';

SELECT N'SUCCESS_POSTING_WITHOUT_SUCCESS_DT' AS Anomaly, p.Id AS PostingId, p.OutboundOrderId
FROM dbo.WMS_FG_OutboundSapPosting p
WHERE p.PostingStatus = 'SUCCESS' AND p.SuccessDt IS NULL;

SELECT N'SHIPPED_ORDER_WITHOUT_SHIPPED_DT' AS Anomaly, o.Id AS OrderId, o.SAPDeliveryNo
FROM dbo.WMS_FG_OutboundOrder o
WHERE o.OrderStatus = 'SHIPPED' AND o.ShippedDt IS NULL;
GO

/* G. Posting attempt, snapshot, and claim integrity. Each result row is an anomaly.
   These queries cannot prove HTTP count; they prove durable database evidence only. */
SELECT N'DUPLICATE_ATTEMPT_NO' AS Anomaly, log.BizId AS PostingId, log.AttemptNo, COUNT(*) AS RowCount
FROM dbo.WMS_FG_SapIntegrationLog log
WHERE log.InterfaceCode = 'S0089' AND log.BizType = 'OUTBOUND_SAP_POSTING'
GROUP BY log.BizId, log.AttemptNo
HAVING COUNT(*) > 1;

SELECT N'ATTEMPT_SEQUENCE_GAP' AS Anomaly, x.BizId AS PostingId, x.AttemptNo, x.PreviousAttemptNo
FROM (
    SELECT log.BizId, log.AttemptNo,
           LAG(log.AttemptNo) OVER (PARTITION BY log.BizId ORDER BY log.AttemptNo, log.Id) AS PreviousAttemptNo
    FROM dbo.WMS_FG_SapIntegrationLog log
    WHERE log.InterfaceCode = 'S0089' AND log.BizType = 'OUTBOUND_SAP_POSTING'
) x
WHERE x.PreviousAttemptNo IS NOT NULL AND x.AttemptNo <> x.PreviousAttemptNo + 1;

SELECT N'MULTIPLE_POSTINGS_FOR_ORDER' AS Anomaly, p.OutboundOrderId, COUNT(*) AS PostingCount
FROM dbo.WMS_FG_OutboundSapPosting p
GROUP BY p.OutboundOrderId
HAVING COUNT(*) > 1;

SELECT N'MULTIPLE_SENDING_POSTINGS_FOR_ORDER' AS Anomaly, p.OutboundOrderId, COUNT(*) AS SendingCount
FROM dbo.WMS_FG_OutboundSapPosting p
WHERE p.PostingStatus = 'SENDING'
GROUP BY p.OutboundOrderId
HAVING COUNT(*) > 1;

SELECT N'POSTING_DETAIL_ORPHAN' AS Anomaly, pd.Id AS PostingDetailId, pd.PostingId
FROM dbo.WMS_FG_OutboundSapPostingDetail pd
LEFT JOIN dbo.WMS_FG_OutboundSapPosting p ON p.Id = pd.PostingId
WHERE p.Id IS NULL;

SELECT N'POSTING_WITHOUT_ATTEMPT_LOG' AS Anomaly, p.Id AS PostingId, p.OutboundOrderId
FROM dbo.WMS_FG_OutboundSapPosting p
LEFT JOIN dbo.WMS_FG_SapIntegrationLog log ON log.BizId = p.Id
    AND log.InterfaceCode = 'S0089' AND log.BizType = 'OUTBOUND_SAP_POSTING'
WHERE log.Id IS NULL;

SELECT N'LAST_LOG_NOT_MATCHING_POSTING' AS Anomaly, p.Id AS PostingId, p.LastIntegrationLogId, log.BizId, log.AttemptNo
FROM dbo.WMS_FG_OutboundSapPosting p
LEFT JOIN dbo.WMS_FG_SapIntegrationLog log ON log.Id = p.LastIntegrationLogId
WHERE p.LastIntegrationLogId IS NOT NULL
  AND (log.Id IS NULL OR log.BizId <> p.Id OR log.InterfaceCode <> 'S0089' OR log.BizType <> 'OUTBOUND_SAP_POSTING');

SELECT N'PAYLOAD_HASH_INVALID' AS Anomaly, p.Id AS PostingId, p.OutboundOrderId, p.PayloadHash
FROM dbo.WMS_FG_OutboundSapPosting p
WHERE p.PayloadHash IS NULL OR LEN(p.PayloadHash) <> 64;
GO

/* H. Post-concurrency-test inspection. Run only after a Human performs two real
   concurrent requests against an approved fixture and records the request IDs and
   SAP/stub invocation count externally. */
DECLARE @ConcurrentSAPDeliveryNo varchar(50) = 'REPLACE_WITH_CONCURRENCY_FIXTURE';

SELECT o.Id AS OrderId, o.SAPDeliveryNo, o.OrderStatus,
       p.Id AS PostingId, p.PostingStatus, p.PayloadHash, p.LastIntegrationLogId, p.LastAttemptDt
FROM dbo.WMS_FG_OutboundOrder o
LEFT JOIN dbo.WMS_FG_OutboundSapPosting p ON p.OutboundOrderId = o.Id
WHERE o.SAPDeliveryNo = @ConcurrentSAPDeliveryNo;

SELECT log.Id, log.BizId AS PostingId, log.BizNo, log.AttemptNo, log.IntegrationStatus,
       log.StartDt, log.EndDt, log.HttpStatusCode, log.ResultCode, log.ResultMessage
FROM dbo.WMS_FG_SapIntegrationLog log
WHERE log.InterfaceCode = 'S0089'
  AND log.BizType = 'OUTBOUND_SAP_POSTING'
  AND log.BizNo = @ConcurrentSAPDeliveryNo
ORDER BY log.AttemptNo, log.Id;

/* Required interpretation: compare the durable attempt rows above with the safe
   stub's actual HTTP invocation count. DB-only evidence cannot prove no HTTP send. */
GO

/* OPTIONAL MUTATION SECTIONS: NONE. Any future acceptance fixture setup, controlled
   SAP call, failure injection, or concurrency execution requires separate Human
   approval and must not be appended to this read-only verification pack by default. */
