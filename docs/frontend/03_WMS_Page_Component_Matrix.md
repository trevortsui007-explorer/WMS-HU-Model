# WMS 7 Page Component Matrix

| 页面 | UI需求 | 力软已有组件 | 推荐方案 | 是否需要自建 |
|---|---|---|---|---|
| 01 成品库存 / HU查询 | 多条件折叠查询、服务端分页、状态、点击详情、批量选择 | lr-layout/query CSS、jfGrid、lrselect、lrdate、auth | `WmsSearchPanel + WmsGrid + WmsStatusTag + WmsSideDrawer` | 是：StatusTag、Drawer、DetailPanel |
| 02 HU详情 / 履历 | Key/Value、PALLET→BOX→PACK 树、时间轴、移动/操作记录 | lrtree、lrtimeline、jfGrid、layer | `WmsDetailPanel + WmsHuTree(lrtree) + WmsTimeline(lrtimeline)` | 是：DetailPanel；Tree/Timeline 仅包装 |
| 03 周码异常排查与重算 | 查询、问题分组、diff、确认、批处理结果 | jfGrid、layerConfirm、loading、alert | `WmsGrid + WmsAuditDiffCell + WmsConfirmDialog + WmsResultPanel` | 是：Diff、ResultPanel |
| 04 成品入库记录 / 异常 | 日期/物料/HU 查询、主从表、异常详情、导出 | lrdate、lrselect、jfGrid、Excel export | `WmsSearchPanel + WmsGrid(master/detail) + StatusTag` | 是：StatusTag/DetailPanel |
| 05 基础数据维护 | 列表 CRUD、表单校验、级联下拉、权限 | WarehouseLocation pattern、form mapping、validator、layerForm | `WmsGrid + WmsDialog + WmsFormField/WmsSelect` | 否；仅 wrapper/skin |
| 06 系统 / 接口运行监控 | KPI、成功/失败分区、趋势、错误详情/JSON | ECharts、jfGrid、Interface Mistake 双表 pattern | `WmsMetricCard + ECharts + WmsGrid + WmsJsonViewer` | 是：MetricCard、JSON Viewer |
| 07 日志审计中心 | 日期/用户/动作查询、分页、JSON、字段 diff、导出 | System Log、UserCenter Log、jfGrid、lrdate、Excel | `WmsSearchPanel + WmsGrid + WmsSideDrawer + JsonViewer + AuditDiffCell` | 是：Drawer、JSON、Diff |

## Common Component Decisions

| Component | Decision | Reason |
|---|---|---|
| WmsSearchPanel | WRAP_AND_REUSE | 复用 `.lr-layout-tool` / query pattern，但统一折叠、重置、Enter 查询和 responsive skin |
| WmsGrid | WRAP_AND_REUSE | jfGrid 行为成熟；补 loading/empty/error、状态 formatter、标准分页参数和销毁约定 |
| WmsStatusTag | CUSTOM_BUILD | 现有状态多用 formatter 内拼色，缺少统一语义和色板 |
| WmsSideDrawer | CUSTOM_BUILD | 未发现成熟 Drawer；iframe layer 不适合所有详情浏览 |
| WmsDetailPanel | CUSTOM_BUILD | 统一 Key/Value 密度、复制值、空值和分组 |
| WmsTimeline | WRAP_AND_REUSE | `lrtimeline` 有真实工作流使用；需 WMS data adapter 与 skin |
| WmsHuTree | WRAP_AND_REUSE | `lrtree` 已支持搜索/checkbox/async；限定节点规模并提供 lazy contract |
| WmsJsonViewer | CUSTOM_BUILD | 未发现通用 JSON pretty/collapse/copy/error viewer |
| WmsConfirmDialog | WRAP_AND_REUSE | `learun.layerConfirm` 成熟；增加 danger/typed confirmation 和 pending lock |
| WmsResultPanel | CUSTOM_BUILD | 无 boxCount 等结构化操作结果组件 |
| WmsMetricCard | CUSTOM_BUILD | 现有 dashboard pattern 不足以形成统一 KPI 组件 |

## Page-level Guardrails

- 任何列表默认服务端分页；HU Tree 可 lazy load，不一次加载整仓层级。
- 所有改变状态的操作都经 `WmsConfirmDialog`，提交后锁按钮并显示 loading。
- Status 颜色不能作为唯一信息载体，必须保留可读文本/图标。
- Drawer 只承载查看和轻量编辑；复杂流程继续使用 `WmsDialog` iframe form。
- 监控页 ECharts 仅做摘要，原始记录仍由 Grid 提供可检索证据。
