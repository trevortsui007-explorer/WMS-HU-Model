# WMS Terms Batch B Page Rollout

## 1. Status and scope

- `WMS_TERMS_FOUNDATION_HUMAN_REVIEW_PASS=True`
- `WMS_TERMS_FOUNDATION_V1_CLOSED=True`
- `PHASE_1D_BATCH_B_IMPLEMENTED=True`
- Rolled out: Page02 Finished Goods Inventory, Page03 Logistics Unit Detail / History, Page04 Finished Goods Inbound Records.
- Regression only: Page01. Not rolled out: Page05, Page06, Page07.
- No backend, API contract, proxy, database, schema, migration, Pager Foundation, Drawer Foundation, jfGrid, HU domain, inventory rule, inbound rule, or EffectiveLocation semantic change.

## 2. Page02 decisions

Page02 grain is `OUTER_BOX`, so its business context is external-box inventory rather than a generic HU list. The page title is `成品库存 / 外箱查询`; `HUCode` is labeled `箱码`; the preview is `外箱详情`; and navigation is `查看物流履历`. `Wms.Terms` supplies HU type, lifecycle, stock, Hold, Hold reason, and location-type display text. The former local `stockLabel` dictionary was removed.

The primary Grid order remains business-first: 箱码、物料、版本、数量、生产周、有效货位、库存状态、冻结状态、所属托盘、包装时间. Direct and effective locations were not conflated. Inherited location text uses `继承自托盘`; internal HU identity and source evidence are placed in the final `技术追溯` section.

## 3. Page03 decisions

Page03 is the generic hierarchy page and therefore uses `物流单元详情 / 履历`, `物流单元编码（HU）`, and `包装层级`. PALLET, OUTER_BOX, and INNER_PACK remain raw DTO values but render as 托盘、外箱、内包 through `Wms.Terms.huType`, including tree nodes.

Direct location, effective location, and inheritance source remain separate. Contents use 周期码、生产周 and mapped stock status. Move, operation, and Hold histories use `moveType`, `operationType`, `holdStatus`, and `holdReasonCode`; raw event codes are no longer timeline titles. OperationNo, MoveNo, HoldNo, RequestId, HU internal identity, and raw JSON remain trace evidence with Chinese context labels. JsonViewer is preserved under `原始审计明细（JSON）`.

## 4. Page04 decisions

Page04 is `成品入库记录`, not an interface-log projection. Its Grid leads with 入库时间、厂别、数据来源、托盘、箱数、内包数、总数量、物料摘要、冻结箱、异常箱、入库状态. `Wms.Terms.inboundStatus` supplies `已入库` / `存在异常`; the old local status dictionary was removed while the existing formatter remains solely for StatusTag tone.

Source type, source record identity, and execution identity follow the business columns and use a muted secondary style. The Drawer separates 入库概要、异常摘要、来源与技术追溯. `ReceiptId` is correctly labeled `内部 Receipt ID`, never `入库单号`. An empty pallet remains `—`; no new exception inference or API field was introduced.

## 5. Before / after terminology

| Context | Before | After |
|---|---|---|
| Page02 row identity | HU Code / technical HU wording | 箱码 |
| Page02 type/status | OUTER_BOX / AVAILABLE / HOLD | 外箱 / 可用 / 已冻结或未冻结 |
| Page03 page/type | HU详情 / OUTER_BOX | 物流单元详情 / 外箱 |
| Page03 timeline | raw MoveType / OperationType | Chinese business event first |
| Page03 Hold | HoldLog / ACTIVE / RELEASED | 冻结履历 / 已冻结 / 已解除冻结 |
| Page04 receipt status | RECEIVED / WITH_EXCEPTION | 已入库 / 存在异常 |
| Page04 technical identity | mixed with primary fields | final secondary columns/section |

Unknown enum values intentionally fall back to their exact raw value. Allowed technical exceptions include MES, SAP, WMS, PCS, the first explanatory HU/DateCode use, RequestId context, and JSON evidence. They are not used as uncontextualized business-primary labels.

## 6. Static counts and readability

Compared with the pre-rollout audit, reviewed business-primary display sites on Page02/03/04 no longer author raw HU/lifecycle/stock/move/operation/inbound enum labels. English-only authored business labels were reduced from Page02 `18`, Page03 `11`, Page04 `25` to zero in the modified primary label sites. Reviewed IT-secondary-as-primary and raw-technical-as-primary sites were reduced to zero by contextual labeling and placement; necessary technical evidence remains available.

- Page02: `A candidate`
- Page03: `A/B candidate`
- Page04: `A/B candidate`

These are categorical Human-review targets, not numeric UI scores. Runtime data may still expose a newly introduced unknown raw code by design.

## 7. Automated regression

`agents/wms-terms-pages-batch-b-harness.js` covers B01–B22 plus script load order, duplicate-mapper prevention, display-only integration, Page04 technical-field de-emphasis, and Page02/03/04 functional integration contracts (B23–B29). B01–B29 pass. Foundation Terms T01–T25, Pager P01–P10 and Page01/02/04/07 integration, Drawer D01–D12 and page integration, Page01 Business Readability B01–B12, Recalculate UX R01–R17, and Parent Modal integration all pass. Modified JavaScript syntax checks pass.

Learun Web Debug build passes with 0 errors. MSBuild emitted 2 pre-existing access-denied warnings while writing dependency-project assembly-reference cache files; the affected dependency DLLs and `Learun.Application.Web.dll` were still built. All Batch B files are UTF-8 with BOM and CRLF; `MOJIBAKE_COUNT=0`.

Automated checks validate static/integration contracts. Human Runtime is still required for real data rendering, Page02 A→B Drawer behavior, Page03 three HU levels and lazy histories, Page04 no-pallet/exception receipts, navigation, and visible Pager behavior.
