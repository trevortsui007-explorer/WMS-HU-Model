# WMS Frontend Foundation V1 实施说明

> V1.1 update: Showcase and component skin now follow `LEARUN_NATIVE_VISUAL_FIRST=True`. All Foundation `.cshtml/.js/.css` files are UTF-8 with BOM. See `09_Learun_Visual_Baseline.md` and `10_Frontend_Encoding_Audit.md`.

> 2026-08-24 Pager debt closure: `WMS_FOUNDATION_PAGER_VISUAL_BASELINE=LEARUN_JFGRID_NATIVE_STYLE` and `WMS_PAGER_DATA_MODE=EXTERNAL_SERVER_PAGING`. The existing Thin Pager remains the only WMS pager renderer; jfGrid stays `isPage:false` for external async data sources. See `14_WMS_Server_Pager_Contract.md`.

## 1. 实施范围

本阶段在现有 Learun-ADMS / jQuery / Bootstrap 3 技术栈内增加 WMS 专属基础层和一个 Mock Showcase。未引入 Vue、React、npm 包或新 Grid；未修改 Learun、Bootstrap、第三方源码；未连接真实 WMS API，也未写生产数据。

## 2. Files Added

Web 项目 `D:\com.delton.gz\Learun.Application.Web`：

- `Areas/WMS/Content/Foundation/Scripts/wms-core.js`
- `Areas/WMS/Content/Foundation/Scripts/wms-api.js`
- `Areas/WMS/Content/Foundation/Scripts/wms-adapters.js`
- `Areas/WMS/Content/Foundation/Scripts/wms-components.js`
- `Areas/WMS/Content/Foundation/Styles/wms-tokens.css`
- `Areas/WMS/Content/Foundation/Styles/wms-components.css`
- `Areas/WMS/Content/Foundation/Styles/wms-foundation-demo.css`
- `Areas/WMS/Controllers/WMSFoundationDemoController.cs`
- `Areas/WMS/Views/WMSFoundationDemo/Index.cshtml`
- `Areas/WMS/Views/WMSFoundationDemo/Index.js`
- `Areas/WMS/Views/WMSFoundationDemo/ModalDemo.cshtml`
- `Areas/WMS/Views/WMSFoundationDemo/ModalDemo.js`

旧式 `Learun.Application.Web.csproj` 已显式登记新增 Controller、View、JS 和 CSS。

## 3. Components and Underlying

| Public component | 底层能力 | 主要用途 |
|---|---|---|
| `Wms.Api` | `learun.httpAsyncGet` / `learun.httpAsyncPost` / 单一 JSON `$.ajax` | 请求与响应归一化 |
| `Wms.SearchPanel` | jQuery DOM/event | 搜索条件、折叠、重置 |
| `Wms.Grid` | `jfGrid` | 服务端分页、选中行、状态覆盖层 |
| `Wms.Select` | `lrselect` | 单选、多选、树、搜索、远程数据 |
| `Wms.DateRange` | `lrdate` / `laydate` | 日期或日期时间范围 |
| `Wms.StatusTag` | WMS scoped DOM/CSS | 安全渲染业务状态 |
| `Wms.DetailPanel` | WMS scoped DOM/CSS | 详情字段组 |
| `Wms.AuditDiffCell` | WMS scoped HTML/CSS | 当前值与候选值的紧凑审计 diff |
| `Wms.ResultPanel` | WMS scoped DOM/CSS | 批处理摘要、箱级结果与详细错误 |
| `Wms.SideDrawer` | WMS scoped DOM/CSS | 列表上下文中的快速详情 |
| `Wms.Modal` | `learun.layerForm` | iframe 复杂表单 |
| `Wms.ConfirmDialog` | `learun.layerConfirm` / `top.layer.prompt` | 普通或 typed 高影响确认 |

## 4. Public API 摘要

- `Wms.Registry.register/get/remove/destroyAll/count`
- `Wms.Api.get/postForm/postJson/normalize`
- `SearchPanel.values/reset/toggle/destroy`
- `Grid.reload/refresh/clear/getSelected/getSelectedRows/setState/destroy`；可选 `loadData + responseAdapter` 以通过 `Wms.Api` 驱动后端分页。`reload` 从第 1 页查询，`refresh` 保留当前页。
- `Select.value/refresh/disabled/destroy`
- `DateRange.value/clear/destroy`
- `DetailPanel.render/destroy`
- `SideDrawer.open/setContent/setFooter/close/destroy`
- `Modal.open/close/currentDepth`
- `ConfirmDialog.open/typed`

所有业务页面应只通过上述包装入口消费组件；新的业务请求统一通过 `Wms.Api`。

## 5. API 归一化

`Wms.Api.normalize()` 将结果归一为：

- `SUCCESS`
- `BUSINESS_ERROR`
- `NETWORK_ERROR`
- `SESSION_EXPIRED`
- `SERVER_ERROR`

Page 01 接入时补充了真实 Nancy `Code/Message/Data` PascalCase 响应支持，并为需要保留 HTTP 400 JSON 的 GET 增加 `preserveHttpError` 选项。JSON POST 的 HTTP 400 也按响应 Code 归类为 `BUSINESS_ERROR`，保留后端 Message。

GET 和 form POST 复用 Learun async helper；JSON POST 因现有 helper 不提供 JSON body 合同，集中保留一个异步 `$.ajax` 实现。全 Foundation 不存在同步 AJAX。

## 6. Lifecycle / Destroy

- 实例注册在 `Wms.Registry`；页面卸载调用 `destroyAll()`。
- 包装层事件使用 `.wms*` 命名空间；销毁时精确解绑。
- SearchPanel、DetailPanel、Drawer 会移除自身 DOM/事件；Grid、Select、DateRange 清理包装层状态。
- `jfGrid`、`lrselect`、`lrdate` 没有一致可靠的公开 destroy 合同。包装层不得重复初始化已存在的底层实例，以免重复框架级全局事件；这也是当前已知限制。
- Showcase 内置 SearchPanel 与 Drawer 10 次创建/销毁自检，并显示 Registry/API normalization 状态。

## 7. Drawer / Modal 协同决策

- `QUICK_DETAIL=Drawer`
- `COMPLEX_FORM=IframeModal`
- `HIGH_IMPACT_ACTION=ConfirmDialog`
- `DRAWER_CAN_OPEN_MODAL=True`
- `MODAL_SUCCESS_REFRESHES_DRAWER=True`
- `MODAL_SUCCESS_REFRESHES_GRID=True`
- `DEFAULT_MAX_MODAL_DEPTH=1`

详细约定见 `07_Modal_Drawer_Usage_Guide.md`。

## 8. CSS Strategy

- 设计 token 使用普通 CSS 常量声明/注释，兼容老浏览器，不依赖 CSS custom properties。
- 页面样式以 `.wms-page` 为根作用域；Top layer 皮肤使用 `wms-layer-*` / `wms-confirm-*` 前缀。
- 不覆盖全局 `body`、Bootstrap、Learun 或第三方选择器，不修改第三方文件。

## 9. Showcase

入口：`/WMS/WMSFoundationDemo/Index`。

页面以 Learun `lr-layout`、55px toolbar、Bootstrap tabs/native buttons、原生 jfGrid 和 `_Form` 表单组合只读 Mock 场景；StatusTag、DetailPanel、Drawer 作为增量能力。Modal 的“保存”只更新宿主页内存中的 revision override；刷新页面即恢复，不写数据库。

## 10. 验证结果与限制

- VS 2022 MSBuild Debug：PASS，0 errors；84 个既有项目 warning。
- 新增 6 个 JS：`node --check` PASS。
- 静态扫描：无同步 AJAX、无 `top.$`、无 Vue/React、无 fetch/Promise/async-await；直接 `$.ajax` 仅存在于 `wms-api.js` 的 JSON POST 适配器。
- IIS Express 可启动，匿名 HTTP 请求按现有框架规则返回登录页。
- 应用内浏览器对 localhost 的访问权限未获批准，因此本轮未声明 Showcase 可视化交互通过；须按 07 文档的人工验收清单复核。

## 11. Known Limitations

- Foundation 继承 Learun V7.0.3、jQuery 1.10.2、Bootstrap 3.3.7 的浏览器与可访问性边界。
- `jfGrid` / `lrselect` / `lrdate` 缺少统一 destroy API，包装层只能安全管理自己拥有的资源并复用底层实例。
- 当前 API adapter 只覆盖已确认的 Learun response 形态；接入真实 WMS API 时需以正式接口合同补充字段映射测试。
- Showcase 未注册菜单，不应作为生产业务入口。

## 12. External Server Pager

- DOM 职责固定为 `.wms-grid-host > .wms-grid-main + .wms-grid-pager`；Grid main 占剩余高度，Pager 正常占用 31px，不进入 jfGrid scroll body。
- Human Runtime 已证明 absolute top/right/bottom/left Grid host 不能同时保留 Foundation 的 `height:100%`。这类 host 使用共享 `.wms-grid-absolute-fill` modifier，将 width/height 设为 `auto !important`，由四个 inset 决定实际尺寸。
- Pager 只解析 navigation target，并通过 Grid 的 `onPageChange(targetPage)` 进入现有 `loadPage`；它不调用 `Wms.Api`，不拥有 filters、pageSize、loading 或 response lifecycle。
- 左侧摘要、最多 10 个数字页、Learun 蓝色 active、首页/上一页/下一页/尾页、总页数和整数 Jump 均由同一 Foundation renderer 输出。
- Page 01、02、04 使用 absolute-fill modifier；Page 07 的 grid 在 absolute pane 内使用 `height:100%`，不应用该 modifier。Page 03、05、06 不接入。业务 API、Proxy、数据库及 backend paging contract 未改变。
