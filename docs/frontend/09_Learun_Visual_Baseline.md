# Learun Visual Baseline

## 1. Decision

```text
LEARUN_NATIVE_VISUAL_FIRST=True
LEARUN_VISUAL_REUSE_TARGET>=70%
FOUNDATION_V11_REUSE_ESTIMATE=80%
```

V1.1 不再以独立 dashboard/card 视觉作为基础。标准列表页直接采用 Learun Index Layout、Toolbar、Bootstrap 3 Tabs、Bootstrap/Learun Buttons、Form Controls、jfGrid 和 layerForm。WMS CSS 只保留 StatusTag、SideDrawer、DetailPanel、loading/empty/error 等 Learun 明显缺失的增强。

## 2. Mature Page Evidence

| Pattern | Page | Layout | Key structure/classes | Evidence used by V1.1 |
|---|---|---|---|---|
| Tree + Grid | `Areas/LR_OrganizationModule/Views/User/Index.cshtml` | `_Index.cshtml` | `lr-layout-left-center`, `lr-layout-left`, `lr-layout-wrap`, `lr-layout-title`, `lr-layout-tool`, `lr-layout-body` | 左树/右表分栏、工具栏、按钮组和 jfGrid 容器 |
| Query Toolbar + Grid | `Areas/LR_SystemModule/Views/Log/Index.cshtml` | `_Index.cshtml` | `lr-layout-wrap-notitle`, `lr-layout-tool-left/right`, `lr-layout-tool-item`, `lrdate`, `form-control` | 55px 紧凑查询工具栏、日期+关键字+查询按钮 |
| Tree + CRUD Grid | `Areas/LR_SystemModule/Views/DataItem/Index.cshtml` | `_Index.cshtml` | `lr-layout-left-center`, `lr-layout-title`, `btn-group-sm`, `btn-default`, `btn-primary` | 基础数据维护、右侧 CRUD button groups |
| WMS Query + Grid | `Areas/WMS/Views/HS_WarehouseLocation/Index.cshtml` | `_Index.cshtml` | `lrMultipleQuery`, `lr-query-formcontent`, `lr-form-item`, `lr-layout-body` | WMS 原生多条件查询、Grid、CRUD layerForm |
| WMS Inventory Split | `Areas/WMS/Views/WMS_MaterialStock/Index.cshtml` | `_Index.cshtml` | `lr-layout-left`, `lr-form-wrap`, `lr-layout-tool-right`, `btn-danger`, `btn-success`, jfGrid | WMS 双栏库存页、危险/普通操作的现有按钮语义 |
| Detail / Form | `Areas/WMS/Views/HS_WarehouseLocation/Form.cshtml` | `_Form.cshtml` | `lr-form-wrap`, `lr-form-item`, `lr-form-item-title`, `form-control`, `col-xs-*` | iframe Modal 内的标准表单排版和必填标记 |

## 3. Measured Baseline

| Element | Learun baseline | Source evidence |
|---|---|---|
| Page background | 由 shell/theme 管理；`body` 为 `background-color: initial` | `lr-common.css` |
| Page font | `PingFangSC-Regular, Microsoft YaHei, SimSun, Arial...`; 12px; `#333` | `lr-common.css` |
| Page inset | `.lr-layout` padding 5px；center 再 padding 5px | `lr-layout.css` |
| Panel | 白底、`1px solid #ccc`、3px radius、极轻 `0 1px 1px` shadow | `.lr-layout-wrap` |
| Panel title | 32px 高，12px bold，`#666`，底边 `#ccc` | `.lr-layout-title` |
| Toolbar | 55px 高，底边 `#ccc`；left 10px，right 15px | `.lr-layout-tool` |
| Query item | 顶部 13px；控件 28px；item 左间隔 5px | `lr-layout-tool-item`, `lr-iframe-index.css` |
| Input | Bootstrap/Learun `.form-control`，列表工具栏宽度通常 200px，高度 28px | `lr-iframe-index.css` |
| Primary button | `btn btn-primary btn-sm` | User/Log/DataItem Index |
| Default button | `btn btn-default`，通常置于 `btn-group btn-group-sm` | User/DataItem/WarehouseLocation Index |
| Danger button | `btn btn-danger` | WMS MaterialStock Index |
| Grid header | 28px；`#f8f8f8`；`1px solid #ccc` 分隔 | `jfgrid.css` |
| Grid row | 默认 `rowHeight: 28`；12px；`#333`；`#ccc` cell border | `jfgrid.js`, `jfgrid.css` |
| Grid selection | `#2F94EE` 白字 | `.jfgrid-selected` |
| Grid footer | 35px；top border `#ddd` | `.jfgrid-page-bar` |
| Tabs | Bootstrap 3 `.nav .nav-tabs`，不创建 WMS tab engine | standard layout dependency |
| Tree panel | 203px 标准左栏；32px title；白底细边框 | User/DataItem Index, `lr-layout.css` |
| Form row | 最小 38px；label 100px 右对齐；上下 padding 5px | `lr-iframe-form.css` |
| Form shell | `_Form.cshtml` + `.lr-form-wrap`，右侧 30px padding | shared layout / form CSS |
| Dialog header/footer | Learun `lr-layer`: 35px header、38px button area、28px layer button | `lr-layer.css` |

## 4. Foundation V1.1 Mapping

| Foundation surface | V1.1 decision |
|---|---|
| Showcase shell | `lr-layout` + `lr-layout-center` + `lr-layout-wrap-notitle` |
| Query | `lr-layout-tool-left` + `lr-layout-tool-item` + native controls/buttons |
| Actions | `lr-layout-tool-right` + `btn-group-sm` + `btn-default/primary` |
| Tabs | Bootstrap 3 `nav nav-tabs` |
| Grid | Native jfGrid skin and native 28px row; WMS only overlays loading/empty/error |
| Detail | Thin-border, 12px compact WMS DetailPanel in the right pane |
| Drawer | Learun-like 36px header, `#f8f8f8`, `#ccc`, Bootstrap buttons; remains a WMS enhancement |
| Modal | `learun.layerForm` native chrome; iframe page uses `.lr-form-wrap/.lr-form-item` |
| Status | Small 18–20px text-bearing tag used within list/detail contexts |

Large hero headers, large card radius/shadows, 36px custom controls, 36px rewritten jfGrid rows and the separate WMS button system were removed from the Showcase/Foundation skin.
