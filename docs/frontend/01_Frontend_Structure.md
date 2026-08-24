# Frontend Structure and Infrastructure Inventory

## Scan Scope

只读扫描 `D:\com.delton.gz\Learun.Application.Web`，排除 `.vs`、`bin`、`obj`、packages cache 和上传数据。统计结果：

| Root | JS | CSS | HTML | CSHTML |
|---|---:|---:|---:|---:|
| Areas | 2,132 | 115 | 3 | 2,409 |
| Content | 258 | 113 | 31 | 0 |
| Views | 79 | 51 | 1 | 105 |

## Actual Frontend Entry and Structure

```text
Learun.Application.Web/
├─ Areas/                         # 业务模块；页面 JS/CSS 与 cshtml 同目录
│  ├─ WMS/Views/                 # 已有原材料/传统 WMS 页面和成熟 pattern
│  ├─ LR_SystemModule/Views/     # 日志、菜单、导入等平台页面
│  └─ LR_OrganizationModule/     # 组织树、用户、权限示例
├─ Views/
│  ├─ Shared/
│  │  ├─ _Admin.cshtml           # shell、菜单、iframe tabs
│  │  ├─ _Index.cshtml           # 查询/列表页公共依赖
│  │  └─ _Form.cshtml            # iframe form 公共依赖
│  └─ LR_Content/
│     ├─ script/                  # learun globals、AJAX、form、auth、tabs
│     ├─ plugin/                  # jfGrid/select/tree/date/layout/timeline/uploader
│     └─ style/                   # common/admin/index/form/layer CSS
└─ Content/
   ├─ jquery/ bootstrap/          # jQuery 1.10.2、Bootstrap 3.3.7
   ├─ echarts/ laydate/ layui/
   ├─ webuploader/ uploadify/ ueditor/
   ├─ jquery-ui-1.12.1.custom/
   ├─ jquery-ui-1.13.2.custom/
   ├─ font/ fonts/ images/
   └─ jqGrid/                     # 与 jfGrid 并存的历史组件
```

未发现 `Scripts` 或 `wwwroot` 作为主要入口；此项目不是 ASP.NET Core 静态资源形态。

## Page Lifecycle

标准业务脚本：

```javascript
var bootstrap = function ($, learun) {
    "use strict";
    var page = {
        init: function () { page.bind(); page.initGrid(); },
        bind: function () {},
        initGrid: function () {}
    };
    page.init();
};
```

`_Index.cshtml` 在取得当前 URL 的按钮/列权限后执行 `bootstrap(jQuery, top.learun)`；`_Form.cshtml` 在 DOM ready 后执行。页面自行绑定 resize，未发现统一 destroy hook。

## Important Global Objects

| Object/API | Definition | Responsibility | Usage |
|---|---|---|---|
| `top.learun` | `lr-base.js` | 全局工具、loading、alert、download | 大量使用 |
| `learun.http*` | `lr-ajax.js` | GET/POST、结果码、410 登录跳转 | 大量使用 |
| `learun.clientdata` | `lr-clientdata.js` | user/modules/company/department/dataItem/sourceData cache | 平台级 |
| `learun.frameTab` | `lr-tabs.js` | iframe tab open/focus/close、当前 frame | shell 级 |
| `learun.layerForm` | `lr-layer.js` | iframe form dialog | 大量使用 |
| `$.fn.jfGrid*` | `plugin/grid/jfgrid.js` | grid render/data/selection/page/edit | 大量使用 |
| `$.fn.lrselect*` | `plugin/select/lr-select.js` | select/multi/tree/search | 大量使用 |
| `$.fn.lrtree*` | `plugin/tree/lr-tree.js` | tree/check/search/async | 常用 |
| `$.fn.lrdate` | `plugin/date/lr-datepicker.js` | 快捷日期区间与自定义范围 | 查询页常用 |
| `$.fn.lrGetFormData` / `lrSetFormData` | `lr-form.js` | 表单 DTO 映射 | 表单页标准 |
| `$.fn.lrtimeline` | `plugin/timeline/lr-timeline.js` | 降序时间轴 | 工作流成熟使用 |

主要 `learun` 方法还包括 `newGuid`、`formatDate`、`parseDate`、`countFileSize`、`download`、`postForm`、`deleteForm`、`layerConfirm`、`layerClose`。

## AJAX / HTTP

真实 API：`httpAsyncGet(url, callback)`、`httpGet(url, param)`、`httpAsyncPost(url, param, callback)`、`httpPost(...)`、`httpAsync(type, url, param, callback)`、`httpSync(...)`、`postForm`、`deleteForm`。

- 默认 `dataType: json`，参数是 query/form 编码；没有通用 JSON body helper。
- 自动附加 `account` header。
- 响应码：200 success、400 fail、500 exception、410 nologin。
- 410 将 top window 导向登录页；500 替换为统一错误文本。
- `postForm/deleteForm` 提供 loading 与 success/error alert。
- 风险：存在同步 helper；无统一 timeout、abort、retry；多个 wrapper 的 callback 语义不同。

WMS 建议建立 `WmsApi`，内部仍调用 `learun.httpAsync*`，但固定 response normalization、loading、timeout policy、business/network error 与 JSON payload 约定。

## Route, Menu, Tab and Authorization

- 菜单数据来自 `learun.clientdata.modules/modulesTree/modulesMap`。
- `learun.frameTab.open({F_ModuleId,F_FullName,F_UrlAddress,F_Icon})` 创建 iframe tab。
- `focus/close/closeByParam` 管理页签；dialog 通过 layer type=2 再创建 iframe。
- Button permission：容器 `learun-authorize="yes"`，按 `lrModuleButtonList` 移除无权 DOM。
- Grid column permission：`lrAuthorizeJfGrid(op)` 按 `lrModuleColumnList` 过滤 `headData`。
- WMS 不应绕过该机制；服务端仍必须独立鉴权。

## Dictionary and Data Source

`learun.clientdata.get/getAsync/getAllAsync/getsAsync/update` 对 user、department、company、dataItem、sourceData 等提供缓存访问。表单层另有 `lrDataItemSelect`、`lrDataSourceSelect`、`lrCompanySelect`、`lrDepartmentSelect`、`lrUserSelect`。不要为 WMS 另建全局字典缓存。

## Third-party Libraries

| Library | Version | Location / evidence | Purpose | Risk |
|---|---|---|---|---|
| jQuery | 1.10.2 | standard layouts / packages.config | DOM/AJAX foundation | HIGH |
| Bootstrap | deployed 3.3.7 | `Content/bootstrap` | layout/forms/tabs | MEDIUM |
| layer | 3.0.3 | `Content/jquery/plugin/layer` | dialog/message | MEDIUM |
| laydate | 5.0.9 | `Content/laydate` | date/time | LOW |
| ECharts | 3.6.0 | `Content/echarts` | chart | MEDIUM |
| toastr | version not evidenced | plugin/toastr | toast | MEDIUM |
| WebUploader | 0.1.5 | `Content/webuploader` | HTML5 upload | MEDIUM |
| Uploadify | 3.2.1 | `Content/uploadify` | Flash upload | HIGH |
| UEditor | build 2016-08-10 | `Content/ueditor` | rich text | HIGH |
| jqGrid | 4.4.3 | plugin/jqGrid | legacy grid | HIGH |
| jQuery UI | 1.12.1 | custom folder | drag/drop/resize | MEDIUM |
| jQuery UI | 1.13.2 | custom folder | drag/drop/resize | MEDIUM |
| Font Awesome | 4.7.0 | `Content/font` | icons | MEDIUM |
| SignalR JS | 2.2.2 | `Content/signalr` | realtime shell | MEDIUM |
| Pace | 1.0.2 | `Content/pace` | page load indicator | MEDIUM |
| Bootstrap Table | 1.18.3 | `Content` | alternate table | MEDIUM |
| Swiper | 4.0.7 | `Content/jquery` | carousel | MEDIUM |
| Vue | 2.5.17 | `Content/jquery/vue.min.js` | isolated legacy pages | HIGH for WMS |

`TOTAL_THIRD_PARTY_LIBRARIES=18`（版本并存项分别计数）。文件存在不等于标准 layout 已加载。

## Existing Page Pattern Mining

| Page | Components | Good pattern | Risk / caveat |
|---|---|---|---|
| WMS MaterialStock Index | jfGrid, select, dialog, async post | 库存分页、formatter、行操作 | 400+ 行、历史注释多 |
| WMS HOLDREASON Index | grid, multi-select, import/export | Hold 查询和批处理参考 | 通用 SI endpoint，耦合 stored procedure |
| WMS Interface Mistake Index | 双 grid、批量重试 | 成功/失败分区适合接口监控 | 状态呈现需新 skin |
| WMS scanResult log Index | date range, select, grid, export | 日志查询模板 | 仍通过通用存储过程入口 |
| WMS InBoundMaterial InBoundQuery | master/detail grids | 多层明细、render complete | Grid 嵌套复杂，关注性能 |
| WMS OutBoundQuery Index | select, master/detail, dialog | 出库查询和关联详情 | formatter 内 bind/click 风险 |
| WMS STOCK IN TOOL Index | 多 grid、selection、动态 select | 批量扫描暂存区参考 | 组件多、状态管理分散 |
| WMS StockInIQC IQCStatistics | grid, select, ECharts | 统计图表示例 | 需 dispose/resize 标准化 |
| WMS EXP Material Index | grid, workflow, dialogs | 复杂查询/附件/履历入口 | 单文件 500+ 行 |
| WMS MonthlyCheck Index | master/detail grids | 盘点主从 Pattern | 打印与查询混合 |
| HS WarehouseLocation Index | select, grid, CRUD dialog | 基础数据维护模板 | 视觉旧，适合 wrapper |
| HS WarehouseLocation Form | form mapping, dependent selects | 标准 Form DTO pattern | 级联请求需 loading/error |
| System Log Index | lrdate, grid, export dialog | 日志审计基线 | 仅传统表格详情 |
| Organization User Index | company tree, grid, permission dialogs | Tree + Grid 成熟 Pattern | 功能密集、iframe 多 |
| System Module Index | tree + grid | 菜单树 Pattern | 不是 HU 语义模型 |
| ExcelImport Form | tree, grid, select, form | 导入配置/错误列示例 | 过于平台化，不照搬 UI |
| UserCenter LogIndex | lrdate + paged grid | 简洁个人日志查询 | 能力有限 |
| NWF Container Form | timeline, upload, dialog, async | `lrtimeline` 的成熟实用证据 | 1,700+ 行，不作为页面结构模板 |

`TOTAL_EXISTING_PAGES_REVIEWED=18`。

## Legacy Frontend Risk Notes

- 新页禁止 `httpSync/httpGet`、直接 `$.ajax` 和同步 XHR。
- Grid 必须 server pagination；导出走服务端任务/既有 export，不能把全量数据放进 DOM。
- formatter 只返回 DOM/文本，不在每次 render 中重复绑定 document/window 事件。
- 页面事件统一 `.on('event.wmsXxx', ...)`；重载/关闭时 `.off('.wmsXxx')`。
- ECharts 页面保存实例并在 iframe unload 时 `dispose`。
- modal/drawer 关闭时清除 timer、pending state、resize handler。
- 避免 `top.$` 全局 selector 扫描；跨 frame 只通过 Learun 既有入口。
- 不混用 jqGrid、bootstrap-table 与 jfGrid；不混用两套 jQuery UI。
