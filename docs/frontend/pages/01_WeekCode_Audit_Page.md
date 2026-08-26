# Page01 周码异常排查与重算

## 当前状态

Page01 的正式交互为：

`Grid → Preview Drawer → Confirm → Recalculate Result Modal → Grid Refresh`

- `DRAWER_ROLE=PREVIEW_ONLY`
- `RECALCULATE_MODAL_ROLE=EXECUTION_AND_FINAL_RESULT`
- Pager 与 Drawer Foundation contract 不在本页复制或改写。
- Codex 不执行真实 Recalculate POST；最终结果须由 Human Runtime 验收。

## 路由与 API

- 页面：`/WMS/WeekCodeAudit/Index`
- 结果 iframe：`/WMS/WeekCodeAudit/RecalculateResult`
- 查询：`GET /api/wms/weekcode/audit`
- 重算：`POST /api/wms/weekcode/recalculate`

浏览器只使用同源 `/api/wms/*`。不在 Page01 写入后端端口、数据库连接或代理目标。

## Grid

主 Grid 保持真实后端分页与既有列：箱码、厂别、物料、版本、DateCode、包装时间、当前 → 候选、异常类型、风险、Hold、原因。

- 初次进入不自动做无条件查询。
- 查询使用 `grid.reload(filters)` 并回到第 1 页。
- 重算完成使用 `grid.refresh()`，保留当前 filters 与 page。
- 已解决箱从异常 Grid 消失是当前异常状态的正确语义，不代表业务记录被删除。

## Preview Drawer

只有明确的用户行点击会打开只读 Preview Drawer。Preview 展示：

- 基础信息：BoxCode、Box HU ID、Plant、Material、Revision、DateCode、包装时间、Pack Count、Total Qty、Hold。
- 当前周码：WeekRuleSource、WeekRuleValue、WeekYear/WeekNo、WeekSortKey。
- 候选周码：直接使用 Audit API 返回的 candidate 字段。
- Diff：复用 `Wms.AuditDiffCell`。
- 异常判断：AnomalyType、RiskLevel、Reason。
- 预计处理结果：只解释当前 Audit evidence，不表示已经执行。
- 重算范围：PlantCode + MaterialCode + MaterialRevision，不是当前单箱。

前端禁止解析 YYWW/WWYY、计算 ISO week 或猜 candidate。candidate 为空也不永久禁用重算；只要三个 scope 字段齐全且 Learun 权限按钮可见，用户可进入 Confirm，因为 POST 会重新读取执行时 MES 当前规则。

## Confirm 与 POST

Confirm 明确展示 Plant、Material、Revision，并说明：

- 按整个 scope 扫描当前有效库存。
- 周码规则以执行时 MES 当前配置为准。
- 客户端不提交 RuleValue。

确认后生成既有格式 `WCR-UI-{learun.newGuid()}`，关闭 Preview Drawer并打开 Foundation `Wms.Modal` / `learun.layerForm`。Modal iframe 仅接收：

```json
{
  "plantCode": "...",
  "materialCode": "...",
  "materialRevision": "...",
  "operator": "...",
  "requestId": "WCR-UI-..."
}
```

不传 RuleValue、CandidateRuleValue、WeekYear、WeekNo 或 WeekSortKey。Operator 使用 Learun `userinfo.account`，缺失时回退 `userinfo.userId`；两者均缺失则不打开执行 Modal。

## Result Modal

Modal iframe 初始化后自动且仅 POST 一次。POST 使用局部 loading，不使用全屏 loading，也不提供 Retry 按钮。业务 400、服务端错误或网络错误均留在 Modal 内，并保留 RequestId；失败时不触发父 Grid refresh。

成功或部分成功展示：

- Scope、MES Rule Source、MES Rule Value、可复制 RequestId。
- BoxCount、UpdatedBoxCount、UnchangedBoxCount、FailedBoxCount、UpdatedItemCount、ReleasedHoldCount。
- UPDATED / UNCHANGED / FAILED 与 partial success 正确语义。
- item 级 DTO 的完整 old/new week values，通过 `Wms.AuditDiffCell` 显示 Before → After。
- 后端 ErrorCode / ErrorMessage。
- 只有 `releasedWeekCodeHold=true` 才显示已解除 `WEEKCODE_UNRESOLVED` Hold。
- 非周码 Hold（例如 QUALITY_FAILED）保持时，不宣称已解除。
- 明确解释已解决异常会从父 Grid 移除。

Modal 先完整 render result，再通过挂在 iframe element 上的明确 callback 标记执行完成。用户主动关闭已完成的 Modal 时，父页面调用一次 `grid.refresh()`；不模拟查询按钮，并保留当前 filters/page。业务错误或执行中直接关闭不刷新 Grid。

## 权限与生命周期

- 继续使用 `learun-authorize="yes"`；不建立第二套权限系统。
- Grid 查询、刷新、分页前关闭 Preview Drawer。
- Result 不使用 Drawer；不存在 `RECALCULATE_RESULT` Drawer context 或 Last Result Drawer。
- 一个 Modal 生命周期只允许一次 POST；父页面在 Modal 关闭前保持重算入口 pending。

## 验证

静态受控 harness：

```text
node agents/wms-page01-recalculate-ux-harness.js D:\com.delton.gz\Learun.Application.Web
node agents/wms-page01-parent-modal-integration-harness.js D:\com.delton.gz\Learun.Application.Web
```

Human Runtime 必须覆盖：完整 candidate、RULE_MISSING business 400、UPDATED、UNCHANGED、FAILED、partial success、WEEKCODE_UNRESOLVED release、非周码 Hold 保留、Modal 结果保持，以及关闭已完成 Modal 后父 Grid 刷新。

## Encoding

本轮修改的 `.js/.css/.cshtml/.md` 使用 UTF-8 with BOM + CRLF，明显 mojibake 为 0。
