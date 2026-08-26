# Finished Goods Location Master Proposal

## 1. Review status and safety gate

- Phase: `Phase 1D Batch C1 - Location Master Baseline & Excel Import Design`
- Primary source: `D:\Desktop\PCB成品仓开发\货位号.xlsx`
- Workbook SHA256 before and after analysis: `A8D86E10C9EB100E280DC0342154F2F0DFB2F405AB557C02CA459B649D47AAC0`
- `DATABASE_READ_ONLY=True`; live WMS_DEV capture used `ApplicationIntent=ReadOnly` and a SELECT-only whitelist.
- `DATABASE_WRITE_EXECUTED=False`; `EXCEL_WRITE_EXECUTED=False`; migration 010–014 and WMS_DEV data were not changed.
- Apply SQL is a Human-review draft with `@Apply=0`; it was generated but not executed.
- Human Review confirms `01W` through `32W` as 32 independent physical labels and approves the three physical LocationType codes in section 9. This document and all generated artifacts use that rebaselined rule.

This proposal follows the evidence order: live WMS_DEV schema/data, current Backend contracts, workbook facts, then historical design intent. The named historical files `WMS_HU_货位基础设计汇报` and `WMS_HU_IT实现设计_详细版` were not found in the project or supplied folder. The latest Human Alignment supplied in this task is therefore recorded explicitly rather than silently substituting another document. `docs/database/003_CreateMasterData.sql` is treated as historical implementation design, not as current schema truth.

## 2. Historical Human Alignment

- LocationCode grain is one physically identifiable, locatable and scannable warehouse location.
- Capacity never creates child Location records. `H201` with capacity 4 remains one Location; no `H201-1` or `H201-01` is generated.
- `H201`-level codes are accepted current physical labels.
- Double-rack evidence supports 4 capacity-equivalent positions per Location, but the unit name remains `PENDING_HUMAN_CONFIRMATION`.
- `B001` is one explicit LocationCode with quantity 10, directly proving that Excel quantity is not Location count.
- `01W` through `32W` are 32 independent LocationCodes with step 1. No suffix such as `01W-1` or `01W-2` may be generated.
- PALLET remains a transport HU. Rack capacity does not change OUTER_BOX inventory, DirectLocation, EffectiveLocation, or inheritance semantics.
- Excel owns physical-location facts only. System virtual Locations remain system-maintained.
- Supervisor scope is primarily `STORAGE` physical Locations.

Formal alignment: `EXCEL_UNIT_SEMANTICS=CAPACITY`, `CAPACITY_CREATES_LOCATION=False`, `SUB_LOCATION_AUTO_GENERATION=False`, `LOCATION_CODE_GRAIN=PHYSICAL_IDENTIFIABLE_SCANNABLE_LOCATION`.

## 3. EXCEL_RAW_STRUCTURE

Workbook contains four visible Sheets, no hidden rows/columns and no autofilters. The first three Sheets merge `B1:C1` under the heading `货位号`, so columns B/C are range start/end rather than two unrelated business fields.

| Sheet | Used range | Data rows | Headers / effective columns | Merged cells | Blank area / notes |
|---|---:|---:|---|---|---|
| 双层货架 | A1:D11 | 9 | 序号; 货位号起点; 货位号终点; 数量（个） | B1:C1 | Final total row; quantity total 404 |
| 三层尾数货架 | A1:E9 | 7 | 序号; 起点; 终点; 注; 数量（个） | B1:C1 | One blank note; final total 85 |
| 平仓库位 | A1:D21 | 19 | 序号; 起点; optional 终点; 数量（个） | B1:C1 | Five standalone-code rows; final total 390 |
| Sheet1 | A1:C5 values; OOXML dimension A1:C6 | 4 descriptive rows | 名称; 数量（块）; 尺寸（CM） | none | Quantities blank; only 成品储存区 has 40*30 size |

Raw physical groups are preserved below. No source code was uppercased, dash-rewritten, or prefixed; only surrounding whitespace is proposed for removal.

| Sheet | Raw code/range | Raw note | Quantity |
|---|---|---|---:|
| 双层货架 | H101–H112 | — | 48 |
| 双层货架 | H201–H212 | — | 48 |
| 双层货架 | H301–H310 | — | 40 |
| 双层货架 | H401–H410 | — | 40 |
| 双层货架 | H501–H511 | — | 44 |
| 双层货架 | H601–H611 | — | 44 |
| 双层货架 | H701–H710 | — | 40 |
| 双层货架 | H801–H812 | — | 48 |
| 双层货架 | H901–H913 | — | 52 |
| 三层尾数货架 | A01–A10; A11–A20; B01–B10; B11–B20; B21–B29 | S/Z/X（上中下） | 10; 10; 10; 10; 9 |
| 三层尾数货架 | D301–D320 | — | 20 |
| 三层尾数货架 | `01W `–`32W` | S/Z/X（上中下） | 16 |
| 平仓库位 | C001–C011; C012–C023 | — | 22; 24 |
| 平仓库位 | D001–D013; D014–D027; D028–D031 | — | 26; 28; 12 |
| 平仓库位 | E001–E019; E020–E044; E045–E048 | — | 38; 50; 16 |
| 平仓库位 | F001–F027; F028–F047 | — | 54; 40 |
| 平仓库位 | B001; B002; B003; B101; B202 | standalone | 10 each |
| 平仓库位 | B301–B307; B401–B407; B501–B508; B601–B608 | — | 7; 7; 8; 8 |

`01W ` contains trailing whitespace and is retained as raw evidence. Human confirmed Trim-only codes `01W` through `32W`, step 1, so this row expands to exactly 32 independent Locations. Quantity 16 remains capacity evidence and does not alter that count or create sublocations.

## 4. Excel unit and capacity evidence

`EXCEL_UNIT_SEMANTICS=CAPACITY` with `CAPACITY_UNIT=PENDING_HUMAN_CONFIRMATION`.

- Double-rack range H201–H212 contains 12 standard codes and quantity 48: 4 per Location. All nine double-rack groups reconcile to 4 per code.
- Live H201–H204 each have `CapacityQty=4.0000`, independently aligning DB with the workbook calculation.
- Flat-location B001 is one code with quantity 10, disproving `quantity = Location count`.
- Other flat groups yield varying per-code values (for example 2, 3, 4 or 10), so capacity must remain row/group evidence rather than a global LocationType rule.
- The schema can store `CapacityQty decimal(18,4)`, but import of capacity is deferred until the unit and uniform allocation within each range are approved. The Apply Draft omits `CapacityQty` from its INSERT column list entirely.

Therefore `CAPACITY_DATA_FOUND_IN_EXCEL=True`, `CAPACITY_MODEL_CURRENTLY_SUPPORTED=True`, and `DEFER_TO_FUTURE_CAPACITY_PHASE=True` for operational enforcement/import until Human confirmation.

## 5. Current implemented Master Data

`CURRENT_MASTER_DATA_ENTITIES`: WMS_Warehouse, WMS_WarehousePlant, WMS_Zone (option/master dependency), WMS_Location. WMS_PackageSpec exists but is not a Page05 tab.

`CURRENT_PAGE05_TABS`: 仓库, 厂别映射, 货位.

`CURRENT_LOCATION_EDIT_FIELDS`: 仓库, 区域, 货位代码, 货位名称, 货位类型, 可扫描, 扫描码, 备注. On edit, Warehouse and LocationCode are locked. Current HU count and system-protection status are read-only Grid fields. Capacity is preserved by the existing Form payload but is not editable in Page05.

Current Backend behavior:

- Create/edit accepts only `DOUBLE_RACK`; system critical codes cannot be created or edited.
- Disable is blocked for protected codes and for any Location with current HU.
- LocationCode is route identity and cannot be changed by Update.
- Backend currently normalizes codes with Trim + uppercase. This conflicts with the new import rule `Trim only`; C1 draft uses the Excel spelling as-is and flags future C2/API normalization for Human review.
- `GET /api/wms/master-data/options` returns Warehouse options from DB, Zone options from `WMS_Zone`, but LocationTypes from a server-owned hardcoded list containing only `DOUBLE_RACK`.

## 6. CURRENT_LOCATION_SCHEMA

Live WMS_DEV columns match frozen schema evidence SHA256 `9CB0B9B227A2D49E7483A5BDC2F04E71835779C53AB8E5AA0FB5AF7EBAEECFF8` for `WMS_Location`.

| Column | Type | Nullable | Default / key evidence |
|---|---|---:|---|
| Id | bigint identity | no | PK clustered |
| WarehouseId | bigint | no | FK WMS_Warehouse |
| ZoneId | bigint | no | FK WMS_Zone; IX with MasterStatus |
| LocationCode | varchar(50) | no | unique with WarehouseId |
| LocationName | nvarchar(200) | yes | — |
| ParentLocationId | bigint | yes | self FK |
| LocationLevel | varchar(20) | no | `BASE` |
| LocationTypeCode | varchar(30) | no | inline code; no FK/check |
| ManageMode | varchar(30) | no | no DB default |
| IsVirtual | bit | no | 0 |
| IsScannable | bit | no | 1 |
| ScanCode | varchar(100) | yes | filtered unique with WarehouseId |
| CapacityMode | varchar(30) | no | no DB default |
| CapacityQty | decimal(18,4) | yes | current capacity field |
| MaxWeightKg | decimal(18,4) | yes | — |
| PlantPolicy | varchar(20) | no | `SAME_PLANT` |
| DedicatedPlantCode | varchar(20) | yes | — |
| AllowMixedMaterial / Batch / Order | bit | no | 0 each |
| PutawayPriority / PickingPriority | int | no | 100 each |
| RouteSequence | int | no | 0 |
| MasterStatus | varchar(20) | no | `ACTIVE`; no check constraint |
| Remark | nvarchar(1000) | yes | — |
| CreateUser / ModifyUser | varchar(50) | yes | — |
| CreateTime / ModifyTime | datetime2(0) | no / yes | CreateTime sysdatetime |
| RowVersion | timestamp | no | — |

There are no check constraints on these five master tables. Live schema has wider name/remark columns than `003_CreateMasterData.sql` (200/1000 vs historical 100/500); this is implementation drift and proves the migration file cannot be used as current schema truth.

`WMS_ZONE_TABLE_EXISTS=True`; Zone is a real master table keyed by Warehouse + ZoneCode. `WMS_LOCATIONTYPE_TABLE_EXISTS=False`; LocationType is an inline `varchar(30)` controlled by Backend validation/options.

## 7. Current WMS_DEV data snapshot

- Warehouse: active `FG01`, 成品仓, `FINISHED_GOODS`.
- WarehousePlant: active `1010→11C3`, `1020→12C3`, plus active `9010→NULL`. Excel has no Plant mapping and will not alter any of them; 9010 is a review observation, not an automatic conflict action.
- Zones: STORAGE, RECEIVE, RETURN, STAGING, SYSTEM; all active.
- Locations: 10 total; 4 physical and 6 virtual; all ACTIVE.

| Location | Zone | Type | Virtual | Capacity | Current HU | Ownership |
|---|---|---|---:|---:|---:|---|
| H201 | STORAGE | DOUBLE_RACK | no | 4 | 1 | Supervisor-maintainable; currently in use |
| H202 | STORAGE | DOUBLE_RACK | no | 4 | 0 | Supervisor-maintainable |
| H203 | STORAGE | DOUBLE_RACK | no | 4 | 0 | Supervisor-maintainable |
| H204 | STORAGE | DOUBLE_RACK | no | 4 | 0 | Supervisor-maintainable |
| INBOUND-PENDING | RECEIVE | VIRTUAL | yes | unlimited | 56 | System-maintained |
| OUTBOUND-STAGING | STAGING | VIRTUAL | yes | unlimited | 0 | System-maintained |
| RETURNED | RETURN | VIRTUAL | yes | unlimited | 0 | System-maintained |
| RETURN-PENDING | RETURN | VIRTUAL | yes | unlimited | 0 | System-maintained |
| SHIPPED | STAGING | VIRTUAL | yes | unlimited | 2 | System-maintained |
| UNLOCATED | SYSTEM | VIRTUAL | yes | unlimited | 0 | System-maintained |

The Backend protected allowlist exactly contains the six system codes above. Excel cannot edit, disable, or remove them.

## 8. EXCEL_TO_WMS_MAPPING

| Excel evidence | Meaning | WMS target | Transform | Confidence / decision |
|---|---|---|---|---|
| Sheet business name | Physical storage form/type | LocationTypeCode proposal | Human-approved mapping in section 9 | high for all three physical types |
| 货位号 B/C | Scannable code or range endpoints | LocationCode | Trim only; standard ranges plus confirmed `01W–32W` step 1 | high |
| 数量（个） | Capacity evidence | CapacityQty candidate | never generates codes; group qty ÷ confirmed code count only after review | medium; unit pending |
| 注 S/Z/X（上中下） | Physical sub-position notation | deferred attribute | preserve raw; no sublocation generation | review |
| Sheet1 名称 | Business/process Zone names | WMS_Zone comparison | map only where existing DB name/code confirms | high for existing four business Zones |
| 尺寸（CM） 40*30 | Physical marker/label size evidence | no current Location field | preserve only | deferred |

LocationName is not supplied by the workbook. The draft does not fabricate `双层货架 + LocationCode`; inserts use NULL until Human supplies a naming rule.

## 9. Location type and Zone proposal

| Excel business type | Current DB code | Proposed code | Existing? | Model support / action |
|---|---|---|---:|---|
| 双层货架 | DOUBLE_RACK | DOUBLE_RACK | yes | Human-approved physical type; 97 missing codes in draft insert |
| 三层尾数货架 | none | TRIPLE_TIER_REMAINDER_RACK | not yet in Backend allowlist | Human-approved physical type; 101 missing codes in draft insert |
| 平仓库位 | none | FLOOR_STORAGE | not yet in Backend allowlist | Human-approved physical type; 184 missing codes in draft insert |
| 系统虚拟货位 | VIRTUAL | VIRTUAL | yes in DB | System-only; not an Excel selectable physical type |

No new LocationType table is required to store approved codes. The inline column can store all approved values. The remaining implementation gap is the future C1.5/C2 server-owned physical allowlist and display mapping; it does not change this reviewed Candidate/Diff baseline.

Zone is different from physical rack classification. Excel `双层/三层尾数/平仓` describes physical form or STORAGE subdivision, while DB `STORAGE/RECEIVE/RETURN/STAGING/SYSTEM` describes process area. `ZONE_SEMANTIC_CONFLICT=True` if these are collapsed. C1 proposes all physical candidates remain under existing STORAGE while physical form is represented by LocationType; any finer H区/尾数区/平仓 subdivision is deferred because the workbook does not provide a separate per-row Zone field.

`ZONE_STORAGE_MODEL=MASTER_TABLE`; `LOCATION_TYPE_STORAGE_MODEL=INLINE_CODE`; `CURRENT_INLINE_CODE_SUFFICIENT=True`; `CURRENT_BACKEND_ALLOWLIST_SUFFICIENT=False`; `NEW_ZONE_TABLE_REQUIRED=False`; `NEW_LOCATION_TYPE_TABLE_REQUIRED=False`.

## 10. Diff and proposed actions

Confirmed expansion produces 386 distinct candidate codes: 101 DOUBLE_RACK, 101 TRIPLE_TIER_REMAINDER_RACK (including `01W`–`32W`), and 184 FLOOR_STORAGE. Duplicate, invalid and range-review counts are all zero.

| Classification | Count | Decision |
|---|---:|---|
| EXCEL_ONLY | 382 | all are Human-approved physical candidates for draft insert |
| MATCHED_SAME | 4 | H201–H204; no action |
| MATCHED_DIFFERENT | 0 | no update proposal |
| DB_ONLY_PHYSICAL | 0 | none |
| SYSTEM_ONLY | 6 | retain; no Excel action |
| In-use Location change | 0 | H201 is occupied but no change is proposed |
| Proposed insert | 382 | draft only; the regenerated Diff still requires Human review |
| Proposed update | 0 | none |
| Proposed remove/disable | 0 | forbidden |

All 386 candidates remain traceable to source Sheet/row/raw range in `artifacts/masterdata/location-master-proposal.json/.csv`. The 32 W-range codes retain the original `01W `–`32W` evidence; no capacity-derived or tier-derived sublocation exists.

## 11. Conflict Matrix

| Topic | Excel | Historical Human design | Current DB | Backend | Recommendation |
|---|---|---|---|---|---|
| Location grain | range codes + standalone codes | one scannable physical label = one Location | H201-level records | code unique per Warehouse; update cannot rename | expand only standard proven ranges; no capacity-derived child codes |
| Zone | process-area names in Sheet1; no per-rack Zone column | physical type and Zone are separate | WMS_Zone master: 5 process Zones | options come from DB Zone | physical candidates use STORAGE; do not encode rack type in Zone |
| Location type | three physical Sheet types | type separate from capacity | inline DOUBLE_RACK/VIRTUAL current data | physical create/edit currently only DOUBLE_RACK | codes approved; extend future server physical allowlist without exposing VIRTUAL |
| Virtual Location | absent from rack Sheets | system maintained | 6 confirmed virtual records | six-code protected allowlist | always SYSTEM_ONLY |
| Capacity | quantity totals 404/85/390 | capacity does not create Locations | CapacityQty exists; H201–H204=4 | create supports field; edit preserves it | preserve evidence; Apply Draft writes no CapacityQty until unit/allocation approval |
| System protection | not Excel-owned | critical locations immutable | virtual rows active, some occupied | create/edit/disable guards | no system code in Apply Draft |
| Warehouse | no warehouse column | FG01 baseline | active FG01 | active Warehouse required | fixed proposal scope FG01; no warehouse creation |
| Plant mapping | absent | 1010/1020 SAP mapping separate | 1010,1020,9010 active | separate maintenance entity | no Excel action; Human may separately review 9010 |
| Code normalization | raw `01W ` shows whitespace | preserve site label; Trim only | current values uppercase | service currently uppercases | draft uses Trim-only values; resolve API normalization in C2 |
| Historical migration | not applicable | design intent only | live lengths 200/1000 | DTO validation matches live | treat live schema as truth; do not rewrite migration 003 |

## 12. SQL drafts and ownership

- `FinishedGoodsLocation_Diff.sql` is SELECT-only and reports Excel-only, matched and DB-only/system classifications.
- `FinishedGoodsLocation_Apply_DRAFT.sql` contains 382 missing physical candidates across the three Human-approved types, `SET XACT_ABORT ON`, explicit transaction, `@Apply=0`, `IF NOT EXISTS`, no `CapacityQty` INSERT column, no system codes, no LocationCode changes, no automatic status changes, and no removal statements.
- Excel source ownership: physical codes/types/capacity evidence.
- System initialization ownership: virtual Locations.
- WMS/SAP mapping ownership: WarehousePlant.

`IDEMPOTENT_APPLY_SQL_DRAFTED=True`, but C1 does not authorize execution. C1.5 must regenerate/finalize it after Human decisions rather than editing raw codes ad hoc.

## 13. Human decisions required

1. Review and approve the regenerated 386-candidate Diff and 382 proposed inserts.
2. Confirm the business unit represented by `数量（个）`; it remains `PENDING_HUMAN_CONFIRMATION`.
3. Confirm whether group quantity is uniformly allocated across every Location in each range before any future CapacityQty import.
4. Confirm whether LocationName may remain blank or provide a naming rule.
5. Implement and verify the future server-owned supervisor allowlist: DOUBLE_RACK, TRIPLE_TIER_REMAINDER_RACK and FLOOR_STORAGE only; VIRTUAL remains system-only.
6. Add future display-only `Wms.Terms` mappings for the three physical types and VIRTUAL without using Terms as an allowed-value source.
7. Confirm DB-only physical retention rule (currently none) and permanent retention of all six system virtual Locations.
8. Decide whether Backend uppercase normalization remains acceptable or must change to Trim-only in C2.
9. Review the extra active FG01/9010 WarehousePlant mapping separately; Excel must not change it.

After these decisions: generate final C1.5 seed, obtain backup/snapshot confirmation, apply once, verify, apply a second time for idempotency, then perform Human acceptance. No database write is allowed before explicit approval.
