# WMS Frontend Component Atlas

Maturity：MATURE / USABLE / LEGACY / RISKY。Recommendation 是 WMS 的决策，不代表删除既有页面依赖。

| Category | Component | API / CSS | Source File | Example Page | Maturity | Recommendation |
|---|---|---|---|---|---|---|
| Layout | Learun layout | `lrLayout` | `plugin/layout/lr-layout.js` | User Index | MATURE | WRAP_AND_REUSE |
| Navigation | iframe tabs | `learun.frameTab.open/focus/close` | `script/lr-tabs.js` | Admin shell | MATURE | WRAP_AND_REUSE |
| Layout | Bootstrap tabs | `.nav-tabs` | bootstrap 3.3.7 | Form pages | MATURE | DIRECT_REUSE |
| Layout | Bootstrap collapse | `.collapse` | bootstrap 3.3.7 | Query forms | USABLE | DIRECT_REUSE |
| Grid | jfGrid | `jfGrid/jfGridSet/Get/Value` | `plugin/grid/jfgrid.js` | MaterialStock | MATURE | WRAP_AND_REUSE |
| Grid | jqGrid | `jqGrid` | `Content/jquery/plugin/jqGrid` | legacy layouts | LEGACY | DO_NOT_USE |
| Grid | Bootstrap Table | `bootstrapTable` | `Content/bootstrap-table.min.js` | isolated pages | USABLE | DO_NOT_USE |
| Form | Form mapping | `lrGetFormData/lrSetFormData` | `script/lr-form.js` | WarehouseLocation Form | MATURE | DIRECT_REUSE |
| Form | Validator | `lrValidform` / validation attributes | `script/lr-validator.js` | standard forms | MATURE | WRAP_AND_REUSE |
| Select | lrselect | `lrselect/Get/Set/Refresh/GetEx` | `plugin/select/lr-select.js` | HOLDREASON | MATURE | WRAP_AND_REUSE |
| Select | Form select | `lrformselect` | `plugin/formselect` | platform forms | USABLE | WRAP_AND_REUSE |
| Select | Input search | `lr-isearch` | `plugin/inputsearch` | form lookups | USABLE | WRAP_AND_REUSE |
| Tree | lrtree | `lrtree/lrtreeSet` | `plugin/tree/lr-tree.js` | Organization User | MATURE | WRAP_AND_REUSE |
| Date | Learun date range | `lrdate` | `plugin/date/lr-datepicker.js` | System Log | MATURE | WRAP_AND_REUSE |
| Date | laydate | `laydate.render` | `Content/laydate` | shared layouts | MATURE | DIRECT_REUSE |
| Dialog | iframe form | `layerForm/layerClose` | `script/lr-layer.js` | WarehouseLocation | MATURE | WRAP_AND_REUSE |
| Dialog | Confirm | `layerConfirm` | `script/lr-layer.js` | delete/retry flows | MATURE | WRAP_AND_REUSE |
| Notification | Alert | `learun.alert.success/info/warning/error` | `script/lr-base.js` | broad | MATURE | DIRECT_REUSE |
| Feedback | Loading | `learun.loading` | `script/lr-base.js` | post/delete form | MATURE | DIRECT_REUSE |
| Upload | Learun uploader | `lrUploader` | `plugin/uploader` | workflow form | MATURE | WRAP_AND_REUSE |
| Upload | WebUploader | `WebUploader.create` | `Content/webuploader` | uploader wrapper | USABLE | DIRECT_REUSE |
| Upload | Uploadify/Flash | `uploadify` | `Content/uploadify` | legacy | RISKY | DO_NOT_USE |
| Export | Learun Excel | `lr-excel` / export form | `script/lr-excel.js` | System Log | MATURE | DIRECT_REUSE |
| Timeline | Learun timeline | `lrtimeline` | `plugin/timeline/lr-timeline.js` | NWF Container | USABLE | WRAP_AND_REUSE |
| Chart | ECharts | `echarts.init/setOption` | `Content/echarts` | IQCStatistics | MATURE | DIRECT_REUSE |
| Authorization | Button/column auth | authorize attr / `lrAuthorizeJfGrid` | `script/lr-authorize.js` | HS_Uom | MATURE | DIRECT_REUSE |
| Data | Client data | `clientdata.get/getAsync/getAllAsync` | `script/lr-clientdata.js` | select/form framework | MATURE | DIRECT_REUSE |
| HTTP | Learun AJAX | `httpAsync*`, `postForm`, `deleteForm` | `script/lr-ajax.js` | broad | MATURE | WRAP_AND_REUSE |
| Search | Custom query | `lr-custmerquery` | `plugin/custmerquery` | index layout | USABLE | WRAP_AND_REUSE |
| Icon | Font Awesome | `.fa` | `Content/font` | broad | MATURE | DIRECT_REUSE |
| Icon | Learun iconfont | iconfont classes | `Content/font/learunfont` | admin shell | USABLE | STYLE_ONLY |
| Feedback | Pace indicator | pace CSS/JS | `Content/pace` | layouts | USABLE | STYLE_ONLY |
| WMS | Status tag | proposed `.wms-status-tag` | none | needed by 7 pages | UNKNOWN | CUSTOM_BUILD |
| WMS | Side drawer | proposed `WmsSideDrawer` | none | HU detail/logs | UNKNOWN | CUSTOM_BUILD |
| WMS | Detail panel | proposed `WmsDetailPanel` | none | HU/detail pages | UNKNOWN | CUSTOM_BUILD |
| WMS | JSON viewer | proposed `WmsJsonViewer` | none | OperationLog | UNKNOWN | CUSTOM_BUILD |
| WMS | Result panel | proposed `WmsResultPanel` | none | week-code recalc | UNKNOWN | CUSTOM_BUILD |
| WMS | Metric card | proposed `WmsMetricCard` | none | monitoring | UNKNOWN | CUSTOM_BUILD |
| WMS | Audit diff cell | proposed formatter/component | none | audit center | UNKNOWN | CUSTOM_BUILD |
| Framework | Vue 2.5.17 | global `Vue` | `Content/jquery/vue.min.js` | isolated legacy | RISKY | DO_NOT_USE |
| Date | My97/WdatePicker | global `WdatePicker` references | grid edit/comments | grid edit path | RISKY | DO_NOT_USE |

## jfGrid Verified Capability Notes

Initialization options include `url`、`param`、`rowdatas`、`headData`、`isShowNum`、`isMultiselect`、`isSubGrid`、`onSelectRow`、`dblclick`、`onRenderComplete`、`onRenderBefore`、`isPage`、`rows`、`sidx`、`sord`、`isTree`、`mainId`、`parentId`、`isEdit`、`isAutoHeight`、`height`、`rowHeight`。

- Server pagination: supported; source explicitly says pagination is server-side only.
- Sorting: supported through header sort and `sidx/sord`.
- Frozen columns: implemented during header initialization.
- Multi-selection: `isMultiselect` + `jfGridGet('rowdata')`.
- Formatter: column `formatter(cellvalue,row,op,$cell)` is broadly used.
- Master/detail and sub-grid: supported; existing WMS pages also compose multiple grids.
- Tree grid: supported only without paging and recommended by source for small data.
- Editable grid: `isEdit` and edit-cell types exist, but WMS should use only after wrapper tests.
- Export: grid display data can be read; production export pattern is separate Excel form/server endpoint.
- No evidence of virtual scrolling; large datasets require server paging.

## Decision Totals

```text
TOTAL_UI_COMPONENTS_FOUND=41
DIRECT_REUSE_COUNT=12
WRAP_AND_REUSE_COUNT=15
STYLE_ONLY_COUNT=2
CUSTOM_BUILD_COUNT=7
DO_NOT_USE_COUNT=5
```
