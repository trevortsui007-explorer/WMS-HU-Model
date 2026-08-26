# Wms.Terms Foundation Contract

## 1. Purpose

`Wms.Terms` is the WMS V1 business terminology display mapper. It converts confirmed backend codes into Human-approved display text while preserving the original raw evidence.

Implementation: `Areas/WMS/Content/Foundation/Scripts/wms-terms.js`.

Namespace: `Wms.Terms`.

## 2. Scope

`Wms.Terms` serves `BUSINESS_STATUS` and a small set of stable label constants. It does not control page layout, decide where technical information is placed, or implement collapsed technical sections.

It is not an i18n framework, remote dictionary, database terminology table, validation service, or domain rule engine.

## 3. Display-only rule

Allowed:

- raw code → approved display text;
- known/unknown metadata;
- exact raw-value preservation;
- display labels such as 物流单元, 包装层级 and 周期码;
- context-specific mapper categories such as Lifecycle and Master active status.

Forbidden:

- API calls or request construction;
- permission or authorization checks;
- pick eligibility or Hold enforcement;
- state transition decisions;
- location updates;
- WeekCode calculation;
- SQL, Repository, Service or database access;
- Chinese display text → raw code reverse mapping.

Page code must continue sending the original DTO/raw value to APIs.

## 4. Load contract

The current Learun Foundation has no WMS-wide automatic script loader. Razor Views explicitly list scripts with `AppendJsFile`.

Any consuming page must load in this order:

```text
wms-core.js
wms-terms.js
page API/adapters/components as needed
consuming page script
```

`wms-terms.js` registers itself through `Wms.Registry.register("Terms", Terms)`. Batch A includes it in `Learun.Application.Web.csproj` but does not bulk-add it to Page01–Page07. Each rollout batch adds the reference only to pages that consume it.

## 5. Result contract

Every mapper returns:

```javascript
{
    text: "外箱",
    raw: "OUTER_BOX",
    known: true
}
```

Rules:

- Lookup trims surrounding whitespace and uses exact, case-sensitive keys.
- Known code: mapped `text`, original input in `raw`, `known=true`.
- Unknown non-empty code: original input in both `text` and `raw`, `known=false`.
- Null, undefined, empty or whitespace-only: `text="—"`, `raw=""`, `known=false`.
- Boolean-aware mappers preserve Boolean raw values.
- No mapper returns “未知” for a new code.
- `Wms.Terms.text(term)` returns `term.text`, or `—` for an absent result.

The preserved raw input means `Wms.Terms.huType("  OUTER_BOX  ")` is known and displays 外箱 while its `raw` remains exactly `"  OUTER_BOX  "`.

## 6. Stable labels

| Key | Text |
|---|---|
| `labels.huGeneric` | 物流单元 |
| `labels.huType` | 包装层级 |
| `labels.dateCodeShort` | 周期码 |
| `labels.dateCodeDetail` | 周期码（DateCode） |
| `labels.requestId` | 执行编号 |
| `labels.detailJson` | 原始审计明细（JSON） |
| `labels.holdStatus` | 冻结状态 |
| `labels.holdReason` | 冻结原因 |
| `labels.holdTime` | 冻结时间 |
| `labels.holdReleaseTime` | 解除冻结时间 |
| `labels.quantity` | 数量 |
| `labels.uom` | 单位 |
| `labels.pcs` | PCS |

Context-specific labels remain page-owned. There is intentionally no global `huCodeLabel()` because Page02 needs 箱码 while generic Page03 needs 物流单元编码.

## 7. Mapper API and known values

| Mapper | Known raw → display text |
|---|---|
| `huType` | PALLET→托盘; OUTER_BOX→外箱; INNER_PACK→内包 |
| `lifecycleStatus` | ACTIVE→有效; CLOSED→已关闭 |
| `stockStatus` | AVAILABLE→可用 |
| `operationType` | RECEIVE→接收入库; CREATE_HU→创建物流单元; ATTACH_HU→建立包装关系; DETACH_HU→解除包装关系; ADD_ITEM→增加库存明细; HU_HOLD→冻结; HU_RELEASE→解除冻结; PICK→拣货; PICK_BATCH→批次拣货; PUTAWAY→上架; UNPACK_INNER→内包拆箱; PACK_INNER→内包装箱; REPACK_TRANSFER→内包换箱; WEEKCODE_RECALCULATE→周码重新计算 |
| `moveType` | RECEIVE→入库移入; PUTAWAY→上架移动; RELOCATION→移库; RETURN→返库移动; STAGE→移至暂存区; CORRECTION→货位更正; UNPACK_INNER→内包拆出; PACK_INNER→内包装入; REPACK_TRANSFER→内包换箱移动; OUTBOUND_PICK→出库拣货; OUTBOUND_SHIP→出库发运; OUTBOUND_RETURN→出库退回; OUTBOUND_RETURN_TO_STOCK→退回库存 |
| `holdStatus` | true/HOLD/ACTIVE HOLD→已冻结; false→未冻结; RELEASED→已解除冻结 |
| `holdReasonCode` | WEEKCODE_UNRESOLVED→周码未解析; QUALITY_FAILED→质量异常 |
| `weekRuleSource` | MES_FLOW→MES工艺流程; SNAPSHOT→入库快照 |
| `riskLevel` | HIGH→高; MEDIUM→中; LOW→低 |
| `weekAuditType` | RULE_MISSING→规则缺失; INVALID_DATECODE→周期码异常; RULE_UNSUPPORTED→规则不支持; legacy alias UNSUPPORTED_WEEK_RULE→规则不支持; SUSPICIOUS_RULE_DIRECTION→周期格式方向可疑; SUSPICIOUS_YEAR→年份可疑 |
| `recalculateResult` | UPDATED→已更新; UNCHANGED→无需变化; FAILED→失败 |
| `monitoringStatus` | RUNNING/CONNECTED→正常; DEGRADED→部分异常; UNAVAILABLE→不可用; AVAILABLE→可用 |
| `auditCapability` | true/AVAILABLE→可审计; false/NOT_OBSERVABLE→当前不可审计 |
| `inboundStatus` | RECEIVED→已入库; WITH_EXCEPTION→存在异常 |
| `masterActiveStatus` | true/ACTIVE→启用; false/INACTIVE→停用 |
| `activeStatus` | Compatibility alias of `masterActiveStatus` |
| `locationType` | DOUBLE_RACK→双层货架 |

`StockStatus` intentionally maps only backend-confirmed `AVAILABLE`. Page02's old `RESERVED` and `HOLD` display branches are not promoted to known global values.

`LOW` is retained as a defensive frontend mapping; the audited backend WeekCode service currently emits HIGH/MEDIUM. `UNSUPPORTED_WEEK_RULE` is a compatibility alias; the current audit service emits `RULE_UNSUPPORTED`.

## 8. Context limitations

- Lifecycle `ACTIVE` defaults to 有效, never globally 在库有效.
- Master `ACTIVE` means 启用 and therefore uses a separate mapper.
- OperationType and MoveType are separate dictionaries even when the raw value is the same.
- WeekRuleValue values such as YYWW/WWYY are configuration values and are not translated.
- DetailJson label mapping does not implement collapse behavior.
- Page06 hides raw codes by default; Page07 may show them as secondary text.

## 9. Unknown and future values

Unknown values are a compatibility path, not an exception. When the backend adds an OperationType, MoveType, HoldReasonCode or other supported category:

1. keep raw fallback unchanged;
2. obtain Human-approved business wording;
3. add one mapper entry;
4. add or extend a harness case;
5. do not introduce reverse mapping or a database/config dictionary.

## 10. Verification

`agents/wms-terms-harness.js` loads real `wms-core.js` followed by real `wms-terms.js` and verifies T01–T25:

- all required core examples;
- empty/unknown/raw-preservation contract;
- Boolean Hold and audit capability;
- all V1 categories and stable labels;
- Operation/Move dictionary separation;
- no API/business/SQL behavior;
- ES5 syntax and Foundation registration/load order.

