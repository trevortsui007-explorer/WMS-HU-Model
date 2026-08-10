# 02 - Database Design

## 当前核心表

### Master Data
- WMS_Warehouse
- WMS_WarehousePlant
- WMS_Zone
- WMS_Location
- WMS_PackageSpec

### HU Core
- WMS_HU
- WMS_HU_Relation
- WMS_HU_Item
- WMS_HU_OperationLog

### HU Location
- WMS_HU_CurrentLocation
- WMS_HU_MoveLog

## 关键约束
- `(WarehouseId, LocationCode)` 唯一
- `HUCode` 唯一
- 一个 Child HU 同时只允许一个有效 Parent HU
- 一个 HU 同时只允许一条 CurrentLocation
- MoveLog 只追加，不覆盖历史
- RequestId 用于移动幂等
