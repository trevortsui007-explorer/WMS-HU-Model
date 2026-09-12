# Page 06 — System / Interface Runtime Monitoring V1

## Purpose

Page 06 is a read-only runtime and integration observability page for IT and administrators. It answers whether the WMS host, WmsDb, MesDb, Learun-to-WMS proxy path, successful inbound activity, and current business issue counts are available. It is not a business operation, retry, database administration, or data-repair page.

- Learun route: `/WMS/FG_Monitoring/Index`
- Browser API contract: `/api/wms/*`
- Refresh: manual only
- Database access: WMS host repositories only; Learun never connects directly
- Writes: none

## Observability Sources

| Source | Storage | Fields / facts | Retention | Queryable? | Suitable for Page 06? | Limitations |
|---|---|---|---|---|---|---|
| `GET /api/health` | Runtime only | service, status, framework, server time | None | Yes | Investigation/reference only | Not under Learun `/api/wms/*`; no dependency detail |
| `GET /api/health/db` | Runtime probe | WmsDb connectivity plus server/database metadata | None | Yes | Not called by Page 06 | WmsDb only and current response can expose raw exception detail |
| `DbConnectionFactory` fixed WmsDb/MesDb probes | Runtime only | connection success, elapsed milliseconds | None | Yes through new overview API | Yes | Point-in-time signal only; no arbitrary connection name or SQL |
| `WMS_FinishedGoodsInboundReceipt` and related committed HU data | SQL Server | successful receipt time, plant, source, box, pack, quantity, exception facts, request id | Database history | Yes | Yes | Contains committed successful receipts, not all inbound attempts |
| WeekCode Audit service/repositories | WmsDb + MesDb current facts | current WeekCode anomalies | Current computed state | Yes | Yes | Not historical request/failure evidence; requires both databases |
| `WMS_HU.IsHold`, active HU/item/hold facts | SQL Server | current active Hold boxes and inbound-after-commit exceptions | Current state plus related history | Yes | Yes | A Hold and WeekCode anomaly may describe the same box; counts are not summed into a failure total |
| HU MoveLog / OperationLog / HoldLog | SQL Server | HU-scoped persisted operations | Database history | Yes | No for V1 summary | Belongs primarily to Page 07; does not prove pre-Receipt inbound rejection |
| Learun `LogEntity` | SQL Server | Learun MVC access/operation/exception records | Manual/system retention | Yes | No | Proxy 502 is handled and is not a complete WMS inbound request log |
| Learun log4net files | Rolling files under configured server path | application log messages | Size/date rolling | Not through a mature safe API | No | No stable inbound request identity contract; arbitrary file-reading API is prohibited |
| Nancy/WMS request diagnostics | Runtime configuration | framework diagnostics | No complete persistent request history found | No | No | No persistent request/error middleware proving rejected-before-Receipt calls |
| ASP.NET `Application_Error` | Runtime callback | current unhandled error object | Not persisted by that handler | No | No | Does not provide queryable inbound failure history |

## Runtime Health Contract

### `GET /api/wms/monitoring/overview`

Returns service status, fixed WmsDb and MesDb dependency status, server time, the natural-day business interval, supported business metrics, and failure-history capability.

- A successful monitoring response proves the WMS host and Learun → WMS proxy path are reachable.
- Dependency failures remain HTTP 200 with `overallStatus = DEGRADED`; they do not collapse the whole endpoint.
- `service.status`: `RUNNING` when the endpoint executes.
- Dependency status: `CONNECTED` or `UNAVAILABLE`.
- Messages are fixed and sanitized. Connection strings, passwords, Windows identity, server file paths, SQL, and stack traces are not returned.
- Each fixed dependency probe uses a copied connection configuration with a 3-second connection timeout and executes only `SELECT 1`.

If the WMS host itself cannot be reached, the Learun proxy returns HTTP 502. The frontend converts that response into an explicit `WMS服务 = 不可用` state and renders unknown business metrics as unavailable, not zero.

## Dependency Health

WmsDb and MesDb are probed independently. For example, WMS host `RUNNING`, WmsDb `CONNECTED`, and MesDb `UNAVAILABLE` produces `DEGRADED`. WmsDb-only business totals remain available when possible; the WeekCode anomaly count is unavailable unless both WmsDb and MesDb are connected because Page 01 semantics require both sources.

## Business Activity Semantics

The daily interval is the server-local natural day `[00:00, next 00:00)`. No established 08:00 WMS business-day contract was found.

The overview supports:

- today successfully committed Receipt count;
- today Box, Pack, and quantity totals derived from committed inbound data;
- current active Hold OUTER_BOX count;
- current WeekCode Audit anomaly count using the Page 01 service semantics;
- current inbound Receipt count with post-commit Hold or WeekCode exception facts.

These are independent facts. An OUTER_BOX can appear in both Hold and WeekCode metrics; the page does not add them into a synthetic failure total.

### `GET /api/wms/monitoring/activity?days=7|14|30`

Returns a backend-aggregated successful Receipt trend and the latest 20 committed Receipt rows for the selected bounded interval. Invalid or missing `days` values use 7 days. Trend and recent-activity sections are isolated so one can remain usable if the other query fails.

The recent grid reuses `FinishedGoodsInboundReadService`; it does not duplicate the Page 04 detail implementation. Its fields are ReceiveTime, Plant, source, SourceEntityId, Box, Pack, Qty, ExceptionBoxCount, and RequestId.

## Successful Receipt vs API Request

`WMS_FinishedGoodsInboundReceipt` proves a successfully committed inbound Receipt. It does not prove the total number of inbound requests because a rejected request can fail before Receipt creation and the transaction can roll back completely.

Therefore:

- `INBOUND_REQUEST_TOTAL_OBSERVABLE=False`
- `INBOUND_FAILURE_HISTORY_PERSISTED=False`
- `INBOUND_FAILURE_HISTORY_QUERYABLE=False`
- `SUCCESS_RATE_METRIC_ALLOWED=False`

The page deliberately does not show request total, failure count/rate, success rate, or “MES push total.”

## Trend Definition

The chart title and contract are “successful inbound Receipt trend.” SQL groups committed Receipts by day and returns only 7, 14, or 30 points; missing days are filled with zero buckets by the service. It is not an API request trend or success-rate chart. The page reuses the existing ECharts 3.6 asset, retains the chart instance, binds `resize.wmsMonitoring`, and disposes it on unload.

## Current Issues

The Current Issues tab is a summary and navigation surface, not a copy of the business grids:

| Issue | Meaning | Target |
|---|---|---|
| WEEKCODE | Current Page 01 audit anomaly count | `/WMS/FG_WeekCodeAudit/Index` |
| HOLD | Current active Hold OUTER_BOX count | `/WMS/FG_FinishedGoodsInventory/Index` |
| INBOUND_WITH_EXCEPTION | Successful inbound Receipts currently associated with post-commit exception facts | `/WMS/FG_FinishedGoodsInboundRecords/Index` |

Navigation uses `learun.frameTab`. The top successful-Receipt, Hold, and WeekCode metric cards use the same Page 04, Page 02, and Page 01 targets respectively.

## Failure History Availability

No complete, persisted, safely queryable source was found for MES inbound failures that occur before Receipt creation. A textual log file is not exposed to the browser and no arbitrary path/download endpoint was added. Page 06 therefore has no “interface failures” grid or drawer.

The UI shows a neutral capability gap:

> 接口失败历史：当前不可追溯。Receipt创建前被拒绝的请求目前没有统一持久化记录。

## Unobservable vs Zero Failures

`available=false` and `status=NOT_OBSERVABLE` mean the system cannot establish a failure count. They never map to `count=0` or “最近没有失败.” Zero is used only for observable facts such as an empty successful-Receipt day bucket.

## Security

- No endpoint accepts connection names, target URLs, SQL, table names, file paths, or connection strings.
- No arbitrary log-file reader or download API exists.
- Responses exclude secrets, full connection data, stack traces, server paths, and Windows tokens.
- All new repository SQL is typed ADO.NET and SELECT-only; there is no `dynamic`, `AddWithValue`, DML, DDL, schema, table, migration, or database write.
- Learun browser code uses only `Wms.Api` and same-origin `/api/wms/*` routes.

## Cross-page Navigation

- WeekCode metric/current issue → Page 01 WeekCode Audit.
- Hold metric/current issue → Page 02 Finished Goods Inventory. V1 opens the page without adding a new cross-frame filter contract.
- Successful inbound metric and inbound-exception issue → Page 04 Finished Goods Inbound Records.

## Verification Evidence

- Backend solution Debug build: passed.
- Learun Web Debug build: passed with existing non-blocking cache/source warnings and zero errors.
- Automated console suite: 58 tests passed, including healthy/partial dependency states, summary counts, zero-filled 7-day trend, latest-20 reuse, sanitized capability output, and SELECT-only contract checks.
- JavaScript syntax: Monitoring API/page and modified Foundation component passed `node --check`.
- Controlled Proxy 502 frontend contract: passed against the actual Page 06 script; the service card became unavailable, Receipt/Hold values remained unavailable rather than zero, and the refresh state terminated without a JavaScript exception. The temporary harness was removed after execution.
- Real GET-only runtime: overview returned HTTP 200 with WmsDb and MesDb connected; active Hold boxes `298`, current WeekCode anomalies `308`, and inbound exception Receipts `34`. The current natural day had zero successful Receipts. Activity returned 7 buckets and 20 recent committed Receipts; 2026-08-19 had 8 Receipts/13 Boxes/13 Packs/Qty 192 and 2026-08-20 had 46 Receipts/1122 Boxes/3659 Packs/Qty 230739.
- No POST, DML, DDL, or migration was executed.
- The in-app browser runtime failed before navigation with `Cannot redefine property: process`; visual/click verification is therefore left to Human Review and is not claimed as passed.

## Known Gaps

- Receipt-before-commit inbound failures have no complete persistent/queryable history. A future Integration Audit V2 must define capture boundary, retention, correlation identity, redaction, and access control before any KPI is permitted.
- Page 05 master-data audit remains a Page 07 concern.
- Automatic refresh is intentionally deferred (`AUTO_REFRESH=False`).
- Existing Foundation deferred work remains unchanged: pager visual, Page 01 Drawer lifecycle, and Page 01 Recalculate Modal migration.
- Human visual validation is required for card density, chart rendering, manual refresh, lazy tabs, 502 presentation, and frame-tab navigation.
