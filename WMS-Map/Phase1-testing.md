# 第一阶段：草稿、正式版本与前端联调

本轮运行后端为 `D:/Codes/WMS_FG/WMS/Standalone`，业务源码维护于 `WMS-Map`。数据库使用本机 SQL Server 的 WMS 库。未部署到生产力软宿主，也未修改库存、HU 或 Location 主数据。

## 操作语义

- Ctrl+S、自动保存：只写浏览器 IndexedDB。
- 保存服务器草稿：第一次创建布局与草稿，之后使用修订号更新共享草稿。
- 发布：从已保存且与当前编辑内容一致的服务器草稿创建不可变正式快照。
- 载入服务器草稿：先备份本机布局，再建立最新并发基线。
- 载入正式版：先备份本机布局，再读取正式快照；继续保存前须载入服务器草稿。
- 其他浏览器通过“载入服务器草稿”获取最新内容；当前没有实时协同推送。

首次打开时，本机有数据优先恢复本机数据；否则读取正式版或未发布草稿。服务器明确返回布局不存在且允许编辑时显示空白地图，点击保存才落库。网络失败不会生成示例数据替代服务器内容。

## 本地启动

1. VS2022 打开 `D:/Codes/WMS_FG/WMS.Standalone.sln`，启动 WMS.Standalone。默认监听 `http://localhost:31175/`。
2. `WMS/Standalone/App.local.config` 的 BaseDb 已配置本机 WMS 与 Windows 身份认证；该文件不提交。更改后重新构建并重启。
3. 前端目录执行 `npm run dev`，默认 `http://127.0.0.1:5173/`。`.env.development` 配置 `/wms-api`，Vite 代理转发到后端 31175。
4. 切换编辑模式，编辑、保存服务器草稿、发布。已有远程布局而本机缺少修订号时，先载入服务器草稿；可以先导出本机工作。

本轮为避免占用 VS 调试进程，临时测试前端端口为 **5180**，后端为 **31176**。前端启动时设置 `WMS_PROXY_TARGET=http://localhost:31176`。这两个进程的 PID 与日志记录在本机忽略目录 `.phase1/`。默认配置仍是 5173 → 31175。

生产前端部署需要配置相同的反向代理或在构建时设置 VITE_WMS_API_BASE；Vite 开发代理不会打包进生产文件。

## 已实现 API

基础路径 `/api/wms-fg/map/layouts`，返回直接 JSON；失败使用真实 HTTP 状态与 `{code,message,traceId}`。

| 方法 | 路径 | 用途 |
|---|---|---|
| GET | / | 布局列表 |
| GET | /{code} | 当前正式版本；不存在返回 exists=false |
| GET | /{code}/draft | 共享草稿与修订号 |
| POST | /{code}/initialize | 首次创建布局与草稿 |
| PUT | /{code}/draft | 带 expectedDraftRevision、basedOnPublishedVersion 保存 |
| POST | /{code}/publish | 带 requestId、修订号与发布基线发布 |
| GET | /{code}/versions | 正式版本目录 |
| GET | /{code}/versions/{version} | 指定正式快照 |

保存与发布在 SQL 事务内锁定布局主记录并校验基线。冲突返回 409，不覆盖本机工作。发布 requestId 在客户端保留，响应丢失时重试同一请求，服务器返回同一版本。发布会增加草稿修订号并更新其正式基线；SavedDt/SavedBy 保留最近一次明确保存的信息。

CreateBy/CreateDt、LastEditBy/LastEditDt、SavedBy/SavedDt、PublishedBy/PublishedDt 均由服务端填写。独立开发宿主使用当前 Windows 身份，不信任客户端 Operator。

## 阶段边界与力软接入

本轮正式保存指**地图布局正式快照**，不是生产部署。实现版状态为 GEOMETRY_ONLY：写入版本、仓库范围和操作日志，但不伪造 WmsLocationId，不写 FG_MapLayoutVersionBinding，不创建 WMS Location 主数据。带 areaBindings 的草稿可以保存，但发布会明确拒绝，等待真实主数据校验接入。已发布货位暂禁止改码、换仓、删除；允许位置和尺寸修改。

实时库存、HU、占用查询与 SignalR 不在本轮实现，页面显示未知/未连接。图纸底图和测量资源仍在本地项目备份中，不作为本轮服务器布局快照上传。

力软入口在 `WMS/Modules/Map`，独立入口在 `WMS/Host.Modules/Map`，后者由同步脚本生成。Portal 自动发现已编译路由，本轮八个地图接口已补中文说明。

传统力软 csproj 首次需要加入新增地图源码与 `WMS/Infrastructure/Map/MapIdentity.cs` 的 Compile 项，并接入可信登录身份与相应权限校验，再配置 `MapIdentity.Resolve`。该委托默认未配置时拒绝请求（401），不能依赖现有 WMS 白名单提供用户。没有对方完整宿主及权限环境，本轮未验证力软运行。

`wms-remote-contract.md` 中 Location 投影、占用删除与 Runtime 仍是后续契约设计，本文件说明本轮实际实现范围。

## 首轮验证记录（2026-10-10）

- 前端 127 项单元/集成回归通过，生产构建通过；覆盖已有编辑功能、首次空白启动、发布重试 requestId、正式快照回读及 Ctrl+S 本机隔离。
- 共享后端源码通过 net461 目标编译与 16 项基础检查；独立宿主使用本机 net48。
- 独立宿主验证 26 个 Module、106 条唯一路由，Portal 覆盖 104 条业务路由，无 Learun 程序集引用。
- 真实 HTTP → 前端生命周期 → SQL 测试：`TEST_PHASE1_2d8d6720`，正式版本 2；覆盖初始化重复、过期修订、发布重试、快照不可变、非法草稿回滚、已发布货位身份保护、版本回读。
- 浏览器实际保存并发布了 MAIN 的空白测试布局：正式版本 1。Ctrl+S 旧逻辑曾使草稿到修订 2；修复后再次 Ctrl+S，服务器修订保持 2，页面显示“本机草稿已保存；未上传服务器”。这些测试记录保留，未删除。
- 浏览器验证正式版回读、服务器草稿回读以及页面刷新后恢复。截图位于 `testing/phase1-browser.png`。

重跑真实数据库测试（每次新增独立 TEST_PHASE1_* 布局）：

```powershell
$env:MAP_TEST_BASE='http://localhost:31176'
node scripts/test-map-integration.mjs
```

现有 Newtonsoft.Json 12.0.1 的 NU1903 警告仍存在，版本遵循现有宿主基线；前端构建有既有包体积提示。二者不影响本轮测试通过，但不代表生产验收完成。
