# Custom Build Candidates

仅列出现有基础设施没有提供、且 WMS UX 确实需要的组件。实现基线仍是 jQuery + HTML + scoped CSS。

## 1. WmsStatusTag

状态集合：ACTIVE、HOLD、SHIPPED、FAILED、UPDATED、UNCHANGED、RECEIVED、DUPLICATE。要求语义色 + 文本 + 可选图标，支持 compact/grid 模式。优先级：P0。

## 2. WmsSideDrawer

当前页面 iframe 内的右侧详情层，支持 overlay、ESC、focus return、loading、宽度档位和 dirty guard。复杂编辑仍转 `WmsDialog`。优先级：P0。

## 3. WmsDetailPanel

统一 Key/Value、分组、空值、复制、monospace identifier 和 responsive 两列/一列布局。优先级：P0。

## 4. WmsJsonViewer

JSON parse error、pretty print、折叠、复制、长文本 wrap、搜索；大 payload 设渲染上限。不得使用 `innerHTML` 注入原始 JSON。优先级：P1。

## 5. WmsResultPanel

用于重算/批处理结果：boxCount、updatedBoxCount、unchangedBoxCount、failedBoxCount、releasedHoldCount；支持摘要 + 可展开失败明细。优先级：P1。

## 6. WmsMetricCard

KPI value、label、trend/secondary text、status tone、click-through；不在组件中自行请求 API。优先级：P1。

## 7. WmsAuditDiffCell

Grid formatter/详情组件，展示 before/after、added/removed/changed，并提供长值 tooltip/drawer。必须 HTML encode。优先级：P1。

## Not Custom Build

- `WmsGrid`：包装 jfGrid。
- `WmsSelect`：包装 lrselect。
- `WmsHuTree`：包装 lrtree；性能验证失败前不重写树。
- `WmsTimeline`：包装 lrtimeline。
- `WmsConfirmDialog`：包装 Learun layer。
- `WmsSearchPanel`：组合既有 layout/query/collapse pattern。

## CSS Recommendation

采用方案 C 的渐进版本：建立 WMS component CSS，同时保留旧组件行为。

```css
.wms-page { /* tokens and page baseline */ }
.wms-page .wms-grid { /* jfGrid scoped skin */ }
.wms-page .wms-search-panel { /* query layout */ }
.wms-page .wms-status-tag { /* custom component */ }
```

建议 token：颜色、字体、4/8px spacing、radius、shadow、control height、grid density、focus ring、status palette。不得用全局 `.btn`、`.table`、`.form-control` 覆盖影响旧系统。
