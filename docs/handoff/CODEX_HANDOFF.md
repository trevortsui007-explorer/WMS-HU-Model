# WMS 成品仓正式出库后端交接

## 2026-09-01 Putaway Recommendation V1.1.1

- 版本：`V1.1.1_DYNAMIC_CLUSTER`。保留 `MinClusterObservationCount=3`、第三真实簇位前不推断容量、先补齐后扩展等 V1.1 硬约束。
- Top3：Observation/Fill 模式先返回真实主簇位置；候选不足时按同排相邻、同货架组近邻、普通空位补充 `OPERATION_BACKUP`。Backup 不计入 `ClusterLocationCount`，不改变推断状态，不表示簇满；LocationCode 仍由字典去重。
- Contract：GET recommendation 新增可选 `itMode`（默认 false）；Result 新增 `ITMode`，item 新增 `ITReason`。`Reason` 简化为操作文案；ITReason 仅在 itMode=true 返回，IT mode 不参与候选与排序。
- CurrentBoxCount：改为候选 Location 内精确匹配当前 PlantCode+MaterialCode+MaterialRevision 的 OUTER_BOX 数量，不再使用该 Location 全部箱数。
- PDA：默认显示首选/次选/备选及简短 Reason，不显示簇元数据；URL `?itMode=1/true` 时请求 API `itMode=true` 并显示簇信息与 ITReason。
- Runtime sync：DTO、Service interface、Service、Module 已同步 Learun WebApi WMS_FG 编译副本；PDA 修改位于 Learun 正式代码树。
- Verification：Model/Test/WMS Host/Learun Web build PASS；排除一个既有且无关的出货页面文本断言后，V1.1.1 与可执行回归 `136/136 PASS`。未改 harness 会在最后的 `ShipmentManagementFrontendKeepsBusinessBoundary` 因当前页面缺少 `查看接口日志` 而失败。WebApi 编译到最终输出后因运行进程/权限拒绝写入 `obj/Debug/Learun.Application.WebApi.dll` 停止，未出现 V1.1.1 源码错误。
- Database：无 Schema、Migration、DML 或 DDL 变化/执行。Git 未提交或暂存。

## 2026-09-01 Putaway Recommendation V1.1

- Root cause：V1 在任意非空 MainCluster 上都会继续生成相邻空位、配对排及通用 fallback；未要求最少真实簇位数，导致两个位置箱数相等时把 `FillGap=0` 误当作扩展依据。
- Implemented：版本升级为 `V1.1_DYNAMIC_CLUSTER`，`MinClusterObservationCount=3`。少于 3 个连续已占用同厂别/同料/同版簇位时只返回现有簇位；达到 3 个后仍先补不平衡位置，仅 3 个以上且当前平衡时允许 `POSSIBLY_FULL -> CLUSTER_EXPANSION`。TOP 3 是上限。
- DTO：`PutawayLocationRecommendationDto` 新增 nullable `ClusterLocationCount` 与 `ClusterInferenceStatus`；状态限定 `NONE/INSUFFICIENT_SAMPLE/FILL_REQUIRED/POSSIBLY_FULL`。无数据库字段或迁移。
- Runtime sync：核心 Service/DTO 与 Learun WebApi WMS_FG 编译副本同步；未修改 PDA、Repository SQL 或其他业务模块。
- Verification：Model/Test/WMS Host build PASS；V1.1 推荐场景及除一个既有出货页面静态断言外的 134 tests PASS。完整未改 harness 被既有 `ShipmentManagementFrontendKeepsBusinessBoundary` 阻断，因为当前前端文件不含 `查看接口日志`；本任务未修改该前端。Learun WebApi 编译到最终输出阶段后因运行环境拒绝写入 `obj/Debug/Learun.Application.WebApi.dll` 而停止，未出现 V1.1 源码编译错误。真实数据库 Runtime GET 未执行。
- Report：`WMS_FG_PUTAWAY_RECOMMENDATION_V1_1_REPORT.md`。Database schema/migration/DML 均未变更或执行；Git 未提交/暂存。

## 2026-09-01 Finished Goods Inventory Inbound-State Filter

- Root cause：前端已发送 `inboundState`，但 `FinishedGoodsInventoryQuery` 无该属性，Module 未读取，Service/Repository/SQL 均未消费，因此 INBOUND 与 INBOUND-PENDING 混合返回。
- Fix：仅扩展成品库存查询链路。Module 按两步转换读取 `inboundState`；Service Trim+Upper，只接受 `INBOUND/PENDING`，空值表示全部；Repository 在共享 effective-location `FROM/WHERE` 中以最终 `dl.LocationCode` 过滤，并向 COUNT 与 Rows 绑定 typed `@InboundState`。过滤发生在 GROUP/OFFSET 之前，COUNT 与 Items 条件一致。
- Runtime sync：主仓实现与实际 `Learun.Application.WebApi/Modules/WMS_FG` 编译副本同步；前端 UI、FinishedGoodsInventoryApi.js、Grid、位置/库存/Hold 语义及其他 API 未改。
- Verification：WMS-HU-Model.Tests 与 WMS Host build PASS；console regression `134/134` PASS；实际 Learun WebApi build PASS，仅有既有依赖/代码警告。未连接 BaseDb、未执行 Runtime GET、DML、DDL 或 migration；需由 Human 在真实环境核对 INBOUND/PENDING 结果及分页总数。

## 2026-08-30 WMS Target Environment Acceptance

- Result: `WMS_TARGET_ENVIRONMENT_ACCEPTANCE_COMPLETE=FALSE`. `BaseDb` remains configured for `.\\SQLEXPRESS/adms705`, but two read-only integrated-authentication probes failed before SQL execution with the local ODBC SSL/SSPI credential error (`no credentials are available in the security package`). No target table query, fixture discovery/creation, DML, DDL, migration, seed, SAP HTTP call, or source fix ran.
- Local reconfirmation: WMS-HU-Model, WMS-HU-Model.Tests, and WMS host Debug builds PASS; regression executable PASS (`124/124`).
- Source-only posting review confirms the HTTP call happens after claim transaction A commits and before finalization transaction B begins; finalizer guards and `SUCCESS -> SHIPPED` transaction remain unmodified. This is not DB-backed acceptance evidence.
- Unexpected source observation for Human Review: manual UNKNOWN-confirmation routes remain registered, but their repository methods currently throw that manual confirmation is deprecated. This conflicts with the target-acceptance task's required temporary-recovery capability and was not changed while TA-0 is blocked.
- Report: `WMS_TARGET_ENVIRONMENT_ACCEPTANCE_REPORT.md`. Next: restore an approved BaseDb TLS/integrated credential path, repeat TA-0/table/permission checks, then obtain an isolated fixture and controlled SAP/stub endpoint. `NEXT_RECOMMENDED_ACTION=HUMAN_REVIEW_TARGET_ACCEPTANCE_BLOCKER`.

## 2026-08-31 WMS Acceptance Reclassification — Manual DB Mode

- Human Review reclassified Codex-local SQL connectivity as an environment gap, not a WMS Runtime, Final Schema, or architecture blocker. `CODEX_DB_CONNECTION_REQUIRED=False`; do not retry or alter BaseDb authentication/TLS/certificates/permissions/configuration from Codex.
- Database verification is now `MANUAL_SQL_EXECUTION`: Human executes approved SQL in SSMS and returns results for analysis. The default read-only pack is `docs/acceptance/WMS_MANUAL_DB_ACCEPTANCE.sql`; it covers final-schema metadata, critical/legacy fields, indexes, single-delivery inspection, SHIPPED integrity, attempt/snapshot anomalies, and post-concurrency inspection.
- Source acceptance review is complete: claim transaction commits before SAP HTTP; PostingDetail is created/frozen; retry is FAILED-only and PayloadHash guarded; UNKNOWN is distinct and blocked from blind retry; SUCCESS finalization is one serializable local transaction and the sole SHIPPED authority. Remote-success/local-finalization-failure remains source-partial and needs manual DB evidence.
- Manual confirmation routes currently return the existing deprecated result; this is recorded as current policy rather than a newly classified source bug. No source/host/database change was made.
- Status: `WMS_TARGET_ENVIRONMENT_ACCEPTANCE_COMPLETE=DEFERRED_MANUAL`; `WMS_SOURCE_ACCEPTANCE_COMPLETE=True`; `WMS_MANUAL_DB_ACCEPTANCE_COMPLETE=False`; `WMS_FUNCTIONAL_ARCHITECTURE_CLEANUP_COMPLETE=True`. Manual DB validation is mandatory before production go-live but does not block planning. Next: `WMS_LEARUN_HOST_INTEGRATION_PLANNING`; do not start it automatically.

## 2026-08-30 Functional Cleanup Phase 3 Loop

- Split formal outbound ownership into focused Order, Picking, and Exception services/repositories; `OutboundModule` remains one module and route contracts are unchanged.
- Deleted the FormalOutbound service/repository/interfaces without a compatibility facade. StartPicking, release, normal/replacement scan, return, return-to-stock, and replacement reservation retain their existing atomic repository transactions, locks, RequestId behavior, and rollback paths.
- Model and host builds PASS; test build PASS with six pre-existing obsolete-model warnings; regression executable PASS (`124/124`). No database, SAP, migration, DDL, DML, or seed ran.
- Next: `WMS_FUNCTIONAL_CLEANUP_PHASE_4_OUTBOUND_MODULE_SPLIT`; do not start it in this task.

## 2026-08-30 Functional Cleanup Phase 4 Outbound Module Split

- Replaced `OutboundModule` with `OutboundOrderModule`, `OutboundPickingModule`, `OutboundExceptionModule`, and `OutboundSapPostingModule`. All 18 formal outbound method/path pairs, request binding, response envelopes, and service calls are unchanged; SAP posting recovery routes remain available.
- Moved the posting-only `PostingFailure` helper intact. Deleted the old module and updated the WMS explicit Compile entries plus one stale structural test.
- Verification: Core/Test/Host builds PASS; regression harness `124/124` PASS. Route audit: 18 routes, 0 duplicate registrations, 0 collisions. No Core business code, repository/SQL/transaction logic, schema, database data, migration, SAP request, or HTTP contract changed.
- Report: `WMS_FUNCTIONAL_CLEANUP_PHASE_4_REPORT.md`. Next recommended action is `WMS_SECONDARY_OCCAM_AUDIT`; it was not started.

## 2026-08-30 Secondary Occam Audit

- Audit-only review of the current post-Phase-4 source tree; no Runtime, schema, migration, configuration, project, or test change was made.
- Found a small evidence-backed future cleanup: declaration-only scan/private row helpers and three unused allocation-write helpers remain in `OutboundExceptionRepository`; `SapOutboundIntegrationModule.ExtractDeliveryNo` is also unused. The report recommends a narrow, review-gated Phase 5 deletion scope only.
- Focused outbound boundaries, Effective Location, planner, SAP SHIPPED finalizer, Putaway/recommendation split, Repack transaction repositories, WeekCode roles, inventory API separation, target-acceptance support, and Nancy host residue are deliberate KEEP/defer decisions. Do not introduce generic abstractions.
- Report: `WMS_SECONDARY_OCCAM_AUDIT_REPORT.md`. Recommended next action: `WMS_FUNCTIONAL_CLEANUP_PHASE_5`; it was not started.

## 2026-08-30 Functional Cleanup Phase 5

- Removed only verified dead private Phase 3 residue: the scan-only helper/row cluster and three declaration-only allocation write helpers from `OutboundExceptionRepository`. `ScanLocationRow` remains because active ReturnPickDetail uses it.
- Removed unused `SapOutboundIntegrationModule.ExtractDeliveryNo`, its unused imports, and the exact redundant catch/rethrow around SAP order receive. No HTTP behavior, service/repository boundary, public interface, transaction, SQL used by live operations, route, DTO, schema, or migration changed.
- Verification: Core/Test/Host builds PASS; test build retains six pre-existing obsolete compatibility warnings; regression executable PASS (`124/124`). No database, SAP, DDL/DML, migration, seed, or target-environment validation ran.
- Report: `WMS_FUNCTIONAL_CLEANUP_PHASE_5_REPORT.md`. Functional source architecture cleanup is complete; remaining shared Picking/Exception transaction-helper observation is explicitly `DEFERRED_NO_ACTION`. Next recommended action: `WMS_TARGET_ENVIRONMENT_ACCEPTANCE`; do not begin it automatically.

## 2026-08-30 WMS_FG Phase 7 SAP Posting Runtime Regression Status

- Rewrote `SapOutboundPostingRepository` to frozen Posting / PostingDetail / IntegrationLog contracts. IntegrationLog query DTO mappings are explicit compatibility aliases; retry validates current payload hash before inserting another attempt; UNKNOWN retry and manual UNKNOWN confirmation are disabled.
- Verification: Model build PASS (0 warnings/errors); Test build PASS (6 existing obsolete compatibility warnings); regression executable PASS (`126/126`).
- `DATABASE_CONNECTION=BaseDb`: `DbConnectionFactory`, MES source repositories, and WeekCode source repositories already use the single `BaseDb` key. There is no Runtime `ConfigurationManager.ConnectionStrings["WmsDb"]` or `["MesDb"]` access. Database runtime regression is blocked before SQL because BaseDb (`.\SQLEXPRESS/adms705`) reports ODBC SSL/integrated credential failure (`no credentials are available in the security package`). No WMS_FG SQL, DDL, DML, fixture, SAP call, or migration ran.
- Report: `WMS_FG_PHASE_7_SAP_POSTING_ALIGNMENT_REPORT.md`. Result: `PHASE_7_COMPLETE=FALSE`, pending a working approved adms705 test connection for PostingDetail/Attempt/FAILED/UNKNOWN/SUCCESS/atomicity/concurrency runtime verification.

## 2026-08-30 WMS_FG Phase 6 Exception / Replacement Completion

- Implemented only Final Schema alignment for exception return, ReturnToStock resolution, replacement reservation, replacement scan completion, and shipment open-exception verification in the shared `FormalOutboundRepository`.
- Exception persistence now uses only `PickDetailId`, `ReturnMoveLogId`, final reason/request/event fields and permitted statuses. Order/batch/HU ownership is derived through PickDetail relationships; removed exception links, type/state fields, timestamps, and replacement IDs are neither read nor written.
- Returned detail history uses `ReturnDt` and `MoveLogId`; replacement relation uses final `OutboundExceptionReplacement(ExceptionId, SequenceNo, ReservationId, PickDetailId)` and exception completion uses `ResolvedDt/ResolvedBy`.
- `ExceptionType` remains an API compatibility request field only and is deliberately not persisted because Final Schema has no such column. Static guard now rejects removed exception physical columns and replacement audit fields. Model/Test Debug builds PASS (0 warnings/errors); generated regression EXE reports `126/126` PASS. No database operation ran.
- SAP Posting and the final `SUCCESS -> SHIPPED` closure remain Phase 7. Report: `WMS_FG_PHASE_6_ALIGNMENT_REPORT.md`. Git remains uncommitted/unstaged and existing dirty work remains preserved.
- Result: `PHASE_6_COMPLETE=TRUE`; `NEXT_PHASE=SAP_POSTING_SUCCESS_SHIPPED`.

## 2026-08-30 WMS_FG Phase 5 Allocation / Reservation / Picking Completion

- Implemented only the core Phase 5 physical-schema alignment in `FormalOutboundRepository`: StartPicking, allocation release, scan-picking writes, and picking-state reads now use the frozen `WMS_FG` Allocation / Reservation / PickBatch / PickBatchDetail / PickDetailItem contracts.
- `OutboundAllocation.RequestId` is the exclusive StartPicking idempotency key. PickBatch no longer persists GroupKey or RequestId; its physical aggregation is `AllocationId + ProdNo + Uom`. GroupKey is read-only derived compatibility data.
- Reservation now writes stage-specific `ReserveDt/ReserveBy`, `ConsumeDt/ConsumeBy`, and `ReleaseDt/ReleaseBy`. PickBatchDetail writes required `ReservationId`; current/historical movement locations are read through `MoveLogId`, not deleted detail location fields. PickDetailItem writes `ProdNo/ProdRev/LotNo/SAPBatchNo`.
- Static guard rejects old INSERT columns for Phase 5 tables in `FormalOutboundRepository`; Model and Test Debug builds PASS (0 warning/error), and the .NET Framework test executable reports `126/126`. A current `dotnet` WMS host retry is blocked by missing `Microsoft.WebApplication.targets` in the installed SDK (`MSB4019`); earlier continuation evidence recorded a WMS Host Debug PASS. No database operation ran; the prior read-only BaseDb probe remains blocked locally before SQL by integrated-authentication SSPI/TLS credentials.
- Exception/replacement flows in the shared repository are intentionally deferred to Phase 6; SAP Posting and `SUCCESS -> SHIPPED` finalization remain Phase 7. Report: `WMS_FG_PHASE_5_ALIGNMENT_REPORT.md`. Git remains uncommitted/unstaged and pre-existing dirty changes remain preserved.
- Result: `PHASE_5_COMPLETE=TRUE`; `NEXT_PHASE=EXCEPTION_REPLACEMENT`.

## 2026-08-30 WMS_FG Phase 4 Outbound Order Receive Completion

- Implemented only SAP Outbound Order Receive Final Schema alignment. `WMS_FG_OutboundOrder` now persists SAPDeliveryNo as the business identifier and stores only final Header fields; API response `OutboundOrderNo` is explicitly derived from SAPDeliveryNo for compatibility.
- `WMS_FG_OutboundOrderLine` now persists `ProdNo`, required/effective quantity, source-order facts, counters, and status. Removed MaterialRevision, line Plant/SAPStorageLocation, item-category, shipped, and payload physical fields are not written. Header remains the source of PlantCode/SAPStorageLocation.
- `WMS_FG_OutboundOrderReceiveLog` now uses its final physical contract, including `ReceiveDt`, without RequestId/InterfaceCode/CreateDt legacy columns. Idempotency remains `SAPDeliveryNo + SourcePayloadHash`: same payload returns duplicate, changed payload rejects.
- Incoming legacy SAP header fields absent from Final Schema remain in the canonical request JSON snapshots only; they are not recreated as database columns. VBELN max validation now matches the final 50-character physical column.
- The SAP receive module no longer writes through the deferred SAP Posting integration-log writer. It relies on the dedicated order receive log; SAP Posting/IntegrationLog final implementation remains Phase 7.
- Static guard now rejects removed Order, OrderLine, and ReceiveLog insert columns in `SapOutboundOrderReceiveRepository`. Verification: Test Debug build PASS with 6 existing obsolete compatibility warnings; console regression `Passed 126 tests`; WMS Debug build PASS, 0 warning/error. No database SQL/DDL/DML/migration/seed/fixture executed.
- Report: `WMS_FG_PHASE_4_ALIGNMENT_REPORT.md`. Git remains uncommitted/unstaged and prior dirty changes remain preserved.
- Result: `PHASE_4_COMPLETE=TRUE`; `NEXT_PHASE=ALLOCATION_RESERVATION_PICKING`.

## 2026-08-30 WMS_FG Phase 3 Finished Goods Inbound Completion

- Implemented only Finished Goods Inbound Final Schema runtime alignment. Receipt persistence now uses `ReceiveDt/ReceiveBy`; Final Schema receipt read/monitoring SQL uses `ReceiveDt`; API/read DTOs retain `ReceiveTime/Operator` through explicit aliases.
- Removed inbound writes to deleted `WMS_FG_HU_Identifier` columns `IsPrimary`, `SourceSystem`, and `SourceEntityId`. HU item persistence writes physical `ProdNo/ProdRev`; `SAPBatchNo` remains separate, and nullable `LotNo` is not fabricated where the ingress snapshot has no source lot fact.
- Corrected a Final Schema runtime failure risk: each inbound OperationLog entry now gets a deterministic unique `FGI-OP-{receiptId}-{sequence}` number, rather than reusing one receipt-level number against the unique `OperationNo` constraint.
- Tightened ingress validation to frozen limits: SourceType 50, alternate identifier 100, ProdRev 20, WeekRuleSource 100, WeekRuleValue 500, and receipt Remark 500.
- Static guard now rejects removed inbound receipt physical columns and deleted HU Identifier insert columns. It continues to enforce `PROJECT_OWNED_RUNTIME_OLD_WMS_TABLE_REFERENCE=0`.
- Verification: Test project Debug build PASS (6 pre-existing obsolete compatibility warnings); console regression `Passed 126 tests`; WMS Debug build PASS (0 warning/error). A direct solution build returned non-zero after building Model while reporting 0 compiler warning/error, so individual Model/Test/WMS builds are the recorded evidence.
- Read-only `BaseDb -> adms705` probe failed before SQL with local integrated-authentication `Failed to generate SSPI context`; no database statement, DDL/DML, migration, seed, or fixture ran. No Final Schema blocker identified.
- Report: `WMS_FG_PHASE_3_ALIGNMENT_REPORT.md`. Git remains uncommitted/unstaged and pre-existing dirty changes remain preserved.
- Result: `PHASE_3_COMPLETE=TRUE`; `NEXT_PHASE=OUTBOUND_ORDER_RECEIVE`.

## 2026-08-29 WMS_FG Phase 1 + Phase 2 Completion Review

- Continuation audit closed two runtime-only gaps left after the first pass: missing `InventoryQueryRepository` result aliases for `ProdNo/ProdRev/LotNo`, and obsolete Location hierarchy/scan/capacity state still flowing through `LocationService`/`ILocationRepository` despite no longer being SQL columns.
- Location foundation now exposes and consumes `bool IsActive`; shared putaway, movement, relocation, repack, merge/unpack, and minimal outbound location checks no longer compare `MasterStatus` strings. `MasterStatus` remains only as an explicit Location API filter/response compatibility alias.
- Final physical contract tightening: IdentifierValue length 100; HU Item `ProdRev` 20, `StockStatus` 30, `WeekRuleSource` 100, `WeekRuleValue` NVARCHAR(500); shared MoveLog/OperationLog identifiers/types aligned; Warehouse/WarehousePlant/Location validation limits aligned to final schema.
- Static guard now also rejects old HU Item qualified columns, legacy OperationLog/HoldLog insert columns, incomplete HU Item result aliases, and regression to string Location status business comparisons.
- Verification: full Visual Studio solution build PASS; static guard PASS; console regression `126/126` PASS; project-owned Runtime old `dbo.WMS_*` references remain 0. Compatibility tests intentionally emit obsolete-member warnings.
- Read-only DB retry again failed before SQL execution, this time during local TLS/integrated credential setup (`SSL provider: no credentials are available in the security package`). No DDL/DML/migration/seed/fixture was executed.
- Result remains `PHASE_1_2_COMPLETE=TRUE`; `NEXT_PHASE=FINISHED_GOODS_INBOUND`. No `FINAL_SCHEMA_BLOCKER` identified.

## 2026-08-29 WMS_FG Final Schema Phase 1 + Phase 2 Alignment

- Implemented scope：开发 Host 统一使用 `BaseDb → adms705`；Runtime 29 张项目自有表按精确映射切换为 `dbo.WMS_FG_*`。没有机械替换历史 migration/docs，也没有修改数据库、FinalSchema、框架或正式宿主配置。
- Foundation：完成 Warehouse/WarehousePlant/Zone/PackageSpec/Location 与 HU/HU Item/Relation/Identifier/CurrentLocation/MoveLog/OperationLog/HoldLog 的物理字段适配。Location 使用 `IsActive` 与 `LocationCode` 扫码；HU Item 使用 `ProdNo/ProdRev/LotNo` 且保留独立 `SAPBatchNo`；HoldReason 只从 active HoldLog 读取；CurrentLocation 的最后移动信息由 `LastMoveLogId` 联查。
- Compatibility：API 的 `MasterStatus/ScanCode/MaterialCode/MaterialRevision/BatchNo` 和历史事件时间/人员名称通过显式 alias 保留；已删除的销售属性与 Location 旧输入字段标记 obsolete，禁止持久化。
- Deferred：FinishedGoodsInbound 仅做 namespace/shared foundation 最小适配并标记 `TODO PHASE_3`；Formal Outbound 标记 `TODO PHASE_5`；SAP Posting 标记 `TODO PHASE_7`。未提前改变这些流程的业务语义。
- Static guard：测试入口扫描 `WMS-HU-Model/` 与 `WMS/` Runtime C#（排除 bin/obj），拒绝旧 `dbo.WMS_*` 表名及基础表已删除物理列。`PROJECT_OWNED_RUNTIME_OLD_WMS_TABLE_REFERENCE=0`。
- Verification：Visual Studio .NET Framework MSBuild 完整 solution build PASS；console regression `126/126` PASS。只读 `BaseDb → adms705` probe 在连接前被本机 `Failed to generate SSPI context` 阻断，未执行任何 SQL；无 DDL/DML/migration/seed。
- Files/report：详见根目录 `WMS_FG_PHASE_1_2_ALIGNMENT_REPORT.md`。既有 dirty `WMS-HU-Model.csproj`/`WMS.csproj` 与其他用户文件均保留，未 stage/commit。
- Result：`PHASE_1_2_COMPLETE=TRUE`；`NEXT_PHASE=FINISHED_GOODS_INBOUND`。Phase 3 前先恢复 SQL Windows authentication 并重跑只读 probe。

## 2026-08-27 SAP CHARG Legacy Rule Alignment

- Rule：`SAP_CHARG_RULE_SOURCE=Legacy PK_BoxTOSAP_API semantics`。新增纯 `ISapChargResolver/SapChargResolver`，输入 PlantCode+PackID+IsSpecialOrderType，严格复刻 SQL RIGHT：1010 普通 A、其他厂 B；特殊 AY/BY；示例 472425→A000472425/B000472425/AY00472425/BY00472425。`SPECIAL_ORDER_TYPES=ZP02,ZP05,ZP06,ZP08`，ZP01 等走普通规则。
- Current persistence diagnosis：MES `PK_Packmaster.PackID` 此前未投影到 WMS。INNER_PACK 的 `WMS_HU.SourceEntityId=PK_Packmaster_Id`，HUCode/PackCode=`CustPackID`，均不是 PackID。`PACKID_CURRENT_SOURCE=adms705.dbo.PK_Packmaster.PackID`；新 ingress persistence 使用现有 `WMS_HU_Identifier` 的 `MES_PACK_ID` 类型，Item 级 SAP CHARG 写现有 `WMS_HU_Item.SAPBatchNo`。`SCHEMA_CHANGE_REQUIRED=False`、`NEW_MIGRATION_FILE=NONE`。
- Special source：MES provider 在读取 PlanStock source graph 时同批投影 PackID，并用 `EXISTS(PK_PackLot INNER JOIN PP_LotCard ON LotNo)` 判断任一源 Lot 的 OrderType 是否特殊；不读取 outbound LFART/PSTYV，不调用 PK_BoxTOSAP_API，无 N+1、无 JavaScript/MES 查询。
- Snapshot chain：FinishedGoodsInboundService 忽略客户端 SapBatchNo 并由 resolver 服务端生成；MES canonical ingress 在同一批量查询中读取每个 `PK_Packmaster_Id` 的全部有效 `PK_PackLot.LotNo`，Trim、去重后按该 LotNo 的最小 `LotSeq ASC`、再按 `LotNo ASC` 稳定排序，以英文逗号无空格拼接。零个有效值返回 `MES_LOTNO_INVALID:`；拼接后超过 500 字符返回 `MES_LOTNO_TOO_LONG:`，不截断、不选择第一条。组合 LotNo 写 `WMS_FG_HU_Item.LotNo`，PackID 写 `MES_PACK_ID` identifier，SAP CHARG 写独立 `WMS_FG_HU_Item.SAPBatchNo`。开发期所有新入库 snapshot 统一使用 `FGI_V3` 固定 9 字段 inner hash 结构，不保留 FGI_V1/FGI_V2 运行时选择。通用 inbound 客户端 LotNo 会被清空，不作为权威事实。Formal Pick 原样复制完整 LotNo/SAPBatchNo 到 PickDetailItem；S0089 继续只读 PickDetailItem.SAPBatchNo、无 BatchNo fallback。
- MES LotNo verification：`WMS_FG_HU_Item.LotNo` 与 `WMS_FG_OutboundPickDetailItem.LotNo` 的 500 字符物理长度由 Human 已完成，本轮不生成/执行 migration。standalone 与 Learun WebApi WMS_FG 的 5 个本轮相关源码文件 SHA-256 一致；Model、Test、WMS Host、Learun WebApi build PASS，控制台完整回归 `142/142` PASS（仅既有编译 warning）。未连接 BaseDb，未执行 DML/DDL/migration，未调用 SAP；开发期 Hash 直接以最新 FGI_V3 为准，不实现旧版本兼容分支。
- Quantity/contract：`LFIMG_BOX_RULE=ROW_BATCH_QTY` 保持不变；UMCHA 未加入 S0089；Endpoint-only/no Basic Authorization 保持不变。MergeBox 只移动既有 INNER_PACK relation，不重建 HU Item，因此 SAPBatchNo 快照保留。
- Historical null strategy：历史 WMS_HU_Item.SAPBatchNo 不自动写 BatchNo、不从 HUCode 推导。可通过 INNER_PACK SourceEntityId=PK_Packmaster_Id 回查 MES PackID+Lot OrderType；后续如需 backfill，应单独生成“只读审计结果→Human Review→受控写入”的工具。本轮未生成/执行 backfill，不修改 WMS_DEV。
- PDA：当前 `WMS_OutboundPicking.cshtml` 已存在专用 SAP confirm dialog 样式并核对符合要求：左侧稍后处理为灰色次按钮、右侧确认出货并过账SAP为蓝色主按钮、5px 圆角，无“是否装车”，SAP V1 行为未改变。本轮不覆盖该既有页面改动。
- Verification：WMS-HU-Model、WMS Host、Test Project、Learun Web Debug build 均 PASS；`BEFORE_TEST_COUNT=122`、`NEW_TEST_COUNT=3`、`TOTAL_TESTS=125`、`PASSED_TESTS=125`、`FAILED_TESTS=0`。本轮依据 MES provider/entity 与 WMS persistence 源码完成 PackID 链路核对，未执行 MES/WMS 数据库运行时查询。migration 018 未修改/执行；无真实 SAP POST、WMS_DEV DML/DDL、Git stage/commit。

## 2026-08-27 SAP Outbound Posting V1 Runtime Preview Gate

- Contract correction：SAP 确认 `LFIMG_BOX` 是当前 BOXNO+POSNR+CHARG 行数量。Repository 现在对分组只计算一次 `lineQuantity` 并同时赋给 LFIMG/LFIMG_BOX；仍按 PickDetail 汇总所有有效 PickDetailItem 并校验等于 PickBatchDetail.EffectiveQty。新增单批 70/70、混 CHARG 40/40+30/30、跨 POSNR+CHARG、禁止整箱数量重复四类回归。
- S0089 configuration：必需项仅 `Sap.S0089.Endpoint`；删除 Username/Password 配置和 transport DTO 字段，不设置 Credentials/PreAuthenticate/Basic Authorization。`Sap.S0089.TimeoutMs` 保留为可选技术参数，空值使用 100000ms，已配置的非正整数仍拒绝。测试/正式 URL 未硬编码到业务代码，仓库 Endpoint 继续为空占位。
- Build/regression：WMS-HU-Model、WMS Host、Test Project Debug build 均 PASS（0 warning/error）；Learun Web Debug build PASS，只有两个既有依赖项目 assembly-reference cache access-denied warning。`TOTAL_TESTS=122`、`PASSED_TESTS=122`、`FAILED_TESTS=0`。
- Migration：migration 018 已由 Human 执行成功；Codex 未重新执行或修改。文件修改前后 SHA-256 均为 `F8C5B5E00C552945D93AB4A5E201ADE1344A6665AB0AB21F706A7D4995E731E5`。`MIGRATION_EXECUTED_BY_HUMAN=True`、`MIGRATION_EXECUTED_BY_CODEX=False`。
- Runtime candidate：WMS Host `/shipping-orders?outboundStatus=PICKED` 返回仅 2 单：8304812756、8304812755，均 PICKED/READY、2 boxes/70 qty。两单 Preview 都返回 HTTP/API 400 `SAP_POSTING_CHARG_MISSING`；分别首先发现 B-B-2608260840125E2P-01、B-B-2608251758532HCO-01 缺少 SAPBatchNo。因此 WMS_DEV 当前没有可生成 data[] 的合格 PICKED VBELN，不能使用 BatchNo fallback 或伪造 CHARG。`MIXED_CHARG_RUNTIME_CASE=NOT_AVAILABLE`。
- Read-only evidence：对 8304812756 执行 before → Preview → after。ShippingState、PickingState、Order、两个 picked box 的 HU detail 完全相同；订单保持 PICKED，PostingStatus=READY、RequestId=null；两个 HU 保持 ACTIVE/OUTBOUND-STAGING/SELF；通用 SAP log total 0→0；MoveLog total 114→114。Preview 为 400、Data=null，没有 SAP 调用。终端直连 SQL 被当前身份 SSPI 阻断，所以没有独立读取 Posting/Detail 原始全局计数；选定订单无 durable Posting、无日志、无移动及代码 SELECT-only 路径共同构成只读证据，该限制不隐藏。
- Runtime result：`PREVIEW_TEST_RESULT=BLOCKED_NO_ELIGIBLE_VBELN`、`PREVIEW_VBELN=8304812756`、`PREVIEW_HTTP_STATUS=400`、`PREVIEW_DATA_COUNT=0`、`PREVIEW_READ_ONLY_VERIFIED=True`、`SAP_CALLED=False`、`DML_EXECUTED=False`、`DDL_EXECUTED=False`。
- Git：未 commit、未 stage。

## 2026-08-27 SAP Outbound Posting V1 Implementation

- Implemented scope：新增 VBELN 聚合的 S0089 出货过账快照、发送状态、通用 SAP 通信审计、只读 preview/shipping-state、人工 UNKNOWN 恢复、受控 retry、PDA 完成确认、Learun“出货管理”和“SAP接口日志”页面。旧 `/orders/{vbeln}/ship` 保留为 deprecated alias，但已改为调用同一 ConfirmShipmentService，不再直接调用 FormalOutboundService.ShipOrder。
- Authoritative snapshot：只读取 completed/current Allocation 下的 PICKED Batch/Detail、PickDetailItem、OutboundOrderLine 和 canonical OUTER_BOX HU。`CHARG=WMS_OutboundPickDetailItem.SAPBatchNo`，无 internal BatchNo fallback；`LGORT=OutboundOrderLine.SAPStorageLocation`；`LFIMG` 按 PickDetail+Line+SAPBatchNo 汇总；`LFIMG_BOX` 等于当前 BOXNO+POSNR+CHARG 行的 LFIMG。每个 PickDetail 的全部 Payload 行 LFIMG 合计仍校验等于 PickBatchDetail.EffectiveQty。历史 RETURNED/QUALITY_HOLD detail 不进入 payload。
- SAP payload：单 VBELN 一个 `{data:[...]}`，包含全部 POSNR/BOX/CHARG；混 CHARG 与跨 POSNR 箱按行输出实际数量，不重复整箱 EffectiveQty。LFIMG、LFIMG_BOX、NTGEW、BRGEW 是 decimal 并由 Json.NET 输出 JSON Number；VRKME/BOXSIZE 为空，NTGEW/BRGEW=0，GEWEI=KG。
- SAP transport：Core 仅依赖 `ISapOutboundPostingClient`；WMS Host 的 S0089 adapter 使用 UTF-8/application-json，不使用账号密码或 Basic Authorization。必需配置仅 `Sap.S0089.Endpoint`；可选 `Sap.S0089.TimeoutMs` 未配置时为 100000ms。配置在 snapshot/claim/attempt 之前验证；无效时不创建 Posting、Attempt 或 SENDING，也不归类 UNKNOWN。未引用 Learun NetHelper/H5TPM，也不读取或记录 Authorization。
- State/concurrency：migration 018 定义 READY/SENDING/SUCCESS/FAILED/UNKNOWN、唯一 SAPDeliveryNo、冻结 PayloadJson/Hash/Detail 和通用 `WMS_SapIntegrationLog`。Transaction A 使用 Serializable + UPDLOCK/HOLDLOCK/ROWLOCK claim；SAP 调用在事务外；Transaction B 以 PostingId+RequestId+AttemptNo ownership 完成。S 执行原子 Shipment Finalizer 并标记 SAP_RESPONSE；E 保持 PICKED；可能已到 SAP 但结果不可确认才为 UNKNOWN。无自动 retry/status query。
- Recovery：UNKNOWN 手工“已过账”不调用 SAP，转 SUCCESS/SHIPPED/MANUAL_CONFIRM；“未过账”转 retry-eligible FAILED 并保留 UNKNOWN 通信证据；retry 使用新 RequestId 和 attempt，原 PayloadJson/Hash/Detail 不变。
- Receive audit：现有 SAP Delivery Receive 继续保留 `WMS_OutboundOrderReceiveLog`，并在每次完成、重复、拒绝或异常请求后追加通用 `WMS_SapIntegrationLog`。
- Schema：`docs/database/018_OutboundSapPosting.sql` 验证 post-014 prerequisites 及 BatchNo/SAPBatchNo，创建 `WMS_OutboundSapPosting`、`WMS_OutboundSapPostingDetail`、`WMS_SapIntegrationLog`。2026-08-27 已由 Human 执行成功；Codex 未执行 migration，也未修改 WMS_DEV。
- Security：`SECURITY_MODEL_PHASE1=LEARUN_UI_AUTHORIZATION_ONLY`；`DIRECT_WMS_IT_RECOVERY_API_SERVER_AUTH=FALSE`。Operator 只是 Learun UI/application convention 填入值，不是密码学或 WMS 服务端认证身份。`ACCEPTED_RESIDUAL_RISK`：能直接访问 WMS Host 的调用者可绕过 Learun 菜单权限并伪造 Operator；部署必须把 WMS Host 限制在可信 Learun/内部网络。
- Automated verification：implementation 前 baseline 104/104；初版新增 16 tests，Runtime Preview 契约修正再新增 2 tests；当前 `TOTAL_TESTS=122`、`PASSED_TESTS=122`、`FAILED_TESTS=0`。WMS-HU-Model Debug PASS、WMS Debug PASS、test project Debug PASS，均 0 warning/error。Learun Web 在显式 `SolutionDir=D:\com.delton.gz\` 且关闭 restore 后 Debug build PASS；生成目标 DLL，只有两个既有依赖项目 `obj\Debug\*.AssemblyReference.cache` access-denied warning。三个新增页面 JS 的 `node --check` PASS。
- Runtime boundary：Human 已执行 migration 018；Codex 未执行 migration。Codex 未执行真实 SAP POST 或数据库写入。Runtime Preview GET 已执行，但当前所有 PICKED 候选均因缺少 SAPBatchNo 返回 400/Data=null。`REAL_SAP_POST_EXECUTED=False`、`SAP_STATUS_QUERY_PHASE1=UNAVAILABLE`、`HUMAN_REVIEW_REQUIRED=True`。
- Recommended Human order：审阅/备份 → 人工执行 018 → 配置 S0089 Endpoint（TimeoutMs 可选）→ 部署 WMS/Learun → GET preview 核对完整 data[] 与 JSON number → GET shipping-state → 经 SAP 方批准后执行单笔 confirm-shipment → 核对 Posting/Detail/IntegrationLog 和 SHIPPED → 再验证 E/UNKNOWN 人工恢复。
- Git：按要求未 commit、未 stage；保留工作区既有改动。

## 2026-08-26 MergeBox DIRECT_RECEIVE Runtime Defect Closure

- Implemented scope：修复 MergeBox 创建的合法 OUTER_BOX 在 `POST /api/wms/putaway/box` 被误报“外箱直接位置与有效位置来源不一致”的问题。生产变更仅将 DIRECT_RECEIVE source read 从旧 `IHuLocationRepository.GetLocation` 切换为已注入、Inventory Lookup/HU location 共用的 `IEffectiveLocationResolver.Resolve`；原 `LocationSource == SELF` guard、PALLET_INHERITED 分支和事务内重验均保留。
- Read-only runtime facts：WMS_DEV 纯 SELECT 得到 B2608260001B：HUId=4904、OUTER_BOX、ACTIVE、IsHold=false；active-parent 查询 `Relation.ChildHUId=4904` 返回 NONE。三条 active child relation 的方向均为 `ParentHUId=4904`，children=T840064330/T840064331/T840064332，child DirectLocation 均为空，不会让外箱成为某个 HU 的 child。
- Direct/effective facts：Direct=`LocationId 1 / INBOUND-PENDING / FG01 / RECEIVE`；Effective=`LocationId 1 / INBOUND-PENDING / RECEIVE`；SourceHU=`4904 / B2608260001B / OUTER_BOX`。权威 resolver 与现有 `/hus/{code}/location`、Inventory Lookup 均返回 `LocationSource=SELF`；Inventory Lookup 同时返回 Material=16I080019D、Revision=82 和三个当前 INNER_PACK。
- Root cause：旧 `HuLocationRepository.GetLocation` SQL 虽投影 `LocationSourceHuId/Code`，却未投影 `LocationSourceHuType` 或 `LocationSource`。Dapper 因而构造 `HuLocationDto.LocationSource=null`；Putaway 原失败条件的实际左右值为 `null` vs `"SELF"`，`string.Equals(...)=false`。数据库 MergeBox lifecycle 无不一致。
- Classification：`SOURCE_MODE_RESOLUTION_DEFECT=True`、`LOCATION_SOURCE_ENUM_DEFECT=True`；`ACTIVE_PARENT_DIRECTION_DEFECT=False`、`DIRECT_LOCATION_RESOLUTION_DEFECT=False`、`EFFECTIVE_LOCATION_RESOLUTION_DEFECT=False`、`DIRECT_EFFECTIVE_COMPARISON_DEFECT=False`、`MERGEBOX_STATE_DEFECT=False`、`DATA_INCONSISTENCY=False`。
- Regression：新增 1 个 method-level test，建立 B-MERGED + 三个 child INNER_PACK；legacy direct DTO 保持缺失 LocationSource，authoritative resolver 返回 SELF。验证 children 不会成为 box parent、DIRECT_RECEIVE 成功；模拟事务后 box 为 SELF/H208/STORAGE，children DirectLocation 仍 null、ParentBox 不变、Effective H208/PARENT_BOX。既有 PALLET_INHERITED 继续通过，并新增真实 Direct/Effective conflict 返回原固定错误的断言。
- Verification：`NEW_TEST_COUNT=1`、`TOTAL_TESTS=104`、`PASSED_TESTS=104`、`FAILED_TESTS=0`；WMS-HU-Model Debug（独立及 test project reference）PASS；WMS Host Debug PASS。修复前失败 RequestId `PDA-BOX-PUTAWAY-1787732550227-62159` 的诊断时只读核对为 OperationLog=0、MoveLog=0。随后 Human Review 已完成 `POST /api/wms/putaway/box` Runtime 验证并确认无误；`RUNTIME_POST_READY=True`、`RUNTIME_POST_VERIFIED=True`。本次文档收尾未重放 POST，也未新增 DML。
- Schema/API/scope：无 schema、migration、DTO、route、response contract 或 Recommendation 变更；migration 017 未修改/执行。诊断阶段仅 SELECT/GET，未修改 WMS_DEV。生产文件仅 `PutawayService.cs`；另修改 console test 与本 handoff。Git：未 commit、未 stage。
- Runtime closure：该 DIRECT_RECEIVE 缺陷已完成只读根因证明、最小修复、104/104 自动化回归、Model/Host 构建和 Human Runtime POST 验收，无剩余阻断项。Human 验证结论作为本轮 Runtime evidence 记录；Codex 未独立重放写请求。无需追加 Runtime 写入测试，后续仅按常规发布流程部署当前 DLL。

## 2026-08-26 V1 Dynamic Cluster Runtime Defect Closure

- Implemented scope：修复 `GET /api/wms/putaway/recommendations` 未识别 H208 既有同料同版库存簇的问题；未改变 `V1_DYNAMIC_CLUSTER` 的 RecommendationLevel 优先级、排序权重、Week tie-break、topology 或候选数量。
- Read-only diagnosis：WMS_DEV 纯 SELECT 证明 B1951241B/B1951243B/B1951245B/B1951247B 均被 `PutawayRecommendationRepository` 加载；每箱均经 active `OUTER_BOX -> Relation -> INNER_PACK -> HU_Item` 解析为 `16I080019D / 82 / 2026-W32`，EffectiveLocation=`H208`、LocationSource=`SELF`。H208 也在合法 STORAGE Location 集合中，Warehouse/Location active、physical、scannable、PlantPolicy=`SAME_PLANT`、AllowMixedMaterial=`0`。
- Root cause/classification：`INVENTORY_SNAPSHOT_DEFECT=False`、`MATERIAL_RESOLUTION_DEFECT=False`、`EFFECTIVE_LOCATION_DEFECT=False`、`TOPOLOGY_DEFECT=False`、`RANKING_DEFECT=False`；确认为 `CLUSTER_BUILD_DEFECT=True`。H208 现有 83 个有效 OUTER_BOX、包含多种历史物料；Service 原先在 `BuildLocationStates` 中用 `!AllowMixedMaterial && boxes.Any(!HasOnlyIncomingMaterial)` 整体删除货位，导致四个精确匹配箱在 SelectMainCluster 之前丢失。V1 合同只允许 AllowMixedMaterial 排除 `MIXED_FALLBACK`，不能排除已经存在目标 Material+Revision 的 cluster。
- Minimum fix：仅删除上述建簇硬过滤。`BuildCandidates` 仍只在 `AllowMixedMaterial=true` 时产生 `MIXED_FALLBACK`；PlantPolicy、WarehousePlant、ACTIVE/RECEIVE/STORAGE/EffectiveLocation 等过滤保持不变。
- Regression：新增真实形态测试：H208 含四个 `16I080019D/82` 目标箱及其他物料、AllowMixedMaterial=false，H207/H209 空；断言 TOP1 H208/EXISTING_CLUSTER、TOP2 H209/CLUSTER_EXPANSION、TOP3 H207/CLUSTER_EXPANSION。控制台全量 `Passed 103 tests`。
- Runtime GET：重新构建后，本机 WMS_DEV 只读 GET 返回 200/success，TOP1=`H208 / EXISTING_CLUSTER`、Matching cluster count=4、TOP2=`H209 / CLUSTER_EXPANSION`。实际 TOP3=`H108 / PAIRED_ROW_NEARBY`；额外纯 SELECT 证明 H207 有 ACTIVE OUTER_BOX `B1951239B`（无 active Item），因此按推荐的“active/effective OUTER_BOX 物理占用”定义并非 EMPTY，用户提出的 H207 条件不成立。Inventory Lookup 的 H207 Item 列表为 0 不代表该位置没有物理外箱占用。
- Verification：`MSBuild WMS-HU-Model.Tests/WMS-HU-Model.Tests.csproj /t:Build /p:Configuration=Debug` PASS（同时构建 model）；`WMS-HU-Model.Tests.exe` PASS 103/103；`MSBuild WMS/WMS.csproj /t:Build /p:Configuration=Debug /p:BuildProjectReferences=false` PASS；真实 recommendation GET PASS。所有数据库诊断均为 SELECT/GET。
- Schema/API/data：无 schema、migration、DTO 或公共 API contract 变更；未修改/执行 migration 017，未执行 WMS_DEV DML/DDL。未修改推荐优先级。
- Files：`WMS-HU-Model/Services/Impl/PutawayRecommendationService.cs`、`WMS-HU-Model.Tests/Program.cs`、本 handoff。建议 Postman 顺序：当前 B2608260001B GET -> 验证 TOP1 H208 -> 查 H208 Inventory -> 查 H207 HU 占用事实 -> 构造真正空的 H207 回归时再验证 H209/H207 forward/backward。Git：按要求未 commit、未 stage。

## 2026-08-26 Recommended Putaway V1 Dynamic Cluster Closure

- Implemented scope：新增只读 `GET /api/wms/putaway/recommendations?warehouseCode=FG01&boxHuCode={OUTER_BOX}`，正式版本固定 `V1_DYNAMIC_CLUSTER`，模式为 `DECISION_ASSISTANCE`，全局排序后最多返回 TOP 3。`RecommendedLocationCode` 始终等于 Rank 1；合法候选不足时允许少于 3/空数组，成功 Message 提示人工扫描，不编造候选。
- Input eligibility：请求 Trim/uppercase，Warehouse 最大30、Box最大100；输入必须为 ACTIVE、非 Hold OUTER_BOX，EffectiveLocation 位于请求仓库 RECEIVE，来源仅允许 SELF（DIRECT_RECEIVE）或 PARENT_PALLET（PALLET_INHERITED），WarehousePlant 必须允许箱厂别。SELF/STORAGE 明确按 relocation 场景拒绝；两种正式上架来源共享同一推荐引擎。
- Primary affinity：输入箱 active Relation/INNER_PACK/Item 必须解析为唯一 `MaterialCode + MaterialRevision`；不选择第一条或主导值，冲突返回 `PUTAWAY_RECOMMENDATION_ITEM_CONFLICT:`。推荐库存以 ACTIVE OUTER_BOX 去重计数，子 INNER_PACK 不重复计数；沿用 SELF → 最近 active parent 的 EffectiveLocation 递归规则，SHIPPED/历史 relation 不参与，HOLD 外箱继续计入实体占用。
- Dynamic cluster：同一 PhysicalRow 的同料同版连续占用位置组成 cluster；多个 cluster 按 matching box总数、占用位置数、连续长度、PutawayPriority、Row/Position 稳定选主簇。多货位主簇 `Max > Min` 时 `CLUSTER_FILL` 先补低于 Max 的位置；平衡后 `CLUSTER_EXPANSION`；单货位簇固定 `EXISTING_CLUSTER` TOP1，不把当前箱数解释为容量。
- Spatial order：同排 expansion 只取主簇边界相邻空位，等条件下 forward（MaxPosition+1）先于 backward；随后为同 `PhysicalRackGroupCode` 的 `PAIRED_ROW_NEARBY`，距离取候选 Position 到主簇最近 Position 的绝对差；再到其他 `EMPTY`、`SAME_MATERIAL_OTHER_REV`、AllowMixedMaterial 明确允许的 `MIXED_FALLBACK`。缺少 topology 时不从 LocationCode/Remark/RouteSequence 猜相邻或配对关系；H212 不跨排连接 H301，H913 不虚构 H813。
- Week/tie break：incoming 使用最早/最小 WeekSortKey 对应的 ISO 周；candidate 使用同料同版库存周。Week 仅在 RecommendationLevel、动态箱数和物理距离等更强条件之后比较，暴露 `SAME/NEAR/UNKNOWN`，不会跨推荐层级。Batch、AllowMixedBatch、AllowMixedOrder 不作 V1 硬过滤。
- Capacity：`CAPACITY_USED=False`、`FIXED_PREFERRED_FILL_COUNT_USED=False`。Service/Repository 不读取或判断 CapacityQty、MaxWeightKg、箱尺寸、重量、体积、MaxBoxCount，也不把 cluster 当前 Max 当真实容量；V2 capacity-aware 明确保留为未来范围。
- Repository/read side：`PutawayRecommendationRepository` 在单连接内以少量集合查询加载输入箱上下文、active Item、WarehousePlant、合法 STORAGE Location 和当前有效外箱分布；递归 ancestor join 复用 `EffectiveLocationResolver.ActiveParentPredicateSql`，只接受 active relation/active parent。使用 typed `SqlParameter`，无 AddWithValue、无 dynamic、无候选 N+1、无 DML/事务锁/Reservation/MoveLog/OperationLog。
- Public DTO：response 含 `BoxHuCode`,`WarehouseCode`,`MaterialCode`,`MaterialRevision`,`RecommendationVersion`,`RecommendedLocationCode`,`Recommendations[]`；每项含 Rank、LocationCode、RecommendationLevel、Reason、CurrentBoxCount、ClusterMin/Max/Gap、RackGroup/Row/Position、WeekAffinity。
- Topology migration：新增未执行草案 `docs/database/017_LocationPhysicalTopology.sql`，仅新增 nullable `PhysicalRackGroupCode varchar(30)`,`PhysicalRowCode varchar(30)`,`PositionSequence int`，使用 filtered unique index `WarehouseId + PhysicalRowCode + PositionSequence`。脚本内编码 101 个确认 H topology 映射：H12/H1-H2、H34/H3-H4、H56/H5-H6、H7 standalone、H89/H8-H9；H913=position13，未创建 H813 或 LocationAdjacency。脚本仅更新 FG01 中已经存在的对应 Location，不创建缺失物理货位，不改 Capacity/Remark/RouteSequence；需 Human Review、备份/快照和人工执行。
- Location contract：`LocationDto` 与现有 Location read SQL 向后兼容增加三项 topology 字段；Create/Update 公共写请求未扩展，避免在本任务扩大主管维护权限。部署新 DLL 前必须先按审批顺序应用 017，否则旧 schema 不含新列时 Location/recommendation 查询不可用。
- PDA：更新当前工作树 `WMS_RecommendedPutaway.cshtml`，外箱 Inventory Lookup 成功后异步调用推荐 GET，紧凑显示 ①/②/③，TOP1 视觉最强并显示版本。推荐失败、超时、空数组或拓扑不足统一 fallback 为“暂无推荐 / 请扫描现场实际货架”；不自动填充、不点击上架、不替代物理扫描。
- Manual override/idempotency：PDA `canSubmit` 仍要求实际 `locationCode`；POST payload 的 ToLocationCode/ScannedLocationCode 都来自实际扫描。推荐 H211 时扫描合法 H212 不被推荐层拦截，现有 `/putaway/box` 仍在事务中独立校验。保留 submitRequestId：首次提交生成、网络失败复用、成功/清空/Box或Target意图变化清除；PDA 不发送 SourceMode。
- Tests：`BEFORE_TEST_COUNT=92`；本轮新增 10 个 method-level tests；`TOTAL_TESTS=102`,`PASSED_TESTS=102`,`FAILED_TESTS=0`。覆盖两种 source、冲突/已入库拒绝、单货位继续、10/9动态补齐与10/10扩展、多位置低箱数/Top3、forward、paired row/no H813、Week同层优先且不跨层、missing topology/no numeric guess、只读/typed/no capacity/migration/API 静态合同、manual override。
- Frontend regression：`agents/wms-recommended-putaway-requestid-harness.js` 扩展为 15 assertions，覆盖原 RequestId 安全、推荐路由/加载、显示最多3、fallback、实际扫描提交门槛、实际值为 payload 权威目标和主 JavaScript block 语法；PASS。
- Build：WMS-HU-Model Debug PASS；WMS Host Debug PASS；WMS-HU-Model.Tests Debug PASS；Learun.Application.Web Debug PASS。Learun build 有 2 个既有 `obj\\Debug\\*.AssemblyReference.cache` access-denied warning，但 exit code 0，目标 DLL 生成。未执行 Runtime GET/Postman/数据库集成，因为 017 未审批/应用；不得宣称 WMS_DEV recommendation runtime PASS。
- Runtime order after Human approval：先 schema diff/backup/人工执行017 → 核对 FG01 topology 及 unique index → A 单货位 H210 TOP1保持 → B 10/9 H211补齐 → C 10/10 H212扩展 → D 阻断同排后 H1 paired row 优先 → E Week仅同层 tie → F 推荐H211但实扫H212，`/putaway/box`成功。所有 recommendation GET 只读；写侧仅在受控 F 场景使用既有接口。
- Files：新增 PutawayRecommendation DTO、Repository/Service interfaces+implementations、017 migration；修改 LocationDto/LocationRepository、model csproj、PutawayModule、console tests、API interface、PDA、PDA harness 和本 handoff。无 LocationAdjacency、无写侧推荐日志、无 `/putaway/box` 业务语义修改、无 SAP/Label/V2 capacity 变更。
- Remaining gaps/risks：017 migration 和真实库存 ranking 尚待 Human Review/Runtime；实际已存在的 H Location 数决定迁移能更新多少行，缺失物理 Location 仍由既有 Location master rollout 负责；其他 LocationType 若没有显式 topology 只走合法 EMPTY/material fallback。并发推荐不预留，同一位置可同时被多名操作员看到，这是 V1 预期。
- Git：按要求未 commit、未 stage；保留工作区此前 Inventory/PDA/Putaway/MergeBox 和其他既有脏改动，不回退、不隔离提交。

## 2026-08-26 Generic OUTER_BOX Putaway Closure

- Implemented scope：保留公共 `POST /api/wms/putaway/box` 和既有 Request DTO，由后端派生 `PALLET_INHERITED` / `DIRECT_RECEIVE`。没有新增 direct/merged/PDA route，没有 SourceMode 请求字段，没有修改 MergeBox，也没有创建假 PALLET。
- Source resolution：存在 active PALLET parent、外箱无 DirectLocation、有效位置来自 PALLET 时走既有 PALLET_INHERITED；无 active parent、外箱 SELF DirectLocation 位于同仓 active RECEIVE 时走 DIRECT_RECEIVE。父级非 PALLET、父级与 DirectLocation 同时存在、无父无位置、SELF/STORAGE 或其他 Zone 均显式拒绝；SELF/STORAGE 提示使用 relocation。
- PALLET_INHERITED：原 `ExecutePutaway` 事务保持独立且保留 relation close、DETACH_HU/PUTAWAY、Box SELF STORAGE CurrentLocation、RemainingBoxCount、最后一箱关托和 RequestId replay。响应向后兼容增加 `SourceMode=PALLET_INHERITED`。
- DIRECT_RECEIVE：新增独立 Serializable transaction repository，事务内锁定并重验 OUTER_BOX ACTIVE/非 Hold/无 active parent/direct RECEIVE source/warehouse、target physical STORAGE、WarehousePlant/PlantPolicy 与 source≠target；写 `INBOUND-PENDING → STORAGE` PUTAWAY MoveLog，条件 UPDATE 外箱 CurrentLocation，写 RelatedHUId=NULL 的 PUTAWAY OperationLog。无 Relation 写入、无 DETACH_HU、无 PALLET 更新、无 child INNER_PACK CurrentLocation 更新；子包继续由 EffectiveLocationResolver 从 PARENT_BOX 继承外箱新 STORAGE 货架。
- Idempotency：抽出兼容 nullable PALLET 的 typed ADO.NET `PutawayIdempotencyReader`；PALLET 使用 LEFT JOIN，审计 RelatedHUId 为空时重建 `DIRECT_RECEIVE` 结果。Direct transaction 在任何 mutation 前用 RequestId `UPDLOCK/HOLDLOCK` 二次判重；相同 Box+Target 返回原结果、IsDuplicate=true，不写第二条 MoveLog；不同意图或其他 WMS RequestId 拒绝。
- PDA retry safety：`WMS_RecommendedPutaway.cshtml` 新增 `submitRequestId`。第一次正式提交才生成；同一 Box+Target 网络失败保留；成功、清空、Box 接受或 Target 改变时清除。PDA 不显示、选择或发送 SourceMode。新增独立 Node harness 验证 8 项状态合同。
- Contract：Request 不变；Response 仅向后兼容增加 `SourceMode`。DIRECT_RECEIVE 的 PalletHuCode 为 null，RemainingBoxCount 保持既有非 nullable DTO 的 0；PALLET_INHERITED 原字段含义不变。
- Schema/database：`DATABASE_MIGRATION=NONE`；无 table/column/SP/migration 变更，未执行 WMS_DEV DML/DDL。MergeBox migration 016 仍仅为前一任务草案，本轮未执行或修改。
- Tests：实际执行前基线 85/85；本轮新增 7 个 method-level tests，覆盖 PALLET path/invalid target、DIRECT_RECEIVE dispatch、invalid direct states、direct replay/conflict/no second write、direct repository atomic/no fake pallet/no detach/no child mutation、pallet remaining/close regression、nullable-pallet replay和 child inheritance contract。最终 `TOTAL=92, PASSED=92, FAILED=0`；另有 PDA RequestId harness 8 assertions PASS。
- Build/verification：WMS-HU-Model Debug build PASS；WMS Host Debug build PASS；WMS-HU-Model.Tests Debug build PASS；Learun.Application.Web Debug build PASS；console 92/92 PASS；PDA RequestId harness 8/8 assertions PASS。未执行 Runtime POST 或数据库集成，不能宣称真实 merged box 上架已 Runtime PASS。
- Runtime checklist：确认 `B2608260001B` 为 ACTIVE/SELF/INBOUND-PENDING/RECEIVE 且无父级 → 用固定 RequestId POST 到 H208 → 响应 SourceMode=DIRECT_RECEIVE、PalletHuCode=null、From/To 正确 → inventory lookup box 为 SELF/H208/STORAGE → child 为 Direct null、ParentBox不变、Effective H208/PARENT_BOX → 同 RequestId replay 为 duplicate 且无第二 MoveLog → 查 PUTAWAY OperationLog、无 DETACH → 再执行一个既有 PALLET_INHERITED 样本回归。
- Files：新增 DirectReceivePutawayCommand、DirectReceivePutawayRepository、PutawayIdempotencyReader、PDA RequestId harness；修改 Putaway Result/Repository interface、Putaway Service/Repository、model csproj、console tests、Recommended Putaway PDA、API document 与本 handoff。
- Risks/gaps：真实 WMS_DEV 的 direct path、并发 replay 与 Merge→Putaway 生命周期尚待人工 Runtime；推荐货位算法仍是独立 Known Gap，本任务仅修复正式上架和 RequestId 安全。无 Label/SAP 变更。
- Git：按要求未提交、未暂存；保留两个工作区原有修改。

## 2026-08-26 Repack Merge-Box Final Closure

- Implemented scope：新增 `POST /api/wms/repack/merge-box`，由 WMS Backend 在全部业务校验通过后、同一正式事务中生成 canonical OUTER_BOX 箱码并完成合箱。Request DTO 仅含 WarehouseCode、InnerPackHuCodes、RequestId、BusinessNo、Operator、Remark，不接受新箱码、Plant、suffix、sequence 或目标库位。
- Canonical code：格式 `B{YYMMDD}{NNNN}{PlantSuffix}`，1010→A、1020→B，固定 12 位；日期是本地 WMS OUTER_BOX 创建日期，序列范围为 `BusinessDate + PlantCode`，0001–9999。`BOX_CODE_DAILY_SEQUENCE_EXHAUSTED:` 为稳定耗尽错误；未使用 GUID、随机数、时间戳正式码或 `MAX(HUCode)+1`。
- Schema：新增迁移草案 `docs/database/016_MergeBoxCodeSequence.sql`，创建最小表 `WMS_BoxCodeSequence(BusinessDate,PlantCode,CurrentNo,CreateTime,ModifyTime)`，主键 `(BusinessDate,PlantCode)`、CurrentNo check 1–9999、表/核心列描述、XACT_ABORT、显式事务、可重跑检查。本轮未执行脚本，未修改 WMS_DEV schema/data，010–014 继续保持 sealed。
- Transaction：Repository 使用单个 Serializable 事务；先用 RequestId range lock 判定幂等，再集合锁定/校验全部 INNER_PACK、active Item、Relation、CurrentLocation、Warehouse/Zone；解析当前仓库唯一 active virtual `INBOUND-PENDING`/RECEIVE 并校验 WarehousePlant/PlantPolicy；最后用 `(BusinessDate,PlantCode)` 的 `UPDLOCK,HOLDLOCK` 分配序号，然后创建 ACTIVE OUTER_BOX、CurrentLocation、active CONTAIN relations，删除所有 child DirectLocation，写审计和幂等快照并提交。任一步失败整体回滚。
- Validation：每个 HU 必须是 ACTIVE、非 Hold、无 active parent 的 INNER_PACK，位于指定仓库 active physical STORAGE，且存在 active Item；同一内包多 active Item 的 Material/Revision 冲突返回 `INNER_PACK_ITEM_CONFLICT:`。全部内包必须同 Plant/Warehouse/Material/Revision；Batch、Week 和 source rack 可不同，cross-location merge 明确允许。仅 1010/1020 可生成当前规范箱码。
- Location semantics：新 OUTER_BOX 为 ACTIVE，Direct/EffectiveLocation=`INBOUND-PENDING`，LocationSource=SELF；子 INNER_PACK 的 DirectLocation 删除，通过 active parent 和既有 EffectiveLocationResolver 得到 `INBOUND-PENDING`/PARENT_BOX。后续现有 `POST /api/wms/putaway/box` 将新箱移至 STORAGE 后，子包继续自动继承新货架。
- Idempotency/audit：成功响应作为 `MERGE_BOX` OperationLog DetailJson 快照持久化；相同 RequestId + 相同 Warehouse/pack set 返回同一 NewBoxHuCode、IsDuplicate=true，不分配第二个序号；冲突请求拒绝。新箱记录 NULL→INBOUND-PENDING 的 RECEIVE MoveLog 与 CREATE_HU/MERGE_BOX；每个内包记录 source STORAGE→INBOUND-PENDING 的 PACK_INNER MoveLog、ATTACH_HU/PACK_INNER OperationLog 和 relation history。
- API response：`NewBoxHuCode`,`PlantCode`,`WarehouseCode`,`MaterialCode`,`MaterialRevision`,`InnerPackCount`,`TotalQty`,`InnerPackHuCodes`,`LocationCode`,`ZoneCode`,`IsDuplicate`。全局 ApiResult 未扩展；沿用 Repack Module 的现有业务失败 envelope 行为。
- Automated tests：新增 10 个 method-level regression，覆盖 1010/1020 格式、Plant 独立序列语义、同厂递增/跨日 reset、非法/超界序号、cross-rack success、Hold/non-ACTIVE/parent/non-STORAGE、Material/Revision/Plant/Warehouse/Item conflict、Service normalize/duplicate-list、幂等 replay/no execute、事务/锁/耗尽/typed SQL/migration/static audit 与 request server-owned field boundary。控制台全量 `TOTAL=85, PASSED=85, FAILED=0`。
- Build commands：`MSBuild WMS-HU-Model/WMS-HU-Model.csproj /t:Build /p:Configuration=Debug` PASS；`MSBuild WMS/WMS.csproj /t:Build /p:Configuration=Debug /p:BuildProjectReferences=false` PASS；`MSBuild WMS-HU-Model.Tests/WMS-HU-Model.Tests.csproj /t:Build /p:Configuration=Debug` PASS；`WMS-HU-Model.Tests/bin/Debug/WMS-HU-Model.Tests.exe` PASS（85/85）。
- Runtime/deviations：未执行 migration、Postman POST、数据库并发或回滚注入集成测试，不能宣称 Merge Runtime/DB integration PASS；并发与原子性当前由 SQL contract/static regression 覆盖。Label printing 未实现且 `LABEL_PRINT_INTEGRATION_PENDING=True`；SAP 不参与。
- Recommended Postman order（迁移经人工 schema review、备份和审批后）：准备 2–3 个 ACTIVE/SELF/physical STORAGE detached INNER_PACK（可放不同货架）→ POST merge-box → 校验箱码格式及新箱 inventory lookup → 逐包 lookup 校验 DirectLocation null/ParentBox/Effective INBOUND-PENDING → 相同 RequestId replay → `POST /api/wms/putaway/box` 上架新箱 → 再查子包继承 STORAGE；另测 Material/Revision/Hold/parent/warehouse/RequestId conflict 和 9999 耗尽（仅受控环境）。
- Files：新增 MergeBox DTO、rules、canonical formatter、transaction repository、migration 016；扩展 Repack Service/Repository interfaces and implementations、Nancy Module、model csproj、console tests；更新 API interface list 与本 handoff。未修改 PDA、SAP、label、现有路由或历史 migrations。
- Git：按用户要求未提交、未暂存；保留工作区原有修改，不回退其他人的文件。

## 2026-08-26 PDA Phase-1 Runtime Closure

- Scope：仅收尾 Human Review 已完成的 Postman Runtime 结果；未新增业务功能，未修改 PDA 页面、Module、Service、Repository、Schema、migration、stored procedure 或公共 API Contract，未执行新的 Runtime 写入测试。
- Runtime Environment：`WMS_DEV`、.NET Framework 4.7.2、Nancy 2.0、SQL Server、Postman；`WarehouseCode=FG01`、`PlantCode=1010`、Storage Location=`H201` / `H305`、Inbound virtual location=`INBOUND-PENDING`。
- Runtime Gate：`RUNTIME_MUTATION_TESTS_EXECUTED=True`。本轮 Human Review 已在 `WMS_DEV` 执行真实 WMS 写入；未在 Production 执行，未调用最终 `SHIP`，未执行推荐上架、Merge Box 或 Label Print。
- Runtime Verified Capabilities：Whole Pallet Putaway、Batch Relocation、PDA Picking Resume、Whole-box Unpack 四项新增后端能力均已 Runtime Verified。
- Whole Pallet Putaway Evidence：`GET /api/wms/putaway/pallet/by-box/{boxHuCode}` 与 `POST /api/wms/putaway/pallet` 已验证。测试 Pallet 包含 Box01=40 PCS（Inner01A=20、Inner01B=20）、Box02=30 PCS（Inner02=30）、Box03=30 PCS（Inner03=30），总计 100 PCS；`Material=14A890336C`、`Revision=41`、`Warehouse=FG01`、`Plant=1010`。
- Formal Outbound State Machine：`WAIT_PICK → ALLOCATED → PICKING → PICKED → SHIPPED`。PDA Picking Resume 对 `ALLOCATED` 和 `PICKING` 均适用；前者恢复已分配但尚未首扫的既有任务，后者恢复首扫后的在途任务。picking-state 保持只读，不调用 StartPicking、不创建或更新拣货事实；本轮未实际调用最终 SHIP。
- Known Gaps：保留 `RECOMMENDED_PUTAWAY_RULE_UNDEFINED`、`NEW_OUTER_BOX_CODE_OWNERSHIP_UNCLEAR`、`LABEL_PRINT_INTEGRATION_PENDING` 三项；本轮未以文档收尾名义补造规则、箱码或打印集成。
- Verification Source：上述 Runtime 结论来自本轮 Human Review 提供的真实 Postman Evidence；本次文档更新未重跑 Postman、数据库 DML/DDL、migration、build 或自动化测试。
- Files：仅更新 `docs/WMS_API_Interface_List.md` 与 `docs/handoff/CODEX_HANDOFF.md`。Git：按要求未创建提交。

## 2026-08-25 PDA Remaining Six Screens - Backend Closure

- Summary：本轮按真实实现完成 4 项安全闭环并保留 3 项 Known Gaps。新增整托原子上架、批量原子移库、正式出库 picking-state 只读恢复、整箱原子拆包；推荐上架、Merge Box 与 Label Print 未伪造规则、箱码或集成。未修改 PDA 前端。
- Whole Pallet Putaway：新增 `GET /api/wms/putaway/pallet/by-box/{boxHuCode}` 和 `POST /api/wms/putaway/pallet`。Read 由 scanned OUTER_BOX 反查 active PALLET，并批量聚合当前 active child boxes 及统一 EffectiveLocation；Write 在单个 Serializable 事务内锁定并重验 Pallet、全部 active child、Hold、DirectLocation、目标 STORAGE、仓库/厂别/PlantPolicy，逐箱复用拆托、MoveLog、CurrentLocation、DETACH_HU/PUTAWAY 语义，最后关闭一次性托盘。任一失败全部回滚。
- Recommended Putaway：`RECOMMENDED_PUTAWAY_RULE_UNDEFINED`。现有 master、Putaway、Repository、文档未发现确定策略；未实现 `TOP 1` 等猜测算法。最终写仍复用 `POST /api/wms/putaway/box`，未来 recommendation 只允许作为 read-only suggestion。
- Relocation：新增 `POST /api/wms/relocation/batch`。仅 OUTER_BOX、显式 Warehouse/Source/Target、去重输入、最多 200 箱；源目标为同仓 active physical STORAGE，箱 ACTIVE、非 Hold、无 active parent，事务内 DirectLocation=Source（即当前允许移动模型下 EffectiveLocation=SELF）。集合加载锁定，单个 Serializable 事务更新全部 CurrentLocation 并写 MoveLog/OperationLog，无 partial success。
- Formal Outbound：新增 `GET /api/wms/outbound/orders/{vbeln}/picking-state`，只读恢复现有 Order/Lines、Allocation、PickBatch、active Reservation remaining tasks、completed PickDetails。正式状态机为 `WAIT_PICK → ALLOCATED → PICKING → PICKED → SHIPPED`；WAIT_PICK 不创建事实，ALLOCATED 与 PICKING 均复用现有 Allocation/Reservation/PickBatch 供 PDA Resume；GET 不调用 StartPicking；完成后不自动 SHIP。多个 active Allocation 或同 GroupKey 多个 active PickBatch 返回 `PICKING_STATE_CONFLICT:`。
- Whole-box Unpack：新增 `POST /api/wms/repack/unpack-box`。复用 unpack-inner 的明确位置语义：关闭 active Box→Inner Relation，并给每个 INNER_PACK 建立实体 STORAGE DirectLocation；未传 ToLocationCode 时确定性使用源箱当前直接货位。单个 Serializable 事务内重验外箱、全部 active children、Hold、位置与厂别策略，逐包写 MoveLog/DETACH_HU/UNPACK_INNER；任一失败全部回滚。
- Merge Box：`NEW_OUTER_BOX_CODE_OWNERSHIP_UNCLEAR`。当前 HU create 要求调用者提供 HUCode，MES inbound 只接收 MES canonical BoxBarCode，未发现权威 WMS 新外箱编码生成器；因此未新增 merge-box 写接口，未使用 GUID/时间戳/随机码。`LABEL_PRINT_INTEGRATION_PENDING`：未找到正式打印 API/协议/服务，未新增打印基础设施。
- EffectiveLocation/Hold：Read context 复用统一 `IEffectiveLocationResolver`；命令在事务内重验当前 relation/location。所有写命令拒绝现有 Hold，不自动 Release，不新增 PDA Hold 定义。
- Idempotency：复用 RequestId 约定；批次 base RequestId 由 OperationLog 判重，每个 child MoveLog 使用 `base:001` 等确定性后缀以满足现有 MoveLog RequestId 唯一约束。未新增通用幂等框架。
- Schema/Safety：无 migration、table、column、SP 或业务状态变更；未执行 migrations 010–014；未对 WMS_DEV/MES/SAP 执行 DML/DDL。Repository 使用 typed SqlParameter/现有 typed log writer，无 AddWithValue、无用户输入 SQL 拼接、Service 以下无 dynamic。
- Tests：Previous baseline=69；New=5；Total=74；Passed=74；Failed=0。覆盖整托 context/EffectiveLocation/command normalization、批量移库 duplicate/source-target/atomic dispatch、picking-state resume/no-write/conflict、whole-box command/default source location、Repository transaction/typed parameter/static contract；既有全部回归继续通过。
- Build：WMS-HU-Model Debug PASS；WMS Debug PASS；WMS-HU-Model.Tests Debug PASS。Runtime：`RUNTIME_MUTATION_TESTS_EXECUTED=True`；Human Review 已在 WMS_DEV 以 Postman 完成 4 项新增后端能力的真实 Runtime 验证，未在 Production 执行，未调用最终 SHIP，未执行推荐上架、Merge Box 或 Label Print。
- Files：新增 WholePalletPutaway、Relocation、UnpackBox、OutboundPickingState DTO；Relocation Service/Repository/Module、WholeBoxUnpackRepository；扩展 Putaway/Repack/FormalOutbound Service/Repository/Module 接口与实现；更新两个 csproj、console tests、API 文档和本 handoff。
- Review：需要 Human 明确推荐上架策略、canonical 新外箱编码 ownership、标签打印集成；确认后才能继续 Recommendation/Merge/Print。Git：按用户要求，本轮未创建提交。

## 2026-08-25 Frontend Phase 2A - Seven Pages Visual Audit

- 状态：`PHASE_1D_BUSINESS_READABILITY_COMPLETE=True`、`PHASE_2_VISUAL_CONSISTENCY_IN_PROGRESS=True`、`PHASE_2A_VISUAL_AUDIT_COMPLETE=True`。本轮 `AUDIT_ONLY=True`，未批量修改 Page01–Page07 生产 UI。
- Learun baseline：本地选定 System Module list/tree/Grid、FilterIP toolbar/Grid + Form、EAP2 ApiUpload execution-log pager/detail、NWF process tabs/timeline 五类参照。静态 native jfGrid Header/Row 均为 28px，WMS Pager 继续 31px。
- Baseline candidates：Standard List=Page02（Page04 为待修查询 fit 的结构共参照）；Detail=Page03；CRUD=Page05；Monitoring exception=Page06。Page01 `LAST_TO_CHANGE=True`。
- Static risks：Page04/Page07 查询区在 1366px 有压缩/裁切风险；Page05 三个 Grid 和 Page06 recent Grid 为 absolute top/bottom Host 且未应用 height-auto modifier。Page01/02/04 的已验证 modifier 仍在。
- Alignment：P01=B、P02=B、P03=C、P04=C、P05=C、P06=C、P07=C。Debt register：Foundation gap=8、page-local=8、business exception=4、runtime risk=5；HIGH=4、MEDIUM=14、LOW=8。
- CSS audit：`IMPORTANT_RULE_COUNT=6`（2 justified、3 suspicious、1 legacy）；inline style=4；custom button-style selectors=12；font-size variants=9；radius variants=3；actual Drawer width variants=1；Modal size variants=2；heavy non-Learun shadow=0。Page01/Page07 `AUDIT_DIFF_VISUAL_REUSE=True`。
- Runtime：新增只读 Console Probe，测量 iframe/page/search/Grid/Pager/Drawer geometry 与 overflow，不 click/query/POST/自动开窗。`VISUAL_STATIC_AUDIT_PASS` 不等于 Runtime PASS；1366×768 和 1920×1080 Human Review 仍是 Gate。
- Verification：Visual audit/probe Node syntax PASS；7 pages、5 references、26 debt items 与 risk-page assertions PASS；read-only probe invariant PASS；UTF-8 BOM、CRLF、mojibake=0、`git diff --check` PASS；Learun Web Debug build PASS（0 error，2 个既有 obj cache 权限 warning）。本轮无 backend 变更，故未重复运行 backend build/test。
- Files：新增 `agents/wms-visual-audit.js`、`agents/wms-visual-runtime-probe.js`、`docs/frontend/23_WMS_Seven_Pages_Visual_Audit.md`；本 handoff 仅追加交接。Backend/database/proxy/Pager/Drawer Foundation/jfGrid 均未修改。Git commits：none。

## 2026-08-25 PDA Inventory Lookup Backend Alignment

- 实现范围：新增只读 `GET /api/wms/inventory/lookup?warehouseCode={warehouseCode}&code={code}`，支持 Location、OUTER_BOX、INNER_PACK、HUCode 与现有 active HU Identifier；业务失败显式返回 HTTP/API 400。
- EffectiveLocation：新增统一 resolver，供 PDA Location 清单与 HU location 查询复用 SELF → 最近 active parent 规则；现有 HU location 继续同时返回 Direct/Effective 字段，并向后兼容增加 `LocationSourceHuType/LocationSource`。LocationSource 支持 SELF、PARENT_BOX、PARENT_PALLET。
- LOCATION：按有效库位查 ACTIVE OUTER_BOX，覆盖箱无直接库位、父 PALLET 提供目标货架的场景；active Relation/INNER_PACK/Item，Hold 可见，SHIPPED 排除，每箱一次，Qty/PackCount/YYWW WeekDisplay 聚合，BoxCode 升序。
- HU：OUTER_BOX 返回当前 pack/item 明细；INNER_PACK 一致维度 Item 汇总 Qty，维度冲突返回稳定 `INNER_PACK_ITEM_CONFLICT:` Message；active 父箱与拆包状态均支持。非 ACTIVE HU 可查，SHIPPED 不作为物理库位。
- 数据库/API：Repository 使用 ADO.NET typed SqlParameter；只读 WMS 表，不访问 MES，不写 OperationLog；无 schema、migration、存储过程或索引变更。未执行 migrations 010–014，未执行 WMS_DEV DML/DDL。
- Files：新增 Inventory lookup DTO、Repository/Service 接口与实现、EffectiveLocationResolver、Nancy Module；更新 HU location DTO/Service、两个项目编译清单、控制台测试、API 文档与本 handoff。
- Verification：WMS-HU-Model、WMS Host、Tests Debug build 均 PASS；控制台 69/69 tests PASS；新增文件静态扫描无 `dynamic`、`AddWithValue`、DML 或 OperationLog。HTTP `localhost:52602` Runtime GET：health 200；H201 LOCATION 200；真实外箱 `B-F-260817114028RRSF-02` 200；由其返回的真实内包 `I-F-260817114028RRSF-02` 200；`INBOUND-PENDING` LOCATION 200 且包含 `B1951228B`，该箱单查返回 `LocationSource=PARENT_PALLET`；现有 `/hus/B1951228B/location` 继续返回 direct null + effective INBOUND-PENDING，并含新增来源字段；SHIPPED Location 返回空 Items；PALLET 与跨仓库分别返回预期 HTTP/API 400。文档示例 `712947`、`PK2608250001` 在当前数据中为 400 not found。HTTPS 44375 因本机会话 SSPI/Schannel 凭据失败未测；active Identifier 样本直连 SELECT 同样因 SSPI context 失败，故 Identifier Runtime 尚未验证。
- Deviations/risks：文档建议复用现有 Dapper，但按项目强制规则，新 Repository 使用 Plain ADO.NET typed parameters；全局 ApiResult 未增加 ErrorCode，防御性冲突码保留在 Message 前缀。现有脏工作区修改均保留。
- 推荐 Postman 顺序：Location `H201` → OUTER_BOX `712947` → INNER_PACK `PK...` → active Identifier → PALLET unsupported → foreign warehouse → not found → 受控冲突数据（如有）。全部为 GET。
- Git commits：`1d4a382 feat: add PDA inventory lookup`。

## 2026-08-25 Page07 AuditLog Runtime Layout Fix

- Human Runtime 截图证明 Page07 工具栏下移且 Grid 不可见。根因是 `lr-layout-wrap-notitle` 的工具栏槽位与页面级 `wms-audit-body top:62px + height:100%` 重复位移，造成 Body 溢出和 Grid 可用高度不可靠。
- Page07 Layout 改为纵向 Flex：Toolbar 固定 62px 置顶，Body `flex:1 1 auto` 且 `min-height:0`，Pane 保持 `top:0/bottom:8px`，Grid/Pager/Drawer Foundation 与 jfGrid 均未修改。
- 后续 Human Review 将四个日志 Tab 要求置于最右侧：Toolbar DOM 调整为筛选条件 → 查询/重置 → Tab，Tab 组使用 `margin-left:auto` 右对齐；Tab click 与查询逻辑不变。
- 更新 Pager integration Harness 的 Page07 高度合同。Page01/02/04/07 Pager integration、Page01/02/04/07 Drawer integration、Batch D D601–D732 全部 PASS；CSS UTF-8 BOM + CRLF、mojibake=0；Learun Web Debug build PASS（0 warning/0 error）。Human Runtime 需刷新缓存后确认工具栏、Grid、Pager 与左右边界。
- Files：Learun `Areas/WMS/Views/AuditLog/Index.css`；Model repo `agents/wms-pager-integration-harness.js` 与本 handoff。无 backend/API/database/schema 变更。Git commits：none。

## 2026-08-25 Frontend Phase 1D Batch D - Page06 / Page07 Business Readability

- 状态：`PHASE_1D_BATCH_D_IMPLEMENTED=True`，代码、D601–D732 Harness 与静态术语复审完成，等待 Human Runtime。Page05 C2 Runtime pending 不阻断本批。
- Page06：接入 Wms.Terms monitoringStatus；WMS服务/WMS数据库/MES数据库与真实业务指标中文化；Recent Activity 移除 SourceEntityId/RequestId 主列；不可观测与 0 分离；无成功率指标，仍仅手动刷新。
- Page07：Operation/Move/Hold/Capabilities Grid 与 Filter 业务化，Terms 映射主显示，raw code 降为次级。Operation Drawer 为业务摘要 → 周码结果 → 追溯信息 → 默认折叠技术详情；DetailJson 完整保留并可复制。
- WeekCode：新增 page-local `AuditLogBusiness.js`安全解析单对象/多 Item 数组，复用 AuditDiffCell 显示周期码 Before→After；无效 JSON 安全 fallback。Request Trace 同 Drawer context 切换，只显示已持久化更新数与精确关联的 release fact，不声称总处理/无变化/失败数。
- 持久化调查：`UPDATED=True`、`UNCHANGED=False`、`FAILED=False`、`REQUEST_SUMMARY=False`。只有 UPDATED 写 WEEKCODE_RECALCULATE OperationLog + 全 Item DetailJson；历史审计不完整说明已显示。
- 术语复审：Page06 English=0/raw enum=1/readability=A candidate；Page07 English=2（日期 placeholder）/raw enum secondary=4/readability=B candidate。
- Scope：无 backend/API/schema/database/audit writer/HU/WeekCode/inbound/outbound 业务变更；无新 audit table/tab；Pager/Drawer Foundation/jfGrid/Page01/Page05 未修改。
- Verification：Batch D D601–D732、Terms T01–T29、Page01 Business/Recalculate/Modal、Page02–04 Batch B、Page05 C201–C228 + SV201–SV207、Pager P01–P10 + Page01/02/04/07 integration、Drawer D01–D12 + Page01/02/04/07 integration 全部 PASS；backend console regression 65/65 PASS；WMS-HU-Model.Tests 与 WMS host Debug build PASS；Learun Web Debug build 在显式 `SolutionDir=D:\com.delton.gz\`、`RestorePackages=false` 下 PASS（0 error，2 个既有 obj cache 权限 warning）；modified JS syntax、static scope、UTF-8 BOM、CRLF、mojibake=0 PASS。未执行真实 Runtime POST 或数据库写入；Page06/Page07 Human Runtime 仍待执行。
- Files：Learun 新增 AuditLogBusiness.js，修改 Page06/Page07 View/JS/CSS 及 web csproj；Model repo 新增 Batch D harness/22 文档，更新 terminology audit、17/18 与本 handoff。Git commits：none。

## 2026-08-25 Frontend Phase 1D Batch C2 - Page05 Controlled Maintenance

- 状态：`PHASE_1D_BATCH_C2_IN_PROGRESS=True`，受控实现及自动回归已完成，等待 Human Runtime；`C1_5_EXECUTION_OWNER=HUMAN`、`DATABASE_SEED_EXECUTED_BY_CODEX=False`。
- Options：复用现有 `GET /api/wms/master-data/options`，Warehouse/WMS_Zone 来自 DB，LocationTypes 由 server-owned allowlist 返回 DOUBLE_RACK、TRIPLE_TIER_REMAINDER_RACK、FLOOR_STORAGE；VIRTUAL 不在主管选项。未新增 endpoint/table/schema/migration。
- Backend：LocationCode create 改为 Trim-only，update 仍以 route/current Id 保持不可改；physical create/update 仅允许 STORAGE + 三类 approved type；create 不写 CapacityQty，update 保留 DB Capacity。系统货位拒绝 update/status；物理货位下线先实时 CountCurrentHus，状态 UPDATE 本身再用 `NOT EXISTS WMS_HU_CurrentLocation` 关闭 count-to-update 竞态窗口。有库存时提示先移货；下线仅置 INACTIVE，不删除、不自动移货、无强制入口，原记录可重新启用。
- Page05：Warehouse/Type filter 与 Form selector 使用 server response，Zone 使用 DB ZoneName 且 STORAGE locked。系统货位 Edit 变 View，Modal 全字段只读并提示联系 IT；物理货位操作按状态显示“下线/启用”，occupied client check 显示 X 个物流单元且仅作 UX；无 Delete。
- Governance/target audit：Code series 由 IT 维护，主管无 Zone/LocationType code 创建入口。Putaway、relocation、unpack placement 及 ReturnToStock 均已有 ACTIVE target guard，`INACTIVE_LOCATION_TARGET_GUARD_GAP=False`。
- Terms：新增 TRIPLE_TIER_REMAINDER_RACK→三层尾数货架、FLOOR_STORAGE→平仓库位、VIRTUAL→虚拟货位；Terms 仍 display-only，unknown raw fallback，不作为 allowed-value source。
- Audit：`MASTER_DATA_AUDIT_GAP=True`、`NEW_MASTER_DATA_AUDIT_TABLE=False`；未复用 HU OperationLog。
- Verification：Final Alignment 修订后 Page05 formal C201–C225 + compatibility C226–C228、Server SV201–SV207、Terms T01–T29、Page01 Business/Recalculate/Modal、Page02–04 Terms rollout、Pager P01–P10 + Page integration、Drawer D01–D12 + Page integration 全部 PASS；C# console regression 65/65 PASS；WMS-HU-Model.Tests 与 WMS host Debug build PASS（0 warning/0 error）；Learun Web Debug build PASS（0 error，2 个既有 dependency obj cache 权限 warning）；modified JS syntax、UTF-8 BOM、CRLF、mojibake/static scope PASS。Solution aggregate wrapper 仍出现既有“失败但0 error”，不用该 wrapper 冒充项目构建结果。Human Runtime 仍待执行。
- Files：新增两个 C2 harness 与 `21_Page05_Controlled_Maintenance_Implementation.md`；修改 LocationService、MasterDataMaintenanceService、tests、Page05 Index/Form、Wms.Terms、02 plan 和本 handoff。Git commits：none。

## 2026-08-24 Page03 Location Name Primary Display Fix

- Root cause：`WMS_Location.LocationName` 已维护（例如 `INBOUND-PENDING → 待上架`），但 `HuLocationDto` 和 `HuLocationRepository` 只返回 LocationCode，导致 Page03 将技术代码作为“有效货位”主显示。
- Fix：现有 direct/effective location SELECT 增加 `DirectLocationName` / `EffectiveLocationName`；Page03 Header 和 Overview 名称优先，代码保留为独立“直接货位代码 / 有效货位代码”。名称为空时安全 fallback 到代码。
- Semantics：直接货位、有效货位和 ACTIVE-parent inheritance 逻辑不变；无 schema/migration/database write，无额外前端 API 请求。
- Verification：Page03 JS syntax PASS；Batch B harness B01–B30 PASS；WMS-HU-Model solution Debug build PASS；现有控制台测试 64/64 PASS；Learun Web Debug build PASS（0 errors，2 个既有 dependency obj cache 权限 warning）。Human Runtime 应确认截图场景显示“待上架”，同时“有效货位代码”仍为 `INBOUND-PENDING`。

## 2026-08-25 Frontend Phase 1D Batch C1 - Human Review Rebaseline

- 状态：`W_RANGE_LABEL_PATTERN_CONFIRMED=True`、`SUB_LOCATION_AUTO_GENERATION=False`、三类 physical LocationType Human Review 通过。旧 provisional candidate 基线已作废并重新生成；`DATABASE_WRITE_EXECUTED=False`、`MASTER_DATA_IMPORT_EXECUTED=False`。
- Workbook：原文件只读重解析，SHA256 仍为 `A8D86E10C9EB100E280DC0342154F2F0DFB2F405AB557C02CA459B649D47AAC0`，4 Sheets、35 source groups、duplicate=0、invalid=0。`01W `–`32W` Trim 后按 step 1 展开为 32 个真实 LocationCode，未生成 `01W-1` 等子货位。
- Candidate：distinct 386；LocationType counts 为 DOUBLE_RACK=101、TRIPLE_TIER_REMAINDER_RACK=101、FLOOR_STORAGE=184。全部 physical ZoneCode=STORAGE；物理类型未混入 Zone；6 个 VIRTUAL 系统货位不在 Excel/Apply ownership。
- Read-only DB Diff：重新以 `ApplicationIntent=ReadOnly` 和 SELECT-only 查询 FG01 当前10条 Location。Excel-only=382、matched same=4、matched different=0、DB-only physical=0、system-only=6、in-use change=0；proposed insert=382，update/delete/LocationCode/system update 均0。
- Capacity：`EXCEL_UNIT_SEMANTICS=CAPACITY`、`CAPACITY_IS_LOCATION_COUNT=False`、`CAPACITY_CREATES_LOCATION=False`；unit 仍 pending。Candidate 保留 group quantity/per-location evidence，但 `CapacityQtyProposal=null`；Apply Draft 完全省略 CapacityQty INSERT column，不存在 `@ApplyCapacity`。
- Type governance：未来 Backend supervisor allowlist 可含 DOUBLE_RACK、TRIPLE_TIER_REMAINDER_RACK、FLOOR_STORAGE，必须排除 VIRTUAL；Wms.Terms 后续仅增加四类 display mapping，不作为 allowed-value source。本轮未改 Backend/API/Page05 production UI/Wms.Terms。
- SQL：Diff 仍 SELECT-only；Apply Draft 含382个三类 physical candidates，`@Apply=0`、XACT_ABORT、transaction、NOT EXISTS；本轮未执行该草案、DML、DDL 或 migration。等待新 Diff Human Review、backup/snapshot 与显式 C1.5 执行授权。
- Verification：C101–C125 Harness PASS（含 W-range、type counts、VIRTUAL exclusion、Capacity write deferred）；generator/harness syntax PASS；SQL safety/static checks PASS；Workbook hash unchanged；WMS-HU-Model 与 WMS-HU-Model.Tests project builds PASS（0 warning/0 error），console regression 64/64 PASS。Solution aggregate 命令在已成功生成 library 后仍返回既有的“失败、0 warning/0 error”空诊断，因此不将该 wrapper 结果冒充 PASS；本轮未修改任何 C# production source。
- Files：更新 generator/harness、Proposal、C2 plan、JSON/CSV Candidate、Diff/Apply Draft SQL 与本 handoff。Git commits：none。

## 2026-08-24 Frontend Phase 1D Batch C1 - Initial Investigation (Superseded by 2026-08-25 Rebaseline)

- 状态：此节保留 initial investigation trace，计数与待审项已由上方 2026-08-25 Human Review Rebaseline 正式替代，不得再作为最终 Candidate/Diff。Primary source 为只读 `D:\Desktop\PCB成品仓开发\货位号.xlsx`，SHA256 `A8D86E10C9EB100E280DC0342154F2F0DFB2F405AB557C02CA459B649D47AAC0`；分析前后 hash 一致，`EXCEL_WRITE_EXECUTED=False`。
- Initial investigation established the read-only Workbook/DB method, schema evidence, ownership split, Proposal/Diff/Apply Draft shape and C2 controlled-selector direction. Its provisional range/type classifications and counts are intentionally omitted here because they are no longer valid after Human Review.
- The authoritative counts, type mappings, range decision, Capacity gate and verification result are exclusively the 2026-08-25 rebaseline above.
- Git commits：none；工作区包含前序未提交改动，本轮未创建提交。

## 2026-08-24 Frontend Phase 1D Implementation Batch B - Business Terminology Rollout

- 状态：`WMS_TERMS_FOUNDATION_HUMAN_REVIEW_PASS=True`、`WMS_TERMS_FOUNDATION_V1_CLOSED=True`、`PHASE_1D_BATCH_B_IMPLEMENTED=True`。Page02/Page03/Page04 已 rollout；Page01 仅回归，Page05/Page06/Page07 未批量修改。
- Page02：按固定 OUTER_BOX grain 使用“外箱库存 / 箱码”，复用 Terms 的包装层级、生命周期、库存、冻结、冻结原因、货位类型；删除本地 `stockLabel`。Drawer 业务信息优先，HU 内部标识置于最后技术追溯；Pager、有效货位继承、Drawer context 和 Page03 导航合同不变。
- Page03：Generic HU 改为“物流单元”，树和概览通过 Terms 展示托盘/外箱/内包与有效/已关闭；库存、移动、操作、冻结履历均以中文业务事件优先。直接/有效货位保持分离；JsonViewer 保留并标注“原始审计明细（JSON）”。
- Page04：定位为“成品入库记录”，入库状态复用 Terms；删除本地 status dictionary，保留 tone-only formatter。业务列前置，来源记录/执行编号降为尾部灰色次级信息；Drawer 分为概要、异常、来源与技术追溯，内部 Receipt ID 未伪装成业务单号。
- Anti-duplication：Page02/03/04 的 Terms 覆盖域未发现重复 mapper，`DUPLICATE_TERMINOLOGY_MAPPER_COUNT=0`；unknown enum 保持 raw fallback，不参与反向业务判断。
- Verification：Batch B B01–B29、Foundation Terms T01–T25、Pager P01–P10 + page integration、Drawer D01–D12 + page integration、Page01 Business Readability B01–B12、Recalculate UX R01–R17、Parent Modal integration、modified JS syntax 全部 PASS。Learun Web Debug build PASS（0 errors；2 个 dependency assembly-reference cache 既有权限 warning，不影响 DLL 产出）。UTF-8 BOM、CRLF、mojibake=0；Human Runtime 仍需按 Page02→Page03→Page04 顺序验收真实数据和交互。
- Files：Learun 修改 Page02/Page03/Page04 的 View/JS 及 Page03/Page04 必要小幅 CSS；Model repo 新增 `agents/wms-terms-pages-batch-b-harness.js`、`docs/frontend/20_WMS_Terms_Batch_B_Page_Rollout.md`，更新术语矩阵与本 handoff。
- Schema/API：无 schema、migration、API contract、backend、proxy、database、Pager Foundation、Drawer Foundation、jfGrid、业务规则变更；未执行 Runtime POST、DML、DDL 或 migration。
- Git commits：none；工作区包含前序未提交改动，本轮未创建提交。

## 2026-08-24 Frontend Phase 1D Implementation Batch A - Wms.Terms V1

- 状态：`PHASE_1D_TERMINOLOGY_AUDIT_COMPLETE=True`、`WMS_TERMS_FOUNDATION_V1_IMPLEMENTED=True`。Human-approved HU/Hold/DateCode/PCS/Lifecycle/Operation/Move/Page06/07 terminology decisions 已固化；等待 Terms API Human Review 后进入 Batch B。
- Foundation：新增独立 ES5 `wms-terms.js`，注册 `Wms.Terms`。所有 mapper 返回 `{ text, raw, known }`；unknown non-empty 原值 fallback，空值为 `—`，原始 input 精确保留；无 reverse mapping。
- Mappings：HUType、Lifecycle、backend-confirmed Stock AVAILABLE、OperationType、独立 MoveType、Hold status/reason、Week rule source、risk、week audit、recalculate result、monitoring、audit capability、inbound、Master ACTIVE/INACTIVE、confirmed DOUBLE_RACK。Page02 的 RESERVED/HOLD 未提升为 global known StockStatus。
- Labels：物流单元、包装层级、周期码/周期码（DateCode）、执行编号、原始审计明细（JSON）、冻结状态/原因/时间、解除冻结时间、数量、单位、PCS。context-specific HUCode label 仍由页面决定。
- Load order：真实 Foundation 没有 WMS global loader，各 View 使用 `AppendJsFile`。合同锁定 `wms-core.js → wms-terms.js → consuming page script`；Batch A 仅加入 web csproj，不批量修改 Page01~Page07 loader。
- Verification：`agents/wms-terms-harness.js` T01–T25 PASS，覆盖指定 T01–T20、全类别、labels、Operation/Move 分离、display-only static scan、ES5 和真实 core→terms load。JS syntax PASS；Learun Web Debug build PASS（0 errors，84 个既有 warnings）；BOM/CRLF、mojibake 与 Page01~Page07 no-bulk-change static scope check PASS。
- Files：Learun 新增 `Areas/WMS/Content/Foundation/Scripts/wms-terms.js` 并更新 `Learun.Application.Web.csproj`；Model repo 新增 harness、`19_WMS_Terms_Foundation_Contract.md`，更新 17/18 和本 handoff。
- Scope：Page01~Page07 无术语批量替换；无 API/backend/proxy/database/schema/pager/drawer/business rule 变更；无 i18n framework 或 terminology table；未执行 Runtime POST、DML、DDL 或 migration。
- Git commits：none；工作区包含前序未提交改动，本轮未创建提交。

## 2026-08-24 Frontend Phase 1D - Business Readability & Terminology Classification Audit

- 状态：`PAGE01_RECALCULATE_UX_HUMAN_PASS=True`、`PAGE01_UI_DEBT_CLOSED=True`、`PHASE_1D_TERMINOLOGY_AUDIT_IN_PROGRESS=True`。本轮 Audit/Classification 产物已完成，等待 Human Review Terminology Matrix；未进入 UI 实施。
- 范围：只读扫描 Page01~Page07 的 `.cshtml/.js/.css`、前端 adapters/Foundation display components，以及后端真实 model/repository/service enum-like values。未修改 Page01~Page07 生产 UI 源码。
- 结论：正式采用 `HU=物流单元`，已知层级 `PALLET=托盘`、`OUTER_BOX=外箱`、`INNER_PACK=内包`，业务标签 `HU Type=包装层级`。Page01/Page02/Page05/Page06 为 B，Page03/Page04 为 C，Page07 为 D；Page07 是最高优先级 technical-first 页面。
- 统计：静态 authored-visible-text 共 605 个 occurrence，其中含中文 446、纯英文 149；人工复核 raw-enum display sites 40、IT secondary shown as primary 21、raw technical shown as primary 21。英文 Grid/Form/Detail label sites 分别 30/11/87。统计口径和逐页明细由 `agents/wms-terminology-audit.js` 固化。
- Backend evidence：确认 HUType 3 值、Lifecycle ACTIVE/CLOSED、StockStatus AVAILABLE、实际 OperationType/MoveType 写入/校验集合、HoldType/ReasonCode、WeekCode anomaly/risk、Monitoring status；明确区分 Page02 前端预留 `RESERVED/HOLD` mapper 与当前生产 repository 未确认值。
- Existing mappers：Page01 audit/risk/result 与独立 message mapper、Page02 stock、Page04 inbound status、Page05 active status、Page06 health status、Page07 capability/Hold status。Foundation `StatusTag` 只负责视觉呈现，目前无全局业务术语 mapper。
- 设计建议：Human Review 后新增 display-only `Wms.Terms`，返回 `{ text, raw, known }`，未知值原样 fallback；禁止承载 pickability、Hold enforcement、WeekCode calculation 或任何业务逻辑。
- Human Review：Hold 中文、DateCode 中文、PCS 展示、OperationType/MoveType 中文、Lifecycle ACTIVE 上下文、LocationType、RETURN wording、Page06/07 raw-code 层级。
- Files added：`docs/frontend/17_WMS_Business_Terminology_Matrix.md`、`docs/frontend/18_WMS_Information_Layering_Guide.md`、`agents/wms-terminology-audit.js`。本文件仅追加 handoff；无 schema/API/backend/proxy/database/pager/drawer 变更。
- Verification：terminology harness 与 Node syntax PASS；新文档/脚本及 handoff 均为 UTF-8 BOM + CRLF，mojibake 0，static scope check PASS。纯文档/只读 harness 阶段未执行 Learun/backend build，也未执行 Runtime POST、DML、DDL 或 migration。
- Git commits：none；工作区含前序未提交改动，本轮未创建提交。

## 2026-08-24 Page01 Recalculate Result Modal Business Readability Polish V1

- 状态：`PAGE01_RECALCULATE_BUSINESS_READABILITY_POLISH_IN_PROGRESS=True`、代码和受控验证已完成，等待 Human Runtime；既有 Grid → Preview → Confirm → Modal → Close → Grid refresh 流程不重构。
- Business first：顶部 scope 拆为厂别/物料/版本/MES周期格式；删除“执行后返回”。SUCCESS/PARTIAL/UNCHANGED/FAILED 使用业务标题、完整说明、WMS 数据影响和建议动作，ErrorCode 不再作为第一屏主信息。
- Error mapper：按真实后端扫描只映射执行前 `WEEK_RULE_NOT_FOUND`、`WEEK_RULE_AMBIGUOUS`，并为真实箱级 `WEEK_RULE_MISSING`、`MISSING_DATECODE`、`INVALID_DATECODE`、`WEEK_ANCHOR_REQUIRED`、`UNSUPPORTED_WEEK_RULE`、`INVALID_WEEK`、`PERSISTENCE_FAILED`、`PARSE_FAILED` 提供业务说明。未修改 backend message。
- Conservative outcome：network/session/proxy unknown 显示“无法确认重新计算结果”；generic server error 显示“重新计算发生系统异常”。两者都不承诺未写数据，要求用执行编号查日志并避免立即重复提交。未知 business error 保留后端 message 且不虚构 no-write。
- Technical second：RequestId 只在默认折叠的技术详情出现一次并可复制；同区显示 ErrorCode、MES Rule Source raw value 和 Backend Message，无 raw JSON、stack/connection/auth/cookie。
- Result：保留 compact summary、Before → After、Hold release/preservation、box failure 与 resolved-row removal。无明细的 pre-execution failure 不创建空 Result Grid 或强制大高度。
- Close/refresh：隐藏本 Modal 的重复 secondary button；SUCCESS/PARTIAL 显示“关闭并刷新列表”并在关闭后 refresh，UNCHANGED/FAILED/unknown 显示“关闭”且不 refresh；无 checkbox、无 Retry。
- Scope：只修改 Page01 Result Modal、Page01 parent callback、独立 presentation mapper、Page01 harness/doc/handoff；未修改 Foundation、Pager、Drawer、jfGrid、API/DTO、backend、proxy、database。
- Safety：Codex 未执行真实 Recalculate POST、数据库写入或 migration。
- Verification：Business readability B01–B12 PASS；既有 R01–R17 PASS；Parent/Modal regression PASS；Pager/Page integration 与 Drawer/Page integration regression PASS；3 个 Page01 JS syntax PASS；Learun Web Debug build PASS（0 error，既有 warning）；UTF-8 BOM + CRLF、mojibake/sensitive field/static audit PASS。
- Git commits：none；工作区含前序未提交改动，本轮未创建提交。

## 2026-08-24 Frontend Foundation Debt Closure Phase 1C - Page01 Recalculate UX

- 状态：`PAGE01_RECALCULATE_UX_V2_IMPLEMENTED=True`；Page01 已迁移为 `Grid → Preview Drawer → Confirm → Result Modal → Grid Refresh`。Preview Drawer 只读，执行与最终结果由独立 iframe Modal 承担；Human Runtime Gate 尚未执行。
- Preview：展示基础信息、当前/候选周码、复用 `Wms.AuditDiffCell` 的 diff、异常原因、Plant + Material + Revision scope 与预计处理结果。前端不解析或猜测周码；candidate 为空不阻止具备完整 scope 的 Confirm。
- Execution：Confirm 明确执行时 MES source of truth。`Wms.Modal`/`learun.layerForm` iframe 自动且只 POST 一次，camelCase 请求只含 scope/operator/requestId；无 RuleValue/candidate/week 值，无 Retry，无全屏 loading。
- Result：展示 scope/rule/可复制 RequestId、完整 counts、UPDATED/UNCHANGED/FAILED/partial success、真实 old/new DTO Before → After、WEEKCODE_UNRESOLVED release 与非周码 Hold preservation、后端错误和 resolved-row removal 说明。
- Lifecycle：Modal 先 render，再以 iframe element callback 标记执行完成；用户关闭已完成 Modal 时父页面调用一次 `grid.refresh()`，保留 filters/page，Preview Drawer 保持关闭。business/network error 或执行中关闭不刷新 Grid。
- Removed：父页面 direct POST、before snapshot、Result Drawer、Last Result、`RECALCULATE_RESULT` context 和 post-refresh anomaly trace；`PAGE01_RESULT_DRAWER_LIFECYCLE_CONFLICT=False`、`PAGE01_RECALCULATE_MODAL_MIGRATION_PENDING=False`。
- Files：Learun Page01 `Index.js/css`、`WeekCodeAuditApi.js`、新增 `RecalculateResult.cshtml/js/css`、Controller view action；Model repo 新增两个 harness、更新 Page01 doc、增加 `16_Page01_Recalculate_UX_Contract.md` 和本 handoff。
- Schema/API：无 backend business、DTO、route API、proxy、database、schema 或 migration 变更；Controller 只增加 iframe GET View action。未执行真实 Recalculate POST。
- Verification：R01–R17 harness PASS；Parent/Modal integration harness PASS；Page01/API/Modal JS syntax PASS；Learun Web Debug build PASS（0 error，既有 warning）；WMS-HU-Model solution Debug build PASS（0 warning/0 error）；backend console regression 64 tests PASS；UTF-8 BOM + CRLF 与 static audit PASS、mojibake 0。
- Runtime/Human：需验证完整 candidate、RULE_MISSING 400、UPDATED/UNCHANGED/FAILED/partial success、Hold release/preservation、Grid refresh、Modal persistence/close、resolved row disappearance。
- 推荐测试顺序：登录 Page01 → query → Preview/diff/scope → cancel Confirm（确认无 POST）→ 受控真实 scope Confirm → Modal result/request copy → 关闭 Modal → 确认父 Grid refresh → Page07 用 RequestId 追查。
- Git commits：none；工作区含前序未提交改动，本轮未创建提交。

## 2026-08-24 Frontend Foundation Debt Closure Phase 1A - Server Pager

- 状态：`WMS_PAGER_FOUNDATION_V1_COMPLETE=True`、`WMS_FOUNDATION_PAGER_DEBT_CLOSED=True`；Page 01/02/04/07 Pager 与 absolute-fill modifier 已完成 Human Runtime 验证。
- Root cause：Human Runtime 已证明 Page 02 absolute top/right/bottom/left Grid host 同时继承 Foundation `height:100%`，导致 host 与 Pager 超出 iframe。正式修复新增共享 `.wms-grid-absolute-fill` modifier，使 absolute-fill host 使用 `width/height:auto !important`；Page 01/02/04 应用，Page 07 保持原布局。
- Visual/data：视觉对齐 Learun “接口上传执行日志” native jfGrid pager；数据仍为 `Wms.Api -> /api/wms/* -> external async datasource -> Wms.Grid -> jfGrid`，外部数据源 `isPage:false`。未修改 `jfgrid.js/css`，未启用 jfGrid native AJAX。
- Behavior：零数据固定摘要；右侧首页/上一页/最多 10 数字页/下一页/尾页/总页数/Jump；边界和 pending 禁用；Jump 只接受有效整数且 Enter 等价点击；Query/Reset page 1；`refresh()` 保留当前页；越界只纠正一次；stale response guard 保留。
- Integration：Page 01/02/04/07 通过现有 `Wms.Grid.init(loadData)` 自动复用；Page 03/05/06 未接入。Page 01 Recalculate 已使用 `grid.refresh()`，保持当前 filters/page。
- Files：Learun `Areas/WMS/Content/Foundation/Scripts/wms-adapters.js`、`Styles/wms-components.css`；Model repo `agents/wms-pager-harness.js`、`docs/frontend/08_WMS_Foundation_Implementation.md`、`docs/frontend/14_WMS_Server_Pager_Contract.md`、本 handoff。
- Schema/API：无 schema、migration、business API、Proxy、Database、backend paging contract 变更；无 DML/DDL。
- Verification：P01-P10 + DOM/layout controlled harness PASS；Foundation JS syntax PASS；Page 01/02/04/07 均为 pageSize 50 + camelCase response adapter + external loadData；Learun Debug build PASS（0 error，既有 warning）；修改的 JS/CSS 为 UTF-8 BOM + CRLF，mojibake 0。
- Runtime：应用内 Browser 初始化冲突，未声明真实 UI click/visual 通过。Human 先测 Page 01：Query、Pager visible、摘要、1→2→1、尾页、首页、Jump、筛选回 page 1；通过后依次 Page 02、04、07。
- Risks：受控 harness 能证明实际 Pager helper、DOM/CSS/source lifecycle contract，但不能替代登录态浏览器的像素、窄宽横向滚动和真实 Network payload 复核。
- Git commits：none；工作区含前序页面未提交改动，本轮未创建提交。

## 2026-08-24 Page 07 Audit Log Center V1

- 状态：`PAGE_06_MONITORING_COMPLETE=True`、`PAGE_07_AUDIT_CENTER_IN_PROGRESS=True`；实现、构建、自动化与真实 GET-only Runtime 核验完成，等待 Human Visual Review。
- Sources：真实事实表为 `WMS_HU_OperationLog`、`WMS_HU_MoveLog`、`WMS_HU_HoldLog`。Page 03 的三个 API 仅支持单 HU Top N，不能复用为全局筛选/分页；保留其合同并复用字段语义、`Wms.JsonViewer`、`Wms.Timeline`、`Wms.StatusTag`。
- API：新增只读 `/api/wms/audit/operations`、`moves`、`holds`、`request-trace?requestId=`、`capabilities`。默认 7 天、最大 90 天、pageSize 默认 50/最大 200；服务端筛选/分页、事件时间 DESC + Id DESC。真实 Release RequestId 含 `:`，Trace 使用 query 参数避免 ASP.NET path validation 拒绝。
- Trace：Operation/Move/Hold 三类 exact RequestId bounded read，返回原始行并按时间升序合并显式 OPERATION/MOVE/HOLD/RELEASE events；未知 RequestId 返回空事实。Detail 与 Trace 共用一个 Drawer，不嵌套。
- Capability：`MOVE_AUDIT_AVAILABLE=True`、`OPERATION_AUDIT_AVAILABLE=True`、`HOLD_AUDIT_AVAILABLE=True`；`MASTER_DATA_AUDIT_AVAILABLE=False`、`PRE_RECEIPT_INBOUND_FAILURE_AUDIT_AVAILABLE=False`。Capability 明确 NOT_OBSERVABLE 不等于 0；未新增 audit table。
- Runtime GET：90 天 Operation 6412，page1/2 各 10、0 overlap、稳定倒序；B1951228B 3；WeekCode Recalculate 44、RECEIVE 56、PUTAWAY 3。Move RECEIVE 57。Hold 340（Active 298、Released 42，WeekCode reason 339）。Trace 样本分别为 Operation 21、Move 1、Release 1、unknown 0；未发现真实跨表同 RequestId，自动化 fake 覆盖多源合并。
- Sensitive detail：抽查最新 200 条 Operation DetailJson，未命中 password/token/authorization/cookie/connectionString/stackTrace，`SENSITIVE_AUDIT_DETAIL_FOUND=False`；结果为样本结论。Viewer 使用 text rendering/invalid JSON fallback。
- Frontend：默认 Operation；Move/Hold/Capability lazy-load；公共与 Tab-specific filters、server pager、dense grids、只读 Detail Drawer、Request Trace Timeline、HU→Page03 frameTab。Page load/query/reset/render/page change 均关闭 Drawer，真实 cell mousedown 才允许 row click 打开。
- Verification：WMS Debug build PASS；64 tests PASS；Learun Debug build PASS（0 error，既有 warning）；2 JS syntax PASS；受控 Page07 lifecycle contract PASS（临时 harness 已删除）；前端 UTF-8 BOM + CRLF。应用内 Browser 初始化冲突，不能宣称 UI click/visual 通过。
- Safety：无 schema/table/audit table/migration 变更；未执行 POST/DML/DDL；新 Repository typed ADO.NET SELECT-only，无 `dynamic`、`AddWithValue`；Learun 无 direct DB。
- Documentation：`docs/frontend/pages/07_Audit_Log_Center_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区包含前序页面未提交改动，本轮未创建提交。

## 2026-08-24 Page 06 System / Interface Runtime Monitoring V1

- 状态：`PAGE_05_MASTER_DATA_IMPLEMENTED=True`、`PAGE_06_MONITORING_IN_PROGRESS=True`；Page 06 实现、构建、自动化和真实 GET-only Runtime 核验完成，等待 Human Visual/Proxy 502 Review。
- Observability：已扫描 WMS、WMS-HU-Model、Learun、health endpoints、Receipt/HU logs、Learun LogEntity/log4net、Nancy diagnostics 和 `Application_Error`。成功 Receipt 不能代表总请求；Receipt 创建前失败没有完整持久化、可查询来源。`INBOUND_REQUEST_TOTAL_OBSERVABLE=False`、`INBOUND_FAILURE_HISTORY_PERSISTED=False`、`INBOUND_FAILURE_HISTORY_QUERYABLE=False`、`SUCCESS_RATE_METRIC_ALLOWED=False`。
- API：新增只读 `GET /api/wms/monitoring/overview` 与 `GET /api/wms/monitoring/activity?days=7|14|30`。固定 WmsDb/MesDb 3 秒最小 `SELECT 1` 探针；依赖失败以 HTTP 200 + `DEGRADED` 返回，消息脱敏。没有任意 connection/sql/table/path/target 参数。
- Metrics：自然日成功 Receipt/Box/Pack/Qty；当前 active Hold Box、Page 01 语义的 WeekCode anomaly、入库后异常 Receipt。Activity 后端按日聚合成功 Receipt 并补零，复用 Page 04 Read Service 返回最近 20 条；不是请求趋势或成功率。
- Frontend：新增 Learun `/WMS/Monitoring/Index`，六个紧凑 `Wms.MetricCard`、Bootstrap tabs、手动刷新、Activity lazy load、现有 ECharts 3.6、最近活动 Grid、当前异常摘要及 Page 01/02/04 `frameTab` 导航。Proxy 502 分支明确显示 WMS 不可用、业务指标不可用而不是 0；无永久 Loading。`NOT_OBSERVABLE` 显示“当前不可追溯”，没有空失败表。
- Runtime GET：overview HTTP 200，WmsDb/MesDb CONNECTED；2026-08-24 自然日成功 Receipt/Box/Pack/Qty 均 0，active Hold Box 298、WeekCode anomaly 308、inbound exception Receipt 34。7-day activity HTTP 200，2026-08-19 为 8/13/13/192，2026-08-20 为 46/1122/3659/230739，recent committed Receipts 20 条。
- Safety：无 schema/table/log table/migration 变更；未执行 POST、DML 或 DDL。新 Repository 为 typed ADO.NET SELECT-only，无 `dynamic`、`AddWithValue`；Learun 无 direct DB、业务 JS 无端口和敏感连接信息。
- Verification：WMS Debug build PASS；58 tests PASS；Learun Debug build PASS（0 error，既有 warning）；Monitoring/Foundation JS syntax PASS；受控 Proxy 502 前端合同 PASS（临时 harness 已删除）；前端 UTF-8 BOM + CRLF。应用内 Browser 初始化报 `Cannot redefine property: process`，因此不宣称 UI click/visual 通过。
- Documentation：`docs/frontend/pages/06_System_Interface_Monitoring_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区包含前序页面未提交改动，本轮未创建提交。

## 2026-08-22 Page 05 WMS Master Data Maintenance V1

- 状态：`PAGE_04_FINISHED_GOODS_INBOUND_COMPLETE=True`、`PAGE_05_MASTER_DATA_IN_PROGRESS=True`；实现、自动化、编译和真实只读 API 核验完成，等待 Human UI/controlled-write Review。
- Ownership：只维护 WMS-owned `WMS_Warehouse`、`WMS_WarehousePlant`、`WMS_Location`；未增加 MES/SAP 主数据 CRUD、schema、table、status dictionary 或 migration。
- Schema contract：自然键分别为 `WarehouseCode`、`WarehouseId + PlantCode`、`WarehouseId + LocationCode`。所有自然键在编辑时不可变；SAP Storage Location 可编辑且允许 NULL。
- API：复用现有 Location model/repository/service/GET/POST create；新增强类型 Warehouse/WarehousePlant read/create/update/enable/disable、Location maintenance query/POST update/enable/disable。Learun 只通过 `/api/wms/*` 和 `Wms.Api`；无 direct DB。
- Rules：Warehouse 停用保护 active Location/current HU/active mapping；新 Mapping/Location 要求 active Warehouse；Location 停用一次聚合检查 Current HU。六个 runtime-confirmed system virtual Location 由 Service allowlist 保护，无物理删除。
- Frontend：单页 Bootstrap tabs，Warehouse 默认加载，Mapping/Location 首次点击 lazy-load；各 Tab 独立 search state。CRUD 使用 jfGrid/Wms.Grid + Wms.Modal/lr-form/lr-validator/lrselect，状态切换使用 Wms.ConfirmDialog；写按钮复用 Learun authorization。Modal 增加失败 release callback，避免保存失败后 pending lock。
- Audit：现有 OperationLog 语义属于 HU，未污染复用；无通用 Master Data audit pattern，`MASTER_DATA_AUDIT_REUSED=False`、`MASTER_DATA_AUDIT_GAP=True`。WarehousePlant 真实表缺少 modify actor/time 字段。
- Runtime GET：FG01 1 个 Warehouse；1010/11C3、1020/12C3、9010/NULL 三个映射；10 个 Location；六个系统虚拟货位；Location current-HU aggregate 59。均为只读证据，未硬编码。
- Safety：未执行 WMS_DEV DML/DDL，未发 create/update/enable/disable 请求；FG01、生产映射和现用 Location 未修改。
- Verification：WMS-HU-Model/WMS solution build PASS（0 warning/0 error）；Learun Debug build PASS（0 error，既有 cache/source warning）；53 tests PASS；4 JS syntax PASS；Page 05 前端与修改的 Foundation/project XML 均为 UTF-8 BOM + CRLF；mojibake、direct port、direct AJAX、dynamic、AddWithValue、DELETE、Drawer、MES MD 引用均为 0。应用内 Browser 初始化冲突，不能宣称 UI click test 通过。
- Documentation：`docs/frontend/pages/05_WMS_Master_Data_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区含前序页面的未提交改动，本轮未创建提交。

## 2026-08-22 Page 04 Finished Goods Inbound Records / Exceptions V1

- 状态：`PAGE_03_HU_DETAIL_HISTORY_COMPLETE=True`、`PAGE_04_FINISHED_GOODS_INBOUND_IN_PROGRESS=True`；实现、编译、自动化和真实只读 API 验证完成，等待 Human UI Review。
- API 调查：原有两个 finished-goods inbound API 均为 POST 写入口；没有 Receipt query/detail GET。新增只读 `GET /api/wms/inbound/finished-goods/receipts/` 与 `GET /api/wms/inbound/finished-goods/receipts/{receiptId}`。
- 失败历史结论：Receipt/HU/Relation/Item/Hold/Move/Operation 在同一 serializable transaction；硬失败整体 rollback，且没有 FGI failure log。因此 `REJECTED_INBOUND_HISTORY_PERSISTED=False`、`REJECTED_INBOUND_HISTORY_QUERYABLE=False`。Tab 明确命名“入库后异常”，只展示成功入库后的 Hold / WeekCode evidence。
- 查询语义：1 行 = 1 `WMS_FinishedGoodsInboundReceipt`；通过现有 `SourceBusinessNo='FGI-'+ReceiptId` 关联 HU。一次 Repository 聚合得到 Pallet/Box/Pack、实际 Item Qty、distinct material、Hold 与 WeekCode exception，全部筛选与分页在服务端执行，无 N+1。
- Detail：lazy GET，Pallet 全量、OUTER_BOX 前 50 + `hasMoreBoxes`；新增现有 Location Repository 的 batch read，一次解析 direct/effective location。支持无 Pallet、多 Pallet、多 Material；未知 Receipt 404。
- Frontend：Learun 原生 toolbar/nav-tabs/jfGrid/SideDrawer；`Wms.Api` + `/api/wms/*` + camelCase。初始、查询、reset、render、paging 都关闭 Drawer，只有真实 cell mousedown 后的 row click 打开。无 Retry/Reprocess/Release/Recalculate/Move/Delete/Export。
- Integration：Page 04 Box 使用 `learun.frameTab` 打开 Page 03；Page 02 Preview 增加 20 行以内的“查看完整 HU 履历”入口。
- Runtime GET：54 Receipts；page 1/2 各 10 且跨页 0 重复；Plant/SourceEntityId/RequestId/date/Box/Material filters 均命中；34 current exception、20 normal；WeekCode 34、Hold 34；Receipt 57 为 61 Box/251 Pack/14 Material/Qty 19210，详情前 50 且 inherited `INBOUND-PENDING`；Receipt 11 验证 no-pallet + 2 boxes；unknown 404。
- Verification：WMS-HU-Model/WMS/Learun Debug build PASS；49 tests PASS；4 JS syntax PASS；相关 frontend UTF-8 BOM + CRLF、mojibake 0。Learun build 仅有原有 warning。应用内 Browser 运行环境无法初始化，因此 UI01–UI06 不宣称通过，需 Human Review。
- Schema/data：无 schema/table/status/transaction/migration 变更；未执行 WMS_DEV DML，未调用 inbound POST。
- Documentation：`docs/frontend/pages/04_Finished_Goods_Inbound_Records_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区有既有未提交改动，本轮未创建提交。

## 2026-08-22 Page 03 HU Detail / History V1

- 状态：`PAGE_02_FINISHED_GOODS_INVENTORY_COMPLETE=True`、`PAGE_03_HU_DETAIL_HISTORY_IN_PROGRESS=True`。
- 当前边界：只读完整 HU 详情与履历；先核对真实 HU/Relation/Location/Item/MoveLog/OperationLog/HoldLog 契约，不增加写动作、schema、table、migration 或业务状态。
- Page 02：本轮先不增加“查看完整履历”入口；待 Page 03 Human Review 通过后再做最小 frameTab integration。

### Implementation result

- Scope: implemented standalone read-only `/WMS/HuDetail/Index?huCode=...`; Page 02 was not modified or integrated.
- API inventory: `docs/frontend/pages/03_HU_Detail_History_Page.md`. Existing location and MoveLog logic is reused; OperationLog/HoldLog had no read endpoint.
- Backend: added `GET /api/wms/hus/{huCode}/detail`, `/operations?top=`, `/holds?top=`. Supports all three HU types, at most two ACTIVE-parent elevations, one current-tree query, backend direct/effective location, typed contents and active Hold. New histories default 50/cap 200.
- Frontend: Tree + Detail/Bootstrap tabs, `WmsHuTree` over `lrtree`, `WmsTimeline` over `lrtimeline`, safe `WmsJsonViewer`, per-HU lazy history cache and stale-response guards. No write controls.
- Schema/data: no schema/table/migration change and no WMS_DEV DML. New repository statements are SELECT-only and HU-scoped.
- Runtime GET: `B1951228B` detail 200, root `PLT-99153`, tree 313, contents 4, direct empty/effective `INBOUND-PENDING`/inherited true. `PLT-99153` returned PALLET with 61 box summaries/direct location. `T840064325` returned INNER_PACK with one Item/inherited location. Existing moves and new operations/holds returned 200. Unknown HU returned 404.
- Verification: backend library/host builds PASS; Learun Debug build PASS with existing warnings; 46 tests PASS; Page 03/Foundation JS syntax checks PASS; frontend files UTF-8 BOM + CRLF. Application browser could not initialize because of its runtime collision, so Human Visual Review remains required.
- Unverified: no SHIPPED sample was discoverable via used read APIs. Explicit SHIPPED lookup is lifecycle-unfiltered in service code and covered by automated test. A separate SELECT attempt from PowerShell failed before query execution due local SSPI context.
- Recommended review: OUTER_BOX direct URL → tree/header/location → INNER_PACK click → Move → Operation/JSON/copy → Hold/current-vs-history → unknown HU → known SHIPPED HU → confirm no write action.
- Git commits: none; the workspace contains prior uncommitted work and no commit was requested.

## 2026-08-22 Page 02 Finished Goods Inventory / HU Query V1

- 状态：`PAGE_01_UI_POLISH_DEFERRED=True`、`PAGE_02_FINISHED_GOODS_INVENTORY_IN_PROGRESS=False`、`FINISHED_GOODS_INVENTORY_FRONTEND_V1_IMPLEMENTED=True`。Page 01 仅记录延期项，没有修改实现。
- 真实 API 调查：原有 HU read API 仅覆盖单 HU 基础信息、树、有效位置、MoveLog、Location 主数据、Repack box 状态和 WeekCode anomaly；没有 current inventory server paging API。完整 `CURRENT_HU_READ_APIS` 见 `docs/frontend/pages/02_Finished_Goods_Inventory_Page.md`。
- 后端：新增只读 `GET /api/wms/inventory/finished-goods/` 与 `GET /api/wms/inventory/finished-goods/{boxCode}/preview`，以及最小 DTO/Service/ADO.NET Repository/Nancy Module。所有参数为 typed `SqlParameter`，无 `dynamic`、`AddWithValue`、DML、DDL 或新依赖。
- 当前库存语义：`OUTER_BOX ACTIVE` + active Relation + `INNER_PACK ACTIVE` + active Item + effective location；direct box location 优先、active parent location fallback；`SHIPPED` 排除，Hold 保留显示；数量为全部当前有效 Item Qty SUM。
- 前端：新增 `/WMS/FinishedGoodsInventory/Index`、紧凑 Learun 查询区、`Wms.Grid` server paging（50/页）、camelCase adapter、Hold/status、multi-week summary 和 PREVIEW ONLY Drawer。页面初始/查询/Reset/Grid render/翻页均关闭 Drawer；仅 grid cell 真实 mousedown 后的行选择允许打开。
- Preview：一次只读请求返回 box、effective location、WeekCode groups 与 InnerPack list；没有 Move/Hold/Release/Pick/Ship 等写动作，也没有 Reservation/Allocation 伪状态。
- Runtime GET：list HTTP/API 200，TotalCount 1136；page 1/2 无重复，箱码/物料筛选有效；`B1951228B` Preview HTTP/API 200，4 packs、Qty 320=pack sum、父级 location fallback 到 `INBOUND-PENDING`。
- 验证：WMS-HU-Model、WMS Nancy Host、Learun Web Debug build PASS；控制台 43 tests PASS；两个新增 JS `node --check` PASS；前端 UTF-8 BOM + CRLF、mojibake 0、read-only/static boundary checks PASS。Learun build 仅有既有 warnings。
- 未验证：Codex in-app Browser 因当前浏览器运行环境初始化冲突未执行登录态 UI01–UI06，不宣称视觉/点击验收通过。Human 需直达 `/WMS/FinishedGoodsInventory/Index` 完成首次 Drawer closed、Query/Loading、真实 Row click、二次查询不自动打开、Hold 与多 Pack Preview。
- Schema/API 边界：`NEW_SCHEMA_ADDED=False`、`NEW_TABLE_ADDED=False`、`DIRECT_DB_FROM_LEARUN=False`；未执行 migration、POST 或数据库写入。
- Foundation deferred：Pagination visual、WeekCode Drawer lifecycle、WeekCode Recalculate final result Modal migration。本轮未修改 Foundation pager 或 Page 01。
- Git commits：本次未创建提交；Learun Web 为非 Git 工作副本。

## 2026-08-22 WMS Grid Runtime Loading / Pagination Fix

- Runtime 现象：Human 已确认 Audit GET/Proxy HTTP 200，但最近 native pager 适配后 Grid 一直显示“正在加载数据”。Audit GET 没有启用 `Wms.Api` 全局 loading；实际 owner 是 `Wms.Grid.setState("loading")` 的 grid-local overlay。
- 根因：jfGrid native pager 只在 `isPage && url` 时创建，且所有 pager handler 都直接调用 jfGrid AJAX。Foundation 用 synthetic URL 初始化、随后清空 URL并接管 handler，强行组合外部 `Wms.Api` datasource，不属于 jfGrid 支持的合同；`jfGridSet("refreshdata")` 后的任何 native render/formatter exception 又未被 try/catch/finalize 包住，导致 state 停留 loading。
- 修复：外部 datasource Grid 恢复 `isPage:false`，jfGrid 只负责 `rowdatas`；Foundation 提供 35px Learun 风格 server pager，支持首页/上一页/数字页/下一页/尾页，仍是真实 server pagination。未修改 `jfgrid.js`。
- Loading lifecycle：Audit query 显式 `Wms.Api loading:false`；Grid 使用 request sequence + pending flag。SUCCESS、BUSINESS_ERROR、NETWORK_ERROR、GRID_RENDER_ERROR、CALLBACK_ERROR 均结束 pending；旧请求结果被忽略，不能覆盖最新查询。渲染异常显示安全错误文案，不输出 stack trace。
- 边界：未修改 WMS/WeekCode/Recalculate backend、Proxy、数据库、连接、CORS 或 DTO；未执行 Recalculate。
- 验证：受控 Grid lifecycle harness 覆盖 success/business/network/render/stale-response 路径并 PASS，临时 harness 已删除；2 个修改 JS `node --check` PASS；3 个修改的前端 JS/CSS 均为 UTF-8 BOM + CRLF，mojibake 0；Learun Debug build PASS（0 errors，两个既有 `MSB3101` cache warnings）。应用内 Browser runtime 初始化失败，未读取 Human Console 或冒充完成 Runtime Gate。
- Git commits：本次未创建提交。

## 2026-08-22 WeekCode Audit Frontend V1.2 Pagination + Result Traceability

- 分页根因：Foundation 异步 `Wms.Grid` 为手工 `rowdatas` 注入设置 `isPage:false` 并创建自定义 pager，绕过 jfGrid 原生 `pageparam/records/total`，因此真实 348/1/50 数据虽有 50 行却没有原生 footer。现改为驱动 jfGrid 原生 page bar；`WEEKCODE_AUDIT_PAGING_FIXED=True`，共享修复无页面 magic code。
- 分页行为：后端 `totalCount/page/pageSize` 映射为 jfGrid `records/page/rows/total`；页码请求保留 filters，查询/Reset 回第 1 页，重算 refresh 保留当前页，越界自动回最后有效页。未修改 `jfgrid.js`。
- 结果根因：旧 response 与 ResultPanel 只有摘要和有限 old/new rule/sort，缺少 RequestId、完整年/周/来源/格式、Hold 前后及异常消失解释。现有 DTO 合同补齐这些 transaction-memory 字段；`WEEKCODE_RECALCULATE_RESULT_TRACEABLE=True`。
- 生命周期：确认 POST 前保存 page-memory Audit snapshot；POST 成功后先保存/打开 Result Drawer，再 refresh Grid。随后用现有 Audit GET 按 group、pageSize 200 全分页复查 Result Box，可靠标记 PRESENT/RESOLVED；复查失败不宣称异常消除。
- Result UX：Header 显示范围、总体状态和可复制 RequestId；Summary 显示六项真实计数；UPDATED/UNCHANGED/FAILED 均显示 Before → After、Hold 与错误；部分成功使用明确警告。Grid 不保留已解决行，Drawer 解释其从当前异常列表移除。
- Backend contract only：增强 `WeekCodeRecalculateResultDto`、box/item result 及既有 fact/plan 字段；已有查询额外投影 `IsHold/WeekFormatCode`，已有 OperationLog detail 补齐 old/new 字段。无新查询、endpoint、transaction、table、schema、Migration 或业务规则。
- 安全：未修改 Proxy、RouteConfig、CORS、连接配置或数据库；本轮未执行真实 Recalculate POST。
- 验证：V1.2 controlled contract harness PASS，覆盖 native pager 初始化、348/50→7 映射、page 1→2 的 filters/pageSize 保留、scope Audit 多页合并、Result-before-refresh 顺序、snapshot 字段、RequestId/Before→After/异常状态 UI 和 Recalculate allowlist；临时 harness 已删除。4 个相关 JS `node --check` PASS；WMS-HU-Model Tests 41/41 PASS；WMS solution 与 Learun Debug build 均 PASS（0 errors，Learun 两个既有 `MSB3101` cache warnings）。6 个修改的前端 JS/CSS/cshtml 均为 UTF-8 BOM + CRLF，mojibake 0。
- Git commits：本次未创建提交。

## 2026-08-22 WeekCode Audit Frontend V1.1 Real Response Binding Fix

- 路由冻结：`WMS_FRONTEND_API_ROUTE_LOCKED=True`；浏览器合同 `FRONTEND_API_CONTRACT=/api/wms/*`；开发代理下游 `DEVELOPMENT_PROXY_DOWNSTREAM=http://localhost:52602`；Postman 端点 `POSTMAN_WMS_ENDPOINT=https://localhost:44375`。
- 根因与修复：真实 Audit 分页和 item 均为 camelCase，页面却以 PascalCase 读取，因此 jfGrid 只生成行号、业务单元格及 Drawer 均为空。现已将分页、Grid、AuditDiffCell、Drawer、标题、选择范围和重算来源统一绑定 camelCase，`WEEKCODE_AUDIT_JSON_CASE=camelCase`。
- 显示合同：null 统一显示 `—`；`INVALID_DATECODE` 显示“DateCode异常”；风险 HIGH/MEDIUM/LOW 显示高/中/低文本；Hold true 显示 `HOLD`、false 显示 `—`。未改变页面视觉结构。
- API 边界：保留 `Wms.Api` 对信封 `code/Code`、`message/Message`、`data/Data` 的兼容；没有递归转换 data/DTO 属性。Recalculate 出站 DTO 仍为 PascalCase allowlist，但值来自所选 camelCase 行；Operator/RequestId 策略未变。
- 安全边界：未修改 WMS 后端、Proxy、RouteConfig、CORS、连接配置、数据库或 Migration；未执行真实 Recalculate POST。
- 验证：两条真实形状 camelCase 样例的受控页面 harness PASS，覆盖分页 348/1/50、首/次箱码、Grid formatter、Diff、四组 Drawer、标题、`1010 + ITMAT001 + R1` 范围和 Recalculate payload allowlist；临时 harness 已删除。两个相关 JS `node --check` PASS，UTF-8 BOM/CRLF 与 mojibake 检查 PASS；Learun Debug build PASS（0 errors，两个既有 `MSB3101` cache write warnings）。Human 仍需重新加载页面，查询真实数据，打开 Drawer，并仅检查范围而不确认重算。
- Git commits：本次未创建提交。

## 2026-08-22 WMS Learun Server Proxy V1

- 实现：新增优先于 Default MVC route 的 `api/wms/{*path}`，由 `WmsApiProxyController` → `WmsProxyClient` 将 GET/POST 转发到唯一服务端配置 `WmsServiceBaseUrl=https://localhost:44375`。
- 边界：Controller 继承 `MvcControllerBase` 复用 Learun `HandlerLogin`；没有 BLL/Service/Repository/数据库/业务 DTO，没有 JS BaseUrl、CORS、第三方代理库或 WMS 后端改动。
- 透传：保留 query、raw POST bytes、allowlisted Accept/Content-Type/X-Requested-With、HTTP status/content type/body；4xx/5xx 从 protocol response 读取，`TrySkipIisCustomErrors=true`；连接/TLS/timeout 失败返回固定 502 JSON。
- 安全：目标 origin 只来自 appSettings；客户端不能指定 URL/host/port/scheme；拒绝 traversal、absolute/protocol-like、反斜杠、残留编码及 query/fragment path；不转发 Cookie、Authorization、Host 或 hop-by-hop headers。
- 证书：使用系统默认 TLS 验证，未关闭证书校验。当前 `https://localhost:44375` 直连在 TLS handshake 阶段报 IIS Express 本地证书/凭证错误，`HTTPS_CERTIFICATE_TRUST_FAILED=True`；未自动降级到 52602。
- 构建/静态：Learun Debug build PASS（`BUILD_EXIT=0`）；URL 单一来源、无 DB/Repository/业务 DTO/证书绕过/JS hardcode、route priority、GET/POST/raw body/status/502/auth markers 均检查通过。
- Runtime：启动 Learun 的授权被拒绝，未重复请求或绕过；因此 Proxy Audit GET、200 passthrough 和 direct/proxy semantics 未验证，`WMS_API_ROUTE_ALIGNMENT_PASS=False`。未执行 Recalculate POST 或数据库写入。
- 文档：新增 `docs/frontend/13_WMS_Server_Proxy.md`，更新 `12_WMS_API_Route_Alignment.md` 与本 handoff。
- 下一步：Human 优先修复/信任 IIS Express localhost HTTPS certificate，启动 Learun 并登录后执行安全 Audit GET 对比；若选择 Development-only HTTP，必须只显式修改 `WmsServiceBaseUrl`，不可代码自动 fallback。
- Git commits：本次未创建提交。

## 2026-08-22 WMS API Runtime Route Alignment Gate

- 调查范围：仅核对 Learun browser origin 到独立 WMS Nancy `/api/wms/*` 的运行路由；未继续数据库直连/SSPI 调查，未修改业务代码、页面视觉或 IIS 配置。
- Origins：Learun 当前项目/IIS Express origin 为 `http://localhost:20471`，application path `/`；WMS 项目配置与运行时共同确认 `http://localhost:52602`。
- Runtime：WMS 52602 的 health 和 WeekCode Audit GET 均 HTTP 200；启动真实 Learun 20471 后，根路径 HTTP 200、WeekCode 页面未登录 HTTP 302、同 origin Audit 相对 URL HTTP 404。
- 判定：`WMS_API_ROUTE_MODE=NO_ROUTE`。Learun Web.config/applicationhost 没有 rewrite/ARR rule，Controller/Handler/Module 没有 server proxy，配置没有 WMS BaseUrl，业务 JS 也没有 52602 hardcode。
- 透传：由于路由不存在，HTTP 200、HTTP 400 JSON 和 JSON POST 的端到端透传均不能通过。WMS 源端自身已静态设置 `httpErrors existingResponse=PassThrough`，但不能替代 Learun 路由验证。
- 浏览器限制：应用内浏览器的已保存权限禁止访问该 localhost origin；未尝试绕过。Gate 结论由真实 IIS Express HTTP 请求支持。
- 数据/行为安全：无 SQL、Repository、DbFactory、数据库写入或真实 Recalculate POST；未修改前端视觉、后端业务逻辑、connection string 或生产 IIS。
- 构建：Learun Debug build PASS（`BUILD_EXIT=0`）；两个既有引用项目缓存文件产生非阻断 `MSB3101` warning，Web DLL 成功生成。
- 文档：新增 `docs/frontend/12_WMS_API_Route_Alignment.md`，并在 `11_Database_Connection_Alignment.md` 中将数据库可用性与 API 路由 Gate 分离。
- 下一步：Human Review 后优先配置 Development same-origin reverse proxy `/api/wms/{R}` → `http://localhost:52602/api/wms/{R}`，再验证 Audit 200 与受控 400 JSON 原样透传；不再执行 Learun Factory DB Gate。
- Git commits：本次未创建提交。

## 2026-08-22 Database Connection Alignment Gate

- 调查范围：核对 Learun `XmlConfig/database.config` 加载机制、MesDb/WmsDb 结构与重复来源、WeekCode 页面运行边界，并执行 SELECT-only connection preflight；未开发新页面。
- Learun loader：`Web.config connectionStrings configSource` → `ConfigurationManager.ConnectionStrings[name]` → `DbFactory.GetIDatabase(name)` → provider/Unity → `Learun.DataBase.SqlServer.Database`。两个 key 结构合法、各精确一项、UTF-8 BOM。
- 架构决策：WeekCode 页面无 Learun DB 或 Controller proxy；浏览器通过 `Wms.Api` 调相对 `/api/wms/weekcode/*`，真正的 Repository/连接配置属于独立 WMS Nancy Host。`LEARUN_DIRECT_DB_CONNECTION_REQUIRED=False`。
- Learun preflight：临时 harness（已删除）通过真实 `RepositoryFactory.BaseRepository("MesDb"/"WmsDb")` 正确解析配置和 SqlServer implementation，但终端身份 `DELTON\953818` 在两次 Open 均报“目标主体名称不正确。无法生成 SSPI 上下文。”；未执行后续 identity/sys.tables SELECT。
- WMS Runtime：复用现有 `GET http://localhost:52602/api/health/db`，HTTP/Code 200，连接 `BT-ME-PC024\SQLEXPRESS / WMS_DEV` 成功；只读 Audit GET `plantCode=1020&page=1&pageSize=1` 返回 TotalCount 230 且 PackCreatedTime 非空，证明当前独立 WMS IIS Runtime 同时可读 WmsDb 和 MesDb。
- 未完成项：现有 endpoint 不返回 `SYSTEM_USER/ORIGINAL_LOGIN` 或规定的完整 sys.tables inventory；IIS Express token owner 受权限限制不可读；Learun origin 到独立 WMS Host 的同源 `/api/wms` 路由未验证。因此 Gate 状态为 incomplete，不能宣称完整 connection alignment 通过。
- 数据安全：执行的数据库语句仅 SELECT；没有 DML/DDL、Migration、业务 POST、配置修改、后端业务代码或前端视觉代码修改。没有记录完整 connection string。
- 构建：`Learun.Application.Web.csproj` Debug build PASS（`BUILD_EXIT=0`）；两个引用项目的既有 `obj\Debug\*.AssemblyReference.cache` 不可写产生 `MSB3101` warning，但程序集及 Web DLL 均成功生成。
- 文档：新增 `docs/frontend/11_Database_Connection_Alignment.md`；包含配置加载器、runtime 分流、SSPI 分类、诊断证据与下一步。
- 推荐下一步：Human 先处理/确认 Learun 进程 SSPI 与 IIS identity，再批准固定 allowlist 的 Development-only identity/table diagnostic；确认同源 API route 后才解除 Gate。
- Git commits：本次未创建提交。

## 2026-08-22 WMS Frontend Page 01 WeekCode Audit & Recalculate V1

- 实现范围：新增 Learun 紧凑操作台页面 `/WMS/WeekCodeAudit/Index`，接线真实 `GET /api/wms/weekcode/audit` 与 `POST /api/wms/weekcode/recalculate`；查询、异常 Drawer、范围确认、pending 防重复、结果 Drawer 和自动刷新闭环均已实现。
- Web 文件：新增 `WeekCodeAuditController.cs`、`Views/WeekCodeAudit/Index.cshtml/Index.css/Index.js/WeekCodeAuditApi.js`；旧式 `Learun.Application.Web.csproj` 已显式 Include。
- Foundation：新增可复用 `Wms.AuditDiffCell`、`Wms.ResultPanel`；`Wms.Grid` 增加经异步数据源驱动的后端分页适配与 clear/empty message；`Wms.Api` 增加 PascalCase `Code/Message/Data` 归一化及保留 HTTP 400 response JSON 的 GET 传输路径。未修改 Learun、Bootstrap、jfGrid 或第三方源码。
- Query/API：Audit 只发送 `plantCode/materialCode/materialRevision/anomalyType/riskLevel/isHold/page/pageSize`；Recalculate payload 严格只含 `PlantCode/MaterialCode/MaterialRevision/Operator/RequestId`，不发送 Box 或规则/周码派生值。
- Operator/RequestId：Operator 优先 Learun `userinfo.account`，回退 `userinfo.userId`，没有稳定标识则阻止提交；Confirm 真正确认后生成一次 `WCR-UI-<learun.newGuid()>`，HTTP 返回前锁定按钮。
- Schema/backend/data：无 schema、Migration、WMS Module/Service/Repository/DTO 或数据库变更；未执行真实 GET；未执行真实 Recalculate POST；未改菜单或权限数据库。
- 构建：`MSBuild Learun.Application.Web.csproj /t:Build /p:Configuration=Debug /p:SolutionDir=D:\com.delton.gz\ /p:RestorePackages=false /m /nologo /v:minimal` PASS，0 errors。首次未传 SolutionDir 的命令仅因 NuGet.targets 路径失败；随后一次带尾随空格的参数触发受限 restore 并失败，最终使用上述命令关闭 restore 后通过。
- 静态验证：6 个相关 JS `node --check` PASS；PascalCase/HTTP400 normalize、重算 payload allowlist、Diff render 合同测试 PASS；受控页面 harness 的查询/分页映射、四组详情、Confirm 后提交、pending、部分失败 Result 和 Grid reload 均 PASS。
- 未验证：未在登录后的真实 Learun Browser 中执行视觉/运行时测试；未验证部署环境是否将相对 `/api/wms/weekcode/*` 同源路由到 Nancy WMS host；未执行真实数据库/Postman 集成。
- 文档：新增 `docs/frontend/pages/01_WeekCode_Audit_Page.md`，包含合同、权限、编码、限制与 UI01–UI12 人工用例。
- 推荐人工顺序：UI01 → UI08 完成只读和取消路径 → Human 授权后仅执行一次 UI09 真实 POST → UI10–UI12 检查部分失败、自动刷新与 Hold release。
- Git commits：本次未创建提交；Web 工程使用 SVN 工作副本，本轮未提交。

## 2026-08-22 WMS Frontend Foundation V1.1 Visual + Encoding

- 实现范围：将 Foundation Showcase 从独立 dashboard/card 风格调整为 Learun native visual first；保留全部 V1 Foundation 行为，不新增业务页面或真实 API。
- 视觉基线：审阅 User、Log、DataItem、HS_WarehouseLocation、WMS_MaterialStock 等成熟页面及 Learun layout/form/grid/layer CSS；估算视觉复用 80%。
- Showcase：采用 `lr-layout/lr-layout-wrap-notitle/lr-layout-tool/lr-layout-body`、Bootstrap 3 tabs/buttons、原生 28px jfGrid；右侧 DetailPanel、Drawer 与 StatusTag 使用紧凑 Learun 协调样式。
- Modal：继续 `learun.layerForm`；iframe demo 改为 `_Form + lr-form-wrap + lr-form-item`，移除独立 modern form chrome。
- Encoding Audit：10 cshtml + 10 JS + 5 CSS 正常中文样本全部为 UTF-8 BOM；原 11 个 Foundation 文件均为有效 UTF-8 no BOM、文本无 mojibake。现已统一为 UTF-8 BOM，payload hash comparison 确认编码转换未改文本。
- Config/charset：Web.config 未显式配置 globalization encoding；`_Admin/_Index/_Form` 均声明 HTML charset UTF-8。
- 验证：7 个 Foundation/Demo JS `node --check` PASS；静态回归确认 Api/Grid/Drawer/Modal/Drawer→Modal/Event Namespace contracts 均保留；无同步 AJAX、无全局 CSS override、无自建 button system。
- 构建：VS 2022 MSBuild Debug PASS；0 errors。两个被引用项目因 workspace 写权限无法更新 `obj/*.AssemblyReference.cache` 产生 MSB3101 warning，但未阻断输出，Web DLL 成功生成。
- 文档：新增 `docs/frontend/09_Learun_Visual_Baseline.md`、`10_Frontend_Encoding_Audit.md`，更新 `08_WMS_Foundation_Implementation.md` 与本 handoff。
- Schema/API/data：无 schema 或后端 API 变更；未调用真实业务 API，未修改真实业务数据。
- 人工验收：需登录后检查 1366×768 / 1920×1080、查询区、Grid、tabs、Modal、Drawer、中文以及 Drawer→Modal callback。
- Git commits：本次未创建提交。

## 2026-08-22 WMS Frontend Foundation V1 Implementation

- 实现范围：在现有 Learun/jQuery/Bootstrap 3 基线上新增 `Wms` namespace、Registry、统一 API、SearchPanel、Grid、Select、DateRange、StatusTag、DetailPanel、SideDrawer、Modal、ConfirmDialog，并新增只读 Mock Showcase。
- Web 文件：新增 `Areas/WMS/Content/Foundation/**`、`Areas/WMS/Controllers/WMSFoundationDemoController.cs`、`Areas/WMS/Views/WMSFoundationDemo/**`；更新 `Learun.Application.Web.csproj` 显式包含项。
- 文档：新增 `docs/frontend/07_Modal_Drawer_Usage_Guide.md` 与 `08_WMS_Foundation_Implementation.md`；更新本 handoff。
- Schema/API：无 schema 变更；无真实 WMS 业务 API 变更；Showcase 仅增加只读 Mock GET `/WMS/WMSFoundationDemo/GetMockRows`，没有数据库访问或生产写入。
- 框架/依赖：Learun、Bootstrap、第三方源码均未修改；未引入 Vue/React/npm/new grid。
- 构建：VS 2022 MSBuild Debug PASS，0 errors；84 个既有 warning。构建命令：`MSBuild Learun.Application.Web.csproj /t:Build /p:Configuration=Debug /p:RestorePackages=false /p:SolutionDir=D:\com.delton.gz /m:1`。
- 静态验证：6 个新增 JS `node --check` PASS；无同步 AJAX、`top.$`、fetch、Promise、async/await、Vue/React；直接 `$.ajax` 仅为 `wms-api.js` 中集中式 JSON POST。
- 浏览器验证：IIS Express 启动成功；匿名请求按框架认证规则返回登录页。应用内浏览器 localhost 权限未获批准，未执行完整可视化交互验收，不宣称 UI runtime PASS。
- 生命周期：WMS 自有事件使用 namespace 并提供 destroy；Showcase 内置 SearchPanel/Drawer 10 次生命周期自检。`jfGrid/lrselect/lrdate` 缺少统一公开 destroy，包装层复用底层实例，只清理自身资源。
- Modal/Drawer：Quick Detail=Drawer；Complex Form=iframe Modal；高影响动作=ConfirmDialog；Drawer 可开 Modal；Modal 成功刷新 Drawer 与 Grid；默认最大 Modal 深度 1。
- 推荐人工验收顺序：登录 Learun → 打开 `/WMS/WMSFoundationDemo/Index` → 搜索/重置/分页 → Drawer 10 次 → Drawer 内 Modal 保存/取消 10 次 → Confirm 取消/确认 → 检查 Registry、遮罩、焦点与控制台。
- 风险/限制：老框架底层组件销毁能力有限；真实 API response 映射尚待正式接口联调；Showcase 未注册菜单且只使用 Mock 数据。
- Git commits：本次未创建提交。

## 2026-08-22 Frontend Foundation Investigation

- 实现范围：只读盘点 `D:\com.delton.gz\Learun.Application.Web` 的 Learun/jQuery 前端结构、组件、基础设施、依赖和 18 个代表页面；新增 `docs/frontend/00` 至 `06` 七份文档。
- 框架结论：Learun-ADMS V7.0.3；标准布局加载 jQuery 1.10.2、Bootstrap 3.3.7；核心 grid 为 jfGrid。
- 文件变更：仅 `docs/frontend/*.md` 与本 handoff；未修改 Web 项目的 JS/CSS/cshtml、后端或配置。
- Schema/API：无变更；未连接或修改 WMS_DEV。
- 验证：完成源码静态扫描、组件 API 对照、第三方版本头核验、18 页 pattern review 和文档一致性检查；未运行浏览器、Postman、数据库或 UI 自动化测试。
- 偏差：无实现性工作；按任务要求仅调查、分类、文档、建议。
- 风险：jQuery/Learun baseline 较旧；同步 AJAX 与直接 `$.ajax` 历史用量高；多个同类库并存；iframe/tab/dialog 存在 lifecycle 和 memory leak 风险。
- 推荐人工审阅顺序：`00_Frontend_Foundation_Review` → `02_Component_Atlas` → `03_WMS_Page_Component_Matrix` → `04_WMS_Component_Wrapper_Proposal` → custom/do-not-reinvent lists。
- Git commits：未创建提交。

日期：2026-08-17

## 已实现

- Migration 014 已完成：`014_OutboundV1Compatibility.sql` 已在真实 `WMS_DEV` 成功执行 FIRST_RUN_RETRY；Human Review 决定不执行真实 SECOND_RUN（`SECOND_RUN_REQUIRED=False`）。
- `014_OutboundV1Compatibility.sql` 保留 009 历史并修正待扫码 Batch、正式状态、SAPBatchNo 快照、多 Replacement、动作幂等、每仓 RETURNED/SHIPPED 以及原子过期过程。
- Item 写入链接入 WeekCode Resolver；没有 MES Provider 时明确失败，不回退 CreateTime。
- HUCode 优先、客户上下文 Identifier 次级解析。
- `MATNR+UOM` 正式 Preview 与确定性精确组合，结果只使用四种正式类型。
- StartPicking 在 Serializable 事务内重算整单，创建 Allocation/Batch/Reservation；Release、Expiry 和 WAIT_PICK VOID 保留历史。
- 扫码事务原子更新 MoveLog、CurrentLocation、OperationLog、Reservation、PickDetail、PickDetailItem、Line/Batch/Order；按 SAPItemNo 升序分摊并快照 BatchNo/SAPBatchNo。
- 单箱异常退回、QUALITY_FAILED 自动 Hold、人工 Hold/Release、多箱 Replacement、ReturnToStock 和显式 SHIPPED。
- MEDIUM-OUTBOUND-01：StartPicking / Replacement 的 `reservations` 仅返回 ACTIVE Reservation，避免 PDA 将历史 CONSUMED 箱误认为当前待扫码任务。
- 旧 `/pick-next`、`/pick-batch` 返回 410；`/queue`、`/plan` 只读保留。

## API

- `GET /api/wms/outbound/orders`
- `GET /api/wms/outbound/orders/{vbeln}`
- `POST /api/wms/outbound/orders/{vbeln}/preview`
- `POST /api/wms/outbound/orders/{vbeln}/start-picking`
- `POST /api/wms/outbound/orders/{vbeln}/void`
- `POST /api/wms/outbound/allocations/{allocationNo}/release`
- `POST /api/wms/outbound/pick-batches/{pickBatchNo}/scan`
- `POST /api/wms/outbound/pick-details/{detailId}/return`
- `POST /api/wms/outbound/pick-batches/{pickBatchNo}/replace`
- `POST /api/wms/outbound/returned/{huCode}/putaway`
- `POST /api/wms/outbound/orders/{vbeln}/ship`
- `POST /api/wms/hus/{huCode}/hold`
- `POST /api/wms/hus/{huCode}/release`

## 验证结果

- VS 2022 MSBuild Debug：通过，0 编译错误。
- VS 2022 MSBuild Release：通过，0 编译错误。
- `WMS-HU-Model.Tests` 可执行算法测试：6/6 通过。
- MEDIUM-OUTBOUND-01 需以 Postman 完成“扫码后 Replacement / 幂等重试”的数据库集成验收；本地算法测试不覆盖 Repository SQL。
- 新增 C# 文件均已显式登记到旧式 `.csproj`。
- 新正式链路未使用 `dynamic` 或 `AddWithValue`；Nancy 路由参数采用两步转换。
- Migration 014 Disposable Clone：Run #1 / Run #2 均成功，Schema 与观察状态在第二次后稳定；真实 `WMS_DEV` 未被 Clone 测试触碰。
- Migration 014 真实 `WMS_DEV` FIRST_RUN_RETRY：成功，SchemaVersion、Replacement table、Expiry procedure、6 个新增列及 8 个预期索引均为 1，`@@TRANCOUNT=0`；真实 post-schema 与 Clone Run #1 语义一致，Unexpected Schema Change=0。
- Canonical SHA：`5FD57C4B981553DCDEB2DB9BAB213CE2D120E6BCE7AB6A47CCC9A2330E66E3D1`。
- Completion Evidence：`.codex/evidence/migration-014-completion.json`，SHA256=`086F84C4801E97EE736554DDD00CA1344E163AF3FCDC4E0DE6E8E2CF1F1C8EFC`。
- Real FIRST_RUN Result SHA：`3B1A269CBB143A33AC2523E6B65F321EBBD50515003B175B534EAE7D64E2DCA1`；Real post-schema SHA：`D3779BC75AB19B8F9CE38C0023083CA0FE377FB295CE6DFA9EADA349E14A1F2D`。
- 最新 WMS_DEV Schema Evidence：`.codex/evidence/wms-dev-schema-post-014-first-run-20260816.json`，SHA256=`D3779BC75AB19B8F9CE38C0023083CA0FE377FB295CE6DFA9EADA349E14A1F2D`。
- 记录时 Git HEAD：`acb449f7d45b5d9a0f312d5b444721cb7fb97db8`。

## 未解决风险

- 未提供 SAP 真实脱敏报文、外层 JSON、最终字段类型/映射，SAP Receive 入口没有猜测实现。
- 未提供 MES GiftQty 与 WeekCode 合同；默认 WeekCode Provider 会明确阻断带 DateCode 的新 Item 写入。
- 外部调度器、执行账号与 Reservation 过期频率未配置。
- 需求示例称 `40+30+30` 在需求 60 时无完整 INNER_PACK 组合，但数学上 `30+30=60`。实现按每个实际 INNER_PACK 均可独立选择，因此会返回 REPACK_REQUIRED；需业务人工确认示例是否有未写出的限制。
- `ExceptionType` 的业务枚举未定义，异常退回 API 要求调用方显式提供；系统只固定校验四个 ReasonCode。

## 下一步人工验收顺序

1. Migration 014 已完成；不执行真实 SECOND_RUN。保留 completion evidence、真实 FIRST_RUN result 和 post-schema evidence 以供审计。
2. 提供 SAP/MES 合同后补 Receive 与 Provider，不修改已应用的 010–014。
3. 准备含混周码、多 POSNR、多 SAPBatchNo、异常与多 Replacement 的测试数据，执行 Repository、并发及完整 API 流程。

## MEDIUM-OUTBOUND-01 交接

- 已实现范围：执行任务 DTO 的 `batches[].reservations` 只返回 `ReservationStatus = ACTIVE` 的 Reservation；CONSUMED 历史继续保留在数据库与 PickDetail 追溯中。
- 修改文件：`WMS-HU-Model/Repositories/Impl/FormalOutboundRepository.cs`、本 handoff 文件。
- Schema/API：无 schema 变更；既有 StartPicking 和 Replacement 响应的 `reservations` 语义收窄为“当前待扫描箱”，不增加 `reservationStatus` 字段。
- 已执行：`MSBuild WMS-HU-Model/WMS-HU-Model.csproj /t:Build /p:Configuration=Debug /m`、`MSBuild WMS-HU-Model.Tests/WMS-HU-Model.Tests.csproj /t:Build /p:Configuration=Debug /m`，均成功且 0 警告/错误；`WMS-HU-Model.Tests/bin/Debug/WMS-HU-Model.Tests.exe` 通过 6/6。
- 未执行：Postman 或数据库集成测试；现有测试没有覆盖 Repository SQL 返回投影。
- 建议 Postman 顺序：StartPicking → 扫描一箱使 Reservation 变为 CONSUMED → Replacement → 验证仅出现新的 ACTIVE 箱 → 使用相同 Replacement RequestId 重试并验证 `isDuplicate=true` 与相同 ACTIVE 任务视图。
- 偏差/风险：无业务偏差；尚需在含多个 PickBatch 的真实测试数据上验证没有 ACTIVE Reservation 的 Batch 返回空 `reservations`。
- Git commits：本变更未创建提交。

## HIGH-OUTBOUND-SHIP-01 交接

- 已实现范围：ShipOrder 锚定订单最新的 COMPLETED Allocation；订单行、当前 Allocation Batch 与订单 Open Exception 分别校验。历史 RELEASED / EXPIRED Allocation 的 Batch 保留审计历史但不再阻断 SHIPPED。
- 修改文件：`WMS-HU-Model/Repositories/Impl/FormalOutboundRepository.cs`、本 handoff 文件。
- Schema/API：无 schema 或 DTO 变更；ShipOrder 的失败消息按 Allocation、订单行、Batch、Exception 原因细分。
- 未执行：Postman 或数据库集成测试；需使用 `StartPicking → Release → StartPicking → Scan → ShipOrder` 验证历史 RELEASED Batch 不阻断，及 Ship RequestId 重试不重复移箱。
- Git commits：本变更未创建提交。

## SAP Outbound Receive V1 交接

状态：`SAP_OUTBOUND_RECEIVE_V1_STATUS=COMPLETE`，`INTEGRATION VERIFIED`，`READY_FOR_COMMIT=True`。

### 实现范围

- API：`POST /api/wms/integration/sap/outbound-orders`。
- 流程：SAP Raw JSON → Canonical SHA-256 → VBELN Idempotency → WarehousePlant Resolution → `WMS_OutboundOrder` / `WMS_OutboundOrderLine` → `WAIT_PICK`。
- 不自动执行 Preview、Reservation、Picking 或 WMS→SAP callback。

### 幂等

- Same VBELN + Same Hash → DUPLICATE；Same VBELN + Different Hash → REJECTED；First VBELN → SUCCESS。
- Existing Hash 判断优先于当前完整业务校验和 WarehousePlant 解析；Create 事务使用 Serializable + `UPDLOCK, HOLDLOCK` 二次检查。
- Duplicate 不修改历史 Order/Line。WarehouseCode 通过历史 WarehouseId 读取，属于 master-data display exception，不参与幂等成功判断。

### Canonical

- `DateParseHandling.None`、`FloatParseHandling.Decimal`；Integer/BigInteger/Decimal 不经 double。
- Object Property 按 Ordinal 排序；仅根 `$.T_ITEM` 按原始字符串 POSNR 排序；Nested `T_ITEM` 保持原顺序。
- POSNR 保留前导零，Unknown Field 参与 Hash，`7515` / `7515.0` / `7515.0000` 等价。
- 第二 Root 或尾随 scalar 拒绝；尾随 whitespace 允许。

### 校验与审计

- SAP 写库字符串长度按真实 schema 校验；VBELN 最大 30。超长 VBELN 在 Existing 查询前 Reject，审计使用 `SAPDeliveryNo=NULL`、Canonical RequestHash 与完整原始 RequestJson。
- LFIMG > 0 且符合 `DECIMAL(18,4)` precision/scale；POSNR 唯一；单 VBELN 单 WERKS；日期时间严格格式；WarehousePlant 必须唯一有效映射。
- `WMS_OutboundOrderReceiveLog` 对 SUCCESS、DUPLICATE、REJECTED 均正常 Commit；RequestId 保持 NULL。坏 JSON 保留 Raw JSON，无法获得的 VBELN/Hash 按真实阶段写 NULL。

### 映射与范围

- `OutboundOrderNo=VBELN`；Order/Line 均为 WAIT_PICK；MaterialRevision=NULL；GiftQty=0；ZKHLH 不做业务解释。
- 无 Plant/LGORT hard-code；不包含 WMS→SAP 箱级回传、GiftQty Resolver、MaterialRevision Resolver、ZKHLH 业务语义、CHARG 真实批次来源或 MES WeekCode Provider。

### 构建与自动化测试

- `WMS-HU-Model` Debug Build：PASS，0 warning / 0 error。
- `WMS-HU-Model.Tests` Debug Build：PASS，0 warning / 0 error。
- `WMS` Debug Build：PASS，0 warning / 0 error。
- Console regression tests：Passed 18 tests。
- 自动化覆盖：trailing root/scalar/whitespace、root/nested T_ITEM、unknown field、malformed JSON、decimal precision/range、schema max length、oversized VBELN Existing-check ordering 与 oversized VBELN audit parameters。

### WMS_DEV 真实集成验证

- PASS：1010 → FG01 / 11C3；1020 → FG01 / 12C3；First Receive SUCCESS；Same Hash DUPLICATE；Header Property、T_ITEM Order、Numeric Representation 变化仍 DUPLICATE；Different Hash REJECTED 且历史 Order/Line 不变。
- PASS：跨 WERKS、重复 POSNR、LFIMG 非法、无 WarehousePlant 映射、空 T_ITEM 均 Reject；坏 JSON 写入 REJECTED Audit。
- 真实 SAP Receive：VBELN=`8300152127`。

### Formal Outbound 跨模块回归

- SAP Receive → WAIT_PICK → Preview → StartPicking：PASS。
- Regression VBELN：`8300152201`；HU：`B-F-260817114028RRSF-02`；Qty：30 PCS；Location：H201；WeekSortKey：202604；Allocation：`OA20260818103305A79B7A12BD`。

### 最终 Code Review

- BLOCKER=0，HIGH=0，MEDIUM=1 ACCEPTED，LOW=0。
- Accepted MEDIUM：真实 WMS_DEV 的 VBELN > 30 烟雾测试未执行，因为数据库写入安全门禁禁止该操作。该分支已通过 Fake Repository 自动化测试验证 Existing 未调用、SAPDeliveryNo=NULL、Canonical Hash 与 Raw JSON 保留，因此不阻塞 V1 提交。
- Git commits：本变更已完成创建提交。

## MES Finished Goods Inbound V1 交接

- 已实现范围：`POST /api/wms/integration/finished-goods/inbound` 接收通用成品入库快照；`POST /api/wms/integration/finished-goods/mes-inbound` 按 MES `PLAN_STOCK` 读取包装层级并完成入库编排。
- 幂等与持久化：以 `SourceSystem + SourceType + SourceEntityId` 和 `FGI_V1` Snapshot Hash 判定重复或冲突；Receipt、HU、标识、层级、明细、MoveLog、CurrentLocation 与 OperationLog 在同一可串行化事务中写入。
- 周码：MES 规则经 `md_flow` 链路查询；明确规则解析后写入 WeekCode 快照，缺失/歧义/不支持规则走异常入库并保留原始 DateCode。
- 数据库：新增迁移草案 `docs/database/015_FinishedGoodsInboundCore.sql`，未执行；WMS_DEV 未修改。执行前仍需 live schema、`INBOUND-PENDING` 主数据、备份与人工审批。
- 验证：Solution Debug/Release Build 均 PASS；控制台回归测试 PASS（36 cases）；未执行 Postman、WMS_DEV 或 MES 实库集成测试。
- 风险：MES 真实表结构、运行账号权限、`INBOUND-PENDING` 位置主数据和迁移 015 的实库兼容性尚未验证；`MES_FGI_BATCH_RESULTS` 是本地运行产物，不纳入 Git。

## WMS HTTP API Interface List 文档交接

- 已实现范围：新增 `docs/WMS_API_Interface_List.md`，以当前 `WMS/Modules` 的 12 个 Nancy Module 为唯一接口来源，记录 42 条实际 Route（GET 14、POST 27、PUT 1、DELETE 0）。
- 文档内容：接口总览、核心 Mermaid 调用链、通用响应约定、按业务域的请求/响应/失败/数据库影响/事务/幂等/OperationLog、MES 入库与 WeekCode Audit/Recalculate 专章、数据库影响矩阵与 Postman Quick Reference。
- Schema/API：无 Schema、Web.config、csproj 或生产代码变更；未执行 Migration、数据库操作、POST Runtime 调用或构建（纯文档任务）。
- 已执行验证：静态扫描 Module 路由、读取关键 DTO/Service/Repository，并检查文档生成成功；未宣称 Runtime 验证通过。
- 偏差/风险：旧 Module 的多数失败路径只携带 `ApiResult.Code=400` 而不显式设置 HTTP 400；文档明确标注此客户端兼容性风险。当前工作区原有未提交代码变更未被修改。
- 推荐 Postman 顺序：health → MES inbound（人工授权的非生产测试数据）→ weekcode audit → weekcode recalculate；本次未执行其中任何写请求。
- Git commits：本次未创建提交。

## WMS Runtime Functional Module Inventory 文档交接

- 已实现范围：新增 `docs/WMS_RUNTIME_FUNCTIONAL_MODULE_INVENTORY.md`，以当前 `WMS/Modules/*.cs` 为准完成运行时功能盘点。
- 已确认：23 个 Nancy Module、85 条具体 Route；正式出库分为 `OutboundOrderModule`、`OutboundPickingModule`、`OutboundExceptionModule`、`OutboundSapPostingModule`。
- 报告包含：每个 Module 的 HTTP/Service/Repository/规则/事务/状态/结果说明；完整路由矩阵、Module-Service-Repository 矩阵、业务操作/事务/状态机/守卫/表使用/业务事实归属矩阵和 Mermaid 端到端流程。
- 验证：只执行源码静态扫描。Module 数、Route 数、文档中所有 Module 名、核心章节和 Mermaid 块均已交叉检查；`git diff --check` 对新增报告通过。
- 未执行：数据库连接、SQL、Migration、HTTP 写请求、构建或 Runtime 测试；未修改 Runtime、测试、项目文件、配置或数据库。
- 风险：报表描述的是当前源代码可见行为，未声称人工 SQL 或 Runtime 验证已经完成。
- Git commits：本次未创建提交。

## WMS Pager Integration Coverage Audit

- 实现范围：审计 Page 01/02/04/07 的真实 view/script/style/API 与共享 Foundation；新增基于真实页面结构的 `agents/wms-pager-integration-harness.js`。未修改 Learun 生产前端、后端、API、Proxy、数据库、Drawer、Modal、DTO 或 `jfgrid.js`。
- 结论：四页均使用 external `Wms.Grid.init(loadData)`、pageSize 50、完整 items/totalCount/page/pageSize 映射和同一 Pager renderer。Human Runtime 证明 Page 02 的 absolute host 与共享 `height:100%` 冲突；Page 01/04 具有相同 CSS 模式。
- 正式修复：共享 Foundation 新增 `.wms-grid-absolute-fill`；Page 01/02/04 接入，Page 07 不改；删除 Page 02 临时 main bottom 31px / absolute pager 补丁。Foundation 由 iframe HTML 内联，旧标签页必须关闭重开。
- Human Runtime：Page 02 在修复前 host bottom 1830、pager bottom 1828，超过 iframe height 1806；Console 将 host width/height 强制设为 auto 后 Pager 立即可见，根因已实测证明。Page 01/04 正式修复后的 runtime 仍需 Human 复核。
- 已执行：Foundation pager harness PASS；integration harness PASS（Page 01/02/04/07 全通过）；Learun Application Web Debug build PASS（0 errors，2 个既有依赖项目 obj cache 写权限 warning）。Page 02 Human Console hotfix Pager PASS；正式 CSS modifier 的全新 iframe 回归以及 Page 01/04 仍需 Human 复核。
- Human 结果：Page 01/02/04/07 Pager 均已验证通过；Pager debt 已关闭并进入 Drawer Phase 1B。
- Schema/API/数据库：无变更；未连接或修改 WMS_DEV。
- Git commits：本次未创建提交。

## WMS Foundation Debt Closure Phase 1B - Drawer Lifecycle

- 状态：`WMS_FOUNDATION_PAGER_DEBT_CLOSED=True`；`FOUNDATION_DEBT_02_DRAWER_LIFECYCLE_IN_PROGRESS=True`。代码、受控 harness 与 build 已完成，等待 Page 02→04→07→01 Human Runtime Gate。
- Foundation：`Wms.Grid` 新增 `onUserRowClick` 与 `onBeforeDataChange`，区分真实 cell intent 和 jfGrid programmatic selection；`Wms.SideDrawer` 增加 CLOSED/OPENING/OPEN/CLOSING 状态、context revision/token、closed mutation guard 和 immediate destroy。
- 页面：Page 01/02/04/07 使用同一 user-intent/close contract；删除 Page 02/04/07 的 `userRowIntentUntil` 页面时序补丁。Page 02/04/07 async detail 使用 context token，A→B、关闭后响应和 network error 均由 Drawer-local lifecycle 处理。Page 07 Detail→Request Trace 复用同一 Drawer。
- Page 01 debt：Audit Preview 已接入；Recalculate Result 仍保留 Drawer，并仅对该 context 的 `grid.refresh()` 保持打开。`PAGE01_RESULT_DRAWER_LIFECYCLE_CONFLICT=True`、`PAGE01_RECALCULATE_MODAL_MIGRATION_PENDING=True`。
- 验证：D01–D12 Drawer harness PASS；Page 01/02/04/07 integration harness PASS；既有 Pager harness 回归 PASS；修改 JS syntax PASS；Learun Application Web Debug build PASS（0 errors，2 个既有依赖项目 obj cache 写权限 warning）。Human Runtime 尚未执行，不能关闭 Drawer debt。
- 范围：未修改 Pager、jfGrid、Wms.Api、backend、Proxy、Database、schema 或 Recalculate API；未实现 Phase 1C Result Modal。
- 文档：`docs/frontend/15_WMS_Drawer_Lifecycle_Contract.md`。
- Git commits：本次未创建提交。

## 2026-08-31 Learun BaseApi Controller 对齐

- Learun 宿主 `D:\com.delton.gz\Learun.Application.WebApi\Modules\WMS_FG\WMS\Modules` 的 23 个 WMS-FG 模块均已由 `NancyModule` 改为继承 `BaseApi`，并引入 `Learun.Application.WebApi`。
- 85 个路由注册均已由旧式 `Get/Post/Put("...", handler)` 改为 Nancy 1.4 索引器写法；原 `LocationModule` 的 `Put` 编译错误已消除。
- 所有 WMS-FG API 根路径已从 `/api/wms` 统一为 `/api/wms-fg`；健康检查为 `/api/wms-fg/health` 与 `/api/wms-fg/health/db`。未保留旧路径别名。
- 保留现有 `ApiResult` 响应体、HTTP 状态和 DTO 绑定；未改动 `BaseApi.cs`、鉴权白名单、业务服务、仓储、SQL、迁移或数据库数据。Learun 鉴权配置由宿主维护者负责。
- `WeekCodeRecalculateModule.cs` 已从压缩单行格式规范化为与其余 Controller 一致的格式。
- 验证：静态扫描确认 `NancyModule` 继承、旧式 `Get/Post/Put(` 注册和旧 `/api/wms` 前缀均为 0；`BaseApi` 模块 23 个、索引器路由 85 个。宿主编译结果见本次任务最终验证记录。
- 宿主完整 `MSBuild /t:Build` 未能完成：缺少旧版 WebApplication targets；使用 VS2022 targets 重试后，宿主既有 Redis 依赖和 Windows SDK `AxImp.exe` 缺失而失败。`CoreCompile` 也被现有 MSBuild 4.0 C# 编译器对宿主既有 C# 6+ 语法的 990 个错误阻断；未发现本次 23 个 Module 的编译错误。
- 后续修正：索引器转换后发现路由注册尾部残留旧方法调用右括号；已将块处理器闭合由 `});` 更正为 `};`，并移除表达式处理器末尾多余 `)`。复核：所有 23 个模块花括号平衡，且不存在该两类旧闭合语法。
- 最终语法修复验证：已从原始模块重新生成迁移结果，按块式与表达式 lambda 分别处理闭合符；23 个模块花括号平衡，路由注册 85 个，旧语法及错误闭合符扫描为 0。
- 最终 Roslyn 语法检查：`Microsoft.Net.Compilers 2.1.0` 对 23 个 WMS-FG Module 源文件的 `CS1002/CS1513` 等语法错误为 0；块式路由残留 `});` 为 0。

## 2026-08-31 WMS-FG Learun Frontend API Mapping Alignment

- 仅更新 Learun 前端 `Areas/WMS` 中 15 个已确认的 WMS-FG 映射文件，将活动运行时前缀从 `/api/wms/` 改为 `/api/wms-fg/`；未改 DTO、HTTP 方法、查询参数、`ApiResult`、错误处理或后端代码。
- PDA 覆盖 Inventory Lookup、Recommended/Pallet Putaway、Relocation、Outbound Picking、Unpack/Merge；当前没有引用 WMS-FG Runtime 的 Inbound PDA 调用。后台覆盖 Master Data/Location、Inbound Records、Finished Goods Inventory、WeekCode、SAP Posting、Audit、Monitoring、HU Detail。
- 交叉核对：前端引用 57 个不同后端路由模板，均在当前 85 个 BaseApi 路由内；`FRONTEND_ROUTE_MISMATCH_COUNT=0`。WMS 前端区域旧 `/api/wms/`、`/api/health`、双前缀和独立 WMS 宿主引用均为 0。
- 验证：8 个 API JavaScript 文件及 7 个 PDA 内联脚本通过 Node syntax check。`Learun.Application.Web.csproj` 构建在编译前因既有 `..\\..\\..\\.nuget\\NuGet.targets` 缺失停止；未执行数据库操作。
- 报告：`WMS_FG_LEARUN_FRONTEND_API_MAPPING_REPORT.md`。结果：`WMS_FG_FRONTEND_API_MAPPING_COMPLETE=TRUE`；下一步建议 `WMS_FG_MASTER_DATA_BASELINE`，本任务未启动导入或业务写测试。- 部署路由确认：测试浏览器入口 `http://localhost:20471` 反向转发至 `http://localhost:31175`；正式浏览器入口 `http://10.9.10.135` 转发至 `http://10.9.10.135:1001`。WMS-FG 浏览器调用继续保持 `/api/wms-fg/...` 相对路径，禁止在前端硬编码后端端口。

## 2026-08-31 WMS-FG 本机反向代理核验

- 结论：当前 `localhost:20471` 的 WMS-FG 反向代理未配置，不能视为端到端可用。直接访问 `http://localhost:31175/api/wms-fg/health` 返回 HTTP 200 和 WMS 健康 `ApiResult`；访问 `http://localhost:20471/api/wms-fg/health` 返回 HTTP 404。
- 对用户提供的 Finished Goods Inventory URL 的浏览器实测显示 IIS `StaticFile` 处理器尝试读取 `D:\com.delton.gz\Learun.Application.Web\api\wms-fg\inventory\finished-goods\`，所以请求没有到达 `31175`。
- `Learun.Application.Web\Web.config` 未发现 WMS-FG URL Rewrite/ARR 转发规则。应在实际承载 20471 的 IIS 站点加规则：`^api/wms-fg/(.*)` -> `http://localhost:31175/api/wms-fg/{R:1}`，保留 query string；生产目标相应为 `http://10.9.10.135:1001/api/wms-fg/{R:1}`。依赖 IIS URL Rewrite 与 ARR proxy 已安装并启用。
- 本次仅核验，未修改 IIS、ARR、Web.config、后端或数据库。

## 2026-08-31 WMS-FG MVC 反向代理源码修复

- 已修改 Learun Web 的既有 MVC 代理链，而非引入 IIS ARR：`RouteConfig` 的代理入口由 `api/wms/{*path}` 改为 `api/wms-fg/{*path}`；`WmsProxyClient` 下游地址同步为 `/api/wms-fg/`。
- 代理允许方法由 GET/POST 扩展到 GET/POST/PUT；POST 与 PUT 均原样读取并转发请求体及 Content-Type。查询字符串、响应状态码、Content-Type 与响应体保持原有透传行为；不保留旧 `/api/wms` 别名。
- 配置：测试 `WmsServiceBaseUrl=http://localhost:31175`；生产配置文件新增 `WmsServiceBaseUrl=http://10.9.10.135:1001`。生产发布时必须以 `system-production.config` 替换运行时 `system.config`。
- 验证：后端 `31175/api/wms-fg/health` 返回 HTTP 200；代理三处源文件 Roslyn 语法检查无 C# 语法错误；旧代理前缀静态计数为 0。正常 Web Build 仍被既有缺失 `D:\.nuget\NuGet.targets` 在编译前阻断。
- 风险/待办：当前 `20471` 是完整 IIS，运行程序集未包含旧或新 MVC 代理路由，`/api/wms/health` 和 `/api/wms-fg/health` 均未进入 Controller。恢复 NuGet 构建入口后，需编译部署并回收该站点应用程序池，再复测 health 和 Finished Goods Inventory GET；未执行任何业务写请求、IIS 重启或数据库操作。

## 2026-08-31 WMS-FG MVC 代理部署验证

- 后续确认项目正确解决方案根为 `D:\com.delton.gz`；带该 `SolutionDir` 的 Learun Web Build 成功完成，`bin\Learun.Application.Web.dll` 已更新。
- `localhost:20471/api/wms-fg/health` 与用户提供的库存查询 URL 已不再被 IIS StaticFile 返回 404；未带 Learun 登录 Cookie 的请求均返回 HTTP 302 至 `/Login/Index`。这证明 `api/wms-fg/{*path}` 已由 Learun MVC 路由接管。
- 302 为既有宿主鉴权行为，未变更鉴权或白名单。已登录的 WMS 页面请求会进入 MVC 代理并转发到测试后端 `localhost:31175`；本次未使用认证会话及未执行任何业务写请求。

## 2026-08-31 Finished Goods Inventory SQL 语法修复

- 根因：`FinishedGoodsInventoryRepository` 将含 `;WITH` 的 CTE 与 `FROM/WHERE` 片段拼接在 `SELECT COUNT` 和分页子查询之后，生成了 CTE 前已有 SELECT、CTE 后直接 FROM 的非法 SQL；直接请求 `31175/api/wms-fg/inventory/finished-goods` 已复现同一 SQL Server `FROM`/`)` 错误，故非前端或 MVC 代理问题。
- 修复：将库存查询拆分为 `CurrentInventoryCteSql` 与 `CurrentInventoryFromWhereSql`。Count 以 CTE 后的完整 SELECT 起始；Rows 将 `InventoryRows` 追加到同一 CTE 链，保留原过滤、分组、分页与 DTO 映射。
- 同步文件：`D:\Code\WMS-HU-Model\WMS-HU-Model\Repositories\Impl\FinishedGoodsInventoryRepository.cs` 与运行时编译副本 `D:\com.delton.gz\Learun.Application.WebApi\Modules\WMS_FG\WMS-HU-Model\Repositories\Impl\FinishedGoodsInventoryRepository.cs`；更新源仓库静态 SQL 测试以覆盖 CTE 与 From/Where 的组合。
- 验证：`WMS-HU-Model.Tests` Build 成功，执行结果 `Passed 124 tests`。Learun WebApi Debug Build 成功（仅既有依赖版本/锁定文件警告），并生成 `Learun.Application.WebApi.dll`。
- 未完成的运行时验收：编译后 `localhost:31175` 由完整 IIS 承载，health 与库存 GET 当前无响应；IIS 站点/应用池映射读取和定向回收需要管理员授权，本次未获批准，故未重启服务、未执行任何业务写请求或数据库操作。管理员回收承载 31175 的应用池后，应先 GET `/api/wms-fg/health`，再以 `page=1&pageSize=1` 复测库存接口。

## 2026-08-31 Page04 入库记录箱明细子表

- 实现范围：Page04 Grid 左侧新增 Learun 原生 `+ / -` 子表入口；展开后复用既有 Receipt Detail GET 显示箱码、物流状态、物料/版本、数量/单位、内包数、生产周、异常摘要和有效货位。普通父行点击继续打开既有只读 Drawer。
- 前端 Foundation：`Wms.Grid` 增加 `subGrid`、`subGridHeight` 与 `onSubGridExpand(subId, row, grid)` 映射；展开图标不计入用户行点击意图。未修改 `jfgrid.js`、Pager 或 Drawer Foundation。
- 数据边界：仍只展示服务端 Detail 合同返回的前 50 箱；`hasMoreBoxes` 为真时页面明确提示截断。加载、空数据、请求失败和折叠/重绘后的失效异步响应均在行内处理。箱码跳转 Page03，并停止事件冒泡。
- Schema/API/后端/数据库：无变更；未执行数据库操作。
- 已执行验证：新增 `wms-page04-inbound-subgrid-harness.js` PASS；Page04 与 Foundation JS syntax PASS；Pager Foundation Harness PASS；Drawer Lifecycle D01–D12 与 Page Integration Harness PASS；Page04 受影响前端文件 UTF-8 BOM、CRLF、mojibake 静态检查 PASS；`git diff --check` 未报告空白错误。
- 未通过/环境限制：Pager Integration Harness 与 Visual Static Audit 均因当前 Page02 缺少其既有的 absolute-fill 高度匹配而失败，非本次 Page04 子表改动；Learun Web Debug Build 在 RestorePackages 阶段因 `D:\com.delton.gz\packages\Microsoft.AspNet.Razor.3.2.7\lib\net45\zh-Hans\System.Web.Razor.resources.dll` 访问被拒绝而未进入编译。
- Human Runtime 待验：`+ / -` 展开/折叠、30 箱显示、父行 Drawer 共存、箱码 Page03 跳转、超过 50 箱的截断提示、失败后的重新展开，以及查询/重置/Tab/翻页后的失效响应清理。

## 2026-08-31 Page04 子表运行态显示修复

- 根因证据：正式后端 `GET /api/wms-fg/inbound/finished-goods/receipts/1` 返回 HTTP 200、`code=200`、30 个 `boxes`，因此数据读取合同可用。旧实现将第二个 jfGrid 嵌入原生子表，而原 jfGrid CSS 对 `.jfgrid_chlidgird` 固定 `height:300px`，与 Page04 的 `subGridHeight:235px` 冲突。
- 修复：保留父 Grid 原生 `+ / -` 与 Detail GET；展开区改为 DOM 安全写入的受控 HTML 表格，并只在 Page04 将旧子表容器高度覆盖为 `100% !important`。表格有独立滚动区，保留 8 个箱事实列、前 50 箱提示、Page03 跳转、错误/空数据/失效响应保护和 Drawer 共存。
- 范围：未修改 `jfgrid.js`、API、DTO、后端、Proxy、数据库、Pager 或 Drawer Foundation；Human Runtime 仍需重新加载页面后验收。
- 已执行验证：Page04 子表 Harness PASS（受控表格、8 列、安全箱码、滚动区、高度覆盖、Drawer 共存）；Page04 JS syntax PASS；真实 GET Probe PASS（`ReceiptId=1`、HTTP 200、30 箱）；Pager Foundation P01–P10、Drawer D01–D12 与 Page Integration Harness 均 PASS；Page04 文件 BOM/CRLF/mojibake 检查 PASS。

## 2026-08-31 Page04 子表可见宿主修复

- Human Runtime 新证据：点击后 `+` 正常变为 `-`，235px 子行已插入，但主数据区域空白。jfGrid 源码确认真实回调内容位于左侧 `.jfgrid-subgird-cell`，同时在 `.jfgrid-scrollarea-content` 插入独立的空白占位单元格；后者覆盖了左侧内容层。
- 修复：Page04 通过左侧子行前一个 `.jfgrid-data-cell-last[rownum]` 定位主滚动区同一行末尾后的可见占位单元格，将加载、空数据、错误和箱表全部渲染到主区；左侧被覆盖的原生内容层隐藏。折叠或重绘后同时校验原生与可见宿主，禁止过期响应回写。
- 范围：仅 Page04 页面内部 DOM 适配与样式；未修改公共接口、`jfgrid.js`、API、后端、数据库、Pager 或 Drawer。
- 验证：可见宿主 Harness PASS；Page04 JS syntax PASS；正式后端 Receipt Detail Probe PASS（`ReceiptId=1`、30 箱）；Pager P01–P10、Drawer D01–D12 和 Page Integration 均 PASS；UTF-8 BOM、CRLF、mojibake 与 `git diff --check` PASS。浏览器控制插件初始化失败，未宣称视觉 Runtime 已通过。

## 2026-09-01 出货管理页面 V1

- 实现范围：将既有 SAP 过账队列壳子升级为 Learun PC 业务页；包含组合查询、六类业务状态筛选、服务端分页主 Grid，以及交货单行/拣货明细/SAP过账三个底部页签。首行响应后自动读取明细。
- 后端：新增 typed `ShipmentManagementQuery`、列表与明细 DTO；新增 `GET /api/wms-fg/outbound/shipment-management/orders` 和 `GET /api/wms-fg/outbound/orders/{vbeln}/shipment-detail`。Repository 使用 typed `SqlParameter` 和只读 ADO.NET 查询；未引入 dynamic/AddWithValue。
- 冻结事实：拣货箱的 INNER_PACK/周期码/内部批次/CHARG 来自 `WMS_FG_OutboundPickDetailItem`，其中 CHARG 明确取 `SAPBatchNo`，不查询当前可变库存。
- 写动作：PICKED+READY 的“确认出货”仍调用原 `confirm-shipment` 服务；FAILED/UNKNOWN 仅跳转按 VBELN 过滤的 SAP 日志，本页没有 Retry/人工确认；SUCCESS/SHIPPED 只读。
- Schema/API 兼容：无数据库 Schema、migration 或现有写 API 变更；未执行数据库写入、migration 或真实 SAP 调用。运行时镜像已同步至 Learun WebApi WMS_FG 模块。
- 自动验证：新增 10 个 Shipment Management contract tests，完整测试结果 `Passed 134 tests`；WMS-HU-Model、WMS Host Debug build 通过；新页面/API JavaScript syntax 通过。Learun WebApi 与 Web 完整构建结果见最终任务记录。
- Human Runtime 待验：真实列表/日期过滤、首行选择视觉、箱子展开、日志 Tab 跳转、PICKED+READY 确认弹窗及成功后的列表/明细刷新。严禁用本次验收触发真实 SAP，除非 Human 明确授权并准备测试单。
- 文档：`docs/frontend/pages/08_Shipment_Management_Page.md`。Git commit：本次未创建提交。

## 2026-09-01 WMS_FG BoxCodeSequence Final Schema 对齐

- 根因：`POST /api/wms-fg/repack/merge-box` 的唯一生产序列分配器仍把 `BusinessDate/PlantCode/CurrentNo` 当作 `dbo.WMS_FG_BoxCodeSequence` 物理列，和已冻结的 `SequenceKey/CurrentValue` Final Schema 不一致。
- 修复：保持原“自然日 + InnerPack PlantCode”scope 与既有 `B+yyMMdd+四位流水+厂别后缀` 箱码格式；新增确定性 key `OUTER_BOX|{PLANT}|{yyyyMMdd}`，SQL 改为 `SequenceKey/CurrentValue`，并补齐 `CreateBy/LastEditBy`。
- 并发/幂等：仍使用同一个 Serializable 合箱事务与 `UPDLOCK,HOLDLOCK`；RequestId 的事务内复查发生在序列申请之前。序列更新与 HU/关系/MoveLog/OperationLog 同事务，后续失败一并回滚。
- 同步：standalone Model 和 Learun WebApi WMS_FG 编译副本的规则/Repository 文件哈希一致；未发现第二个 Runtime OUTER_BOX generator。
- 静态结果：序列表 SQL 中 `BusinessDate/PlantCode/CurrentNo` 物理引用均为 0；无 `dynamic`、`AddWithValue`、`MAX(HUCode)`，未修改 migration 或 Final Schema。
- 验证：Model、Tests、WMS Host Debug build PASS；完整 138 项执行为 137 PASS、1 个既有无关前端断言失败。临时排除该断言后 `Passed 137 tests`，随后已恢复测试源。Learun WebApi 到 C# 编译阶段无源码错误，最终因 `obj/Debug/Learun.Application.WebApi.dll` 访问被拒绝报 CS2012。
- 未执行：真实 merge POST、WMS_DEV DML/DDL、migration、DB-backed 并发测试、Git stage/commit。目标环境需验证连续两个 RequestId 取得 N/N+1，重放 RequestId 不再次消费序列。
- 报告：`WMS_FG_BOX_CODE_SEQUENCE_FINAL_ALIGNMENT_REPORT.md`。

## 2026-09-02 出货管理 V1 接口审计与完整对接

- 审计结论：`shipping-orders` 后端存在但 Repository 仍为空分页 stub；既有 `shipment-management/orders` 与组合 `shipment-detail` Route 可复用，因此未新增 Route。PDA `picking-state`、`shipping-state`、confirm-shipment、SAP logs/recovery 均保持原 contract/状态机。
- Contract：Shipment Management 页面 DTO 独立使用 `SapDeliveryNo/SapPostingStatus/SapItemNo/SapBatchNo`，JSON 为 `sapDeliveryNo/sapPostingStatus/sapItemNo/sapBatchNo`；未全局重命名 legacy DTO。
- 列表：customer/material/date/SAP/business 等条件、COUNT、全过滤范围 summary、OFFSET/FETCH 均在 SQL Server；PickedQty 来自 frozen PickDetailItem.AllocatedQty，PickedBoxCount 为 distinct frozen BoxHUId。
- 明细：一次请求返回 lines/boxes/posting；一物理箱一父 DTO，冻结 item 为 children；CHARG 来自 PickDetailItem.SAPBatchNo，来源库位来自 Pick MoveLog.FromLocation，日期字段明确为 DateCode。MaterialRevision 未持久化时返回 null，不从当前库存推断。
- 页面：Tab 顺序/默认值为 LINES,PICKING,POSTING / lines；summary 改为服务端；FAILED/UNKNOWN 仍仅跳 SAP 日志，confirm 继续复用现有接口。
- 验证：基线 138，完成后 141/141 PASS；Model、Test、WMS Host、Learun Web build PASS。Learun WebApi 完成 CoreCompile 且本轮源码无错误，但运行中宿主占用 `obj/Debug/Learun.Application.WebApi.dll`，最终 CS2012，未部署新 DLL。
- Read-only runtime：旧宿主 list GET 返回 200 但无 summary/目标单 total=0，detail GET 返回 404，证明仍加载旧 DLL；部署/重启后必须只读复验 8300999010（若仍存在）的 2 box / 6 frozen item。
- 安全：无 schema/migration、无 DML/DDL、无真实 SAP、未执行 confirm-shipment、未 stage/commit。
- 报告：`WMS_FG_SHIPMENT_MANAGEMENT_V1_AUDIT_REPORT.md`。

## 2026-09-02 正式出库扫码 PickBatch Final Schema 修复

- Runtime 故障：`POST /api/wms-fg/outbound/pick-batches/{pickBatchNo}/scan` 在进入扫码写事务前，由 `GetPickBatchCustomerCode` 报 `列名 'OutboundOrderId' 无效`。
- 根因：旧 SQL 读取不存在的 `WMS_FG_OutboundPickBatch.OutboundOrderId`。Final Schema 权威关联为 `PickBatch.AllocationId → Allocation.OutboundOrderId → OutboundOrder.Id`；OrderLine/Allocation 等表的合法 `OutboundOrderId` 不受影响。
- 修复：standalone 与 Learun WebApi WMS_FG Repository 均改为经 Allocation 读取 CustomerCode；API、Service、DTO、Schema 和状态机未改变。
- 回归守卫：新增专项 SQL topology 测试，并扩展 Runtime Final Schema guard 禁止 `b.OutboundOrderId`；生产 Runtime 扫描计数为 0。
- 验证：完整 `142/142` PASS；Model、Test、WMS Host build PASS。Learun WebApi 本轮源码无编译错误，最终仍因运行中宿主占用 `obj/Debug/Learun.Application.WebApi.dll` 报 CS2012。
- Runtime：未执行扫码 POST、DML/DDL、migration 或 SAP。原失败发生在任何扫码业务写入前，部署新 DLL 后可由 Human 使用原 RequestId 重试并核对 PickDetail/Item、Reservation、位置与日志。
