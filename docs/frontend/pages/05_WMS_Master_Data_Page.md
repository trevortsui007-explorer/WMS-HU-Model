# Page 05 — WMS Master Data Maintenance V1

## Purpose

Page 05 maintains the small set of master data owned by finished-goods WMS. It uses one Learun page with Warehouse, WarehousePlant and Location tabs. Each tab follows Grid → Modal → Save → current-grid reload; enable/disable uses confirmation. No physical delete is exposed.

## Ownership Boundary

WMS owns:

- `WMS_Warehouse`
- `WMS_WarehousePlant`
- `WMS_Location`

WMS does not own MES Material, `MD_Flow`, `MD_FlowStation`, `MD_StationAttValue`, WeekCode rules, process stations, or SAP Material Master. Page 05 does not read or mutate those entities.

## Existing API Inventory

| Entity | Method | Route | Request | Response | Can reuse | Gap |
|---|---|---|---|---|---|---|
| Warehouse | — | — | — | — | No | No existing query or maintenance API |
| WarehousePlant | — | — | — | — | No | No existing query or maintenance API |
| Location | GET | `/api/wms/locations/` | optional warehouse/zone/status query | `LocationDto[]` | Yes, model/repository/service/list route | No maintenance filters, usage count or protection marker |
| Location | GET | `/api/wms/locations/{warehouseCode}/{locationCode}` | route keys | `LocationDto` | Yes | None for detail |
| Location | POST | `/api/wms/locations/` | `LocationCreateRequest` | `LocationDto` | Yes | Needed V1 validation and system-location protection |
| Location | PUT | `/api/wms/locations/{warehouseCode}/{locationCode}` | `LocationUpdateRequest` | `LocationDto` | Backend only | Learun proxy forwards GET/POST only; Page 05 uses POST update |

New browser-compatible maintenance contracts are under `/api/wms/master-data/*`; Location maintenance query and POST update/status routes remain under `/api/wms/locations/*`. JSON remains camelCase through the existing WMS host serializer.

## Warehouse Contract

The real schema fields used by V1 are `WarehouseCode`, `WarehouseName`, `WarehouseType`, `IsActive`, `Remark` and existing create/modify audit fields. `WarehouseCode` is the unique natural key and cannot be changed after creation. `WarehouseType` is a backend-provided select; the reviewed schema/runtime currently supports `FINISHED_GOODS`.

Disabling a warehouse is rejected when it has active locations, current HUs, or active WarehousePlant mappings. Enabling and disabling preserve all history.

## WarehousePlant Contract

The natural key is `(WarehouseId, PlantCode)`, presented as `(WarehouseCode, PlantCode)`. Both key parts are immutable on edit. `SAPStorageLocation` is editable and nullable, matching the real nullable column and existing runtime data. A new or re-enabled mapping requires an active warehouse. Duplicate keys are checked in the service and protected by the database unique constraint.

The table has `CreateTime` but no modify-user/modify-time columns. V1 does not add schema solely for UI audit needs.

## Location Contract

The natural key is `(WarehouseId, LocationCode)`, presented as `(WarehouseCode, LocationCode)`. Location code and warehouse are immutable on edit because current-location and movement facts reference Location rows. V1 exposes name, zone, physical type, scan code, active state and remark; it does not introduce capacity design.

The maintenance list calculates `CurrentHUCount` in one set-based query. It does not make one HU request per row. `MasterStatus` is the actual active-state field (`ACTIVE`/`INACTIVE`).

The six system virtual locations found at runtime are protected in the service by a reviewed allowlist because the schema has no `IsSystem`/`SystemManaged` column:

- `INBOUND-PENDING`
- `RETURN-PENDING`
- `OUTBOUND-STAGING`
- `UNLOCATED`
- `RETURNED`
- `SHIPPED`

They cannot be created through the normal physical-location form, edited, or disabled. This allowlist requires human review if deployment scripts add another system location.

## Natural Keys and Immutable Fields

| Entity | Natural key | Immutable on edit |
|---|---|---|
| Warehouse | `WarehouseCode` | `WarehouseCode` |
| WarehousePlant | `WarehouseId + PlantCode` | Warehouse and PlantCode |
| Location | `WarehouseId + LocationCode` | Warehouse and LocationCode |

## Enable / Disable Rules

- There are no DELETE routes or DELETE statements.
- Warehouse disable blocks active child locations, current HUs and active mappings.
- WarehousePlant disable changes only future routing availability; it does not rewrite receipts or HU history.
- New mappings and locations require an active warehouse.
- Location disable uses a repository count/EXISTS-style database check and is rejected when current HUs exist.
- System virtual locations are protected independently of UI button state.

## Modal Pattern and Validation

Forms use `Wms.Modal`/`learun.layerForm`, `_Form.cshtml`, `lr-form-wrap`, `lr-form-item`, `lrGetFormData`, `lrSetFormData`, `lr-validator` and `lrselect`. The Foundation modal now exposes a failure-release callback so an unsuccessful HTTP/business response unlocks Save without closing the form. Success closes the modal and reloads only the active entity grid.

The default Warehouse tab loads on entry. WarehousePlant and Location load only on first tab activation. Each tab owns its search DOM/state, and switching an already-loaded clean tab causes no request.

## Permissions

The MVC controller and same-origin WMS proxy reuse Learun login enforcement. Write toolbar buttons use `learun-authorize="yes"`; a viewer can still load the page and grids while Learun hides unauthorized actions. Service-side business guards remain mandatory. The current integration does not add a second role system or finer-grained WMS permission store.

## Audit Gap

The existing operation log is HU-scoped and is not reused for master data. There is no suitable general master-data audit table/pattern. Therefore `MASTER_DATA_AUDIT_REUSED=False` and `MASTER_DATA_AUDIT_GAP=True`. Warehouse retains its existing create/modify metadata; WarehousePlant schema cannot retain modifier identity. A unified audit design is deferred to Page 07.

## Runtime Evidence and Safety

Read-only runtime GETs found one Warehouse (`FG01`), three mappings (`1010/11C3`, `1020/12C3`, `9010/NULL`), ten locations, all six protected virtual codes, and 59 current HU associations in aggregate. These values are evidence, not frontend constants. Codex executed no create, update, enable or disable request and no WMS_DEV DML/DDL.

## Human Runtime Test

1. Open `/WMS/MasterData/Index`; confirm Warehouse alone loads initially.
2. Activate each remaining tab and confirm its first-load/lazy-load behavior.
3. Open each Add/Edit modal and verify immutable keys are read-only during edit.
4. Use dedicated non-production test records chosen by a human to test create, edit, disable and enable.
5. Verify a nullable SAP storage location saves and redisplays correctly.
6. Verify a Location containing a current HU cannot be disabled.
7. Verify inactive warehouses cannot be selected for new mappings or locations.
8. Confirm FG01, production mappings and system virtual locations remain untouched.

