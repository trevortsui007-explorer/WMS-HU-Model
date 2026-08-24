# WMS Modal / Drawer 使用指南

## 1. 选择矩阵

| 场景 | 组件 | 原因 |
|---|---|---|
| 查看 HU 摘要 | `Wms.SideDrawer` | 保留列表与筛选上下文 |
| 查看日志详情 | `Wms.SideDrawer` | 只读、轻量、快速返回列表 |
| 查看入库技术详情 | `Wms.SideDrawer` | 辅助信息不应中断主任务 |
| 编辑货位 | `Wms.Modal` | 需要独立表单、校验和提交生命周期 |
| 新增基础数据 | `Wms.Modal` | 多字段复杂表单 |
| 复杂筛选器配置 | `Wms.Modal` | 配置内容超出搜索区的轻量交互范围 |
| Recalculate 二次确认 | `Wms.ConfirmDialog` | 高影响动作必须明确确认，可要求 typed text |
| Recalculate Result | `Wms.SideDrawer` / 后续 ResultPanel | V1 用 Drawer 展示；专用 ResultPanel 属于后续 P1 |
| 查看 InnerPack 明细 | `Wms.SideDrawer` | 只读层级明细，保留 HU 上下文 |
| 复杂 InnerPack 修改 | `Wms.Modal` | 复杂编辑需要 iframe 生命周期 |
| 删除、释放、作废、发运等高影响动作 | `Wms.ConfirmDialog` | 明确二次确认；必要时使用 typed confirm |
| 普通成功/失败提示 | Learun toast / `learun.alert.*` | 不应为简单通知创建新容器 |

固定决策：`QUICK_DETAIL=Drawer`、`COMPLEX_FORM=IframeModal`、`HIGH_IMPACT_ACTION=ConfirmDialog`。

## 2. Drawer 规则

- Drawer 只承载当前页面的快速详情，不加载新的 iframe。
- 一个页面同一时刻只保留一个活动 Drawer；再次打开同一实例会更新内容。
- `open()` 后可调用 `setContent()` 或 `setFooter()`；`close()` 关闭，`destroy()` 解绑命名空间事件并移除 DOM。
- Drawer 内允许打开 Modal：`DRAWER_CAN_OPEN_MODAL=True`。
- Modal 保存成功后，先刷新 Drawer 当前详情，再刷新主 Grid：`MODAL_SUCCESS_REFRESHES_DRAWER=True`、`MODAL_SUCCESS_REFRESHES_GRID=True`。
- 页面卸载时必须调用 `destroy()`，不能只隐藏节点。

## 3. Modal 规则

- 统一通过 `Wms.Modal.open()`，底层只调用 Learun `learun.layerForm`；禁止业务页面直接拼装 iframe layer。
- 表单页使用 Learun `_Form` 布局，并暴露 `acceptClick(callback)`。保存成功时由表单页调用 callback，宿主页在 `onSuccess` 中刷新状态。
- 默认最大嵌套深度为 1：`DEFAULT_MAX_MODAL_DEPTH=1`。第一层 Modal 内不得再打开第二层 Modal。
- 标题、URL、宽高和回调均由调用方显式传入；URL 使用 `Wms.Util.rootUrl()` / `appendQuery()` 处理。
- `onSuccess` 只在表单 callback 成功时执行；`onCancel` 只处理关闭/取消；`onComplete` 用于两种路径的共同清理。
- Modal 打开期间由 Learun layer 提供加载态；提交按钮的防重复逻辑仍由表单页负责。

```javascript
Wms.Modal.open({
    id: 'wms-edit-demo',
    title: '编辑示例',
    url: Wms.Util.rootUrl('/WMS/WMSFoundationDemo/ModalDemo?id=' + id),
    width: '720px',
    height: '480px',
    onSuccess: function (result) {
        drawer.setContent(renderDetail(result));
        grid.reload();
    }
});
```

## 4. ConfirmDialog 规则

- 普通高影响动作使用 `Wms.ConfirmDialog.open()`。
- 极高风险动作使用 `Wms.ConfirmDialog.typed()`，要求输入指定文本后才执行回调。
- 确认回调只负责调用统一 `Wms.Api` 并刷新界面；不得绕过 API 层直接写数据。
- Foundation Showcase 的确认框仅演示回调，不调用业务 API，不写生产数据。

## 5. 生命周期与焦点

- 每个实例必须支持或提供明确的 `destroy()` 路径。
- 事件统一使用 `.wms*` 命名空间；重新初始化前先解绑本包装层事件。
- Drawer/Modal 关闭后将焦点还给触发元素；页面切换或卸载时释放 Registry 中的实例。
- 不修改 Learun、Bootstrap 或第三方源码。底层组件自身未提供可靠销毁 API 时，包装层只销毁自己创建的 DOM、事件和状态，并复用底层实例，避免重复绑定框架级全局事件。

## 6. 人工验收

1. 打开/关闭 Drawer 10 次，确认只存在一个遮罩和一个 Drawer，关闭后焦点返回。
2. 打开/取消 Modal 10 次，确认没有叠加 iframe、遮罩或重复回调。
3. Drawer 中打开 Modal，模拟保存后确认 Drawer 和 Grid 各刷新一次。
4. Confirm 取消时不得执行动作；确认时只执行一次。
5. 页面离开后检查 Registry 已清理，控制台无新增异常。
