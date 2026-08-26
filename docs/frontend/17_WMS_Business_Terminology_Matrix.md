# WMS Business Terminology Matrix

## 1. Audit status and scope

- Phase: `Phase 1D - Business Readability & Terminology Classification Audit`
- Mode: `AUDIT + CLASSIFICATION ONLY`
- Audited pages: Page01 through Page07.
- Audited evidence: Learun WMS `.cshtml/.js/.css`, page API adapters, shared Foundation display components, and current backend model/repository/service literals.
- Production UI source changed: **No**.
- Backend/API/database/proxy changed: **No**.
- Human terminology review: **Approved**.
- Implementation status: **Wms.Terms Foundation V1 closed; Batch B Page02/Page03/Page04, Batch C2 Page05 and Batch D Page06/Page07 rolled out**.

Page rollout status: Page01 existing partial / regression only; Page02 `ROLLED_OUT`; Page03 `ROLLED_OUT`; Page04 `ROLLED_OUT`; Page05 `ROLLED_OUT`; Page06 `ROLLED_OUT`; Page07 `ROLLED_OUT`.

### Batch D post-rollout recount

| Page | English-only | Raw enum sites | IT secondary shown primary | Raw technical shown primary | English Grid/Form/Detail | Readability |
|---|---:|---:|---:|---:|---:|---|
| Page06 Monitoring | 0 | 1 | 0 | 0 | 0 / 0 / 0 | A candidate |
| Page07 Audit Log Center | 2 | 4 | 1 | 0 | 0 / 0 / 0 | B candidate |

Page06's raw site is the approved SourceSystem acronym. Page07's four raw-code sites are retained secondary evidence; no raw technical value remains business-primary. The two English-only Page07 values are `yyyy-MM-dd` placeholders.

This matrix is a display-layer proposal. It does not rename DTO properties, C# models, database columns, codes, or persisted values.

## 2. Pre-rollout baseline counting method

`agents/wms-terminology-audit.js` performs a read-only static scan. `TOTAL_VISIBLE_LABEL_COUNT` counts authored occurrences found in explicit UI-bearing constructs: page title, visible markup text, placeholder, tooltip, Grid/Detail `label`, section/modal `title`, empty/initial state, `.text("...")`, embedded table headings, and CSS `content`. Dynamic backend messages and runtime values are excluded because a static audit cannot enumerate them reliably.

The counts are source occurrences, not unique phrases and not a runtime DOM census. `CHINESE_BUSINESS_LABEL_COUNT` means an authored occurrence contains Chinese; `ENGLISH_BUSINESS_LABEL_COUNT` retains the requested historical metric name but actually means an authored occurrence containing Latin text and no Chinese, including technical labels. Neutral punctuation/date-format-only occurrences explain why Chinese plus English may be lower than total. Raw-enum and information-layer counts are separately reviewed **display sites**, counted once even if several values can appear there.

| Page | Total visible | Chinese | English-only | Raw enum sites | IT secondary shown primary | Raw technical shown primary | English Grid headers | English Form labels | English Detail labels | Readability |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Page01 WeekCode Audit | 140 | 119 | 21 | 3 | 3 | 3 | 2 | 1 | 15 | B - Mostly Business Readable |
| Page02 Finished Goods Inventory | 77 | 59 | 18 | 5 | 1 | 4 | 1 | 2 | 6 | B - Mostly Business Readable |
| Page03 HU Detail / History | 53 | 42 | 11 | 14 | 3 | 4 | 3 | 1 | 5 | C - Mixed Technical |
| Page04 Finished Goods Inbound Records | 76 | 51 | 25 | 5 | 5 | 2 | 4 | 2 | 21 | C - Mixed Technical |
| Page05 Master Data | 94 | 85 | 0 | 2 | 0 | 2 | 0 | 0 | 0 | B - Mostly Business Readable |
| Page06 Monitoring | 60 | 53 | 7 | 1 | 2 | 0 | 4 | 0 | 0 | B - Mostly Business Readable |
| Page07 Audit Log Center | 105 | 37 | 67 | 10 | 7 | 6 | 16 | 5 | 40 | D - Technical First |
| **Total** | **605** | **446** | **149** | **40** | **21** | **21** | **30** | **11** | **87** | — |

Level definitions are in `18_WMS_Information_Layering_Guide.md`. These are categorical reviews, not pseudo-precise scores.

### Batch B post-rollout recount

The same authored-text scan was rerun after Page02/Page03/Page04 rollout. Reviewed issue-site counts were updated to reflect the actual new information layer; system names such as MES/SAP/WMS are allowed, while Page04 `SourceType` remains one visible raw enum as deliberately secondary technical evidence.

| Page | Total visible | Chinese | English-only | Raw enum sites | IT secondary shown primary | Raw technical shown primary | English Grid headers | English Form labels | English Detail labels | Readability target |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Page02 Finished Goods Inventory | 78 | 78 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | A candidate |
| Page03 HU Detail / History | 54 | 54 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | A/B candidate |
| Page04 Finished Goods Inbound Records | 79 | 79 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | A/B candidate |

Counts do not treat an unknown future runtime enum fallback as authored raw UI text.

## 3. RAW_TERMINOLOGY_INVENTORY

This section records real raw fields/values before recommending translations. “Confirmed” means found in current production C#/SQL paths; test-only examples are not promoted to confirmed production values.

| Field | Raw value(s) actually found | Current display | Page/context | Example/evidence |
|---|---|---|---|---|
| `HUType` | `PALLET`, `OUTER_BOX`, `INNER_PACK` | Raw in generic/detail/audit views; Page02 Grid already uses 箱码 | Page02/03/04/07 | Repository SQL predicates and inbound creation paths |
| `LifecycleStatus` | `ACTIVE`, `CLOSED` | Raw `Lifecycle`, raw header tag, raw pallet table | Page02/03/04 | Most active-HU predicates; Putaway closes empty pallet |
| `StockStatus` | `AVAILABLE` confirmed | Page02 maps AVAILABLE to 可用; Page03 content table displays raw | Page02/03 | `HuService` default and outbound eligibility SQL |
| `StockStatus` | `RESERVED`, `HOLD` frontend mapping only | Page02 mapper has 已分配 / Hold | Page02 | No current production repository write was found in this audit; do not treat as confirmed full enum |
| `OperationType` | `RECEIVE`, `CREATE_HU`, `ATTACH_HU`, `DETACH_HU`, `ADD_ITEM`, `HU_HOLD`, `HU_RELEASE`, `PICK`, `PICK_BATCH`, `PUTAWAY`, `UNPACK_INNER`, `PACK_INNER`, `REPACK_TRANSFER`, `WEEKCODE_RECALCULATE` | Raw Grid, Drawer and request trace | Page03/07 | Operation-log writers and repository predicates |
| `MoveType` | accepted by location service: `RECEIVE`, `PUTAWAY`, `RELOCATION`, `RETURN`, `STAGE`, `CORRECTION` | Raw timeline/audit | Page03/07 | `HuLocationService` whitelist |
| `MoveType` | additionally persisted: `UNPACK_INNER`, `PACK_INNER`, `REPACK_TRANSFER`, `OUTBOUND_PICK`, `OUTBOUND_SHIP`, `OUTBOUND_RETURN`, `OUTBOUND_RETURN_TO_STOCK` | Raw timeline/audit | Page03/07 | Repack and FormalOutbound repository writes |
| `HoldType` | `INBOUND_EXCEPTION`, `QUALITY`; arbitrary request value is also accepted by generic Hold API | Raw | Page03/07 | Inbound and formal outbound repository writes |
| `HoldReasonCode` | `WEEKCODE_UNRESOLVED`, `QUALITY_FAILED`; inbound failure codes can also be retained as reason text/code | Raw | Page01/02/03/04/07 | Inbound/recalculate/outbound repositories |
| `IsHold` / hold status | Boolean plus UI raw `HOLD`, `ACTIVE HOLD`, `RELEASED` | English primary status | Page01/02/03/04/07 | Page-specific formatters |
| `WeekRuleSource` | `MES_FLOW`, `SNAPSHOT` | Raw | Page01/03 | MES provider and resolver |
| `WeekRuleValue` | data-driven such as `YYWW`/`WWYY`; not a closed enum | Raw | Page01/03 | MES-configured value and persisted snapshot |
| `AnomalyType` | `RULE_MISSING`, `INVALID_DATECODE`, `RULE_UNSUPPORTED`, `SUSPICIOUS_RULE_DIRECTION`, `SUSPICIOUS_YEAR` | Page01 Grid mapped; Drawer shows mapped text plus raw | Page01 | `WeekCodeAuditService` |
| `RiskLevel` | `HIGH`, `MEDIUM`; frontend also maps `LOW` defensively | Grid mapped; Drawer shows mapped text plus raw | Page01 | Service currently emits HIGH/MEDIUM only |
| Recalculate result | `UPDATED`, `UNCHANGED`, `FAILED` | 已更新 / 无需变化 / 失败 | Page01 result Modal | Page01 result mapper |
| Monitoring health | `RUNNING`, `CONNECTED`, `DEGRADED`, `UNAVAILABLE`; business availability `AVAILABLE` | Chinese mapper | Page06 | `MonitoringService` |
| Audit capability | `AVAILABLE`, `NOT_OBSERVABLE` plus Boolean `available` | 可审计 / 当前不可审计 | Page07 | `AuditService` and Page07 mapper |
| Inbound display status | `RECEIVED`, `WITH_EXCEPTION` | 已入库 / 存在异常, with raw repeated in Drawer | Page04 | Page04 formatter/read model |
| Master status | `ACTIVE` plus non-ACTIVE fallback | 启用 / 停用 | Page05 | Page05 mapper |
| `SourceSystem` | `MES`, `SAP`, and WMS-owned audit/runtime sources where supplied | Raw system name | Page02/03/04/06/07 | Integration models and page DTOs |
| Technical identity | `RequestId`, `OperationNo`, `MoveNo`, `HoldNo`, database `Id`, `SourceEntityId` | Mostly raw English labels | Page01/02/03/04/06/07 | Direct DTO projection |
| Raw evidence | `DetailJson`, error code, backend raw message, event `Source` | Direct or same-level technical content | Page01/07 | Recalculate technical details; Audit drawer/trace |

Not established as closed enums: `HoldType`, `HoldReasonCode`, `SourceType`, `SourceEntityId`, `WeekRuleValue`, `ReasonCode`, `BusinessNo`. `Wms.Terms` falls back to the raw value.

## 4. Terminology matrix

| Domain | Raw field/value | Current UI | Recommended short UI label | Recommended detail label | Recommended business text | Raw code visible? | Information layer | Notes |
|---|---|---|---|---|---|---|---|---|
| HU | `HU` generic term | HU | 物流单元 | 物流单元（HU） on first mention | 物流单元 | Technical detail only | BUSINESS_PRIMARY | Human-approved; never use 货品 |
| HU | `HUCode` generic | HU Code | 物流单元编码 | 物流单元编码（HU） first mention | Preserve value | No in primary | BUSINESS_PRIMARY | Page03 generic context |
| HU | `HUCode` with `OUTER_BOX` known | 箱码 / HU Code | 箱码 | 外箱编码（箱码） | Preserve value | No | BUSINESS_PRIMARY | Page02 row is known outer box |
| HU | `HUType` | HU Type | 包装层级 | 包装层级（HU Type） | Mapped value | Technical detail | BUSINESS_PRIMARY | Do not show raw as primary |
| HU type | `PALLET` | PALLET/Pallet | 托盘 | 包装层级：托盘 | 托盘 | Optional secondary | BUSINESS_PRIMARY | |
| HU type | `OUTER_BOX` | OUTER_BOX/Box | 外箱 | 包装层级：外箱 | 外箱 | Optional secondary | BUSINESS_PRIMARY | |
| HU type | `INNER_PACK` | INNER_PACK/Pack | 内包 | 包装层级：内包 | 内包 | Optional secondary | BUSINESS_PRIMARY | |
| HU | database `Id` / `HUId` | HU ID / Box HU ID | Hide | 物流单元内部标识 | Preserve numeric value | Yes | IT_SECONDARY | ID is not 编号 |
| Material | `MaterialCode` | 物料 | 物料 | 物料编码 | Preserve value | No | BUSINESS_PRIMARY | Never translate as 货品 |
| Material | `MaterialRevision` | 版本 / Rev | 版本 | 物料版本 | Preserve value | No | BUSINESS_PRIMARY | Keep Grid short |
| Inventory item | `WMS_HU_Item` / Item | Item / Item snapshot | 库存明细 | 物料库存明细 | — | No | BUSINESS_PRIMARY | Choose by context; do not call HU 货品 |
| Quantity | `Qty` | Qty | 数量 | 数量 | Preserve value | No | BUSINESS_PRIMARY | |
| Quantity | `TotalQty` | Total Qty | 总数量 | 总数量 | Preserve value | No | BUSINESS_PRIMARY | |
| Quantity | `PackCount` | Pack Count | 内包数 | 内包数量 | Preserve value | No | BUSINESS_PRIMARY | Page04 uses inner-pack semantics |
| Quantity | `BoxCount` | Box | 箱数 | 外箱数量 | Preserve value | No | BUSINESS_PRIMARY | |
| UOM | `Uom`, `PCS` | UOM/PCS | 单位 | 计量单位 | PCS | Optional | BUSINESS_PRIMARY | Human-approved: preserve `PCS` display and raw value |
| Lifecycle | `LifecycleStatus` | Lifecycle/ACTIVE | 当前状态 | 物流单元当前状态 | Map by context | Technical detail | BUSINESS_STATUS | Avoid literal “生命周期状态” |
| Lifecycle | `ACTIVE` | ACTIVE | 有效 | 当前有效 | Page03 content may use 当前有效 | Optional secondary | BUSINESS_STATUS | “在库有效” only where inventory context proves it |
| Lifecycle | `CLOSED` | CLOSED | 已关闭 | 包装关系已关闭 | 已关闭 | Optional secondary | BUSINESS_STATUS | Empty pallet closure semantics |
| Inventory | `StockStatus` | 库存状态/raw | 库存状态 | 库存可用状态 | Mapped value | Technical detail | BUSINESS_STATUS | |
| Inventory | `AVAILABLE` | 可用/raw | 可用 | 可用于库存业务 | 可用 | Optional secondary | BUSINESS_STATUS | Confirmed production value |
| Inventory | frontend-only `RESERVED` | 已分配 | 已分配 | 已分配 | 已分配 | Optional secondary | BUSINESS_STATUS | Confirm backend semantics before rollout |
| Location | `LocationCode` | 货位/Location | 货位 | 货位编码 | Preserve value | No | BUSINESS_PRIMARY | |
| Location | `DirectLocationCode` | 直接位置 | 直接货位 | 直接货位编码 | Preserve value | No | BUSINESS_PRIMARY | HU owns this location record |
| Location | `EffectiveLocationCode` | 有效位置/有效货位 | 有效货位 | 当前有效货位 | Preserve value | No | BUSINESS_PRIMARY | May be inherited from parent HU |
| Location | inherited location | 位置来源/父级 HU 回退 | 继承货位 | 继承自上级物流单元的货位 | Preserve source HU if helpful | No | BUSINESS_PRIMARY | Do not merge with direct location |
| Location | `LocationTypeCode` | 类型/raw code | 货位类型 | 货位类型代码 | Mapper required after value inventory review | Technical detail | BUSINESS_STATUS | `DOUBLE_RACK` is confirmed; full master vocabulary needs Human review |
| Hold | `IsHold` | Hold/HOLD | 冻结状态 | 冻结状态 | true/HOLD → 已冻结; false → 未冻结 | Yes in technical detail | BUSINESS_STATUS | Human-approved |
| Hold | active | ACTIVE HOLD/HOLD | 已冻结 | 当前已冻结 | 已冻结 | Optional secondary | BUSINESS_STATUS | Do not use English-only primary status |
| Hold | released | RELEASED | 已解除冻结 | 冻结已解除 | 已解除冻结 | Optional secondary | BUSINESS_STATUS | |
| Hold | `HoldReasonCode` | 原因码/raw | 冻结原因 | 冻结原因代码 | Known code mapped; raw fallback | Yes secondary | IT_SECONDARY | Reason text is business primary; code is supporting evidence |
| WeekCode | `DateCode` | DateCode | 周期码 | 周期码（DateCode） | Preserve raw value | Optional | BUSINESS_PRIMARY | Human-approved |
| WeekCode | `WeekFormatCode` / `WeekRuleValue` | WeekRuleValue | 周期格式 | 周期格式（原始规则） | `YYWW`, `WWYY`, etc. | Yes secondary | BUSINESS_STATUS | Data-driven, not closed enum |
| WeekCode | `WeekRuleSource=MES_FLOW` | WeekRuleSource/MES_FLOW | 规则来源 | 周码规则来源 | MES工艺流程 | Yes secondary | IT_SECONDARY | `SNAPSHOT` → 入库快照 |
| WeekCode | `WeekYear` + `WeekNo` | WeekYear / WeekNo | 生产周 | 生产年份 / 周次 | e.g. `2026-W31` | No | BUSINESS_PRIMARY | |
| WeekCode | `WeekSortKey` | WeekSortKey | Hide | 周码排序键 | Preserve value | Yes | RAW_TECHNICAL | Technical detail only |
| WeekCode audit | `RULE_MISSING` | 规则缺失 | 规则缺失 | 周码规则缺失 | 规则缺失 | Optional secondary | BUSINESS_STATUS | Existing mapper |
| WeekCode audit | `INVALID_DATECODE` | DateCode异常 | 周期码异常 | 周期码无法解析 | 周期码异常 | Optional secondary | BUSINESS_STATUS | Human-approved |
| WeekCode audit | `RULE_UNSUPPORTED` | 规则不支持 | 规则不支持 | 当前周期格式不支持 | 规则不支持 | Optional secondary | BUSINESS_STATUS | Mapper also accepts legacy `UNSUPPORTED_WEEK_RULE` |
| WeekCode audit | `SUSPICIOUS_RULE_DIRECTION` | 规则方向可疑 | 周期格式方向可疑 | 当前周期格式方向可能不匹配 | 周期格式方向可疑 | Optional secondary | BUSINESS_STATUS | |
| WeekCode audit | `SUSPICIOUS_YEAR` | 年份可疑 | 年份可疑 | 生产周年份可疑 | 年份可疑 | Optional secondary | BUSINESS_STATUS | |
| Risk | `HIGH`/`MEDIUM`/`LOW` | 高/中/低 | 高/中/低 | 风险等级：高/中/低 | Existing mapping | Optional secondary | BUSINESS_STATUS | LOW is defensive frontend mapping |
| Result | `UPDATED` | 已更新 | 已更新 | 重新计算后已更新 | 已更新 | Optional secondary | BUSINESS_STATUS | Existing mapping |
| Result | `UNCHANGED` | 无需变化 | 无需变化 | 重新计算后无需变化 | 无需变化 | Optional secondary | BUSINESS_STATUS | |
| Result | `FAILED` | 失败 | 失败 | 重新计算失败 | 失败 | Yes in technical detail | BUSINESS_STATUS | Error code/raw message remains technical |
| Inbound | `SourceSystem` | 来源/raw system name | 数据来源 | 数据来源系统 | Keep MES/SAP/WMS | Yes | BUSINESS_PRIMARY | System acronyms are approved retained English |
| Inbound | `SourceEntityId` | SourceEntityId | Hide | 来源记录标识 | Preserve value | Yes | IT_SECONDARY | Move out of primary Grid where possible |
| Inbound | `RECEIVED` | 已入库 | 已入库 | 入库已完成 | 已入库 | Optional secondary | BUSINESS_STATUS | |
| Inbound | `WITH_EXCEPTION` | 存在异常 | 存在异常 | 入库完成但存在后续异常 | 存在异常 | Optional secondary | BUSINESS_STATUS | |
| Operation | `OperationType` | OperationType/raw | 操作类型 | 操作类型（原始代码） | Map confirmed values | Yes secondary | BUSINESS_STATUS | Full proposed mapping below |
| Move | `MoveType` | MoveType/raw | 移动类型 | 移动类型（原始代码） | Map confirmed values | Yes secondary | BUSINESS_STATUS | Separate dictionary from OperationType |
| Technical identity | `RequestId` | RequestId | 执行编号 | 执行编号（RequestId） | Preserve value | Yes | IT_SECONDARY | Copy remains available |
| Technical identity | `OperationNo` | OperationNo | 操作编号 | 操作编号 | Preserve value | Yes | IT_SECONDARY | No → 编号 |
| Technical identity | `MoveNo` | MoveNo | 移动编号 | 移动编号 | Preserve value | Yes | IT_SECONDARY | |
| Audit | `BusinessNo` | BusinessNo | 业务编号 | 关联业务编号 | Preserve value | Yes | IT_SECONDARY | |
| Audit | `DetailJson` | DetailJson expanded | Hide/collapse | 原始审计明细（JSON） | Preserve JSON | Yes | RAW_TECHNICAL | Default collapsed |
| Monitoring | `RUNNING`/`CONNECTED` | 运行中/已连接 | 正常 | 服务正常/连接正常 | 正常 | Yes secondary | BUSINESS_STATUS | Context-specific detail wording allowed |
| Monitoring | `DEGRADED` | 部分降级 | 部分异常 | 部分依赖异常 | 部分异常 | Yes secondary | BUSINESS_STATUS | |
| Monitoring | `UNAVAILABLE` | 不可用 | 不可用 | 服务或依赖不可用 | 不可用 | Yes secondary | BUSINESS_STATUS | |
| Monitoring | `WmsDb`/`MesDb` | WmsDb/MesDb | WMS数据库/MES数据库 | WMS数据库连接/MES数据库连接 | — | Optional secondary | BUSINESS_PRIMARY | Keep product/system acronyms |
| Audit capability | `NOT_OBSERVABLE` / false | 当前不可审计 | 当前不可审计 | 尚未建立完整持久化审计 | Current mapper is acceptable | Yes secondary | BUSINESS_STATUS | Never render False or infer zero events |

## 5. Confirmed operation and move display candidates

These values are the Human-approved V1 display dictionary. Unknown values still use raw fallback; this table is not treated as a closed backend enum.

| Raw domain | Raw value | Recommended text | Alternative/detail | Review |
|---|---|---|---|---|
| OperationType | `RECEIVE` | 接收入库 | 接收入库记录 | Required |
| OperationType | `CREATE_HU` | 创建物流单元 | 创建物流单元（HU） | Required |
| OperationType | `ATTACH_HU` | 建立包装关系 | 关联物流单元 | Required |
| OperationType | `DETACH_HU` | 解除包装关系 | 取消物流单元关联 | Required |
| OperationType | `ADD_ITEM` | 增加库存明细 | 增加物料明细 | Required |
| OperationType | `HU_HOLD` | 冻结 | — | Approved |
| OperationType | `HU_RELEASE` | 解除冻结 | — | Approved |
| OperationType | `PICK` | 拣货 | 箱拣货 | Required |
| OperationType | `PICK_BATCH` | 批次拣货 | 批量拣货 | Required |
| OperationType | `PUTAWAY` | 上架 | 外箱上架 | Required |
| OperationType | `UNPACK_INNER` | 拆出内包 | 内包拆箱 | Required |
| OperationType | `PACK_INNER` | 装入内包 | 内包装箱 | Required |
| OperationType | `REPACK_TRANSFER` | 内包换箱 | 重新包装转移 | Required |
| OperationType | `WEEKCODE_RECALCULATE` | 周码重新计算 | 生产周重新计算 | Required |
| MoveType | `RECEIVE` | 入库移入 | 移入待入库货位 | Required |
| MoveType | `PUTAWAY` | 上架移动 | 上架 | Required |
| MoveType | `RELOCATION` | 移库 | 货位调整 | Required |
| MoveType | `RETURN` | 返库移动 | 返回库存 | Required |
| MoveType | `STAGE` | 移至暂存区 | 暂存 | Required |
| MoveType | `CORRECTION` | 位置更正 | 库位更正 | Required |
| MoveType | `UNPACK_INNER` | 拆箱移出内包 | 内包拆出 | Required |
| MoveType | `PACK_INNER` | 装箱移入内包 | 内包装入 | Required |
| MoveType | `REPACK_TRANSFER` | 内包换箱移动 | 重新包装转移 | Required |
| MoveType | `OUTBOUND_PICK` | 出库拣货移动 | 拣至出库暂存区 | Required |
| MoveType | `OUTBOUND_SHIP` | 发运移出 | 出库发运 | Required |
| MoveType | `OUTBOUND_RETURN` | 出库退回 | 退回区移动 | Required |
| MoveType | `OUTBOUND_RETURN_TO_STOCK` | 退回库存 | 返库上架 | Required |

Unknown-value contract: trim only for lookup; known code returns the mapped text, raw value and `known=true`; unknown non-empty code returns the original value unchanged and `known=false`; null/empty returns `—`. Never return blank or discard new backend evidence.

## 6. Existing display mappers

- `WeekCodeAudit/Index.js`: Page01 `auditLabel`, `riskLabel`, `resultLabel`, and raw-plus-label `codeWithLabel`.
- `WeekCodeAudit/WeekCodeRecalculateMessageMapper.js`: Page01-only business error/message mapping. It must remain page-specific.
- `FinishedGoodsInventory/Index.js`: Page02 now uses `Wms.Terms.stockStatus`; the duplicate `stockLabel` was removed in Batch B.
- `FinishedGoodsInboundRecords/Index.js`: Page04 retains a tone-only `statusFormatter` which gets its label from `Wms.Terms.inboundStatus`; the duplicate `statusLabel` dictionary was removed in Batch B.
- `MasterData/Index.js`: Page05 active/inactive status formatters.
- `Monitoring/Index.js`: Page06 `statusLabel` and `healthTone`.
- `AuditLog/Index.js`: Page07 capability text mapping and Hold status formatter.
- Foundation `Wms.StatusTag`: visual status/tone renderer; it is not a global business terminology dictionary.
- Foundation `Wms.AuditDiffCell`: structured before/after display; it does not translate domain codes.
- Foundation `Wms.ResultPanel`: generic result renderer; it is not a substitute for domain wording.
- Foundation `Wms.Terms`: Batch A global display-only mapper for approved raw-code → business-text mappings.

Page-specific presentation composition remains local. Shared raw-enum dictionaries already covered by `Wms.Terms` must not be recreated on rollout pages.

## 7. Implemented mapper boundary

The Foundation now exposes the display-only namespace `Wms.Terms`. Example API:

```javascript
Wms.Terms.huType("OUTER_BOX")
// { text: "外箱", raw: "OUTER_BOX", known: true }

Wms.Terms.operationType("NEW_OPERATION_X")
// { text: "NEW_OPERATION_X", raw: "NEW_OPERATION_X", known: false }
```

Allowed responsibility: normalized raw code → display metadata. Forbidden responsibility: permission, pickability, Hold enforcement, WeekCode calculation, state transition, API branching, or any other business decision.

Keep separate maps for `OperationType` and `MoveType`. Reuse page-specific message mapping instead of moving it into the global dictionary.

Because WMS Foundation scripts are explicitly listed by each Razor View, rollout pages must load `wms-core.js`, then `wms-terms.js`, then the consuming page script. Batch A adds the file to the web project but deliberately does not modify the seven page loaders.

## 8. Human review decisions approved

1. HU generic term is 物流单元; HU type label is 包装层级.
2. Hold uses 冻结 / 解除冻结; active/released states use 已冻结 / 已解除冻结.
3. DateCode uses 周期码 in Grid and 周期码（DateCode） in Detail.
4. PCS remains PCS.
5. Generic Lifecycle `ACTIVE` / `CLOSED` maps to 有效 / 已关闭.
6. Approved OperationType and MoveType wording is locked by section 5 and `wms-terms.js`.
7. Page06 hides raw code by default; Page07 shows business text first and may show raw code as secondary text.
8. DetailJson business label is 原始审计明细（JSON） and future page rollout keeps it collapsed by default.

## 9. Proposed implementation batches after review

- Batch A: **implemented** — display-only `Wms.Terms`, raw fallback, T01–T25 harness and contract.
- Batch B: **implemented** — Page02/03/04 use shared terms; Page02 keeps its known-outer-box context label `箱码`; Page03 retains generic `物流单元`; Page04 keeps technical source identifiers after business columns and visually secondary.
- Batch C: Page05 master-data labels and location-type map.
- Batch D: Page06/07 dual-layer IT presentation; make Page07 raw JSON and raw codes secondary/collapsed.
- Batch E: Page01 regression-only application to residual Drawer/Grid terms; preserve the Human-approved Recalculate Modal flow.

