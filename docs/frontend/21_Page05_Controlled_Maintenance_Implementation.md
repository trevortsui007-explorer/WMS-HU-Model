# Page05 Controlled Master Data Maintenance Implementation

## Scope and ownership

`SUPERVISOR_SCOPE=STORAGE_PHYSICAL_LOCATION`. Page05 retains the Warehouse, WarehousePlant and Location tabs, with C2 focused on safe Location maintenance. C1.5 seed execution remains Human-owned and was not executed by Codex.

Code-series governance is IT-owned. Supervisors cannot create ZoneCode, LocationTypeCode, system LocationCode or any new code series. A new business code requires an IT change to the server-owned allowlist, its Wms.Terms display mapping and deployment before it becomes selectable.

The existing contracts remain the authority:

- Warehouse selector: active WMS_Warehouse rows returned by `/api/wms/master-data/options`.
- Zone selector: active, warehouse-scoped WMS_Zone rows; Page05 limits physical maintenance to STORAGE and displays ZoneName before ZoneCode.
- LocationType selector: server-owned allowlist containing DOUBLE_RACK, TRIPLE_TIER_REMAINDER_RACK and FLOOR_STORAGE.
- Wms.Terms: display-only Chinese mapping. VIRTUAL is known for protected-row display but is not a supervisor-selectable value.

No new options endpoint, master table, schema or migration was needed.

## Physical Location behavior

- Create: Warehouse and LocationType are controlled selectors; Zone is resolved from the selected warehouse and locked to STORAGE; LocationCode is editable and Trim-only; LocationName is optional.
- Edit: Warehouse, Zone and LocationCode are locked; LocationName, approved physical LocationType, scan settings and Remark remain editable.
- CapacityQty is neither rendered nor submitted. Supervisor create forces it to null; update preserves the persisted value.
- Grid status remains `启用/停用`; the physical operation is `下线/启用`. A positive Grid CurrentHuCount shows `当前货位仍有 X 个物流单元，请先移货，清空货位后再下线。` and does not issue the request. This is UX only.
- LocationService is authoritative and repeats the live repository occupancy count. The status UPDATE itself also contains a `NOT EXISTS WMS_HU_CurrentLocation` guard, closing the count-to-update race window. An occupied result is `当前货位仍有库存，请先完成移货，确认货位为空后再下线。`.
- Offline changes MasterStatus to INACTIVE only. It does not delete the master row, create a Move, relocate inventory or expose a force-disable path. The same empty physical Location can later be enabled again.
- Delete is unavailable.

## System Location behavior

The six protected codes and every `IsVirtual=true` Location enter system mode. The Grid identifies them as system-maintained; Edit becomes View; the Modal title is `查看系统货位`, displays `该货位由系统维护，如需调整请联系 IT。` and locks all controls. Backend update and both status directions reject system Locations, including virtual codes not present in the six-code list.

## Inactive target audit

`INACTIVE_LOCATION_TARGET_GUARD_GAP=False`. Existing backend guards already reject an inactive target in normal putaway (`PutawayService`), relocation (`HuLocationService`), unpack placement (`RepackService`) and return-to-stock (`FormalOutboundRepository`). Return-to-stock resolves and locks the target inside its transaction before checking ACTIVE, physical and STORAGE constraints. C2 therefore did not add a Page05-only target filter or duplicate target-selection rule.

## Business display

- Grid uses `货位编码`, `货位类型`, `虚拟货位`, `当前物流单元` and `系统维护`.
- Zone displays WMS_Zone.ZoneName with ZoneCode fallback.
- LocationType displays Wms.Terms text with raw-code fallback for future unknown values.
- Empty LocationName displays LocationCode without writing it back.

## Validation and audit boundary

LocationService enforces the approved physical type allowlist, STORAGE-only physical scope, LocationCode Trim-only normalization, route-immutable edit identity, system protection and live occupancy guard. Client checks are UX only.

`MASTER_DATA_AUDIT_AVAILABLE=False`, `MASTER_DATA_AUDIT_GAP=True`, `NEW_MASTER_DATA_AUDIT_TABLE=False`. C2 does not misuse HU OperationLog for master-data CRUD.

## Verification

- `agents/wms-page05-controlled-maintenance-harness.js`: C201–C225 formal contract plus C226–C228 retained compatibility checks.
- `agents/wms-page05-server-validation-harness.js`: SV201–SV207.
- `agents/wms-terms-harness.js`: T01–T29.
- Existing Page01–Page04, Pager and Drawer harnesses remain the regression gate.
- Human Runtime must still validate create, H201 edit, INBOUND-PENDING view, occupied disable, selector contents, Chinese Grid display and tab/filter preservation.
