# WMS_FG 双宿主

## API 启动页与工程目录（2026-10-12 更新）

当前独立工程已由用户改为 .NET Framework 4.8；力软工程仍保持 4.6.1，公共源码继续以兼容 4.6.1 为约束。下方早期 net461 构建记录不代表当前独立工程目标。

F5 启动后访问 http://localhost:31175/（自定义端口以控制台为准）。根路径是 API 目录，提供 11 个功能分类、HTTP 方法筛选、路径/模块搜索和复制路径。清单从 Nancy 实际路由缓存获取；/api-catalog 提供 JSON。只读取路由元数据，不执行所列业务接口。页面为内嵌资源，无外部 CDN 或前端构建依赖。新增未知模块自动归入“其他接口”，可在 Portal/ApiPortalModule.cs 中补充分类名称。

VS 源码引用使用显式 Compile + Link（数量以同步检查结果为准），保留 WMS-HU-Model 下 Infrastructure、Models、Repositories、Services 的原有子目录。没有移动或复制公共源码。
新增或删除源码后，执行 Build/Sync-HostModules.ps1，再执行 Build/Sync-StandaloneItems.ps1，然后在 VS 重新加载工程。构建会检查两份清单，防止入口和项目文件夹映射漏同步。若 VS 提示外部项目文件变化，选择重新加载，不要以旧内存配置覆盖磁盘文件。

新增启动页只编译进独立宿主，不纳入力软 WebApi。正常启动只输出首页地址，不自动弹出浏览器。


现有 `WMS/Modules` 保持继承力软 `BaseApi`，原文件和业务源码不变。
`WMS/Host.Modules` 是本地独立 Nancy 的入口副本，继承 `NancyModule`。
独立工程直接编译现有 Models、Services、Repositories、Infrastructure 源码，不引用 Learun。

## 本地启动

1. 安装 VS2022 的 .NET 桌面开发工具。独立宿主目标为 .NET Framework 4.8，需要对应 Developer/Targeting Pack；共享地图测试仍以 net461 验证源码兼容性。
2. 打开根目录 `WMS.Standalone.sln`，还原 NuGet 包并构建。传统 csproj 使用 PackageReference，仅影响独立工程，不改变同事的 packages.config。
3. F5（调试参数为 `--development`），或运行 `WMS/Standalone/bin/Debug/WMS.Standalone.exe --development`。
4. 访问 `http://localhost:31175/api/wms-fg/health`。此接口不访问数据库。
5. 数据接口使用前，将 `App.local.config.example` 复制为同目录 `App.local.config`，填入测试数据库 BaseDb，再构建。该本地文件不提交。没有配置时仅健康检查可用。

命令行可指定 `--development --port 31176`。只绑定 localhost，不自动更改 HTTP.sys URL ACL；如遇 Access denied，由本机管理员为实际账户配置该端口的 URL 使用权限。
这是本地开发宿主，不实现力软登录；保留原有请求 Operator 语义，不能用作公开生产服务。没有自动执行 SQL、数据库初始化或 SAP 调用。
浏览器跨域代理建议由前端开发服务器配置到 localhost:31175；默认不回显任意 Origin、不启用跨域 credentials。

独立宿主目标 net48，共享业务兼容 net461；依赖为 Nancy 1.4.5 / Nancy.Hosting.Self 1.4.1 / Newtonsoft.Json 12.0.1 / Dapper 2.1.35。
SelfHost 是独立 NuGet 包，版本与 Nancy 核心不同。

## 更新接口

修改 `WMS/Modules` 后执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Build/Sync-HostModules.ps1
```

脚本只替换命名空间、力软 using 和基类，将两处继承的 Logger.Error 适配为 Trace，并修正独立健康检查的旧框架文字。
Host.Modules 生成文件提交到 Git，禁止手工修改；每次构建会以 `-Check` 比对完整入口文本。
若入口开始依赖 userInfo 等宿主能力，脚本会拒绝生成，应先实现显式的独立适配。
业务逻辑只修改公共源码，不在两个入口里维护两份业务。

## 同事接入力软

现有力软模块继续沿用原 Compile 项；新增地图源码、入口和 MapIdentity 必须加入力软工程，并接入可信身份及权限校验。
不要把 Host.Modules/Standalone 加入力软工程，不要把独立宿主 bin 复制到力软 bin。
独立 Bootstrapper 仅注册自身程序集 WMS.Host.Modules 命名空间中的 Module。
以后新增供力软使用的业务源文件、接口、配置或数据库对象，仍须在对方工程中纳入相应内容。

力软的鉴权、日志、异常邮件、全局 JSON/CORS 行为没有被移植。独立宿主保留业务路由与返回方式；未捕获异常保留 HTTP 200 + code/info/data 结构，异常详情写入 Trace，响应返回追踪号。
两套入口相同不代表全局中间件行为相同。旧接口保持原身份和状态码约定；新增地图接口已接入 WMS-Map，返回真实 HTTP 状态及结构化错误。独立地图入口使用 Windows 身份，力软 MapIdentity 未配置时拒绝请求。

## 验证

`Build/Sync-HostModules.ps1 -Check` 校验全部入口同步；`Build/Test-Standalone.ps1` 验证构建产物依赖、所有 Module 路由注册以及不访问数据库的 HTTP 健康检查。
力软完整工程不在本仓库，需同事在完整环境构建验证。本地 smoke test 不访问数据库、SAP，不调用业务写接口。

早期基础宿主验证记录：VS2022 MSBuild 构建、24 个独立 Module / 96 条唯一方法与路径组合、无 Learun 程序集引用、显式开发启动开关、HTTP 健康响应与 no-store。
这是 net461 目标构建以及本机已安装 Framework 运行时的测试，不代表在仅安装 4.6.1 的机器上完成验证。
还原时 NuGet 对兼容基线 Newtonsoft.Json 12.0.1 报告 NU1903；本次保留版本以避免悄悄改变宿主契约，依赖升级需与力软宿主另行协调。未抑制该警告。

### 接口中文说明及新增接口

业务接口的中文用途保存在 WMS/Host.Modules/Portal/ApiDescriptions.zh-CN.json，按“Module名称|HTTP方法|完整路径（无末尾斜杠）”定位。页面显示中文用途并支持中文搜索。这是接口用途摘要，不是完整参数文档。

新增接口流程：
1. 在原 WMS/Modules 添加路由及对应业务实现。
2. 运行 Build/Sync-HostModules.ps1；新增/删除文件时再运行 Build/Sync-StandaloneItems.ps1。
3. 在 ApiDescriptions.zh-CN.json 补充中文说明；新增 Module 的分类可在 ApiPortalModule.cs 的 Categories 补充。
4. 重新构建、重启宿主并刷新页面。

目录从 Nancy 已注册路由读取，不需要在 HTML 里再写一份接口列表；不是修改源码后的热更新。缺少中文说明仍会显示接口并标记“用途说明待补充”，新 Module 没有分类时显示在“其他接口”。回归检查会检查说明覆盖率，提醒补齐说明。力软工程如新增源码文件，仍需同事加入 Compile 项。
## 当前目录结构

Portal 已迁入 WMS/Host.Modules/Portal；该目录是手写的独立宿主功能，不参与入口生成。入口生成脚本递归处理原 Modules 下的子目录，保留 Map 等相对路径；同步检查明确排除 Portal。

地图业务已集中在 WMS-Map（Models、Services、Repositories、Infrastructure、Database、Tests）。SQL 脚本在 WMS-Map/Database，不在根目录 Database/Map。已接入八个地图接口：列表、初始化、草稿读写、发布、当前正式版、版本列表与指定版本回读。当前为 GEOMETRY_ONLY；真实 Location 绑定、库存/HU、SignalR 和生产身份权限仍待接入。历史联调结果见 WMS-Map/Phase1-testing.md，不将历史测试视为本次复验。

新增地图入口放在 WMS/Modules/Map，独立副本生成到 WMS/Host.Modules/Map。Bootstrapper 和目录页支持子命名空间；页面仍从实际注册路由生成列表，并排除 Portal 自身。

VS 中选择重新加载工程以看到迁移后的 Portal、WMS-Map 以及数据库脚本。不要以打开前的旧项目配置覆盖磁盘文件。独立宿主默认端口统一为 31175，前端通过 5173 的 /wms-api 开发代理访问；生产部署需另配反向代理。
## 2026-10-12 维护复验

旧地图 backend 原型已从前端仓库移除，后端统一维护于本仓库。当前默认端口 31175；App.config、Program 默认值和前端开发代理一致。

本次使用 bin/Verification 独立输出构建，避免覆盖正在运行的宿主。构建通过；16 项 net461 目标无数据库测试、26 个模块和 106 条唯一路由验证通过；25 个生成入口与 262 条显式工程引用同步检查通过。本次没有运行真实数据库写入或力软生产环境验收。
