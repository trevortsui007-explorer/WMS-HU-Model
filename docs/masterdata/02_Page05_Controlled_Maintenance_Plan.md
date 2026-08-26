# Page05 Controlled Maintenance Plan

## 1. Scope

This plan is implemented by Phase 1D Batch C2. `PAGE05_PRODUCTION_UI_CHANGED=True` and `BACKEND_VALIDATION_ENHANCED=True`; the existing `/api/wms/master-data/options` endpoint was reused, so `BACKEND_OPTIONS_ENDPOINT_ADDED=False`.

Primary operator scope is `STORAGE_PHYSICAL_LOCATION`. Physical Locations are supervisor-maintainable; system virtual Locations are system-maintained and read-only.

## 2. Current baseline

Page05 already has Warehouse, WarehousePlant and Location tabs. Location create/edit exposes Warehouse, Zone, LocationCode, LocationName, LocationType, IsScannable, ScanCode and Remark. Warehouse and LocationCode are locked on edit. Grid also shows IsVirtual, Current HU, MasterStatus and system protection.

The existing options endpoint is already the right minimal boundary:

- Warehouses: active/inactive DB WMS_Warehouse options.
- Zones: DB WMS_Zone rows scoped by Warehouse.
- LocationTypes: server list, currently only DOUBLE_RACK. Human has approved DOUBLE_RACK, TRIPLE_TIER_REMAINDER_RACK and FLOOR_STORAGE for a future supervisor-maintainable physical allowlist; VIRTUAL remains system-only.
- Status: currently hardcoded in Page05 and should move to a server-owned option contract.

`Wms.Terms` remains display-only and must never be enumerated to decide valid values.

## 3. Proposed controlled selectors

| Field | Proposed allowed-value source | Display | Create/edit behavior |
|---|---|---|---|
| Warehouse | existing `/master-data/options` active WMS_Warehouse | code + name | select on create; read-only on edit |
| Zone | existing options from active WMS_Zone for selected Warehouse | code + name | required controlled select |
| LocationType | server-owned approved physical-type allowlist returned by options endpoint | DOUBLE_RACK→双层货架; TRIPLE_TIER_REMAINDER_RACK→三层尾数货架; FLOOR_STORAGE→平仓库位; VIRTUAL→虚拟货位 (display only) | required controlled select; supervisor list excludes VIRTUAL; never `Object.keys(Wms.Terms)` |
| Status | server-owned MasterStatus allowlist, initially ACTIVE/INACTIVE for supervisor workflow | Chinese business text | no free text; disable invokes guard API |

Formal proposals:

- `WAREHOUSE_SELECT_SOURCE_PROPOSAL=WMS_Warehouse via existing server options endpoint`
- `ZONE_SELECT_SOURCE_PROPOSAL=WMS_Zone via existing warehouse-scoped server options endpoint`
- `LOCATION_TYPE_SELECT_SOURCE_PROPOSAL=server-owned approved physical LocationType allowlist`
- `STATUS_SELECT_SOURCE_PROPOSAL=server-owned Location MasterStatus allowlist and guarded enable/disable routes`

## C2 Final governance alignment

- `CODE_SERIES_MAINTENANCE_OWNER=IT`
- `SUPERVISOR_CAN_CREATE_CODE_SERIES=False`
- `SUPERVISOR_CAN_CREATE_LOCATION_TYPE=False`
- `SUPERVISOR_CAN_CREATE_ZONE=False`
- System Locations are view-only in Page05 and reject update/status changes in Backend.
- Physical Location status remains 启用/停用; the supervisor operation uses 下线/启用.
- Offline requires a live empty-location check, changes status only, never deletes or moves inventory, and allows later re-enable.
- Existing putaway, relocation, unpack placement and return-to-stock target paths require an ACTIVE target; `INACTIVE_LOCATION_TARGET_GUARD_GAP=False`.
- `PAGE05_SELECT_VALUES_SERVER_OWNED=True`
- `WMS_TERMS_USED_AS_ALLOWED_VALUE_SOURCE=False`

## 4. Maintenance policy

### Physical STORAGE Location

- Add: allowed after Warehouse/Zone/type validation and uniqueness check.
- Edit: LocationName, approved Zone, approved LocationType, scanning fields and Remark.
- LocationCode: immutable after create.
- Enable/disable: allowed only when Backend confirms not system-protected and not in use.
- Current HU: read-only.
- IsVirtual/SystemProtected: read-only.
- Capacity: not exposed until Capacity unit and update semantics are approved.

### System virtual Location

- Clearly tag as `系统货位`.
- Entire record read-only in supervisor UI.
- No create using protected code, code/type/Zone edit, disable, or removal action.
- Continue system initialization ownership.

## 5. C2 Backend proposal

Keep the current model and avoid new tables unless later evidence requires them.

1. Extend `MasterDataOptionsDto` with server-owned status options and the approved physical LocationType values DOUBLE_RACK, TRIPLE_TIER_REMAINDER_RACK and FLOOR_STORAGE.
2. Keep Zone options DB-owned through WMS_Zone.
3. Keep LocationType storage inline; validation must use the same server allowlist returned by options.
4. Return a business display label or let the page apply `Wms.Terms` only for known display text, including VIRTUAL→虚拟货位 on protected rows. Allowed values must remain independent from Terms, and VIRTUAL must not appear in the supervisor create selector.
5. Preserve critical Location allowlist and current-HU disable guard.
6. Decide explicitly whether code normalization remains uppercase or becomes Trim-only before importing nonstandard codes such as `01W`.

## 6. Capacity phase boundary

The current schema already supports CapacityMode and CapacityQty, so no schema change is required merely to retain a numeric value. However, C2 must not expose capacity editing until Human confirms:

- the unit;
- whether each Excel group quantity distributes uniformly;
- whether Capacity is advisory or enforced;
- interaction with EffectiveLocation and mixed HU storage.

Until then, Capacity is read-only evidence and `DEFER_TO_FUTURE_CAPACITY_PHASE=True`.

## 7. Implemented C2 controls

- Supervisor scope is locked to `STORAGE_PHYSICAL_LOCATION` in both Page05 and LocationService.
- Warehouse values come from WMS_Warehouse options; Zone values come from warehouse-scoped WMS_Zone options and display the DB ZoneName; LocationType values come from the server-owned physical allowlist.
- Approved physical types are DOUBLE_RACK, TRIPLE_TIER_REMAINDER_RACK and FLOOR_STORAGE. VIRTUAL is displayable but absent from supervisor options and rejected by create/update validation.
- LocationCode is Trim-only on create and route-immutable on edit. LocationName remains optional and the Grid falls back to LocationCode without writing a generated name.
- CapacityQty is absent from the Page05 payload and is reset to null for supervisor create. Existing capacity is preserved by update and remains non-editable.
- Any IsVirtual Location or protected system code opens in read-only mode and cannot be edited or enabled/disabled. Physical disable uses a client freshness warning only; the repository-backed Current HU count remains authoritative.
- There is no delete action. Save refreshes only the current entity Grid, preserving the selected tab and filters.
- Wms.Terms maps all four display codes but is not enumerated as the allowed-value source.
- `MASTER_DATA_AUDIT_GAP=True`; no HU OperationLog reuse and no master-data audit table were introduced.

## 8. C2 acceptance outline

- Select sources verified from server responses, not page dictionaries.
- LocationType shows Chinese text but posts raw approved code.
- System virtual rows visibly protected and cannot open a mutable form.
- Physical row with Current HU cannot be disabled.
- LocationCode remains immutable.
- DirectLocation/EffectiveLocation semantics unchanged.
- Page05 terms rollout, Pager, Modal and authorization behavior pass regression.
