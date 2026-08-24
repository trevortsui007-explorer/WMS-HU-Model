# WMS Frontend Foundation Review

调查日期：2026-08-22  
只读源工程：`D:\com.delton.gz\Learun.Application.Web`  
文档落点：`D:\Code\WMS-HU-Model\docs\frontend`  

## Executive Summary

现有前端是 ASP.NET MVC/Razor + jQuery + Learun 的多 iframe 页签系统。框架源码明确标识为 **Learun-ADMS V7.0.3**；标准 Index/Form 布局实际加载 **jQuery 1.10.2** 和 **Bootstrap 3.3.7**。核心能力覆盖 Grid、表单、校验、Select、Tree、日期、弹窗、分页、权限、客户端数据、导入导出、图表和页面生命周期，足以承载 7 个 WMS 页面。

建议不是直接复制旧页面，而是保留成熟行为，通过 WMS scope 的 wrapper 和 CSS skin 建立一致设计。`jfGrid`、`lrselect`、`lrtree`、`layerForm`、AJAX 应包装复用；状态标签、侧边抽屉、详情面板、JSON Viewer、结果面板、Metric Card、Audit Diff Cell 应自建。禁止改动 Learun/Bootstrap/plugin 原始 CSS。

## Framework Identification

```text
LEARUN_FRAMEWORK_VARIANT=Learun-ADMS
LEARUN_FRAMEWORK_VERSION=V7.0.3
JQUERY_VERSION=1.10.2
BOOTSTRAP_VERSION=3.3.7
```

证据：

- `Views/LR_Content/script/lr-base.js`、`lr-ajax.js` 等文件头写明 `Learun-ADMS V7.0.3`。
- `Views/Shared/_Index.cshtml` 与 `_Form.cshtml` 加载 `Content/jquery/jquery-1.10.2.min.js`。
- `Content/bootstrap/bootstrap.min.js` 与 CSS 文件头写明 Bootstrap 3.3.7；`packages.config` 的 3.0.0 记录与部署静态文件不一致，应以运行时引用文件为准。
- 页面入口是 Razor layout + 页面同目录 JS；layout 最后调用 `bootstrap(jQuery, top.learun)`。

## Existing Component Coverage

| Capability | Finding | WMS decision |
|---|---|---|
| Grid | `jfGrid` 支持服务端分页、多选、排序、formatter、冻结列、树表、子表、编辑、刷新 | `WmsGrid` 包装复用 |
| Form | `lrGetFormData`、`lrSetFormData`、`lr-validator` | 直接复用序列化；包装验证展示 |
| Select | `lrselect` 支持普通、多选、树形、搜索和远程数据 | `WmsSelect` 包装复用 |
| Tree | `lrtree` 支持搜索、checkbox、异步节点 | `WmsHuTree` 包装复用 |
| Date | `lrdate` + laydate 5.0.9 | `WmsDateRange` 包装；单值日期直接用 laydate |
| Dialog | `layerForm` 基于 layer 3.0.3，iframe form pattern 成熟 | `WmsDialog` 包装复用 |
| Notice | `learun.alert.*`、`learun.loading`、toastr | 直接复用统一入口 |
| Upload/export | `lr-uploader`、WebUploader 0.1.5、`lr-excel` | 上传包装，导出直接复用 |
| Chart | ECharts 3.6.0 | 直接复用并集中 chart lifecycle |
| Permission | `learun-authorize="yes"`、按钮/列映射 | 直接复用，不自建权限体系 |

完整条目见 [02_Component_Atlas.md](./02_Component_Atlas.md)。

## Reuse Strategy

优先级：UX/Design → 可维护性 → 框架兼容 → 复用 → 实现成本。

1. 使用 `_Index.cshtml` / `_Form.cshtml` 的加载与生命周期约定。
2. 用薄 wrapper 隔离旧 API、统一 loading/empty/error、分页参数和无障碍语义。
3. 所有 WMS skin 仅在 `.wms-page` 下覆盖，新增 `wms-tokens.css`、`wms-components.css`、`wms-pages.css`。
4. 不新增 Vue/React/Angular 或 npm build chain；项目中虽存在 Vue 2.5.17 文件，但不作为 WMS 基础设施。

## Custom Component Strategy

CUSTOM_BUILD：`WmsStatusTag`、`WmsSideDrawer`、`WmsDetailPanel`、`WmsJsonViewer`、`WmsResultPanel`、`WmsMetricCard`、`WmsAuditDiffCell`。这些组件以 jQuery + HTML + scoped CSS 实现，不依赖新的框架。

`WmsTimeline` 不应从零实现：已有 `lrtimeline`，先 wrapper + skin。`WmsHuTree` 也应以 `lrtree` 为行为内核；只有验证大规模 HU 节点性能不满足后才重新评估。

## 7 WMS Page Assessment

七页均可在现有 page/tab/auth/http 生命周期内实现。Grid 密集页面共用 `WmsSearchPanel + WmsGrid`；详情/履历页使用 `WmsSideDrawer + WmsDetailPanel + WmsTimeline + WmsHuTree`；监控和日志页增加 `WmsMetricCard`、`WmsJsonViewer`、`WmsAuditDiffCell`。详见 [03_WMS_Page_Component_Matrix.md](./03_WMS_Page_Component_Matrix.md)。

## Technical Risks

- jQuery 1.10.2 与 Learun 2018 年框架属于 legacy baseline；本阶段不升级。
- 业务页面中扫描到 534 次 `httpSync`、42 次同步 `httpGet`、698 次直接 `$.ajax`；新 WMS 页面禁止新增同步请求并优先走 wrapper。
- 同类库并存：jfGrid/jqGrid/bootstrap-table、jQuery UI 1.12.1/1.13.2、主 jQuery/UEditor 自带 jQuery。页面必须只引用标准 layout 已加载的组件。
- iframe 页签与 iframe dialog 产生跨 frame 引用和清理风险。drawer 适合在当前 iframe 内实现；dialog 关闭必须注销页面级事件、timer、chart resize。
- `jfGrid` 是全量 DOM 渲染，不是虚拟滚动；必须使用服务端分页，禁止一次加载大量库存/日志记录。
- 现有插件含全局 document/window 事件；WMS 事件使用 namespace，避免重复 bind。
- `lr-ajax.js` 无统一 timeout/cancel，错误表现不一致；wrapper 应规范 network/empty/business error。

## Recommended WMS Frontend Foundation

结合现有 MVC Area，不引入 SPA 目录：

```text
Areas/WMS/Views/FinishedGoods/
├─ Components/          # WMS wrapper / custom jQuery components
├─ Services/            # 仅页面 API adapter
├─ Styles/
│  ├─ wms-tokens.css
│  ├─ wms-components.css
│  └─ wms-pages.css
├─ Utils/
└─ <PageName>/          # Index.cshtml + Index.js (+ page-only css)
```

如现有控制器/路由要求每个功能保持独立目录，可将共享文件置于 `Areas/WMS/Views/Shared/Wms`，不要另建运行时或 bundler。

## Next Step

1. 人工确认 Atlas 的 wrapper/custom 分界。
2. 先做不接真实业务 API 的 WMS visual prototype，确认密度、状态色、drawer 与 responsive 行为。
3. 实现最小 foundation：tokens、`WmsSearchPanel`、`WmsGrid`、`WmsDialog`、`WmsStatusTag`。
4. 以“成品库存/HU 查询”作为首个集成页面验证分页、权限、错误态与详情 drawer。

本调查未修改 `Learun.Application.Web` 的 JS、CSS、cshtml、配置或后端代码，也未运行浏览器/数据库集成测试。
