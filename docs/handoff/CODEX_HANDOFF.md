# WMS 成品仓正式出库后端交接

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
- Git commits：本变更未创建提交。
