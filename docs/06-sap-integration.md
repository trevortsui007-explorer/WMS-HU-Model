# 06 - SAP Integration

## 当前边界

WMS 负责：
- HU 管理
- 包装关系
- 货位
- 上架 / 移库 / 拣货 / 出库现场执行
- MoveLog / OperationLog
- 现场库存事实

SAP 继续负责：
- 财务库存账
- 销售库存账
- Plant / Storage Location
- 物料、批次、工单等 SAP 业务维度

## 后续方向

WMS 需要把 HU 级别的现场事实聚合成 SAP 可接受的库存移动维度，例如：
- Plant
- LGORT
- Material
- Batch
- Production Order
- Qty
- UOM
- Movement Type

后续计划：
- SAP Posting
- Interface Outbox
- Retry / Idempotency
- Request / Response Log
