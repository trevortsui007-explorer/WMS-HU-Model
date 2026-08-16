# WMS 成品仓正式出库后端交接

日期：2026-08-14

## 已实现

- 新增 `014_OutboundV1Compatibility.sql`，保留 009 历史并修正待扫码 Batch、正式状态、SAPBatchNo 快照、多 Replacement、动作幂等、每仓 RETURNED/SHIPPED 以及原子过期过程。
- Item 写入链接入 WeekCode Resolver；没有 MES Provider 时明确失败，不回退 CreateTime。
- HUCode 优先、客户上下文 Identifier 次级解析。
- `MATNR+UOM` 正式 Preview 与确定性精确组合，结果只使用四种正式类型。
- StartPicking 在 Serializable 事务内重算整单，创建 Allocation/Batch/Reservation；Release、Expiry 和 WAIT_PICK VOID 保留历史。
- 扫码事务原子更新 MoveLog、CurrentLocation、OperationLog、Reservation、PickDetail、PickDetailItem、Line/Batch/Order；按 SAPItemNo 升序分摊并快照 BatchNo/SAPBatchNo。
- 单箱异常退回、QUALITY_FAILED 自动 Hold、人工 Hold/Release、多箱 Replacement、ReturnToStock 和显式 SHIPPED。
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
- 新增 C# 文件均已显式登记到旧式 `.csproj`。
- 新正式链路未使用 `dynamic` 或 `AddWithValue`；Nancy 路由参数采用两步转换。

## 阻塞与风险

- 测试库 `WMS_DEV` 连接仍失败。System.Data.SqlClient 报 `Cannot generate SSPI context`；ODBC 17 报客户端加密/SSL 凭据不可用。因此 014 尚未在实库执行，所有 Repository SQL、约束与并发流程仍需数据库集成验证。
- 未提供 SAP 真实脱敏报文、外层 JSON、最终字段类型/映射，SAP Receive 入口没有猜测实现。
- 未提供 MES GiftQty 与 WeekCode 合同；默认 WeekCode Provider 会明确阻断带 DateCode 的新 Item 写入。
- 外部调度器、执行账号与 Reservation 过期频率未配置。
- 需求示例称 `40+30+30` 在需求 60 时无完整 INNER_PACK 组合，但数学上 `30+30=60`。实现按每个实际 INNER_PACK 均可独立选择，因此会返回 REPACK_REQUIRED；需业务人工确认示例是否有未写出的限制。
- `ExceptionType` 的业务枚举未定义，异常退回 API 要求调用方显式提供；系统只固定校验四个 ReasonCode。

## 下一步人工验收顺序

1. 修复测试机 SSPI/ODBC 环境，仅以只读方式导出现有 DDL并与 014 对比。
2. 在测试库连续执行 014 两次，核查 009 历史、约束、索引、RETURNED/SHIPPED 和过期过程。
3. 提供 SAP/MES 合同后补 Receive 与 Provider，不修改已应用的 010–014。
4. 准备含混周码、多 POSNR、多 SAPBatchNo、异常与多 Replacement 的测试数据，执行 Repository、并发及完整 API 流程。
