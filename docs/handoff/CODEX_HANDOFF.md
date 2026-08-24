# WMS 成品仓正式出库后端交接

## 2026-08-24 Page 07 Audit Log Center V1

- 状态：`PAGE_06_MONITORING_COMPLETE=True`、`PAGE_07_AUDIT_CENTER_IN_PROGRESS=True`；实现、构建、自动化与真实 GET-only Runtime 核验完成，等待 Human Visual Review。
- Sources：真实事实表为 `WMS_HU_OperationLog`、`WMS_HU_MoveLog`、`WMS_HU_HoldLog`。Page 03 的三个 API 仅支持单 HU Top N，不能复用为全局筛选/分页；保留其合同并复用字段语义、`Wms.JsonViewer`、`Wms.Timeline`、`Wms.StatusTag`。
- API：新增只读 `/api/wms/audit/operations`、`moves`、`holds`、`request-trace?requestId=`、`capabilities`。默认 7 天、最大 90 天、pageSize 默认 50/最大 200；服务端筛选/分页、事件时间 DESC + Id DESC。真实 Release RequestId 含 `:`，Trace 使用 query 参数避免 ASP.NET path validation 拒绝。
- Trace：Operation/Move/Hold 三类 exact RequestId bounded read，返回原始行并按时间升序合并显式 OPERATION/MOVE/HOLD/RELEASE events；未知 RequestId 返回空事实。Detail 与 Trace 共用一个 Drawer，不嵌套。
- Capability：`MOVE_AUDIT_AVAILABLE=True`、`OPERATION_AUDIT_AVAILABLE=True`、`HOLD_AUDIT_AVAILABLE=True`；`MASTER_DATA_AUDIT_AVAILABLE=False`、`PRE_RECEIPT_INBOUND_FAILURE_AUDIT_AVAILABLE=False`。Capability 明确 NOT_OBSERVABLE 不等于 0；未新增 audit table。
- Runtime GET：90 天 Operation 6412，page1/2 各 10、0 overlap、稳定倒序；B1951228B 3；WeekCode Recalculate 44、RECEIVE 56、PUTAWAY 3。Move RECEIVE 57。Hold 340（Active 298、Released 42，WeekCode reason 339）。Trace 样本分别为 Operation 21、Move 1、Release 1、unknown 0；未发现真实跨表同 RequestId，自动化 fake 覆盖多源合并。
- Sensitive detail：抽查最新 200 条 Operation DetailJson，未命中 password/token/authorization/cookie/connectionString/stackTrace，`SENSITIVE_AUDIT_DETAIL_FOUND=False`；结果为样本结论。Viewer 使用 text rendering/invalid JSON fallback。
- Frontend：默认 Operation；Move/Hold/Capability lazy-load；公共与 Tab-specific filters、server pager、dense grids、只读 Detail Drawer、Request Trace Timeline、HU→Page03 frameTab。Page load/query/reset/render/page change 均关闭 Drawer，真实 cell mousedown 才允许 row click 打开。
- Verification：WMS Debug build PASS；64 tests PASS；Learun Debug build PASS（0 error，既有 warning）；2 JS syntax PASS；受控 Page07 lifecycle contract PASS（临时 harness 已删除）；前端 UTF-8 BOM + CRLF。应用内 Browser 初始化冲突，不能宣称 UI click/visual 通过。
- Safety：无 schema/table/audit table/migration 变更；未执行 POST/DML/DDL；新 Repository typed ADO.NET SELECT-only，无 `dynamic`、`AddWithValue`；Learun 无 direct DB。
- Documentation：`docs/frontend/pages/07_Audit_Log_Center_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区包含前序页面未提交改动，本轮未创建提交。

## 2026-08-24 Page 06 System / Interface Runtime Monitoring V1

- 状态：`PAGE_05_MASTER_DATA_IMPLEMENTED=True`、`PAGE_06_MONITORING_IN_PROGRESS=True`；Page 06 实现、构建、自动化和真实 GET-only Runtime 核验完成，等待 Human Visual/Proxy 502 Review。
- Observability：已扫描 WMS、WMS-HU-Model、Learun、health endpoints、Receipt/HU logs、Learun LogEntity/log4net、Nancy diagnostics 和 `Application_Error`。成功 Receipt 不能代表总请求；Receipt 创建前失败没有完整持久化、可查询来源。`INBOUND_REQUEST_TOTAL_OBSERVABLE=False`、`INBOUND_FAILURE_HISTORY_PERSISTED=False`、`INBOUND_FAILURE_HISTORY_QUERYABLE=False`、`SUCCESS_RATE_METRIC_ALLOWED=False`。
- API：新增只读 `GET /api/wms/monitoring/overview` 与 `GET /api/wms/monitoring/activity?days=7|14|30`。固定 WmsDb/MesDb 3 秒最小 `SELECT 1` 探针；依赖失败以 HTTP 200 + `DEGRADED` 返回，消息脱敏。没有任意 connection/sql/table/path/target 参数。
- Metrics：自然日成功 Receipt/Box/Pack/Qty；当前 active Hold Box、Page 01 语义的 WeekCode anomaly、入库后异常 Receipt。Activity 后端按日聚合成功 Receipt 并补零，复用 Page 04 Read Service 返回最近 20 条；不是请求趋势或成功率。
- Frontend：新增 Learun `/WMS/Monitoring/Index`，六个紧凑 `Wms.MetricCard`、Bootstrap tabs、手动刷新、Activity lazy load、现有 ECharts 3.6、最近活动 Grid、当前异常摘要及 Page 01/02/04 `frameTab` 导航。Proxy 502 分支明确显示 WMS 不可用、业务指标不可用而不是 0；无永久 Loading。`NOT_OBSERVABLE` 显示“当前不可追溯”，没有空失败表。
- Runtime GET：overview HTTP 200，WmsDb/MesDb CONNECTED；2026-08-24 自然日成功 Receipt/Box/Pack/Qty 均 0，active Hold Box 298、WeekCode anomaly 308、inbound exception Receipt 34。7-day activity HTTP 200，2026-08-19 为 8/13/13/192，2026-08-20 为 46/1122/3659/230739，recent committed Receipts 20 条。
- Safety：无 schema/table/log table/migration 变更；未执行 POST、DML 或 DDL。新 Repository 为 typed ADO.NET SELECT-only，无 `dynamic`、`AddWithValue`；Learun 无 direct DB、业务 JS 无端口和敏感连接信息。
- Verification：WMS Debug build PASS；58 tests PASS；Learun Debug build PASS（0 error，既有 warning）；Monitoring/Foundation JS syntax PASS；受控 Proxy 502 前端合同 PASS（临时 harness 已删除）；前端 UTF-8 BOM + CRLF。应用内 Browser 初始化报 `Cannot redefine property: process`，因此不宣称 UI click/visual 通过。
- Documentation：`docs/frontend/pages/06_System_Interface_Monitoring_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区包含前序页面未提交改动，本轮未创建提交。

## 2026-08-22 Page 05 WMS Master Data Maintenance V1

- 状态：`PAGE_04_FINISHED_GOODS_INBOUND_COMPLETE=True`、`PAGE_05_MASTER_DATA_IN_PROGRESS=True`；实现、自动化、编译和真实只读 API 核验完成，等待 Human UI/controlled-write Review。
- Ownership：只维护 WMS-owned `WMS_Warehouse`、`WMS_WarehousePlant`、`WMS_Location`；未增加 MES/SAP 主数据 CRUD、schema、table、status dictionary 或 migration。
- Schema contract：自然键分别为 `WarehouseCode`、`WarehouseId + PlantCode`、`WarehouseId + LocationCode`。所有自然键在编辑时不可变；SAP Storage Location 可编辑且允许 NULL。
- API：复用现有 Location model/repository/service/GET/POST create；新增强类型 Warehouse/WarehousePlant read/create/update/enable/disable、Location maintenance query/POST update/enable/disable。Learun 只通过 `/api/wms/*` 和 `Wms.Api`；无 direct DB。
- Rules：Warehouse 停用保护 active Location/current HU/active mapping；新 Mapping/Location 要求 active Warehouse；Location 停用一次聚合检查 Current HU。六个 runtime-confirmed system virtual Location 由 Service allowlist 保护，无物理删除。
- Frontend：单页 Bootstrap tabs，Warehouse 默认加载，Mapping/Location 首次点击 lazy-load；各 Tab 独立 search state。CRUD 使用 jfGrid/Wms.Grid + Wms.Modal/lr-form/lr-validator/lrselect，状态切换使用 Wms.ConfirmDialog；写按钮复用 Learun authorization。Modal 增加失败 release callback，避免保存失败后 pending lock。
- Audit：现有 OperationLog 语义属于 HU，未污染复用；无通用 Master Data audit pattern，`MASTER_DATA_AUDIT_REUSED=False`、`MASTER_DATA_AUDIT_GAP=True`。WarehousePlant 真实表缺少 modify actor/time 字段。
- Runtime GET：FG01 1 个 Warehouse；1010/11C3、1020/12C3、9010/NULL 三个映射；10 个 Location；六个系统虚拟货位；Location current-HU aggregate 59。均为只读证据，未硬编码。
- Safety：未执行 WMS_DEV DML/DDL，未发 create/update/enable/disable 请求；FG01、生产映射和现用 Location 未修改。
- Verification：WMS-HU-Model/WMS solution build PASS（0 warning/0 error）；Learun Debug build PASS（0 error，既有 cache/source warning）；53 tests PASS；4 JS syntax PASS；Page 05 前端与修改的 Foundation/project XML 均为 UTF-8 BOM + CRLF；mojibake、direct port、direct AJAX、dynamic、AddWithValue、DELETE、Drawer、MES MD 引用均为 0。应用内 Browser 初始化冲突，不能宣称 UI click test 通过。
- Documentation：`docs/frontend/pages/05_WMS_Master_Data_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区含前序页面的未提交改动，本轮未创建提交。

## 2026-08-22 Page 04 Finished Goods Inbound Records / Exceptions V1

- 状态：`PAGE_03_HU_DETAIL_HISTORY_COMPLETE=True`、`PAGE_04_FINISHED_GOODS_INBOUND_IN_PROGRESS=True`；实现、编译、自动化和真实只读 API 验证完成，等待 Human UI Review。
- API 调查：原有两个 finished-goods inbound API 均为 POST 写入口；没有 Receipt query/detail GET。新增只读 `GET /api/wms/inbound/finished-goods/receipts/` 与 `GET /api/wms/inbound/finished-goods/receipts/{receiptId}`。
- 失败历史结论：Receipt/HU/Relation/Item/Hold/Move/Operation 在同一 serializable transaction；硬失败整体 rollback，且没有 FGI failure log。因此 `REJECTED_INBOUND_HISTORY_PERSISTED=False`、`REJECTED_INBOUND_HISTORY_QUERYABLE=False`。Tab 明确命名“入库后异常”，只展示成功入库后的 Hold / WeekCode evidence。
- 查询语义：1 行 = 1 `WMS_FinishedGoodsInboundReceipt`；通过现有 `SourceBusinessNo='FGI-'+ReceiptId` 关联 HU。一次 Repository 聚合得到 Pallet/Box/Pack、实际 Item Qty、distinct material、Hold 与 WeekCode exception，全部筛选与分页在服务端执行，无 N+1。
- Detail：lazy GET，Pallet 全量、OUTER_BOX 前 50 + `hasMoreBoxes`；新增现有 Location Repository 的 batch read，一次解析 direct/effective location。支持无 Pallet、多 Pallet、多 Material；未知 Receipt 404。
- Frontend：Learun 原生 toolbar/nav-tabs/jfGrid/SideDrawer；`Wms.Api` + `/api/wms/*` + camelCase。初始、查询、reset、render、paging 都关闭 Drawer，只有真实 cell mousedown 后的 row click 打开。无 Retry/Reprocess/Release/Recalculate/Move/Delete/Export。
- Integration：Page 04 Box 使用 `learun.frameTab` 打开 Page 03；Page 02 Preview 增加 20 行以内的“查看完整 HU 履历”入口。
- Runtime GET：54 Receipts；page 1/2 各 10 且跨页 0 重复；Plant/SourceEntityId/RequestId/date/Box/Material filters 均命中；34 current exception、20 normal；WeekCode 34、Hold 34；Receipt 57 为 61 Box/251 Pack/14 Material/Qty 19210，详情前 50 且 inherited `INBOUND-PENDING`；Receipt 11 验证 no-pallet + 2 boxes；unknown 404。
- Verification：WMS-HU-Model/WMS/Learun Debug build PASS；49 tests PASS；4 JS syntax PASS；相关 frontend UTF-8 BOM + CRLF、mojibake 0。Learun build 仅有原有 warning。应用内 Browser 运行环境无法初始化，因此 UI01–UI06 不宣称通过，需 Human Review。
- Schema/data：无 schema/table/status/transaction/migration 变更；未执行 WMS_DEV DML，未调用 inbound POST。
- Documentation：`docs/frontend/pages/04_Finished_Goods_Inbound_Records_Page.md`。
- Foundation deferred：Pager visual、Page 01 Drawer lifecycle、Page 01 Recalculate Modal migration。
- Git commits：none；工作区有既有未提交改动，本轮未创建提交。

## 2026-08-22 Page 03 HU Detail / History V1

- 状态：`PAGE_02_FINISHED_GOODS_INVENTORY_COMPLETE=True`、`PAGE_03_HU_DETAIL_HISTORY_IN_PROGRESS=True`。
- 当前边界：只读完整 HU 详情与履历；先核对真实 HU/Relation/Location/Item/MoveLog/OperationLog/HoldLog 契约，不增加写动作、schema、table、migration 或业务状态。
- Page 02：本轮先不增加“查看完整履历”入口；待 Page 03 Human Review 通过后再做最小 frameTab integration。

### Implementation result

- Scope: implemented standalone read-only `/WMS/HuDetail/Index?huCode=...`; Page 02 was not modified or integrated.
- API inventory: `docs/frontend/pages/03_HU_Detail_History_Page.md`. Existing location and MoveLog logic is reused; OperationLog/HoldLog had no read endpoint.
- Backend: added `GET /api/wms/hus/{huCode}/detail`, `/operations?top=`, `/holds?top=`. Supports all three HU types, at most two ACTIVE-parent elevations, one current-tree query, backend direct/effective location, typed contents and active Hold. New histories default 50/cap 200.
- Frontend: Tree + Detail/Bootstrap tabs, `WmsHuTree` over `lrtree`, `WmsTimeline` over `lrtimeline`, safe `WmsJsonViewer`, per-HU lazy history cache and stale-response guards. No write controls.
- Schema/data: no schema/table/migration change and no WMS_DEV DML. New repository statements are SELECT-only and HU-scoped.
- Runtime GET: `B1951228B` detail 200, root `PLT-99153`, tree 313, contents 4, direct empty/effective `INBOUND-PENDING`/inherited true. `PLT-99153` returned PALLET with 61 box summaries/direct location. `T840064325` returned INNER_PACK with one Item/inherited location. Existing moves and new operations/holds returned 200. Unknown HU returned 404.
- Verification: backend library/host builds PASS; Learun Debug build PASS with existing warnings; 46 tests PASS; Page 03/Foundation JS syntax checks PASS; frontend files UTF-8 BOM + CRLF. Application browser could not initialize because of its runtime collision, so Human Visual Review remains required.
- Unverified: no SHIPPED sample was discoverable via used read APIs. Explicit SHIPPED lookup is lifecycle-unfiltered in service code and covered by automated test. A separate SELECT attempt from PowerShell failed before query execution due local SSPI context.
- Recommended review: OUTER_BOX direct URL → tree/header/location → INNER_PACK click → Move → Operation/JSON/copy → Hold/current-vs-history → unknown HU → known SHIPPED HU → confirm no write action.
- Git commits: none; the workspace contains prior uncommitted work and no commit was requested.

## 2026-08-22 Page 02 Finished Goods Inventory / HU Query V1

- 状态：`PAGE_01_UI_POLISH_DEFERRED=True`、`PAGE_02_FINISHED_GOODS_INVENTORY_IN_PROGRESS=False`、`FINISHED_GOODS_INVENTORY_FRONTEND_V1_IMPLEMENTED=True`。Page 01 仅记录延期项，没有修改实现。
- 真实 API 调查：原有 HU read API 仅覆盖单 HU 基础信息、树、有效位置、MoveLog、Location 主数据、Repack box 状态和 WeekCode anomaly；没有 current inventory server paging API。完整 `CURRENT_HU_READ_APIS` 见 `docs/frontend/pages/02_Finished_Goods_Inventory_Page.md`。
- 后端：新增只读 `GET /api/wms/inventory/finished-goods/` 与 `GET /api/wms/inventory/finished-goods/{boxCode}/preview`，以及最小 DTO/Service/ADO.NET Repository/Nancy Module。所有参数为 typed `SqlParameter`，无 `dynamic`、`AddWithValue`、DML、DDL 或新依赖。
- 当前库存语义：`OUTER_BOX ACTIVE` + active Relation + `INNER_PACK ACTIVE` + active Item + effective location；direct box location 优先、active parent location fallback；`SHIPPED` 排除，Hold 保留显示；数量为全部当前有效 Item Qty SUM。
- 前端：新增 `/WMS/FinishedGoodsInventory/Index`、紧凑 Learun 查询区、`Wms.Grid` server paging（50/页）、camelCase adapter、Hold/status、multi-week summary 和 PREVIEW ONLY Drawer。页面初始/查询/Reset/Grid render/翻页均关闭 Drawer；仅 grid cell 真实 mousedown 后的行选择允许打开。
- Preview：一次只读请求返回 box、effective location、WeekCode groups 与 InnerPack list；没有 Move/Hold/Release/Pick/Ship 等写动作，也没有 Reservation/Allocation 伪状态。
- Runtime GET：list HTTP/API 200，TotalCount 1136；page 1/2 无重复，箱码/物料筛选有效；`B1951228B` Preview HTTP/API 200，4 packs、Qty 320=pack sum、父级 location fallback 到 `INBOUND-PENDING`。
- 验证：WMS-HU-Model、WMS Nancy Host、Learun Web Debug build PASS；控制台 43 tests PASS；两个新增 JS `node --check` PASS；前端 UTF-8 BOM + CRLF、mojibake 0、read-only/static boundary checks PASS。Learun build 仅有既有 warnings。
- 未验证：Codex in-app Browser 因当前浏览器运行环境初始化冲突未执行登录态 UI01–UI06，不宣称视觉/点击验收通过。Human 需直达 `/WMS/FinishedGoodsInventory/Index` 完成首次 Drawer closed、Query/Loading、真实 Row click、二次查询不自动打开、Hold 与多 Pack Preview。
- Schema/API 边界：`NEW_SCHEMA_ADDED=False`、`NEW_TABLE_ADDED=False`、`DIRECT_DB_FROM_LEARUN=False`；未执行 migration、POST 或数据库写入。
- Foundation deferred：Pagination visual、WeekCode Drawer lifecycle、WeekCode Recalculate final result Modal migration。本轮未修改 Foundation pager 或 Page 01。
- Git commits：本次未创建提交；Learun Web 为非 Git 工作副本。

## 2026-08-22 WMS Grid Runtime Loading / Pagination Fix

- Runtime 现象：Human 已确认 Audit GET/Proxy HTTP 200，但最近 native pager 适配后 Grid 一直显示“正在加载数据”。Audit GET 没有启用 `Wms.Api` 全局 loading；实际 owner 是 `Wms.Grid.setState("loading")` 的 grid-local overlay。
- 根因：jfGrid native pager 只在 `isPage && url` 时创建，且所有 pager handler 都直接调用 jfGrid AJAX。Foundation 用 synthetic URL 初始化、随后清空 URL并接管 handler，强行组合外部 `Wms.Api` datasource，不属于 jfGrid 支持的合同；`jfGridSet("refreshdata")` 后的任何 native render/formatter exception 又未被 try/catch/finalize 包住，导致 state 停留 loading。
- 修复：外部 datasource Grid 恢复 `isPage:false`，jfGrid 只负责 `rowdatas`；Foundation 提供 35px Learun 风格 server pager，支持首页/上一页/数字页/下一页/尾页，仍是真实 server pagination。未修改 `jfgrid.js`。
- Loading lifecycle：Audit query 显式 `Wms.Api loading:false`；Grid 使用 request sequence + pending flag。SUCCESS、BUSINESS_ERROR、NETWORK_ERROR、GRID_RENDER_ERROR、CALLBACK_ERROR 均结束 pending；旧请求结果被忽略，不能覆盖最新查询。渲染异常显示安全错误文案，不输出 stack trace。
- 边界：未修改 WMS/WeekCode/Recalculate backend、Proxy、数据库、连接、CORS 或 DTO；未执行 Recalculate。
- 验证：受控 Grid lifecycle harness 覆盖 success/business/network/render/stale-response 路径并 PASS，临时 harness 已删除；2 个修改 JS `node --check` PASS；3 个修改的前端 JS/CSS 均为 UTF-8 BOM + CRLF，mojibake 0；Learun Debug build PASS（0 errors，两个既有 `MSB3101` cache warnings）。应用内 Browser runtime 初始化失败，未读取 Human Console 或冒充完成 Runtime Gate。
- Git commits：本次未创建提交。

## 2026-08-22 WeekCode Audit Frontend V1.2 Pagination + Result Traceability

- 分页根因：Foundation 异步 `Wms.Grid` 为手工 `rowdatas` 注入设置 `isPage:false` 并创建自定义 pager，绕过 jfGrid 原生 `pageparam/records/total`，因此真实 348/1/50 数据虽有 50 行却没有原生 footer。现改为驱动 jfGrid 原生 page bar；`WEEKCODE_AUDIT_PAGING_FIXED=True`，共享修复无页面 magic code。
- 分页行为：后端 `totalCount/page/pageSize` 映射为 jfGrid `records/page/rows/total`；页码请求保留 filters，查询/Reset 回第 1 页，重算 refresh 保留当前页，越界自动回最后有效页。未修改 `jfgrid.js`。
- 结果根因：旧 response 与 ResultPanel 只有摘要和有限 old/new rule/sort，缺少 RequestId、完整年/周/来源/格式、Hold 前后及异常消失解释。现有 DTO 合同补齐这些 transaction-memory 字段；`WEEKCODE_RECALCULATE_RESULT_TRACEABLE=True`。
- 生命周期：确认 POST 前保存 page-memory Audit snapshot；POST 成功后先保存/打开 Result Drawer，再 refresh Grid。随后用现有 Audit GET 按 group、pageSize 200 全分页复查 Result Box，可靠标记 PRESENT/RESOLVED；复查失败不宣称异常消除。
- Result UX：Header 显示范围、总体状态和可复制 RequestId；Summary 显示六项真实计数；UPDATED/UNCHANGED/FAILED 均显示 Before → After、Hold 与错误；部分成功使用明确警告。Grid 不保留已解决行，Drawer 解释其从当前异常列表移除。
- Backend contract only：增强 `WeekCodeRecalculateResultDto`、box/item result 及既有 fact/plan 字段；已有查询额外投影 `IsHold/WeekFormatCode`，已有 OperationLog detail 补齐 old/new 字段。无新查询、endpoint、transaction、table、schema、Migration 或业务规则。
- 安全：未修改 Proxy、RouteConfig、CORS、连接配置或数据库；本轮未执行真实 Recalculate POST。
- 验证：V1.2 controlled contract harness PASS，覆盖 native pager 初始化、348/50→7 映射、page 1→2 的 filters/pageSize 保留、scope Audit 多页合并、Result-before-refresh 顺序、snapshot 字段、RequestId/Before→After/异常状态 UI 和 Recalculate allowlist；临时 harness 已删除。4 个相关 JS `node --check` PASS；WMS-HU-Model Tests 41/41 PASS；WMS solution 与 Learun Debug build 均 PASS（0 errors，Learun 两个既有 `MSB3101` cache warnings）。6 个修改的前端 JS/CSS/cshtml 均为 UTF-8 BOM + CRLF，mojibake 0。
- Git commits：本次未创建提交。

## 2026-08-22 WeekCode Audit Frontend V1.1 Real Response Binding Fix

- 路由冻结：`WMS_FRONTEND_API_ROUTE_LOCKED=True`；浏览器合同 `FRONTEND_API_CONTRACT=/api/wms/*`；开发代理下游 `DEVELOPMENT_PROXY_DOWNSTREAM=http://localhost:52602`；Postman 端点 `POSTMAN_WMS_ENDPOINT=https://localhost:44375`。
- 根因与修复：真实 Audit 分页和 item 均为 camelCase，页面却以 PascalCase 读取，因此 jfGrid 只生成行号、业务单元格及 Drawer 均为空。现已将分页、Grid、AuditDiffCell、Drawer、标题、选择范围和重算来源统一绑定 camelCase，`WEEKCODE_AUDIT_JSON_CASE=camelCase`。
- 显示合同：null 统一显示 `—`；`INVALID_DATECODE` 显示“DateCode异常”；风险 HIGH/MEDIUM/LOW 显示高/中/低文本；Hold true 显示 `HOLD`、false 显示 `—`。未改变页面视觉结构。
- API 边界：保留 `Wms.Api` 对信封 `code/Code`、`message/Message`、`data/Data` 的兼容；没有递归转换 data/DTO 属性。Recalculate 出站 DTO 仍为 PascalCase allowlist，但值来自所选 camelCase 行；Operator/RequestId 策略未变。
- 安全边界：未修改 WMS 后端、Proxy、RouteConfig、CORS、连接配置、数据库或 Migration；未执行真实 Recalculate POST。
- 验证：两条真实形状 camelCase 样例的受控页面 harness PASS，覆盖分页 348/1/50、首/次箱码、Grid formatter、Diff、四组 Drawer、标题、`1010 + ITMAT001 + R1` 范围和 Recalculate payload allowlist；临时 harness 已删除。两个相关 JS `node --check` PASS，UTF-8 BOM/CRLF 与 mojibake 检查 PASS；Learun Debug build PASS（0 errors，两个既有 `MSB3101` cache write warnings）。Human 仍需重新加载页面，查询真实数据，打开 Drawer，并仅检查范围而不确认重算。
- Git commits：本次未创建提交。

## 2026-08-22 WMS Learun Server Proxy V1

- 实现：新增优先于 Default MVC route 的 `api/wms/{*path}`，由 `WmsApiProxyController` → `WmsProxyClient` 将 GET/POST 转发到唯一服务端配置 `WmsServiceBaseUrl=https://localhost:44375`。
- 边界：Controller 继承 `MvcControllerBase` 复用 Learun `HandlerLogin`；没有 BLL/Service/Repository/数据库/业务 DTO，没有 JS BaseUrl、CORS、第三方代理库或 WMS 后端改动。
- 透传：保留 query、raw POST bytes、allowlisted Accept/Content-Type/X-Requested-With、HTTP status/content type/body；4xx/5xx 从 protocol response 读取，`TrySkipIisCustomErrors=true`；连接/TLS/timeout 失败返回固定 502 JSON。
- 安全：目标 origin 只来自 appSettings；客户端不能指定 URL/host/port/scheme；拒绝 traversal、absolute/protocol-like、反斜杠、残留编码及 query/fragment path；不转发 Cookie、Authorization、Host 或 hop-by-hop headers。
- 证书：使用系统默认 TLS 验证，未关闭证书校验。当前 `https://localhost:44375` 直连在 TLS handshake 阶段报 IIS Express 本地证书/凭证错误，`HTTPS_CERTIFICATE_TRUST_FAILED=True`；未自动降级到 52602。
- 构建/静态：Learun Debug build PASS（`BUILD_EXIT=0`）；URL 单一来源、无 DB/Repository/业务 DTO/证书绕过/JS hardcode、route priority、GET/POST/raw body/status/502/auth markers 均检查通过。
- Runtime：启动 Learun 的授权被拒绝，未重复请求或绕过；因此 Proxy Audit GET、200 passthrough 和 direct/proxy semantics 未验证，`WMS_API_ROUTE_ALIGNMENT_PASS=False`。未执行 Recalculate POST 或数据库写入。
- 文档：新增 `docs/frontend/13_WMS_Server_Proxy.md`，更新 `12_WMS_API_Route_Alignment.md` 与本 handoff。
- 下一步：Human 优先修复/信任 IIS Express localhost HTTPS certificate，启动 Learun 并登录后执行安全 Audit GET 对比；若选择 Development-only HTTP，必须只显式修改 `WmsServiceBaseUrl`，不可代码自动 fallback。
- Git commits：本次未创建提交。

## 2026-08-22 WMS API Runtime Route Alignment Gate

- 调查范围：仅核对 Learun browser origin 到独立 WMS Nancy `/api/wms/*` 的运行路由；未继续数据库直连/SSPI 调查，未修改业务代码、页面视觉或 IIS 配置。
- Origins：Learun 当前项目/IIS Express origin 为 `http://localhost:20471`，application path `/`；WMS 项目配置与运行时共同确认 `http://localhost:52602`。
- Runtime：WMS 52602 的 health 和 WeekCode Audit GET 均 HTTP 200；启动真实 Learun 20471 后，根路径 HTTP 200、WeekCode 页面未登录 HTTP 302、同 origin Audit 相对 URL HTTP 404。
- 判定：`WMS_API_ROUTE_MODE=NO_ROUTE`。Learun Web.config/applicationhost 没有 rewrite/ARR rule，Controller/Handler/Module 没有 server proxy，配置没有 WMS BaseUrl，业务 JS 也没有 52602 hardcode。
- 透传：由于路由不存在，HTTP 200、HTTP 400 JSON 和 JSON POST 的端到端透传均不能通过。WMS 源端自身已静态设置 `httpErrors existingResponse=PassThrough`，但不能替代 Learun 路由验证。
- 浏览器限制：应用内浏览器的已保存权限禁止访问该 localhost origin；未尝试绕过。Gate 结论由真实 IIS Express HTTP 请求支持。
- 数据/行为安全：无 SQL、Repository、DbFactory、数据库写入或真实 Recalculate POST；未修改前端视觉、后端业务逻辑、connection string 或生产 IIS。
- 构建：Learun Debug build PASS（`BUILD_EXIT=0`）；两个既有引用项目缓存文件产生非阻断 `MSB3101` warning，Web DLL 成功生成。
- 文档：新增 `docs/frontend/12_WMS_API_Route_Alignment.md`，并在 `11_Database_Connection_Alignment.md` 中将数据库可用性与 API 路由 Gate 分离。
- 下一步：Human Review 后优先配置 Development same-origin reverse proxy `/api/wms/{R}` → `http://localhost:52602/api/wms/{R}`，再验证 Audit 200 与受控 400 JSON 原样透传；不再执行 Learun Factory DB Gate。
- Git commits：本次未创建提交。

## 2026-08-22 Database Connection Alignment Gate

- 调查范围：核对 Learun `XmlConfig/database.config` 加载机制、MesDb/WmsDb 结构与重复来源、WeekCode 页面运行边界，并执行 SELECT-only connection preflight；未开发新页面。
- Learun loader：`Web.config connectionStrings configSource` → `ConfigurationManager.ConnectionStrings[name]` → `DbFactory.GetIDatabase(name)` → provider/Unity → `Learun.DataBase.SqlServer.Database`。两个 key 结构合法、各精确一项、UTF-8 BOM。
- 架构决策：WeekCode 页面无 Learun DB 或 Controller proxy；浏览器通过 `Wms.Api` 调相对 `/api/wms/weekcode/*`，真正的 Repository/连接配置属于独立 WMS Nancy Host。`LEARUN_DIRECT_DB_CONNECTION_REQUIRED=False`。
- Learun preflight：临时 harness（已删除）通过真实 `RepositoryFactory.BaseRepository("MesDb"/"WmsDb")` 正确解析配置和 SqlServer implementation，但终端身份 `DELTON\953818` 在两次 Open 均报“目标主体名称不正确。无法生成 SSPI 上下文。”；未执行后续 identity/sys.tables SELECT。
- WMS Runtime：复用现有 `GET http://localhost:52602/api/health/db`，HTTP/Code 200，连接 `BT-ME-PC024\SQLEXPRESS / WMS_DEV` 成功；只读 Audit GET `plantCode=1020&page=1&pageSize=1` 返回 TotalCount 230 且 PackCreatedTime 非空，证明当前独立 WMS IIS Runtime 同时可读 WmsDb 和 MesDb。
- 未完成项：现有 endpoint 不返回 `SYSTEM_USER/ORIGINAL_LOGIN` 或规定的完整 sys.tables inventory；IIS Express token owner 受权限限制不可读；Learun origin 到独立 WMS Host 的同源 `/api/wms` 路由未验证。因此 Gate 状态为 incomplete，不能宣称完整 connection alignment 通过。
- 数据安全：执行的数据库语句仅 SELECT；没有 DML/DDL、Migration、业务 POST、配置修改、后端业务代码或前端视觉代码修改。没有记录完整 connection string。
- 构建：`Learun.Application.Web.csproj` Debug build PASS（`BUILD_EXIT=0`）；两个引用项目的既有 `obj\Debug\*.AssemblyReference.cache` 不可写产生 `MSB3101` warning，但程序集及 Web DLL 均成功生成。
- 文档：新增 `docs/frontend/11_Database_Connection_Alignment.md`；包含配置加载器、runtime 分流、SSPI 分类、诊断证据与下一步。
- 推荐下一步：Human 先处理/确认 Learun 进程 SSPI 与 IIS identity，再批准固定 allowlist 的 Development-only identity/table diagnostic；确认同源 API route 后才解除 Gate。
- Git commits：本次未创建提交。

## 2026-08-22 WMS Frontend Page 01 WeekCode Audit & Recalculate V1

- 实现范围：新增 Learun 紧凑操作台页面 `/WMS/WeekCodeAudit/Index`，接线真实 `GET /api/wms/weekcode/audit` 与 `POST /api/wms/weekcode/recalculate`；查询、异常 Drawer、范围确认、pending 防重复、结果 Drawer 和自动刷新闭环均已实现。
- Web 文件：新增 `WeekCodeAuditController.cs`、`Views/WeekCodeAudit/Index.cshtml/Index.css/Index.js/WeekCodeAuditApi.js`；旧式 `Learun.Application.Web.csproj` 已显式 Include。
- Foundation：新增可复用 `Wms.AuditDiffCell`、`Wms.ResultPanel`；`Wms.Grid` 增加经异步数据源驱动的后端分页适配与 clear/empty message；`Wms.Api` 增加 PascalCase `Code/Message/Data` 归一化及保留 HTTP 400 response JSON 的 GET 传输路径。未修改 Learun、Bootstrap、jfGrid 或第三方源码。
- Query/API：Audit 只发送 `plantCode/materialCode/materialRevision/anomalyType/riskLevel/isHold/page/pageSize`；Recalculate payload 严格只含 `PlantCode/MaterialCode/MaterialRevision/Operator/RequestId`，不发送 Box 或规则/周码派生值。
- Operator/RequestId：Operator 优先 Learun `userinfo.account`，回退 `userinfo.userId`，没有稳定标识则阻止提交；Confirm 真正确认后生成一次 `WCR-UI-<learun.newGuid()>`，HTTP 返回前锁定按钮。
- Schema/backend/data：无 schema、Migration、WMS Module/Service/Repository/DTO 或数据库变更；未执行真实 GET；未执行真实 Recalculate POST；未改菜单或权限数据库。
- 构建：`MSBuild Learun.Application.Web.csproj /t:Build /p:Configuration=Debug /p:SolutionDir=D:\com.delton.gz\ /p:RestorePackages=false /m /nologo /v:minimal` PASS，0 errors。首次未传 SolutionDir 的命令仅因 NuGet.targets 路径失败；随后一次带尾随空格的参数触发受限 restore 并失败，最终使用上述命令关闭 restore 后通过。
- 静态验证：6 个相关 JS `node --check` PASS；PascalCase/HTTP400 normalize、重算 payload allowlist、Diff render 合同测试 PASS；受控页面 harness 的查询/分页映射、四组详情、Confirm 后提交、pending、部分失败 Result 和 Grid reload 均 PASS。
- 未验证：未在登录后的真实 Learun Browser 中执行视觉/运行时测试；未验证部署环境是否将相对 `/api/wms/weekcode/*` 同源路由到 Nancy WMS host；未执行真实数据库/Postman 集成。
- 文档：新增 `docs/frontend/pages/01_WeekCode_Audit_Page.md`，包含合同、权限、编码、限制与 UI01–UI12 人工用例。
- 推荐人工顺序：UI01 → UI08 完成只读和取消路径 → Human 授权后仅执行一次 UI09 真实 POST → UI10–UI12 检查部分失败、自动刷新与 Hold release。
- Git commits：本次未创建提交；Web 工程使用 SVN 工作副本，本轮未提交。

## 2026-08-22 WMS Frontend Foundation V1.1 Visual + Encoding

- 实现范围：将 Foundation Showcase 从独立 dashboard/card 风格调整为 Learun native visual first；保留全部 V1 Foundation 行为，不新增业务页面或真实 API。
- 视觉基线：审阅 User、Log、DataItem、HS_WarehouseLocation、WMS_MaterialStock 等成熟页面及 Learun layout/form/grid/layer CSS；估算视觉复用 80%。
- Showcase：采用 `lr-layout/lr-layout-wrap-notitle/lr-layout-tool/lr-layout-body`、Bootstrap 3 tabs/buttons、原生 28px jfGrid；右侧 DetailPanel、Drawer 与 StatusTag 使用紧凑 Learun 协调样式。
- Modal：继续 `learun.layerForm`；iframe demo 改为 `_Form + lr-form-wrap + lr-form-item`，移除独立 modern form chrome。
- Encoding Audit：10 cshtml + 10 JS + 5 CSS 正常中文样本全部为 UTF-8 BOM；原 11 个 Foundation 文件均为有效 UTF-8 no BOM、文本无 mojibake。现已统一为 UTF-8 BOM，payload hash comparison 确认编码转换未改文本。
- Config/charset：Web.config 未显式配置 globalization encoding；`_Admin/_Index/_Form` 均声明 HTML charset UTF-8。
- 验证：7 个 Foundation/Demo JS `node --check` PASS；静态回归确认 Api/Grid/Drawer/Modal/Drawer→Modal/Event Namespace contracts 均保留；无同步 AJAX、无全局 CSS override、无自建 button system。
- 构建：VS 2022 MSBuild Debug PASS；0 errors。两个被引用项目因 workspace 写权限无法更新 `obj/*.AssemblyReference.cache` 产生 MSB3101 warning，但未阻断输出，Web DLL 成功生成。
- 文档：新增 `docs/frontend/09_Learun_Visual_Baseline.md`、`10_Frontend_Encoding_Audit.md`，更新 `08_WMS_Foundation_Implementation.md` 与本 handoff。
- Schema/API/data：无 schema 或后端 API 变更；未调用真实业务 API，未修改真实业务数据。
- 人工验收：需登录后检查 1366×768 / 1920×1080、查询区、Grid、tabs、Modal、Drawer、中文以及 Drawer→Modal callback。
- Git commits：本次未创建提交。

## 2026-08-22 WMS Frontend Foundation V1 Implementation

- 实现范围：在现有 Learun/jQuery/Bootstrap 3 基线上新增 `Wms` namespace、Registry、统一 API、SearchPanel、Grid、Select、DateRange、StatusTag、DetailPanel、SideDrawer、Modal、ConfirmDialog，并新增只读 Mock Showcase。
- Web 文件：新增 `Areas/WMS/Content/Foundation/**`、`Areas/WMS/Controllers/WMSFoundationDemoController.cs`、`Areas/WMS/Views/WMSFoundationDemo/**`；更新 `Learun.Application.Web.csproj` 显式包含项。
- 文档：新增 `docs/frontend/07_Modal_Drawer_Usage_Guide.md` 与 `08_WMS_Foundation_Implementation.md`；更新本 handoff。
- Schema/API：无 schema 变更；无真实 WMS 业务 API 变更；Showcase 仅增加只读 Mock GET `/WMS/WMSFoundationDemo/GetMockRows`，没有数据库访问或生产写入。
- 框架/依赖：Learun、Bootstrap、第三方源码均未修改；未引入 Vue/React/npm/new grid。
- 构建：VS 2022 MSBuild Debug PASS，0 errors；84 个既有 warning。构建命令：`MSBuild Learun.Application.Web.csproj /t:Build /p:Configuration=Debug /p:RestorePackages=false /p:SolutionDir=D:\com.delton.gz /m:1`。
- 静态验证：6 个新增 JS `node --check` PASS；无同步 AJAX、`top.$`、fetch、Promise、async/await、Vue/React；直接 `$.ajax` 仅为 `wms-api.js` 中集中式 JSON POST。
- 浏览器验证：IIS Express 启动成功；匿名请求按框架认证规则返回登录页。应用内浏览器 localhost 权限未获批准，未执行完整可视化交互验收，不宣称 UI runtime PASS。
- 生命周期：WMS 自有事件使用 namespace 并提供 destroy；Showcase 内置 SearchPanel/Drawer 10 次生命周期自检。`jfGrid/lrselect/lrdate` 缺少统一公开 destroy，包装层复用底层实例，只清理自身资源。
- Modal/Drawer：Quick Detail=Drawer；Complex Form=iframe Modal；高影响动作=ConfirmDialog；Drawer 可开 Modal；Modal 成功刷新 Drawer 与 Grid；默认最大 Modal 深度 1。
- 推荐人工验收顺序：登录 Learun → 打开 `/WMS/WMSFoundationDemo/Index` → 搜索/重置/分页 → Drawer 10 次 → Drawer 内 Modal 保存/取消 10 次 → Confirm 取消/确认 → 检查 Registry、遮罩、焦点与控制台。
- 风险/限制：老框架底层组件销毁能力有限；真实 API response 映射尚待正式接口联调；Showcase 未注册菜单且只使用 Mock 数据。
- Git commits：本次未创建提交。

## 2026-08-22 Frontend Foundation Investigation

- 实现范围：只读盘点 `D:\com.delton.gz\Learun.Application.Web` 的 Learun/jQuery 前端结构、组件、基础设施、依赖和 18 个代表页面；新增 `docs/frontend/00` 至 `06` 七份文档。
- 框架结论：Learun-ADMS V7.0.3；标准布局加载 jQuery 1.10.2、Bootstrap 3.3.7；核心 grid 为 jfGrid。
- 文件变更：仅 `docs/frontend/*.md` 与本 handoff；未修改 Web 项目的 JS/CSS/cshtml、后端或配置。
- Schema/API：无变更；未连接或修改 WMS_DEV。
- 验证：完成源码静态扫描、组件 API 对照、第三方版本头核验、18 页 pattern review 和文档一致性检查；未运行浏览器、Postman、数据库或 UI 自动化测试。
- 偏差：无实现性工作；按任务要求仅调查、分类、文档、建议。
- 风险：jQuery/Learun baseline 较旧；同步 AJAX 与直接 `$.ajax` 历史用量高；多个同类库并存；iframe/tab/dialog 存在 lifecycle 和 memory leak 风险。
- 推荐人工审阅顺序：`00_Frontend_Foundation_Review` → `02_Component_Atlas` → `03_WMS_Page_Component_Matrix` → `04_WMS_Component_Wrapper_Proposal` → custom/do-not-reinvent lists。
- Git commits：未创建提交。

日期：2026-08-17

## 已实现

- Migration 014 已完成：`014_OutboundV1Compatibility.sql` 已在真实 `WMS_DEV` 成功执行 FIRST_RUN_RETRY；Human Review 决定不执行真实 SECOND_RUN（`SECOND_RUN_REQUIRED=False`）。
- `014_OutboundV1Compatibility.sql` 保留 009 历史并修正待扫码 Batch、正式状态、SAPBatchNo 快照、多 Replacement、动作幂等、每仓 RETURNED/SHIPPED 以及原子过期过程。
- Item 写入链接入 WeekCode Resolver；没有 MES Provider 时明确失败，不回退 CreateTime。
- HUCode 优先、客户上下文 Identifier 次级解析。
- `MATNR+UOM` 正式 Preview 与确定性精确组合，结果只使用四种正式类型。
- StartPicking 在 Serializable 事务内重算整单，创建 Allocation/Batch/Reservation；Release、Expiry 和 WAIT_PICK VOID 保留历史。
- 扫码事务原子更新 MoveLog、CurrentLocation、OperationLog、Reservation、PickDetail、PickDetailItem、Line/Batch/Order；按 SAPItemNo 升序分摊并快照 BatchNo/SAPBatchNo。
- 单箱异常退回、QUALITY_FAILED 自动 Hold、人工 Hold/Release、多箱 Replacement、ReturnToStock 和显式 SHIPPED。
- MEDIUM-OUTBOUND-01：StartPicking / Replacement 的 `reservations` 仅返回 ACTIVE Reservation，避免 PDA 将历史 CONSUMED 箱误认为当前待扫码任务。
- 旧 `/pick-next`、`/pick-batch` 返回 410；`/queue`、`/plan` 只读保留。

## API

- `GET /api/wms/outbound/orders`
- `GET /api/wms/outbound/orders/{vbeln}`
- `POST /api/wms/outbound/orders/{vbeln}/preview`
- `POST /api/wms/outbound/orders/{vbeln}/start-picking`
- `POST /api/wms/outbound/orders/{vbeln}/void`
- `POST /api/wms/outbound/allocations/{allocationNo}/release`
- `POST /api/wms/outbound/pick-batches/{pickBatchNo}/scan`
- `POST /api/wms/outbound/pick-details/{detailId}/return`
- `POST /api/wms/outbound/pick-batches/{pickBatchNo}/replace`
- `POST /api/wms/outbound/returned/{huCode}/putaway`
- `POST /api/wms/outbound/orders/{vbeln}/ship`
- `POST /api/wms/hus/{huCode}/hold`
- `POST /api/wms/hus/{huCode}/release`

## 验证结果

- VS 2022 MSBuild Debug：通过，0 编译错误。
- VS 2022 MSBuild Release：通过，0 编译错误。
- `WMS-HU-Model.Tests` 可执行算法测试：6/6 通过。
- MEDIUM-OUTBOUND-01 需以 Postman 完成“扫码后 Replacement / 幂等重试”的数据库集成验收；本地算法测试不覆盖 Repository SQL。
- 新增 C# 文件均已显式登记到旧式 `.csproj`。
- 新正式链路未使用 `dynamic` 或 `AddWithValue`；Nancy 路由参数采用两步转换。
- Migration 014 Disposable Clone：Run #1 / Run #2 均成功，Schema 与观察状态在第二次后稳定；真实 `WMS_DEV` 未被 Clone 测试触碰。
- Migration 014 真实 `WMS_DEV` FIRST_RUN_RETRY：成功，SchemaVersion、Replacement table、Expiry procedure、6 个新增列及 8 个预期索引均为 1，`@@TRANCOUNT=0`；真实 post-schema 与 Clone Run #1 语义一致，Unexpected Schema Change=0。
- Canonical SHA：`5FD57C4B981553DCDEB2DB9BAB213CE2D120E6BCE7AB6A47CCC9A2330E66E3D1`。
- Completion Evidence：`.codex/evidence/migration-014-completion.json`，SHA256=`086F84C4801E97EE736554DDD00CA1344E163AF3FCDC4E0DE6E8E2CF1F1C8EFC`。
- Real FIRST_RUN Result SHA：`3B1A269CBB143A33AC2523E6B65F321EBBD50515003B175B534EAE7D64E2DCA1`；Real post-schema SHA：`D3779BC75AB19B8F9CE38C0023083CA0FE377FB295CE6DFA9EADA349E14A1F2D`。
- 最新 WMS_DEV Schema Evidence：`.codex/evidence/wms-dev-schema-post-014-first-run-20260816.json`，SHA256=`D3779BC75AB19B8F9CE38C0023083CA0FE377FB295CE6DFA9EADA349E14A1F2D`。
- 记录时 Git HEAD：`acb449f7d45b5d9a0f312d5b444721cb7fb97db8`。

## 未解决风险

- 未提供 SAP 真实脱敏报文、外层 JSON、最终字段类型/映射，SAP Receive 入口没有猜测实现。
- 未提供 MES GiftQty 与 WeekCode 合同；默认 WeekCode Provider 会明确阻断带 DateCode 的新 Item 写入。
- 外部调度器、执行账号与 Reservation 过期频率未配置。
- 需求示例称 `40+30+30` 在需求 60 时无完整 INNER_PACK 组合，但数学上 `30+30=60`。实现按每个实际 INNER_PACK 均可独立选择，因此会返回 REPACK_REQUIRED；需业务人工确认示例是否有未写出的限制。
- `ExceptionType` 的业务枚举未定义，异常退回 API 要求调用方显式提供；系统只固定校验四个 ReasonCode。

## 下一步人工验收顺序

1. Migration 014 已完成；不执行真实 SECOND_RUN。保留 completion evidence、真实 FIRST_RUN result 和 post-schema evidence 以供审计。
2. 提供 SAP/MES 合同后补 Receive 与 Provider，不修改已应用的 010–014。
3. 准备含混周码、多 POSNR、多 SAPBatchNo、异常与多 Replacement 的测试数据，执行 Repository、并发及完整 API 流程。

## MEDIUM-OUTBOUND-01 交接

- 已实现范围：执行任务 DTO 的 `batches[].reservations` 只返回 `ReservationStatus = ACTIVE` 的 Reservation；CONSUMED 历史继续保留在数据库与 PickDetail 追溯中。
- 修改文件：`WMS-HU-Model/Repositories/Impl/FormalOutboundRepository.cs`、本 handoff 文件。
- Schema/API：无 schema 变更；既有 StartPicking 和 Replacement 响应的 `reservations` 语义收窄为“当前待扫描箱”，不增加 `reservationStatus` 字段。
- 已执行：`MSBuild WMS-HU-Model/WMS-HU-Model.csproj /t:Build /p:Configuration=Debug /m`、`MSBuild WMS-HU-Model.Tests/WMS-HU-Model.Tests.csproj /t:Build /p:Configuration=Debug /m`，均成功且 0 警告/错误；`WMS-HU-Model.Tests/bin/Debug/WMS-HU-Model.Tests.exe` 通过 6/6。
- 未执行：Postman 或数据库集成测试；现有测试没有覆盖 Repository SQL 返回投影。
- 建议 Postman 顺序：StartPicking → 扫描一箱使 Reservation 变为 CONSUMED → Replacement → 验证仅出现新的 ACTIVE 箱 → 使用相同 Replacement RequestId 重试并验证 `isDuplicate=true` 与相同 ACTIVE 任务视图。
- 偏差/风险：无业务偏差；尚需在含多个 PickBatch 的真实测试数据上验证没有 ACTIVE Reservation 的 Batch 返回空 `reservations`。
- Git commits：本变更未创建提交。

## HIGH-OUTBOUND-SHIP-01 交接

- 已实现范围：ShipOrder 锚定订单最新的 COMPLETED Allocation；订单行、当前 Allocation Batch 与订单 Open Exception 分别校验。历史 RELEASED / EXPIRED Allocation 的 Batch 保留审计历史但不再阻断 SHIPPED。
- 修改文件：`WMS-HU-Model/Repositories/Impl/FormalOutboundRepository.cs`、本 handoff 文件。
- Schema/API：无 schema 或 DTO 变更；ShipOrder 的失败消息按 Allocation、订单行、Batch、Exception 原因细分。
- 未执行：Postman 或数据库集成测试；需使用 `StartPicking → Release → StartPicking → Scan → ShipOrder` 验证历史 RELEASED Batch 不阻断，及 Ship RequestId 重试不重复移箱。
- Git commits：本变更未创建提交。

## SAP Outbound Receive V1 交接

状态：`SAP_OUTBOUND_RECEIVE_V1_STATUS=COMPLETE`，`INTEGRATION VERIFIED`，`READY_FOR_COMMIT=True`。

### 实现范围

- API：`POST /api/wms/integration/sap/outbound-orders`。
- 流程：SAP Raw JSON → Canonical SHA-256 → VBELN Idempotency → WarehousePlant Resolution → `WMS_OutboundOrder` / `WMS_OutboundOrderLine` → `WAIT_PICK`。
- 不自动执行 Preview、Reservation、Picking 或 WMS→SAP callback。

### 幂等

- Same VBELN + Same Hash → DUPLICATE；Same VBELN + Different Hash → REJECTED；First VBELN → SUCCESS。
- Existing Hash 判断优先于当前完整业务校验和 WarehousePlant 解析；Create 事务使用 Serializable + `UPDLOCK, HOLDLOCK` 二次检查。
- Duplicate 不修改历史 Order/Line。WarehouseCode 通过历史 WarehouseId 读取，属于 master-data display exception，不参与幂等成功判断。

### Canonical

- `DateParseHandling.None`、`FloatParseHandling.Decimal`；Integer/BigInteger/Decimal 不经 double。
- Object Property 按 Ordinal 排序；仅根 `$.T_ITEM` 按原始字符串 POSNR 排序；Nested `T_ITEM` 保持原顺序。
- POSNR 保留前导零，Unknown Field 参与 Hash，`7515` / `7515.0` / `7515.0000` 等价。
- 第二 Root 或尾随 scalar 拒绝；尾随 whitespace 允许。

### 校验与审计

- SAP 写库字符串长度按真实 schema 校验；VBELN 最大 30。超长 VBELN 在 Existing 查询前 Reject，审计使用 `SAPDeliveryNo=NULL`、Canonical RequestHash 与完整原始 RequestJson。
- LFIMG > 0 且符合 `DECIMAL(18,4)` precision/scale；POSNR 唯一；单 VBELN 单 WERKS；日期时间严格格式；WarehousePlant 必须唯一有效映射。
- `WMS_OutboundOrderReceiveLog` 对 SUCCESS、DUPLICATE、REJECTED 均正常 Commit；RequestId 保持 NULL。坏 JSON 保留 Raw JSON，无法获得的 VBELN/Hash 按真实阶段写 NULL。

### 映射与范围

- `OutboundOrderNo=VBELN`；Order/Line 均为 WAIT_PICK；MaterialRevision=NULL；GiftQty=0；ZKHLH 不做业务解释。
- 无 Plant/LGORT hard-code；不包含 WMS→SAP 箱级回传、GiftQty Resolver、MaterialRevision Resolver、ZKHLH 业务语义、CHARG 真实批次来源或 MES WeekCode Provider。

### 构建与自动化测试

- `WMS-HU-Model` Debug Build：PASS，0 warning / 0 error。
- `WMS-HU-Model.Tests` Debug Build：PASS，0 warning / 0 error。
- `WMS` Debug Build：PASS，0 warning / 0 error。
- Console regression tests：Passed 18 tests。
- 自动化覆盖：trailing root/scalar/whitespace、root/nested T_ITEM、unknown field、malformed JSON、decimal precision/range、schema max length、oversized VBELN Existing-check ordering 与 oversized VBELN audit parameters。

### WMS_DEV 真实集成验证

- PASS：1010 → FG01 / 11C3；1020 → FG01 / 12C3；First Receive SUCCESS；Same Hash DUPLICATE；Header Property、T_ITEM Order、Numeric Representation 变化仍 DUPLICATE；Different Hash REJECTED 且历史 Order/Line 不变。
- PASS：跨 WERKS、重复 POSNR、LFIMG 非法、无 WarehousePlant 映射、空 T_ITEM 均 Reject；坏 JSON 写入 REJECTED Audit。
- 真实 SAP Receive：VBELN=`8300152127`。

### Formal Outbound 跨模块回归

- SAP Receive → WAIT_PICK → Preview → StartPicking：PASS。
- Regression VBELN：`8300152201`；HU：`B-F-260817114028RRSF-02`；Qty：30 PCS；Location：H201；WeekSortKey：202604；Allocation：`OA20260818103305A79B7A12BD`。

### 最终 Code Review

- BLOCKER=0，HIGH=0，MEDIUM=1 ACCEPTED，LOW=0。
- Accepted MEDIUM：真实 WMS_DEV 的 VBELN > 30 烟雾测试未执行，因为数据库写入安全门禁禁止该操作。该分支已通过 Fake Repository 自动化测试验证 Existing 未调用、SAPDeliveryNo=NULL、Canonical Hash 与 Raw JSON 保留，因此不阻塞 V1 提交。
- Git commits：本变更已完成创建提交。

## MES Finished Goods Inbound V1 交接

- 已实现范围：`POST /api/wms/integration/finished-goods/inbound` 接收通用成品入库快照；`POST /api/wms/integration/finished-goods/mes-inbound` 按 MES `PLAN_STOCK` 读取包装层级并完成入库编排。
- 幂等与持久化：以 `SourceSystem + SourceType + SourceEntityId` 和 `FGI_V1` Snapshot Hash 判定重复或冲突；Receipt、HU、标识、层级、明细、MoveLog、CurrentLocation 与 OperationLog 在同一可串行化事务中写入。
- 周码：MES 规则经 `md_flow` 链路查询；明确规则解析后写入 WeekCode 快照，缺失/歧义/不支持规则走异常入库并保留原始 DateCode。
- 数据库：新增迁移草案 `docs/database/015_FinishedGoodsInboundCore.sql`，未执行；WMS_DEV 未修改。执行前仍需 live schema、`INBOUND-PENDING` 主数据、备份与人工审批。
- 验证：Solution Debug/Release Build 均 PASS；控制台回归测试 PASS（36 cases）；未执行 Postman、WMS_DEV 或 MES 实库集成测试。
- 风险：MES 真实表结构、运行账号权限、`INBOUND-PENDING` 位置主数据和迁移 015 的实库兼容性尚未验证；`MES_FGI_BATCH_RESULTS` 是本地运行产物，不纳入 Git。

## WMS HTTP API Interface List 文档交接

- 已实现范围：新增 `docs/WMS_API_Interface_List.md`，以当前 `WMS/Modules` 的 12 个 Nancy Module 为唯一接口来源，记录 42 条实际 Route（GET 14、POST 27、PUT 1、DELETE 0）。
- 文档内容：接口总览、核心 Mermaid 调用链、通用响应约定、按业务域的请求/响应/失败/数据库影响/事务/幂等/OperationLog、MES 入库与 WeekCode Audit/Recalculate 专章、数据库影响矩阵与 Postman Quick Reference。
- Schema/API：无 Schema、Web.config、csproj 或生产代码变更；未执行 Migration、数据库操作、POST Runtime 调用或构建（纯文档任务）。
- 已执行验证：静态扫描 Module 路由、读取关键 DTO/Service/Repository，并检查文档生成成功；未宣称 Runtime 验证通过。
- 偏差/风险：旧 Module 的多数失败路径只携带 `ApiResult.Code=400` 而不显式设置 HTTP 400；文档明确标注此客户端兼容性风险。当前工作区原有未提交代码变更未被修改。
- 推荐 Postman 顺序：health → MES inbound（人工授权的非生产测试数据）→ weekcode audit → weekcode recalculate；本次未执行其中任何写请求。
- Git commits：本次未创建提交。
