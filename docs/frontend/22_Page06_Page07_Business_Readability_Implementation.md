# Page06 / Page07 Business Readability Implementation

## Scope

Phase 1D Batch D changes presentation only. Page06 and Page07 now use `Wms.Terms`; Pager, Drawer Foundation, jfGrid, database schema, audit writers and WMS business rules were not changed. Raw audit facts remain available as secondary evidence.

## Page06 monitoring language

- RUNNING and CONNECTED display as `正常`; DEGRADED as `部分异常`; UNAVAILABLE as `不可用` through `Wms.Terms.monitoringStatus`.
- WMS Service, WmsDb and MesDb display as `WMS服务`, `WMS数据库` and `MES数据库`. Raw health codes are hidden from the primary layer.
- Real metrics remain: today successful receipts, outer boxes, inner packs, quantity, active held boxes, WeekCode anomalies and inbound exception receipts. No success-rate, failure-rate or invented metric was added.
- A persisted count of zero remains `0`. Missing failure history is `当前不可观测`, explicitly not zero. The page explains that complete inbound request success rate cannot be calculated.
- Successful receipt trend logic and zero-filled natural-day buckets are unchanged. Recent activity now leads with time, source, box/pack counts, quantity and status; SourceEntityId and RequestId are removed from the primary Grid.
- Refresh remains manual. `AUTO_REFRESH=False`.

## Page07 information hierarchy

Grid cells show mapped business text for HU type, OperationType, MoveType, hold status and known hold reasons. Operation, Move, Hold and Capability tabs and filters use Chinese business labels. Unknown terms retain their raw fallback.

Operation Drawer order is business summary, WeekCode result when applicable, trace metadata, then collapsed technical detail containing raw OperationType, internal record context and complete DetailJson through `Wms.JsonViewer`. Move and Hold details also lead with business facts and retain identifiers/raw codes in trace sections.

Request Trace reuses the same `auditCenterDrawer`; it does not open a nested Drawer. Timeline titles use business terms first, with raw event code and source in secondary content. HU links still navigate to Page03.

## WeekCode Before / After

`AuditLogBusiness.js` safely parses every item in WEEKCODE_RECALCULATE DetailJson. It accepts a single object or an array and never selects only index zero. Each DateCode is displayed as `周期码`, and the existing `Wms.AuditDiffCell` renders old-to-new production-week evidence. Empty old values display as `未解析`. Invalid JSON or missing required fields displays a safe explanation while raw JSON remains intact.

Request Trace derives its WeekCode summary only from returned persisted Operation/Hold facts. It labels the count `已记录更新外箱`, never total processed/success/unchanged/failed boxes. Hold release is marked only when a returned Hold fact has the same HU, exact `ReleaseRequestId == RequestId`, and ReleaseTime.

## Audit completeness limitation

- `RECALCULATE_UPDATED_PERSISTED=True`: every successfully updated box writes one WEEKCODE_RECALCULATE OperationLog with all item Before/After fields.
- `RECALCULATE_UNCHANGED_PERSISTED=False`: unchanged results are returned live but do not write an OperationLog.
- `RECALCULATE_FAILED_PERSISTED=False`: failures are returned live but have no guaranteed audit log.
- `RECALCULATE_REQUEST_SUMMARY_PERSISTED=False`: there is no independent durable request-result summary.

Therefore `PERSISTED_UPDATE_COUNT != REQUEST_PROCESSED_COUNT`. Request Trace visibly explains that unchanged and failed boxes cannot necessarily be reconstructed from historical logs. No audit table or backend read API was added.

## Post-rollout terminology audit

| Page | English-only authored labels | Raw enum visible sites | IT secondary shown primary | Raw technical shown primary | Readability |
|---|---:|---:|---:|---:|---|
| Page06 | 0 | 1 (SourceSystem business acronym) | 0 | 0 | A candidate |
| Page07 | 2 (date placeholders) | 4, all secondary | 1 (RequestId retained in audit Grid) | 0 | B candidate |

Harness: `agents/wms-terms-pages-batch-d-harness.js`, D601–D732.
