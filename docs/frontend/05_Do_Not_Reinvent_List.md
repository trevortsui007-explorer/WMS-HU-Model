# Do Not Reinvent List

后续 WMS 开发禁止重复实现以下已确认能力；如视觉不满足，使用 wrapper/skin。

| Capability | Existing standard | Rule |
|---|---|---|
| Page lifecycle | `bootstrap(jQuery, top.learun)` | 保持标准 Index/Form 启动方式 |
| Menu/tab navigation | `learun.frameTab` | 不自建 SPA router/tab host |
| Authorization | authorize attribute + grid column auth | 不以隐藏按钮代替服务端权限，也不另建前端权限缓存 |
| AJAX/session redirect | `learun.httpAsync*` | 不直接复制 410/500 处理；经 WmsApi |
| Loading/alerts | `learun.loading`、`learun.alert.*` | 不创建第二套全局 toast/loading |
| Dialog/form modal | `learun.layerForm`、`layerConfirm` | 复杂表单继续 iframe dialog；经 wrapper |
| Grid | `jfGrid` | 不引入 DataTables/新 grid，不选 jqGrid/bootstrap-table |
| Server pagination | jfGrid paging contract | 不在浏览器分页大量库存/日志数据 |
| Form mapping | `lrGetFormData/lrSetFormData` | 不手写遍历所有表单控件 |
| Validation | `lr-validator` | 不引入新验证库 |
| Select/multi/tree select | `lrselect` | 不引入 Select2 |
| Tree | `lrtree` | HU Tree 先 adapter，不从零写树引擎 |
| Date/date range | laydate + `lrdate` | 不引入 Moment/date picker 新依赖 |
| Dictionary/data source | `learun.clientdata` | 不自建重复全局缓存 |
| File upload | `lr-uploader`/WebUploader | 不用 Uploadify，不写隐藏 iframe uploader |
| Excel export | `lr-excel` / ExcelExportForm / server export | 不把全量 Grid 转成客户端 Excel |
| Timeline engine | `lrtimeline` | HU Timeline 先 wrapper/skin |
| Charts | ECharts 3.6.0 | 不引入 Chart.js/Highcharts 到 WMS 新页 |
| Icons | Font Awesome 4.7 + existing iconfont | 不增加新的 icon package |
| Date/number/string utils | `learun.formatDate` 等 | 先复用已确认 utility，缺口再补 WMS utils |

## Explicit Non-standards

- 项目里存在不代表允许使用：Vue 2.5.17、jqGrid 4.4.3、bootstrap-table、Uploadify、两套 jQuery UI。
- 不修改 `learun.css`、Bootstrap CSS、jfGrid CSS、plugin 原文件。
- 不复制成熟页面的 stored-procedure endpoint coupling；只借鉴 UI pattern。
