# 周码异常排查与重算 V1

## Deferred UI Issues

`WEEKCODE_PAGE_UI_POLISH_DEFERRED=True`。Page 02 开发期间不修改 Page 01 实现；以下事项留待独立 UI polish 阶段：

1. Pagination visual 在 Human Runtime 中仍未成功显示。
2. Drawer 存在非预期自动打开现象。
3. Recalculate 成功后的 Result Drawer 虽能显示，但会很快自动消失。
4. Drawer 后续职责调整为 Preview / 快速详情，重点展示当前状态 → 预计修复后状态（存在可修复规则时）。
5. Recalculate 最终执行 UI 后续迁移为 Confirm → Modal；Modal 展示执行情况、Before → After、Hold Release、Updated/Unchanged/Failed 和 RequestId，不再使用 Drawer 承载最终执行结果。

## Purpose

本页用于查询当前库存箱的周码异常、查看当前值与候选值，并由用户明确确认后，按厂别 + 物料 + 版本触发服务端周码重算。Audit 查询只读；前端不会自动重算、自动 Hold 或修改 MES/WMS 数据。

## Route

- 页面直达：`/WMS/WeekCodeAudit/Index`
- V1 不注册正式菜单、不修改角色或按钮权限数据。

## Backend APIs

- `GET /api/wms/weekcode/audit`
- `POST /api/wms/weekcode/recalculate`

接口路径和字段来自 `WeekCodeAuditModule`、`WeekCodeAuditModels`、`WeekCodeRecalculateModule` 和 `WeekCodeRecalculateModels`。前端使用相对应用根路径，部署时需保证 Learun Web 同源可访问 Nancy WMS API，或由现有网关提供同路径转发。

## Runtime JSON Contract

浏览器固定调用相对路径 `/api/wms/*`；开发环境由 Learun Server Proxy 转发到 `http://localhost:52602`。Audit 实际响应信封为 `code/message/data`，其中分页数据为 `totalCount/page/pageSize/items`，`items` 中的业务 DTO 属性统一使用真实 HTTP JSON 的 camelCase，例如 `boxCode`、`plantCode`、`weekRuleValue`、`candidateWeekSortKey`、`anomalyType` 和 `isHold`。

`Wms.Api` 仅在响应信封边界兼容 `code/Code`、`message/Message`、`data/Data`。Envelope normalization 不等于 DTO property normalization：`data` 和业务 DTO 原样传递，不递归转换属性大小写，也不建立第二套 PascalCase 前端 DTO。Recalculate 的出站请求仍按后端绑定合同显式组装 `PlantCode/MaterialCode/MaterialRevision/Operator/RequestId`，但范围值来自所选 camelCase 行。

## Frontend Components

- `Wms.SearchPanel`：查询、重置、Enter 查询。
- `Wms.Grid` / jfGrid：后端分页数据装载、loading/empty/error。
- `Wms.StatusTag`：风险、Hold 和结果状态。
- `Wms.AuditDiffCell`：HTML encode 的当前值 → 候选值紧凑 diff。
- `Wms.SideDrawer` + `Wms.DetailPanel`：异常快速详情。
- `Wms.ConfirmDialog`：重算二次确认。
- `Wms.ResultPanel` + `Wms.SideDrawer`：批次摘要、箱级结果和详细错误。
- `Wms.WeekCodeAuditApi`：页面薄 API adapter；页面不直接调用 AJAX。

## Query Contract

只发送后端 Audit Module 实际支持的参数：

- `plantCode`
- `materialCode`
- `materialRevision`
- `anomalyType`
- `riskLevel`
- `isHold`
- `page`
- `pageSize`

页面不发送 BoxCode。初次进入不自动执行无条件查询，避免触发后端 5000 候选记录保护；用户查询后可刷新当前条件。

## Grid Contract

每页 50 条。`Wms.Grid` 的异步数据源适配将当前页和筛选条件交给 `Wms.Api`，Audit API 返回 `totalCount/page/pageSize/items`；浏览器不加载全量数据后再分页。空结果显示“暂无周码异常”。

主列为箱码、厂别、物料、版本、DateCode、包装时间、当前 → 候选、异常类型、风险、Hold 和原因。DTO 的其他信息进入 Drawer，避免主 Grid 继续横向膨胀。

## Pagination Contract

真实 jfGrid 原生 pager 只支持由 jfGrid 自己持有 `url` 并直接执行 AJAX；它没有供外部 `Wms.Api` datasource 注入分页结果和 page callback 的稳定合同。因此异步 `Wms.Grid` 保持 `isPage:false`，只用 jfGrid 渲染 `rowdatas`，并由 Foundation 提供 35px 的薄 Learun 风格 server pager。该 pager 不维护第二份业务数据，只把 `pageSize/page/totalCount` 映射为当前页、总页数和按钮状态；例如 348 条、每页 50 条映射为 7 页。首页、上一页、数字页、下一页和尾页均调用同一个异步 `loadData`，请求使用 `page=<target>&pageSize=50` 并保留当前筛选条件。

查询调用 `reload(filters)`，固定回到第 1 页；普通分页不改变 filters；Reset 清空 filters 和分页状态。重算后的 `refresh()` 保留当前页；若异常减少导致当前页大于新的最大页，Grid 自动加载最后有效页，不留下越界空页。Grid surface 明确为 pager 预留 35px，不修改 Learun 原生 `jfgrid.js`。

Audit GET 的 loading owner 是 `Wms.Grid` 的 grid-local state；`Wms.Api` 对该 GET 显式 `loading:false`，页面层不再维护 loading。每次 load 使用递增 request sequence，旧响应不能覆盖新查询。成功、业务错误、network rejection、response adapter/jfGrid formatter/render callback 异常都在同一 load lifecycle 内结束 pending；渲染错误显示“数据已返回，但列表渲染失败”，不会永久保留 loading overlay。

## Drawer Contract

单击行打开只读 Drawer，使用 DetailPanel 分四组：

1. 基础信息：Box/HU、厂别、物料、版本、DateCode、包装时间、Pack 数和数量。
2. 当前周码：规则来源/值、年、周、排序键。
3. 候选周码：候选规则、年、周、排序键。
4. 异常判断：友好文本、原始 AnomalyType、风险、Hold、原因。

## Recalculate Contract

前端只提交：

```json
{
  "PlantCode": "...",
  "MaterialCode": "...",
  "MaterialRevision": "...",
  "Operator": "...",
  "RequestId": "..."
}
```

- 范围来自所选异常行的厂别 + 物料 + 版本；不提交 BoxHUId/BoxCode。
- 不提交 RuleValue、CandidateRuleValue、WeekYear、WeekNo 或 WeekSortKey；服务端重新读取 MES Flow Rule。
- Operator 优先使用 Learun `userinfo.account`，缺失时使用 `userinfo.userId`；两者都缺失则阻止提交。
- 用户在 Confirm 中真正确认后才生成 `WCR-UI-<learun.newGuid()>`；同一次 HTTP 提交只使用一个 RequestId。
- 提交使用 `Wms.Api.postJson()`；按钮和 Confirm callback 在 HTTP 返回前保持 pending 防重复。
- HTTP 200 即使包含失败箱，也展示 Result Drawer 并刷新当前 Audit；部分成功不降级为整体失败。
- HTTP 400 显示后端真实 Message，不创建成功 Result Drawer。

## Recalculate Result UX

用户真正确认、发送 POST 前，页面在内存中冻结当前页内同一厂别 + 物料 + 版本的 Audit snapshot；不写入 localStorage。响应返回后按以下顺序处理：保存 Last Result → 打开“周码重算结果”Drawer → 刷新当前 Audit 页 → 按重算 group 全分页复查当前异常 → 更新 Drawer 的“仍异常/已移出”结论。Grid 刷新、查询和分页不会清空或关闭 Last Result；Last Result 只在下一次重算或页面 reload 时替换。

Drawer Header 展示 Plant、Material、Revision、总体状态及可复制 RequestId。Summary 展示 BoxCount、UpdatedBoxCount、UnchangedBoxCount、FailedBoxCount、UpdatedItemCount 和 ReleasedHoldCount。箱级明细展示 UPDATED/UNCHANGED/FAILED、item 数、Hold 结果、错误以及每个 DateCode 的 Before → After。

全部成功、部分成功和失败使用不同的明确文本；部分成功不会显示成“重算成功”。Toast 保持简短，完整证据留在自动打开的 Result Drawer。

## Before / After

Recalculate response 在原有 endpoint 和 transaction 内补充 RequestId、BeforeIsHold/AfterIsHold，以及 item 级 old/new WeekRuleSource、WeekRuleValue、WeekFormatCode、WeekYear、WeekNo、WeekSortKey。值来自重算已持有的 fact/plan/transaction memory，不为展示再次查询数据库。

- UPDATED：展示完整 Before → After；若释放 Hold，明确显示“已释放 WEEKCODE_UNRESOLVED Hold”。
- UNCHANGED：Before 与 After 都返回，Result 明确为 UNCHANGED；不制造 UPDATE 或 OperationLog。
- FAILED：保留 Before、错误码和错误信息；After 显示“未应用”，不假设事务写入成功。

## Result Persistence

页面内的 Last Result 与 Result Drawer 独立于表示当前异常状态的 Grid。自动刷新使用 `grid.refresh()`，不会先关闭 Drawer；普通查询和翻页也不会清除 Last Result。用户主动关闭 Drawer 后 V1.2 不自动重开，也不增加额外入口。

## Audit Row Disappearance Semantics

Audit Grid 只表示 CURRENT ANOMALY STATE。已解决异常在刷新后从 Grid 消失是预期行为，不代表业务数据被删除。为避免分页误判，页面以 group 条件调用现有 Audit GET，并按最大 200 条逐页读取完整异常集合，再按 BoxCode 匹配：

- 能在完整 group 结果中找到：显示“重算完成，但当前仍存在周码异常”，并给出当前异常类型和原因。
- 完整 group 查询成功且找不到：显示“当前已不再被周码 Audit 判定为异常，已从异常列表移除”。
- 复查请求失败：只说明异常列表已刷新/核对失败，不宣称异常已消除。

Resolved anomaly disappearing from Grid is expected behavior. Result history belongs to Result Drawer / OperationLog，不会把已修复箱强行混回异常 Grid。

## Permission

重算按钮使用 `learun-authorize="yes"`，不建立自定义权限系统。正式菜单和“执行 Recalculate”按钮权限仍需页面通过后由 Human 配置。

## Encoding

本页及修改的 Foundation `.js/.css/.cshtml` 使用 UTF-8 with BOM；HTML 布局声明 UTF-8。交付检查要求首字节 `EF BB BF` 且明显 mojibake 为 0。

## Human Test Cases

- CASE UI01：打开 `/WMS/WeekCodeAudit/Index`。
- CASE UI02：查询真实 Audit。
- CASE UI03：按物料过滤。
- CASE UI04：打开异常详情 Drawer。
- CASE UI05：观察 Suspicious Rule Direction Diff。
- CASE UI06：检查 Rule Missing 及原始 code。
- CASE UI07：点击 Recalculate 并检查范围/影响说明。
- CASE UI08：取消 Confirm，确认没有 POST。
- CASE UI09：Human 执行一次真实 Recalculate。
- CASE UI10：检查 Result Drawer 的全部/部分成功与箱级错误。
- CASE UI11：确认 Audit 自动刷新。
- CASE UI12：确认异常消失或 WEEKCODE_UNRESOLVED Hold 已释放。

补充检查：1366×768 与 1920×1080；分页上一页/下一页；无结果；业务 400；Drawer 重复开关 10 次；重算按钮双击；权限按钮显示；浏览器控制台。

## Known Limitations

- Runtime 同源链路已由 Human 验证联通；本轮绑定修复仍需登录后的 Human 页面刷新确认真实 Grid、Drawer 和重算范围显示。
- Codex 未执行且不得执行真实 Recalculate POST。
- 本轮没有注册菜单或权限数据；直接 URL 下重算按钮的实际授权显示取决于 Learun 当前模块/按钮权限上下文。
- jfGrid 没有可注入的异步请求 hook。Foundation 以 WMS 自有的紧凑分页条驱动 jfGrid 每页渲染，后端仍是真正分页源；排序在本页关闭，因为 Audit API 没有排序合同。
