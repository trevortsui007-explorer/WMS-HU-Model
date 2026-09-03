# 出货管理页面 V1

## 定位

PC 端 Learun 业务页面，服务于仓库主管和出货操作员。页面按“查询与状态汇总 → 出货单分页列表 → 选中单据业务明细”组织，不作为 SAP 技术日志页。

## 数据与动作边界

- 主列表：`GET /api/wms-fg/outbound/shipment-management/orders`，服务端分页并支持交货单、客户、物料、厂别、仓库、业务状态、SAP 状态和计划出货日期过滤；`summary` 始终按完整过滤范围统计，不按当前页统计。
- 明细：`GET /api/wms-fg/outbound/orders/{vbeln}/shipment-detail`，一次返回交货单行、冻结拣货箱/内包事实和 SAP 业务结果。
- CHARG 来源固定为 `WMS_FG_OutboundPickDetailItem.SAPBatchNo`，不得从当前库存反推。
- 确认出货继续调用既有 `POST /api/wms-fg/outbound/orders/{vbeln}/confirm-shipment`。
- FAILED/UNKNOWN 在本页仅允许跳转 SAP 接口日志；不提供 Retry 或人工确认。
- SUCCESS/SHIPPED 只读。

## 状态口径

- WAIT_PICK → 待拣货。
- ALLOCATED/PICKING → 拣货中。
- PICKED + 无 Posting/READY → 待确认出货。
- PICKED + SENDING → SAP处理中。
- FAILED/UNKNOWN → SAP异常。
- SHIPPED/SUCCESS → 已出货。

## 明细层级

页签固定为“交货单行 / 拣货明细 / SAP过账”，默认“交货单行”。每个 OUTER_BOX 是一个父行，可展开冻结 PickDetailItem 子项，显示 SAP 行号、物料/版本、日期码、内部批次、冻结 `sapBatchNo`(CHARG)、数量与单位；不把一个箱的多个 CHARG 扁平化成多个父行。交货单行没有持久化 MaterialRevision 时返回空值，不从当前库存推断。原始 request/response payload 不在业务页展示。

## 页面专用 JSON Contract

- 页面 DTO 使用 `SapDeliveryNo`、`SapPostingStatus`、`SapItemNo`、`SapBatchNo`，camelCase JSON 固定为 `sapDeliveryNo`、`sapPostingStatus`、`sapItemNo`、`sapBatchNo`。
- 该命名仅用于 Shipment Management read model；不重命名 PDA/SAP 既有 DTO。
- `PickedQty`、`PickedBoxCount`、BOX/CHARG/来源库位/物料/版本/日期码都来自冻结拣货链；不从当前 `HU_Item` 或 CurrentLocation 重建历史。

## Human Runtime 待验

需在已部署、已登录 Learun 环境验证首行自动加载、分页/筛选、箱展开、日志跳转自动带 VBELN，以及 PICKED+READY 的真实确认弹窗和完成后刷新。本阶段未调用真实 SAP。
