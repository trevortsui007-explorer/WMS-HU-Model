# Page 04 — Finished Goods Inbound Records / Exceptions V1

## Purpose

Page 04 is a read-only finished-goods inbound traceability page. It answers which HU facts were created by one accepted `WMS_FinishedGoodsInboundReceipt`, and which successfully received boxes currently carry Hold or WeekCode exception evidence. It is not a receiving, retry, repair, Hold release, or WeekCode recalculation page.

## Existing inbound APIs

| Method | Route | Request | Response | Purpose | Reused by Page 04 |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/wms/integration/finished-goods/inbound` | `FinishedGoodsInboundReceiveRequest` | `FinishedGoodsInboundResultDto` | General finished-goods inbound write | No; write endpoint |
| POST | `/api/wms/integration/finished-goods/mes-inbound` | PlanStock identity/request | PlanStockId, ReceiptId, IsDuplicate | MES PlanStock inbound orchestration | No; write endpoint |
| GET | `/api/wms/hus/{huCode}/operations` | HU code and top | HU operation log list | Page 03 HU history | No; not Receipt-grained |
| GET | `/api/wms/inventory/finished-goods/{boxCode}/preview` | Box code | Current box inventory preview | Page 02 current inventory | No; not Receipt history |

No Receipt query or Receipt detail GET existed before Page 04. Page 04 adds only the two read endpoints documented below.

## Inbound record semantics

- Grid grain is exactly one persisted `WMS_FinishedGoodsInboundReceipt` row.
- A persisted Receipt means the inbound transaction committed. A box with `WEEKCODE_UNRESOLVED` Hold is therefore shown as successful inbound with an exception, never as an inbound failure.
- `displayStatus` is a view-only value: `RECEIVED` when `exceptionBoxCount = 0`, otherwise `WITH_EXCEPTION`. It is not stored and does not add a business status.
- Default query scope is the most recent 30 calendar days.
- All filters, aggregates, ordering, and paging run in the repository in one list query plus one count query. There is no per-Receipt child query.

## Receipt model and source identity

The receipt natural identity remains the existing `SourceSystem + SourceType + SourceEntityId`. Receipt-created HU rows are associated through the existing source business identity:

`WMS_HU.SourceBusinessNo = 'FGI-' + ReceiptId`

No Receipt-to-HU foreign key, inbound order, inbound task, batch, or snapshot model was introduced.

## Grid contract

`GET /api/wms/inbound/finished-goods/receipts/`

Query parameters:

- `page`, `pageSize` (default 1/50, maximum 200)
- `dateFrom`, `dateTo` in `yyyy-MM-dd`; the end date is inclusive at the API boundary
- `plantCode`, `sourceSystem`, `sourceType`, `sourceEntityId`
- `palletCode`, `boxCode`, `materialCode`, `requestId`
- `exceptionOnly`
- `exceptionCategory`: `WEEKCODE` or `HOLD`

Response data follows the existing server paging contract: `totalCount`, `page`, `pageSize`, `items`. Each item contains Receipt/source identity, time, plant, pallet summary/count, box and InnerPack counts, actual Item quantity, material summary/count, Hold/WeekCode/union exception box counts, RequestId, operator, remark, and display status.

Pallet and material are summaries, not single-value assumptions. A Receipt with no pallet returns a zero count and `—` in the UI. Multiple pallets show the first stable summary plus the count; multiple materials are returned as a distinct material summary plus `materialCount`.

`totalQty` is the sum of `WMS_HU_Item.Qty` attached to Receipt-created `INNER_PACK` HUs. It does not reuse an unverified MES source quantity.

## Detail contract

`GET /api/wms/inbound/finished-goods/receipts/{receiptId}`

The response contains:

- `record`: the same Receipt aggregate contract as the grid
- `pallets`: all Receipt-created pallets with lifecycle and direct/effective location
- `boxes`: the first 50 Receipt-created OUTER_BOX rows, each with material/revision, actual Item quantity, UOM, pack count, WeekCode summary, Hold/exception facts, and direct/effective location
- `boxLimit` and `hasMoreBoxes`

Location lookup is a single batch repository call over the detail HU ids. It follows the existing direct-location-first, active-parent fallback semantics. Unknown Receipt ids return 404.

## Exception semantics

The UI tab is deliberately named **入库后异常**. It includes only committed Receipts for which current/provable WMS facts show:

- `HOLD`: the Receipt-created OUTER_BOX currently has `IsHold = 1`.
- `WEEKCODE`: an active HoldLog exists for that box with `HoldReasonCode = WEEKCODE_UNRESOLVED`.

`exceptionBoxCount` is the union of those box sets so one box is counted once. Released historical HoldLog rows do not keep a resolved box in the current exception tab; its history remains available on Page 03.

## Successful inbound versus rejected inbound

The existing inbound repository inserts the Receipt and all HU/Relation/Item/Hold/Move/Operation facts in one serializable transaction. Hard validation and duplicate barcode failures roll the transaction back. No finished-goods inbound failure/audit repository or table currently persists those rejected source requests.

Therefore:

- `REJECTED_INBOUND_HISTORY_PERSISTED=False`
- `REJECTED_INBOUND_HISTORY_QUERYABLE=False`
- Page 04 must not imply that all rejected requests are queryable.
- A future integration audit/monitoring capability should own rejected HTTP/request history; Page 04 does not add `InboundFailureLog`.

## HU integration

- The Grid's native `+ / -` child row is a fast box-fact view. Expansion reads the same Receipt detail GET as the Drawer, renders a controlled HTML table rather than a nested jfGrid, shows up to the server-owned first 50 boxes, and states the limit when `hasMoreBoxes = true`.
- Learun jfGrid creates its callback content inside the left row-number layer and a separate empty placeholder inside the main scroll area. Page 04 resolves both DOM nodes by `rownum`, hides the covered left-layer content, and renders loading/data/error states into the visible main-area placeholder.
- The child row shows box code, lifecycle status, material/revision, quantity/UOM, InnerPack count, WeekCode, exception summary, and effective location. It has loading, empty, and retry-safe error states; stale async responses are discarded after collapse or grid redraw.
- Clicking a real Receipt row opens a read-only `Wms.SideDrawer`; page load, query, reset, paging, and grid rendering keep it closed.
- Clicking the `+ / -` icon does not open the Drawer. Clicking a normal parent-row cell still opens it.
- Clicking a box uses `learun.frameTab` to open `/WMS/HuDetail/Index?huCode=...` (Page 03). It does not stack another Drawer.
- Page 02's inventory preview has the same small “查看完整 HU 履历” frameTab entry.
- The Drawer includes no retry, receive, reprocess, Release, Recalculate, move, edit, delete, or export action.

## Permissions and transport

The Learun MVC page is a normal authenticated query/view page. Business JavaScript calls only `Wms.Api` through `/api/wms/*`, binds camelCase response fields, and has no direct database access or hard-coded backend port.

## Known backend gaps

- Rejected-before-commit inbound history is not persisted or queryable.
- There is no existing reconciliation result contract comparing MES requested quantity with committed WMS quantity; V1 does not invent one.
- The current Foundation pager visual remains deferred; server paging is implemented and uses the stable Page 02 grid pattern.

## Verification record

- Backend library, Nancy host, and Learun Web Debug builds pass.
- Console regression suite passes 49 tests.
- Runtime GET verification returned 54 Receipt rows; page 1/2 had no duplicate ids. Plant, source identity, RequestId, date, box, material, exception, WeekCode, and Hold filters returned consistent results.
- Runtime samples covered 61 boxes / 251 packs / 14 materials / Qty 19210, inherited location, 20 normal Receipts, 34 current-exception Receipts, and a no-pallet Receipt with two boxes.
- JavaScript syntax, UTF-8 BOM, CRLF, camelCase/read-only route, and no-mojibake checks pass.
- The application browser runtime could not initialize, so logged-in visual and click verification remains a human review item.
