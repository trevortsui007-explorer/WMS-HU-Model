# WMS Information Layering Guide

## 1. Purpose

This guide defines how WMS information should be presented. It changes presentation hierarchy only; backend values, audit evidence, DTO fields, database schema, and business rules remain unchanged.

Business readability is not simple translation. The first question is “what does the user need to decide or recognize?”, followed by “what evidence does support or IT need to diagnose it?”.

Approved and implemented terminology: HU generic term 物流单元; Hold 冻结 / 解除冻结; DateCode Grid 周期码 and Detail 周期码（DateCode）; PCS remains PCS; generic Lifecycle ACTIVE/CLOSED is 有效/已关闭. Page06 raw codes are hidden by default. Page07 shows business text first and retains raw code as secondary evidence. DetailJson is 原始审计明细（JSON） and is collapsed by default.

## 2. Four information layers

### BUSINESS_PRIMARY

The user's main business object, quantity, time, location, and scope. Show directly, using short Chinese business terms.

Examples: 箱码, 物料, 版本, 数量, 单位, 生产周, 有效货位, 包装时间.

Rules:

- Do not show raw field names as labels.
- When a concrete HU type is known, prefer 托盘码/箱码/内包码.
- Generic HU screens use 物流单元编码（HU） on first mention, then 物流单元编码.
- Database IDs are not business numbers and are normally hidden.

### BUSINESS_STATUS

Backend states and event types translated into a stable business label. Show the business label first.

Examples: `AVAILABLE` → 可用; `WITH_EXCEPTION` → 存在异常; `WEEKCODE_RECALCULATE` → 周码重新计算.

Rules:

- Mapping is presentation-only.
- Context may refine wording, but one raw value must not receive contradictory meanings on different pages.
- On business pages, omit the raw code by default.
- On IT/audit pages, show raw code as smaller secondary text where it helps diagnosis.
- Unknown values fall back to their raw value; never blank them.

### IT_SECONDARY

Identifiers and system context needed for traceability but not for the primary business decision.

Examples: 执行编号（RequestId）, 操作编号, 来源记录标识, rule source, source system, operator technical account.

Rules:

- Place after the business summary or in a collapsible “技术信息/追溯信息” area.
- Preserve copy actions and monospace formatting.
- Keep exact raw values.
- Do not place `RequestId`, `OperationNo`, or `SourceEntityId` between primary business columns unless the page's purpose is audit/IT.

### RAW_TECHNICAL

Uninterpreted diagnostic evidence.

Examples: DetailJson, ErrorCode, backend raw message, HTTP status, WeekSortKey, raw enum/event source.

Rules:

- Default collapsed or hidden behind “技术详情”.
- Preserve exact content and copyability.
- Never delete audit evidence to improve readability.
- Do not infer business success from a technical code unless the backend contract explicitly defines it.

## 3. Page examples

### Page01 WeekCode Audit

Current good pattern: the Recalculate Result Modal leads with scope, outcome and affected counts; execution number, error code, MES rule source and backend raw message are under technical details.

Recommended layering for the remaining preview:

1. BUSINESS_PRIMARY: 箱码, 厂别, 物料, 版本, 周期码, 包装时间, 当前生产周 → 候选生产周.
2. BUSINESS_STATUS: 异常类型, 风险, 冻结状态.
3. IT_SECONDARY: 规则来源, internal HU ID.
4. RAW_TECHNICAL: WeekRuleValue, WeekSortKey, raw AnomalyType/RiskLevel.

The Human-approved Recalculate lifecycle and refresh behavior must not change during terminology implementation.

### Page03 HU Detail / History

This is a generic HU page, so the first screen mention should be `物流单元详情 / 履历` and `物流单元编码（HU）`.

Recommended layering:

1. BUSINESS_PRIMARY: 物流单元编码, 包装层级, 包装结构, 物料/库存明细, direct/effective location distinction.
2. BUSINESS_STATUS: 当前状态, 库存状态, 冻结状态, mapped operation/move types.
3. IT_SECONDARY: HU internal ID, 冻结编号, RequestId, source system.
4. RAW_TECHNICAL: raw HUType/LifecycleStatus/OperationType/MoveType/HoldType codes.

The history timeline title should show a business event first. Raw code and RequestId belong in secondary metadata, not the title's only meaning.

### Page07 Audit Log Center

Page07 may retain more technical evidence than business pages, but “audit page” does not mean “raw DTO projection”.

Recommended operation row:

- Primary: 操作时间, 物流单元编码, 包装层级, 操作类型, 操作人, 说明.
- Secondary: 执行编号（RequestId）, 业务编号, 操作编号.
- Technical detail: raw OperationType, related HU internal context, DetailJson collapsed.

Recommended request trace:

- Primary timeline title: mapped business event, e.g. 周码重新计算.
- Secondary: raw code `WEEKCODE_RECALCULATE`, execution number, operator, source.
- Technical: raw DetailJson and unparsed source evidence.

Page07 WEEKCODE_RECALCULATE now provides a business summary and all-item Before → After view before the collapsed raw JSON. Request summaries count persisted updates and never claim a complete processed total.

## 4. Naming rules

### Code, No and Id

| Suffix | Display rule | Examples |
|---|---|---|
| `Code` | 编码, or established short “码” for scanned business objects | WarehouseCode → 仓库编码; HUCode in Page02 → 箱码 |
| `No` | 编号 | OperationNo → 操作编号; MoveNo → 移动编号 |
| `Id` / GUID | 内部标识 or trace identifier; usually secondary | HUId → 物流单元内部标识; RequestId → 执行编号（RequestId） |

An internal `Id` must not be mislabeled as a business 编号.

### Time

- CreateTime → 创建时间
- ModifyTime → 修改时间
- OperationTime → 操作时间
- MoveTime → 移动时间
- HoldTime → 冻结时间
- ReleaseTime → 解除冻结时间

### Quantity and unit

- Qty → 数量
- TotalQty → 总数量
- InnerPackCount / PackCount → 内包数
- BoxCount → 箱数
- Uom → 单位
- PCS display remains `PCS`.

### Location

- LocationCode → 货位编码
- DirectLocationCode → 直接货位
- EffectiveLocationCode → 有效货位
- inherited location → 继承货位 / 继承自上级物流单元

Do not collapse these into one generic “Location” label.

## 5. Display mapper contract

Implemented namespace: `Wms.Terms`, matching the existing Foundation `Wms.*` style. See `19_WMS_Terms_Foundation_Contract.md`.

```javascript
function displayTerm(map, value) {
    var raw = value == null ? "" : String(value).trim();
    if (!raw) { return { text: "—", raw: raw, known: false }; }
    return map[raw]
        ? { text: map[raw], raw: raw, known: true }
        : { text: raw, raw: raw, known: false };
}
```

Allowed:

- `Wms.Terms.huType(value)`
- `Wms.Terms.lifecycleStatus(value, context)` when context only selects wording
- `Wms.Terms.operationType(value)`
- `Wms.Terms.moveType(value)`
- `Wms.Terms.monitoringStatus(value)`

Forbidden:

- deciding whether a box can be picked;
- rejecting a Hold operation;
- calculating WeekCode;
- changing API requests based on a display label;
- replacing raw values in DTOs;
- silently mapping every unknown value to “未知”.

Keep Page01 `WeekCodeRecalculateMessageMapper.js` page-specific. It converts execution errors into business messages and is not a global terminology store.

## 6. Layout rules

- Business Grid: business label only; raw code omitted.
- Business Drawer/Modal: business summary first, IT metadata in a lower or collapsible section.
- Monitoring/Audit Grid: business label first; raw code may appear as secondary text.
- Raw JSON and backend messages: collapsed by default.
- Preserve copy buttons for identifiers and raw evidence.
- Prefer short labels in Grid and fuller labels in Detail views.
- Translation must not widen Grid headers with verbose phrases when a stable short business term exists.

## 7. Readability levels

- **A — Business Ready:** primary tasks can be completed without interpreting technical field names or raw codes.
- **B — Mostly Business Readable:** primary flow is business-oriented; a limited number of English/technical residues remain.
- **C — Mixed Technical:** business and DTO/enum language share the same visual level; users must interpret technical terms.
- **D — Technical First:** the page is primarily a raw model/audit projection; business meaning is secondary or absent.

Current classification: Page01 B, Page02 B, Page03 C, Page04 C, Page05 B, Page06 B, Page07 D.

## 8. Review and rollout gate

Human terminology review is approved and Batch A `Wms.Terms` is implemented. Human should now review the API shape and raw-fallback behavior before Batch B applies it to Page02/03/04. Terminology work must not alter API payloads, backend rules, database values, Pager, Drawer lifecycle, or Page01 Recalculate refresh semantics.

