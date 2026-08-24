# Page 07 — Audit Log Center V1

## Purpose

Page 07 is the read-only WMS persisted audit facts center. It supports global, bounded searches across HU operation, movement, and Hold/Release facts by date, HUCode, RequestId, operator, and log-specific fields. It is audit-centric rather than HU-centric: Page 03 remains the detailed history page for one HU.

- Learun route: `/WMS/AuditLog/Index`
- Browser API contract: `/api/wms/*`
- Default range: current day plus the preceding six natural days
- Maximum range: 90 inclusive calendar days
- Default/max page size: 50/200
- Ordering: event time descending, then log Id descending
- Database writes: none

## Audit Sources

| Source | Table | Existing read API | Queryable fields before Page 07 | Ordering | Can reuse | Limitations |
|---|---|---|---|---|---|---|
| HU Operation | `WMS_HU_OperationLog` | `GET /api/wms/hus/{huCode}/operations?top=` | HUCode and bounded Top only at API level; table contains OperationType, BusinessNo, RequestId, DetailJson, Operator, OperationTime and Remark | OperationTime DESC, Id DESC | DTO/field semantics and Page 03 viewer can be reused | Existing endpoint is HU-scoped and has no global filters or paging |
| HU Move | `WMS_HU_MoveLog` | `GET /api/wms/hus/{huCode}/moves?top=` | HUCode and bounded Top only at API level; table contains From/To location, MoveType, RequestId, ReasonCode, Operator and MoveTime | MoveTime DESC, Id DESC | DTO/field semantics can be reused | Existing endpoint is HU-scoped and has no global filters or paging |
| Hold / Release | `WMS_HU_HoldLog` | `GET /api/wms/hus/{huCode}/holds?top=` | HUCode and bounded Top only at API level; table contains Hold/Release users, times and separate RequestIds | HoldTime DESC, Id DESC | DTO/field semantics can be reused | Existing endpoint is HU-scoped; system-created Hold rows may legitimately have no HoldRequestId |

The frozen schema evidence confirms the real columns. Operation has HU/time, OperationNo, and RequestId indexes; Move has HU/time and ToLocation/time indexes plus unique non-null RequestId; Hold has active-HU and Hold/Release RequestId indexes but no general HoldTime index. Page 07 therefore keeps default and maximum time windows even though current data volume is moderate. No index or schema change was made.

Existing Page 03 API endpoints are preserved but are not called by Page 07 because they cannot provide global server-side filtering or `totalCount/page/pageSize`. The new repository joins `WMS_HU` once per query to resolve HUCode/HUType; the browser never performs HUId-to-HUCode N+1 requests.

## Read API Contract

- `GET /api/wms/audit/operations`
- `GET /api/wms/audit/moves`
- `GET /api/wms/audit/holds`
- `GET /api/wms/audit/request-trace?requestId=...`
- `GET /api/wms/audit/capabilities`

Common log parameters are `dateFrom`, `dateTo`, `huCode`, `requestId`, `operator`, `page`, and `pageSize`. Operation adds `operationType`; Move adds `moveType`; Hold adds `holdReason` and `status=ACTIVE|RELEASED`. HUCode and types are normalized to uppercase, RequestId remains exact and case-preserving, and operator/reason use bounded server-side contains filters.

Request Trace uses a query parameter rather than a path parameter because real Release RequestIds contain `:`. ASP.NET rejects that character in a Request path before Nancy routing, while an encoded query parameter safely preserves the legitimate value.

All SQL parameters are typed ADO.NET parameters. Inputs are never concatenated into SQL. The repository contains SELECT statements only.

## OperationLog Semantics

One row is one persisted `WMS_HU_OperationLog` fact. The page displays the real OperationType without maintaining a fabricated complete enum. Grid columns include time, HUCode/HUType, OperationType, operator, RequestId, BusinessNo and Remark. The row Drawer displays OperationNo, Related HU and the full stored DetailJson.

DetailJson reuses `Wms.JsonViewer`, which formats valid JSON, falls back to text for invalid JSON, inserts content through text nodes, supports copy/wrap, and does not use raw log content as HTML. Empty DetailJson displays `—` and is not treated as an error.

## MoveLog Semantics

One row is one physical location movement fact. FromLocation may be null for initial WMS receipt; ToLocation is the recorded destination. Page 07 does not reinterpret MoveType as a derived business success state. Location codes remain text in V1.

## HoldLog Semantics

One row is one Hold lifecycle record. Both active and released history are shown by default. The filter can select Active or Released. The Drawer preserves Hold reason/source/user/request and Release time/user/reason/request separately. Missing RequestIds display `—`; the UI never generates replacements.

## RequestId Trace

Clicking a non-null RequestId opens a single Request Trace Drawer. The backend performs three exact, independently bounded reads (maximum 200 per source), returns the original Operation/Move/Hold rows, and constructs a chronological event list. Each event explicitly identifies `OPERATION`, `MOVE`, `HOLD`, or `RELEASE`; the timeline is only an aggregation view over persisted facts.

A RequestId with only one source is valid. An unknown RequestId returns an empty event list with an explanatory message, not “incomplete data.” Operation Detail and Request Trace share one Drawer container: opening trace closes detail first, so Drawers are never nested.

The timeline reuses `Wms.Timeline`; no second timeline engine was introduced.

## HU Navigation

HUCode is resolved in SQL and shown as a copyable link. Clicking it uses `learun.frameTab` to open the existing Page 03 route:

`/WMS/HuDetail/Index?huCode=...`

Page 07 does not duplicate HU tree, content, location, or object-specific history views.

## Audit Capability

| Capability | Status | Source / reason |
|---|---|---|
| HU movement | Available | `WMS_HU_MoveLog` |
| HU operation | Available | `WMS_HU_OperationLog` |
| Hold / Release | Available | `WMS_HU_HoldLog` |
| Master data change | NOT_OBSERVABLE | Page 05 writers do not persist a complete before/after audit event across Warehouse, Mapping and Location changes |
| Pre-Receipt inbound failure | NOT_OBSERVABLE | Rejected-before-Receipt requests can roll back without a persisted Receipt or complete failure audit fact |

No Learun LogEntity source was found that reliably stores entity, before, after, operator, time, and all Page 05 writes. Current modify-user/time fields are last-known entity metadata, not an immutable change history.

## Observable vs Unobservable

An empty result from one of the three available audit sources means no matching persisted rows were found in the requested bounded interval. `NOT_OBSERVABLE` means the system lacks a complete audit source. The Capability tab explicitly says that “查不到” must not be interpreted as “没有发生.” It never renders Master Data changes or pre-Receipt failures as a zero count.

## Permissions

The MVC controller inherits the existing Learun `MvcControllerBase` authorization behavior. The page exposes view/query actions only and is intended for IT, Admin, or Audit roles. No new role, permission record, menu record, or database authorization data was created.

## Security

- No Edit, Delete, Replay, Rollback, Release, Retry, Recalculate, Export, POST, or file-log access exists.
- No arbitrary SQL, table, connection, target URL, or file-path parameter exists.
- Responses do not add connection strings, passwords, tokens, authorization headers, cookies, stack traces, Windows identities, or server paths.
- A GET-only sample of the latest 200 Operation DetailJson values was checked for password/token/authorization/cookie/connectionString/stackTrace markers; none were found (`SENSITIVE_AUDIT_DETAIL_FOUND=False`). This is a sample-based finding, not a guarantee about future writer payloads.
- Learun uses only `Wms.Api` and same-origin `/api/wms/*`; direct database and host-port access are absent.

## Frontend Lifecycle

- Operation is the initial tab and only Operation is requested on first load.
- Move, Hold, and Capability are lazy-loaded on first selection.
- Query affects the current tab only; changed common filters mark other log tabs dirty for their next activation.
- Page load, query, reset, grid render, and page change close the Drawer.
- Only a real grid cell mousedown followed by row selection opens log detail. HU/Request/copy controls stop row propagation.
- Foundation `Wms.Grid` provides request-sequence stale-response protection and local success/business/network/render-error loading finalization.
- Request Trace and log detail share one `Wms.SideDrawer`; Page 07 has no Modal.

## Runtime Evidence

GET/SELECT-only verification for 2026-05-27 through 2026-08-24 returned:

- Operation total 6,412; page 1/page 2 had 10 rows each and zero duplicate Ids; stable descending ordering confirmed.
- `B1951228B` had 3 Operation rows.
- `WEEKCODE_RECALCULATE` 44; `RECEIVE` 56; `PUTAWAY` 3; operator `system` 44; unknown HU 0.
- Move `RECEIVE` 57; `PLT-99153` showed null From and `INBOUND-PENDING` To; exact RequestId returned one row.
- Hold total 340: Active 298, Released 42; WeekCode reason matched 339. A Release RequestId containing `:` returned one Hold row.
- Request Trace samples returned 21 Operation events, 1 Move event, 1 Release event, and 0 for an unknown RequestId. No current RequestId spanning multiple source tables was found in the inspected samples; multi-source chronological merge is covered by automated service tests.

Builds passed for the WMS solution and Learun Web. The console suite passed 64 tests. Monitoring/Audit JavaScript syntax checks and a controlled Page 07 lifecycle harness passed; the temporary harness was removed. The in-app browser failed during initialization with `Cannot redefine property: process`, so visual/click acceptance is not claimed.

## Known Limitations

- Master Data Audit remains unavailable; V2 should define immutable entity type/key, before/after, operator, RequestId, and time.
- Pre-Receipt Integration Audit remains unavailable; V2 must persist received/validation/commit/failure events outside the rolled-back Receipt transaction before request totals or success rates are possible.
- HoldTime has no general frozen-schema index; bounded date windows mitigate but do not replace a future evidence-based index review.
- Request Trace is bounded to 200 rows per source and does not claim completeness beyond persisted data.
- Export is intentionally absent due audit sensitivity.
- Foundation pager visual, Page 01 Drawer lifecycle, and Page 01 Recalculate Modal migration remain deferred.
