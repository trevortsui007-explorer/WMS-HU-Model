# WMS 成品仓正式出库后端执行计划

状态：APPROVED / ACTIVE  
业务依据：`docs/outbound/WMS_成品仓出库后端搭建需求文档_v1.0.md`  
数据库依据：已部署 010–013，后续只允许新增 014+ 纠正迁移。

## 2026-08-14 执行状态

- 已完成：014 兼容迁移、周码强类型边界、HU Identifier、正式精确组合算法。
- 已完成：订单列表/详情/VOID、Preview、StartPicking、Reservation、Release 与 SQL 过期过程。
- 已完成：扫码、POSNR/SAPBatchNo 快照分摊、异常退回、质量 Hold/Release、Replacement、ReturnToStock、显式 SHIPPED。
- 已完成：旧 pick-next/pick-batch 写入口停用；queue/plan 保留只读诊断。
- 已验证：VS 2022 MSBuild Debug/Release 构建；无外部测试框架的算法可执行测试 6/6 通过。
- 未完成（外部合同阻塞）：SAP Receive DTO/字段映射/规范化哈希与 MES GiftQty 适配器。未提供真实合同前不生成猜测实现。
- 未验证（环境阻塞）：014 实库双执行、Repository/并发/Postman 流程。`WMS_DEV` 集成认证仍在握手阶段失败。

## 已确认边界

- 正式链路：SAP Receive → WAIT_PICK → Preview → StartPicking → Reservation → Scan → PICKED → 显式 SHIPPED。
- 分配按 `MATNR + UOM` 聚合；Revision 不参与分组；整单受 WERKS/LGORT 限定。
- 每个 OUTER_BOX 只有一个 MATNR，可混 WeekCode 和 SAPBatchNo。
- FIFO 使用箱内 Active Item 的最小 WeekSortKey；缺失周码直接阻断，不回退 CreateTime。
- 只允许完整 OUTER_BOX 出库；完整箱不精确时检查完整 INNER_PACK 组合，但不拆 INNER_PACK 数量。
- 合法结果仅为 EXACT_WHOLE_BOX、REPACK_REQUIRED、SHORTAGE、NO_EXACT_PACK_COMBINATION。
- 同 MATNR 多 POSNR 按 SAPItemNo 升序做账务分摊；物理 INNER_PACK 不拆。
- Reservation 有效期 3 小时；首箱扫码后停止自动过期。
- 旧 queue/plan 只读保留；pick-next/pick-batch 停止生产写入。
- Hold/Release v1 仅作用于 OUTER_BOX；仅 QUALITY_FAILED 自动 Hold。
- Replacement 可由多个整箱补足，所有异常历史保留。
- SHIPPED 使用不可扫描虚拟货位；SAP Picking/PGI 与冲销不在本阶段。
- SAP 批次拆分来源为 `WMS_HU_Item.SAPBatchNo`。

## 实施阶段

### 0. 基线

- 保存本计划。
- 获取真实 SAP 脱敏报文/映射、MES GiftQty 与 WeekCode 契约、目标库 DDL。
- 外部契约未提供时只实现强类型边界，禁止猜测或提供伪实现。

验收：外部未决项被显式阻断并记录。

### 1. 014 兼容迁移

- 保留 009 历史，修正待扫码 PickBatch 无法保存 `PickedQty=0/PickTime=NULL` 的约束。
- 固化正式 Batch/Detail 状态约束。
- 扩展 PickDetailItem 的 BatchNo/SAPBatchNo 快照。
- 增加异常与多个 Replacement 的关联表。
- 增加 Release/VOID/SHIPPED 幂等字段。
- 创建每仓库 SHIPPED 虚拟货位。
- 创建可重入 Reservation 过期过程并记录 SchemaVersion。

验收：迁移可重复执行、无历史删除、旧数据仍可查询。

### 2. 周码与 Identifier

- Item 写入链固化 DateCode 原值及标准周码字段。
- 提供 WeekCode Provider/Resolver，不识别格式时失败。
- 提供 HUCode 优先、Active Identifier 次级的解析服务。

验收：周码不回退 CreateTime；标识唯一约束和客户上下文生效。

### 3. SAP 接单、查询与 VOID

- 规范化业务字段并按 POSNR 排序计算 SHA-256。
- 同 VBELN 同内容幂等成功，不同内容拒绝。
- GiftQty 由独立 Provider 获取并固化；失败不留半张订单，失败日志独立保存。
- 提供订单列表、详情和 WAIT_PICK VOID。

验收：接单不移动、不预留；RequestId 和 VBELN 幂等通过。

### 4. Preview

- 过滤 STORAGE、ACTIVE、AVAILABLE、非 Hold、未预留、正确 WERKS/LGORT 的完整箱。
- 计算箱级周码特征，执行不超过 100 候选的确定性精确组合。
- Preview 零写入。

验收：四种正式结果、混周码、缺周码、混 MATNR、库存不足和组合不足均覆盖。

### 5. StartPicking 与 Reservation

- 锁定整张订单并重新计算；任一物料失败则全部回滚。
- 成功创建一个 Allocation、每组一个 Batch、每箱一个 Reservation。
- Release/Expiry 保留 Batch/Reservation 历史并将订单退回 WAIT_PICK。

验收：并发只允许一份有效分配；同箱最多一个 Active Reservation。

### 6. 扫码

- HUCode/Identifier 解析后只接受当前 Allocation 的 Active Reservation。
- 单事务写 MoveLog、CurrentLocation、OperationLog、Reservation、PickDetail、PickDetailItem 和数量状态。
- 首箱进入 PICKING，全部完成进入 PICKED。

验收：重复请求幂等；位置变化或错误箱不产生部分写入；POSNR/SAPBatchNo 分摊正确。

### 7. 异常与 Hold

- 固定原因：QUALITY_FAILED、ORDER_CHANGED、WRONG_PICK、MANUAL_EXCEPTION。
- 异常箱进入 RETURNED；质量失败自动 Hold；Release 后才能参与 FIFO。
- Replacement 重新 FIFO 分配，可关联多个替代箱。
- ReturnToStock 优先原货位，否则要求实体 STORAGE 货位。

验收：EffectivePickedQty 可回退，HistoricalPickedQty 只增不减，历史不可删除。

### 8. SHIPPED

- 仅全单 PICKED、数量精确且无未决异常时允许显式发货。
- 每箱从 OUTBOUND-STAGING 移到 SHIPPED，写移动和操作日志。

验收：重复请求不重复移动；SHIPPED 后普通取消/退回被拒绝。

### 9. 验证与交接

- VS 2022 MSBuild Debug/Release 构建。
- 单元、数据库、并发和 Postman 流程测试。
- 检查 dynamic、AddWithValue、事务、SQL 类型及旧式 csproj 文件登记。
- 更新 `docs/handoff/CODEX_HANDOFF.md`。

## 外部阻塞项

- 目标 WMS_DEV 当前 Windows 集成认证失败，尚不能执行真实 schema/迁移测试。
- 尚未提供 SAP 真实报文与最终字段映射。
- 尚未提供 MES GiftQty/WeekCode 查询契约和全部格式样例。
- SQL 过期过程的外部调度账号与频率待运维配置。
