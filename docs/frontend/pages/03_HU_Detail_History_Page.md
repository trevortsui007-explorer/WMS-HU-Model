# Page 03 — HU Detail / History V1

## Purpose

`/WMS/HuDetail/Index?huCode={HUCode}` is the read-only full-detail page for a single handling unit. It explains the selected HU, its current ACTIVE packaging context, inventory facts, direct/effective location, and bounded history. It is not a Page 02 drawer and exposes no warehouse write action.

## HU_DETAIL_EXISTING_APIS

| Method | Route | Request | Response | Can Reuse? | Gap |
|---|---|---|---|---|---|
| GET | `/api/wms/hus/{huCode}` | route HUCode | `HuDto` | Partial | Basic HU only; no relation, location, contents or hold summary. |
| GET | `/api/wms/hus/{huCode}/tree` | route HUCode | nested `HuTreeNodeDto` | Partial | Descendants only and item loading is per node; no ancestor context. |
| GET | `/api/wms/hus/{huCode}/location` | route HUCode | `HuLocationDto` | Yes | Reused by aggregate service; existing direct → ACTIVE parent fallback remains authoritative. |
| GET | `/api/wms/hus/{huCode}/moves?top=N` | route HUCode, optional top | `HuMoveLogDto[]` | Yes | Existing bounded, HU-scoped, newest-first query reused unchanged by the page. |
| — | — | — | — | No | No OperationLog read endpoint existed. |
| — | — | — | — | No | No HoldLog read endpoint existed. |
| Repository | `IHuRepository.GetActiveParent` | HUId | `HuDto` | Yes | Used at most twice to elevate to the three-level root. |
| Repository | `IHuRepository.GetTree` | root HUId | flat ACTIVE relation rows | Yes | Used once for the current tree. |
| Repository | `IHuRepository.GetItems` | HUId | active `HuItemDto[]` | Partial | Page aggregate uses one typed batch content query to avoid N+1. |
| GET | `/api/wms/inventory/finished-goods/{boxCode}/preview` | OUTER_BOX code | Page 02 preview | No | Current-inventory/OUTER_BOX-only semantics exclude historical HU types and SHIPPED HU. |

## V1 API contract

- `GET /api/wms/hus/{huCode}/detail`: basic HU, current parent/children, current flat tree, backend-computed location, current active hold, and type-specific contents.
- `GET /api/wms/hus/{huCode}/operations?top=50`: HU-scoped newest-first OperationLog, default 50, maximum 200.
- `GET /api/wms/hus/{huCode}/holds?top=50`: HU-scoped newest-first full Hold history, default 50, maximum 200.
- The existing `GET /api/wms/hus/{huCode}/moves?top=50` remains the MoveLog contract.

No schema, table, migration, view or stored procedure was added. All Page 03 repository statements are SELECT-only with typed parameters.

## HU types and current relation

PALLET, OUTER_BOX and INNER_PACK are valid entry points. The selected HU is elevated through at most two ACTIVE parents, then the existing recursive tree query loads the current maximum three-level structure in one call. Closed historical relations are not mixed into this tree. Relation history is deferred and may be inferred from OperationLog where recorded.

## Direct vs effective location

The frontend never derives location from the tree. The existing location service returns direct location fields, effective location fields after the verified ACTIVE-parent fallback, and `IsInherited`/`LocationSourceHuCode`. The page displays direct and effective values separately, including explicit `—` for a missing direct location.

## Item contract

- PALLET: one summary row per directly contained OUTER_BOX; descendant INNER_PACK rows are not flattened.
- OUTER_BOX: directly related ACTIVE INNER_PACK units joined to active Item snapshots in one query.
- INNER_PACK: its own active Item snapshots.

The page shows material/revision, batch/order, DateCode, Week format/rule/year/week/sort key, quantity/UOM, stock status and creation time. INNER_PACK is presented as the indivisible finished-goods inventory unit, not split into PCS rows.

## History, tree and lazy loading

- MoveLog uses the existing API and repository SQL; no CurrentLocation or MoveLog SQL was copied.
- OperationLog and HoldLog received minimal read-only HU-scoped APIs because none existed.
- Move and Hold records use `WmsTimeline` backed by Learun `lrtimeline`.
- Operation `DetailJson` uses `WmsJsonViewer`: parse/pretty print, copy, wrap, invalid-JSON fallback and text-node rendering.
- Current Hold in Overview means the one active Hold. Hold History includes active and released records.
- Each history tab lazy-loads once per HU and caches independently. A new search clears all detail/history state. Sequence guards discard stale detail and history responses.
- `WmsHuTree` adapts flat WMS DTO rows to Learun `lrtree`; node text is encoded before entering the legacy plugin. Tree selection only changes the page master/detail context.

## SHIPPED history and permissions

Explicit detail lookup does not apply Page 02 current-inventory lifecycle/location filters. ACTIVE, SHIPPED, VOID and CLOSED rows remain addressable when the HU exists. The page has no Move, Putaway, Pick, Cancel, Return, Hold, Release, Ship, Replace or Recalculate control.

## Known limitations

- V1 loads the most recent 50 records per history tab and does not yet expose “load more”; server APIs cap new history reads at 200.
- Alternate identifiers, dedicated relation history and combined all-activity timeline are deferred.
- Application-browser visual automation could not initialize in this environment; Human Visual Review remains required.
- Page 02 integration is intentionally deferred until Page 03 passes Human Review.
