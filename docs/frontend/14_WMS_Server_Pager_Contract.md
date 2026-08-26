# WMS Server Pager Contract

## 1. Baseline and data mode

```text
WMS_FOUNDATION_PAGER_VISUAL_BASELINE=LEARUN_JFGRID_NATIVE_STYLE
REFERENCE_PAGER_DATA_MODE=JFGRID_NATIVE_URL_AJAX
WMS_PAGER_DATA_MODE=EXTERNAL_SERVER_PAGING
JFGRID_NATIVE_PAGING_ENABLED_FOR_WMS=False
```

视觉来源是 Learun “接口上传执行日志”页及 `Content/jquery/plugin/jfGrid/jfgrid.js/css` 的 native pager。WMS 只复用其 31px 白底、`#ddd` 顶边、12px 方形按钮、蓝色 active、左右信息布局；不复用 native URL/AJAX lifecycle。

WMS 数据链路保持：

```text
Wms.Api -> /api/wms/* -> external async datasource -> Wms.Grid -> jfGrid row renderer
```

外部数据源的 jfGrid 固定 `isPage:false`。Foundation Thin Pager 是唯一 Pager renderer，不创建 synthetic URL，也不修改 `jfgrid.js/css`。

## 2. DOM and sizing

```text
.wms-grid-host
  .wms-grid-main
    .wms-grid-surface (jfGrid)
    .wms-grid-state
  .wms-grid-pager
```

Host 使用 column flex。Pager 正常占用 31px；Grid main 使用剩余高度并独立 `overflow:hidden`。Pager 位于 jfGrid scroll body 外。低宽度保持单行并允许横向 overflow。

Human Runtime 在 Page 02 证明真实不可见根因是：页面 Grid host 已使用 absolute `top/right/bottom/left` 定尺寸，却又继承 Foundation `.wms-grid-host { height:100% }`。显式高度与 top/bottom 共同计算后令 host 超出 iframe，Pager 随之落到 viewport 下方。共享 modifier 让 absolute-fill host 的 width/height 回到 auto：

```text
PAGER_DOM_RENDERED_BEFORE_FIX=True
PAGER_HIDDEN_REASON=ABSOLUTE_GRID_HOST_TOP_BOTTOM_CONFLICT_WITH_FOUNDATION_HEIGHT_100_PERCENT
ABSOLUTE_FILL_MODIFIER=.wms-grid-host.wms-grid-absolute-fill
```

## 3. Presentation

左侧固定：

```text
显示第 {start} - {end} 条记录 | 检索到 {totalCount} 条记录
```

零数据为 `显示第 0 - 0 条记录 | 检索到 0 条记录`。

右侧顺序固定为：首页、上一页、数字页、下一页、尾页、`共{totalPages}页`、到、input、跳转。不提供 pageSize selector。

数字窗口最多 10 页：总页数不超过 10 时全部显示；current <= 5 时显示 1..10；接近末尾时显示 last-9..last；中间显示 current-4..current+5。

## 4. Navigation and lifecycle

- Pager 只产生 `onPageChange(targetPage)`；Grid 合并 current filters、pageSize 后执行 `loadPage`。
- 数字、首页、上一页、下一页、尾页在边界或 pending 时 disabled，不发请求。
- Jump 只接受当前有效范围内的正整数；0、-1、abc、超尾页或当前页不发请求；Enter 等价于点击跳转。
- Query/Reset 使用 `reload(query)`，固定从 page 1 开始。
- 普通数据刷新使用 `refresh()`，保留当前 filters 与 page。
- 返回总页数缩小时最多纠正一次到最后有效页；第二次仍越界则进入 Grid error，禁止循环 reload。
- pending 禁用 Pager controls；Grid local loading、request sequence 和 stale-response guard 保持。Pager render error 通过 safe renderer 报告，并且不会阻止 loading finalization。

## 5. Integration

| Page | pageSize | Pager |
|---|---:|---|
| Page 01 WeekCode Audit | 50 | Foundation Thin Pager |
| Page 02 Finished Goods Inventory | 50 | Foundation Thin Pager |
| Page 04 Finished Goods Inbound Records | 50 | Foundation Thin Pager |
| Page 07 Audit Log Center | 50 | Foundation Thin Pager |

Page 03、05、06 不接入本 Pager。本阶段不修改 Drawer、Recalculate Modal、Monitoring、MasterData、business API、Proxy、Database 或 backend paging contract。

## 6. Verification

受控 harness：`agents/wms-pager-harness.js`。它从真实 `wms-adapters.js` 提取 Pager helper，并检查 P01–P10、真实 reload/stale source guard、DOM/CSS layout contract。运行：

```powershell
node agents/wms-pager-harness.js <wms-adapters.js> <wms-components.css>
```

Human Runtime 必须先验证 Page 01：Query、可见性、摘要、1→2→1、尾页、首页、Jump、筛选后回第 1 页。Page 01 通过后再按 Page 02、Page 04、Page 07 顺序验证。

## 7. Integration coverage audit (2026-08-24)

Page 07 是 Known Good baseline；Page 02 已通过 Human Console `height:auto !important; width:auto !important` 热修复验证根因。对四页真实 `Index.cshtml`、`Index.js`、`Index.css`、`Api.js` 和共享 Foundation 的比较及正式修复结果如下：

| Page | Initializer | Metadata pass-through | Fresh iframe pager host | Height mode | Expected visibility |
|---|---|---|---|---|---|
| 01 WeekCode Audit | `Wms.Grid.init(loadData)`, 50, `autoLoad:false` | items/totalCount/page/pageSize | created | absolute, top 36/bottom 5 + absolute-fill modifier | visible |
| 02 Finished Goods Inventory | `Wms.Grid.init(loadData)`, 50, `autoLoad:false` | items/totalCount/page/pageSize | created | absolute, top 36/bottom 5 + absolute-fill modifier | visible; Human hotfix verified |
| 04 Finished Goods Inbound Records | `Wms.Grid.init(loadData)`, 50, `autoLoad:false` | items/totalCount/page/pageSize | created | absolute, top 68/bottom 5 + absolute-fill modifier | visible |
| 07 Audit Log Center | `Wms.Grid.init(loadData)`, 50, `autoLoad:false` | items/totalCount/page/pageSize | created | active pane top 128/bottom 8; grid height 100%; shared flex split | visible (Human verified) |

`autoLoad:false` 只控制首个数据请求，不控制 Pager host 创建。当前共享 adapter 在 external `loadData` 分支先创建 `.wms-grid-main` 和 `.wms-grid-pager`，再决定是否自动加载；四页均不启用 jfGrid native paging（`isPage:false`）。

四页都通过 `AppendJsFile/AppendCssFile` 把 Foundation 内容内联到 iframe HTML，而不是加载独立的 Foundation 静态 URL。当前 `system.config` 和 `system-production.config` 均为 `JsCompressorCache=false`。旧 iframe 仍需关闭重开以取得正式修复，但缓存不再视为已观测 Page 02 问题的根因。

```text
PAGER_ROOT_CAUSE=ABSOLUTE_GRID_HOST_TOP_BOTTOM_CONFLICT_WITH_FOUNDATION_HEIGHT_100_PERCENT
ABSOLUTE_GRID_MODIFIER_IMPLEMENTED=True
PAGE_SPECIFIC_INTEGRATION_DIFFERENCE_FOUND=True
DUPLICATE_FOUNDATION_SCRIPT_FOUND=False
STALE_FOUNDATION_REFERENCE_FOUND=False
SHARED_FOUNDATION_FIX_REQUIRED=True
PAGE_SPECIFIC_INTEGRATION_FIX_REQUIRED=True
MODIFIER_APPLIED_PAGES=Page01,Page02,Page04
MODIFIER_NOT_APPLIED_PAGES=Page07
```

覆盖 harness：`agents/wms-pager-integration-harness.js`。它直接读取四页真实结构与共享 adapter/CSS，检查初始化、metadata、唯一资源引用、pager/main 创建分支、页面高度槽、页面级覆盖和两套 JsCompressorCache 配置：

```powershell
node agents/wms-pager-integration-harness.js D:\com.delton.gz\Learun.Application.Web
```

Human Runtime 已按 fresh iframe 流程确认 Page 01/02/04/07 Pager 全部通过，absolute-fill modifier 运行态通过。`WMS_FOUNDATION_PAGER_DEBT_CLOSED=True`，允许进入 Drawer Phase 1B。
