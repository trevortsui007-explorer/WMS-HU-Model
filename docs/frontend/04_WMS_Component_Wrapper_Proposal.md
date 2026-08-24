# WMS Component Wrapper Proposal

本文件仅定义 proposal，不包含实现。

## WmsGrid

- Purpose：统一库存、入库、审计、日志列表。
- Underlying：`jfGrid`。
- Why wrapper：隔离旧 option、统一分页/排序、loading/empty/error、status formatter 和事件清理。
- API proposal：`init({url, columns, pageSize, selectable, onRowClick, onRowDoubleClick})`、`reload(query)`、`getSelected()`、`setState('loading|empty|error')`、`destroy()`。

## WmsSelect

- Purpose：普通/搜索/多选/远程/树形下拉。
- Underlying：`lrselect`。
- Why wrapper：统一 value/text、placeholder、loading、disabled、remote error。
- API proposal：`init({data,url,valueField,textField,multiple,searchable})`、`value([next])`、`refresh(options)`、`disabled(flag)`、`destroy()`。

## WmsDateRange

- Purpose：查询页日期范围与快捷时间。
- Underlying：`lrdate` + laydate。
- Why wrapper：统一返回 ISO-like local string、范围边界和清空行为。
- API proposal：`init({presets,includeTime,maxRangeDays})`、`value()`、`clear()`、`destroy()`。

## WmsHuTree

- Purpose：PALLET → OUTER_BOX → INNER_PACK。
- Underlying：`lrtree`。
- Why wrapper：把 HU DTO 转换为 Learun node contract；统一 lazy load、selected node、node badge。
- API proposal：`init({rootUrl,loadChildren,onSelect,searchable})`、`select(id)`、`expand(id)`、`reload(id)`、`destroy()`。

## WmsTimeline

- Purpose：HU 移动、入库、Hold、Pick、Ship 履历。
- Underlying：`lrtimeline`。
- Why wrapper：现有组件数据模型面向工作流，需 WMS adapter 和 status skin。
- API proposal：`render(events)`、`append(event)`、`clear()`；event 固定 `{time,title,actor,status,summary,details}`。

## WmsDialog

- Purpose：新增/编辑/复杂查看。
- Underlying：`learun.layerForm`。
- Why wrapper：统一尺寸、按钮、callback、pending 防重、关闭清理。
- API proposal：`open({id,title,url,size,confirmText,onConfirm,onClose})`、`close(id)`。

## WmsConfirmDialog

- Purpose：Recalculate/Hold/Release 等高影响操作。
- Underlying：`learun.layerConfirm` / layer。
- Why wrapper：统一 warning/danger 语义、pending 和可选 typed confirmation。
- API proposal：`confirm({title,message,tone,confirmText,requiredText,onConfirm})`。

## WmsSearchPanel

- Purpose：查询、折叠/展开、重置、提交。
- Underlying：Learun index layout/query CSS + Bootstrap collapse。
- Why wrapper：现有页面各自绑定事件，缺少一致 responsive 与 Enter 行为。
- API proposal：`init({form,collapsed,onSearch,onReset})`、`values()`、`reset()`、`toggle()`、`destroy()`。

## WmsApi

- Purpose：WMS 页面统一 HTTP adapter。
- Underlying：`learun.httpAsync/httpAsyncGet/httpAsyncPost`。
- Why wrapper：禁止同步 API，归一化 response/loading/network/business error。
- API proposal：`get(url,query,options)`、`post(url,data,options)`、`request(options)`；可返回 jQuery Deferred，需在实现阶段按现有浏览器基线确认。

## WmsUploader

- Purpose：附件与批量导入。
- Underlying：`lr-uploader` / WebUploader 0.1.5。
- Why wrapper：统一文件限制、进度、错误、结果摘要；禁止 Uploadify。
- API proposal：`init({url,accept,maxSize,multiple,onProgress,onComplete})`、`files()`、`clear()`、`destroy()`。

## Wrapper Rules

- wrapper 薄且可追溯，不复制框架源码。
- 只暴露 WMS 实际需要的 API，不做通用组件平台。
- DOM/CSS 全部以 `.wms-page` / `.wms-*` scope。
- 每个 wrapper 必须明确 event namespace 与 `destroy()`，即使 underlying plugin 没有 destroy。
