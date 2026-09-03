# 成品库存 / HU 查询 V1

## Purpose

提供当前成品库存的统一只读查询入口。主表以 OUTER_BOX 为粒度，一次返回箱、物料、当前有效数量、有效货位、库存状态、Hold、周码摘要、父托盘和创建时间；用户真实单击箱行后再读取 HU Quick Preview。

页面路由：`/WMS/FinishedGoodsInventory/Index`。

## Business Scope

- `READ_ONLY_PAGE=True`
- 当前库存查询，不是入库、移动、Hold、拣货或出库操作页。
- 不提供 Move、Hold、Release、Putaway、Pick、Ship、Cancel、Export 或批量动作。
- 不新增 schema、table、inventory snapshot、ledger 或业务状态。
- Learun 只通过 `Wms.Api` 调用相对 `/api/wms/*`；`DIRECT_DB_FROM_LEARUN=False`。

## CURRENT_HU_READ_APIS

以下清单来自当前 Nancy Module、Service、Repository 和 DTO，不依据历史 Prompt 猜测 URL。

| Method | Route | Request | Response data | 用途 |
| --- | --- | --- | --- | --- |
| GET | `/api/wms/hus/{huCode}` | path `huCode` | `HuDto` | 按编码读取 HU 基础信息 |
| GET | `/api/wms/hus/{huCode}/tree` | path `huCode` | `HuTreeNodeDto` | 读取当前有效 HU 包装树及节点 Item |
| GET | `/api/wms/hus/{huCode}/location` | path `huCode` | `HuLocationDto` | 读取直接位置、有效位置及父级位置来源 |
| GET | `/api/wms/hus/{huCode}/moves` | path `huCode`, query `top` | `List<HuMoveLogDto>` | 读取最近移动日志 |
| GET | `/api/wms/locations` | query `warehouseCode`, `zoneCode` | `List<LocationDto>` | 查询 WMS 货位主数据 |
| GET | `/api/wms/locations/{warehouseCode}/{locationCode}` | path warehouse/location | `LocationDto` | 读取单一货位 |
| GET | `/api/wms/repack/boxes/{boxHuCode}` | path `boxHuCode` | `BoxRepackStatusDto` | 读取箱、位置、InnerPack 数量的拆包状态 |
| GET | `/api/wms/weekcode/audit` | audit filters + paging | `WeekCodeAuditResultDto` | 仅查询周码异常，不是完整库存列表 |

调查结论：上述接口均为单 HU、主数据、移动日志、拆包状态或异常审计接口；此前没有同时支持库存 Grid 字段、筛选和服务端分页的 current inventory query。`EXISTING_INVENTORY_QUERY_API_FOUND=False`。

V1 新增只读接口：

| Method | Route | Request | Response data | 用途 |
| --- | --- | --- | --- | --- |
| GET | `/api/wms-fg/inventory/finished-goods/` | `plantCode/materialCode/materialRevision/boxCode/innerPackCode/locationCode/weekCode/stockStatus/inboundState/isHold/page/pageSize` | `FinishedGoodsInventoryResultDto` | 一次返回 OUTER_BOX 当前库存分页 Grid 所需全部字段；`inboundState=PENDING` 仅返回有效货位 `INBOUND-PENDING`，`INBOUND` 仅返回有效非待入库货位 |
| GET | `/api/wms-fg/inventory/finished-goods/{boxCode}/preview` | path `boxCode` | `FinishedGoodsInventoryPreviewDto` | 单击箱行后聚合基础信息、位置、周码组和 InnerPack |

两者均使用 `{ code, message, data }` envelope；浏览器中的业务 DTO 为 camelCase。

## Inventory Semantics

当前库存严格复用已有 WeekCode Recalculate 的 effective-location 口径：

1. 根 HU 为 `OUTER_BOX` 且 `LifecycleStatus = ACTIVE`。
2. 箱到 `INNER_PACK` 的 Relation 必须 `IsActive = 1`。
3. `INNER_PACK` 必须 `LifecycleStatus = ACTIVE`。
4. Item 必须 `IsActive = 1`。
5. 必须存在有效位置：箱直接 CurrentLocation 优先，否则回退到有效父 HU 的 CurrentLocation。
6. effective LocationCode 为 `SHIPPED` 时排除；SHIPPED 是 V1 当前库存硬边界。
7. Hold 不排除；Hold 是当前库存状态事实并在 Grid/Preview 显示。

SQL 字段和长度已与冻结证据 `.codex/evidence/wms-dev-schema.json` 对照。本轮没有执行 migration、DML 或 DDL。

## Grid Contract

- 粒度：`1 row = 1 OUTER_BOX`；PALLET 仅作为 Parent Pallet 信息。
- 分页：`page/pageSize`，默认 `pageSize=50`，最大 200；响应为 `totalCount/page/pageSize/items`。
- 字段：箱码、厂别、物料、版本、数量、UOM、周码摘要、有效货位、库存状态、Hold、父托盘、创建时间。
- 筛选：厂别、物料、版本、箱码、货位、Hold；高级条件为周码、InnerPack、库存状态。
- 主表无逐行 Location/Week/Hold 请求，不存在 N+1。
- 查询、重置、错误和翻页均关闭 Drawer，避免显示旧行 Preview。

`FOUNDATION_PAGER_VISUAL_DEFERRED=True`。Page 02 保持完整 server paging contract，不重新设计 Foundation pager。

## Quantity Aggregation

箱数量为其所有当前有效 INNER_PACK 下有效 `WMS_HU_Item.Qty` 的 `SUM`。物料、状态等筛选使用 EXISTS 选择箱，不会把箱数量缩减为匹配子集，也不读取 MES 历史 `UnitQty`。

Runtime 样本 `B1951228B`：Grid/Preview 箱数量 320，4 个有效 InnerPack Qty 合计也是 320。

## Location Semantics

Repository 返回 `EffectiveLocationCode/WarehouseCode/ZoneCode/LocationTypeCode/IsLocationInherited`。前端不计算 parent fallback。

Runtime 样本 `B1951228B` 的 `IsLocationInherited=True`，有效位置为父级回退得到的 `INBOUND-PENDING`，验证了箱无直接位置时的 pallet fallback 路径。

## WeekCode Summary

Grid 返回箱下所有有效 Item 的 distinct 周码摘要与数量：单周码直接显示 `YYYY-Www`，多周码返回逗号分隔列表；未解析事实显示“未解析”。Preview 按 WeekYear、WeekNo、WeekSortKey 分组并返回 PackCount 与 Qty，因此支持一个箱包含多个 WeekCode，不拆箱、不覆盖现有周码模型。

## HU Preview Contract

`FinishedGoodsInventoryPreviewDto` 只包含：

- `box`：Grid 行的完整只读库存事实。
- `weekCodes`：WeekCode、WeekSortKey、PackCount、Qty。
- `innerPacks`：PackHuId、PackCode、Qty、UOM、DateCode、WeekCode、WeekSortKey、StockStatus、Hold。

Reservation/Allocation 未加入 V1，因为现有轻量 HU read API 无单箱可靠聚合合同；本页没有为此重建出库 Reservation UI。

## Drawer Lifecycle

- 页面首次加载、查询、Grid render、Reset、翻页：Drawer closed。
- jfGrid 的 programmatic selection 不打开 Drawer。
- 页面在 grid cell `mousedown` 记录短时真实用户意图，只有紧随其后的 `onRowClick` 才调用 `Drawer.open()`。
- Drawer 内单次请求遵守 Loading → Response → Render/Error → Close；新查询会使旧 Preview response 失效。
- Drawer 是 PREVIEW ONLY，不是操作结果容器。

## Permissions

MVC Controller 继承 `MvcControllerBase`，复用 Learun 登录与授权入口。页面仅有查询和查看详情，无写按钮、无按钮权限数据变更、无菜单数据库变更。

## Verification

- `WMS-HU-Model` Debug build PASS。
- `WMS` Nancy Host Debug build PASS。
- `Learun.Application.Web` Debug build PASS；仅有既有 warnings。
- 控制台自动化：43 tests PASS；新增覆盖分页默认/边界、筛选传递、OUTER_BOX/ACTIVE/Item/Relation、SHIPPED 排除、Item Qty SUM、effective-location SQL 对齐。
- Runtime read-only GET：分页查询 HTTP/API 200，TotalCount 1136；page 1/page 2 无重复；箱码与物料筛选有效；Preview HTTP/API 200；Qty 与 pack sum 一致；父级位置回退成立。
- 两个新增 JS `node --check` PASS；UTF-8 BOM + CRLF PASS；mojibake 静态扫描 0。
- 未执行任何 POST、DML、DDL 或 migration。

## Known Limitations

- Foundation pager visual 仍是 deferred issue；server paging contract 和请求行为已实现。
- 没有 Reservation/Allocation 摘要。
- 未注册正式菜单或修改权限数据库；通过直达 URL 进行 Human Review。
- Codex in-app Browser 在本会话因浏览器运行环境初始化冲突未能执行 UI01–UI06，不能宣称登录态视觉/交互验收通过。

## Deferred Foundation Issues

- Pagination visual。
- WeekCode Drawer lifecycle。
- WeekCode Recalculate final result 从 Drawer 迁移到 Modal。
