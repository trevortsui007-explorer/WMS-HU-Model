# WMS-Map 地图业务

本目录是迁入 WMS_FG 的地图业务源码，数据库脚本随业务保存在 `Database/`。初期与 WMS-HU-Model 一样由宿主直接编译，不是独立部署服务。

- Models：草稿 DTO、结果及依赖接口。
- Services：草稿读写编排、JSON 与布局数据校验。
- Repositories：SQL 草稿读写、并发版本检查、初始化与操作日志事务。
- Infrastructure：连接、事务、时钟、WMS HTTP 客户端及资源释放。
- Database：六张 FG_Map 表的建表/回滚脚本。迁移不会执行 SQL。
- Tests：从原地图后端迁入的回归检查，直接编译本目录源码，以 net461 验证 API 兼容性。

暂时保留原有 Map.Application / Map.Infrastructure 命名空间，减少迁移风险；目录结构不要求与命名空间逐字对应。数据库脚本和通用业务代码来自地图系统的 backend 原型，后续以本目录作为 WMS_FG 地图业务维护入口。地图前端仓库中的旧 backend 原型已退出维护并移除；本目录为唯一后端业务维护入口，运行入口是 WMS.Standalone.sln。

## 入口与状态

力软地图入口放在 `WMS/Modules/Map`，继承 BaseApi；独立地图入口生成到 `WMS/Host.Modules/Map`，继承 NancyModule。Portal 在 `WMS/Host.Modules/Portal` 单独维护，不参与入口生成。

已实现八个地图 Nancy 接口：布局列表、首次初始化、草稿读取/并发保存、发布、当前正式版、版本列表与指定版本回读。独立宿主通过 Windows 身份记录操作人；力软需接入可信身份和权限。实时库存与 Location 绑定仍待实现。详见 Phase1-testing.md。

持久化规则：Ctrl+S 仍在浏览器 IndexedDB；保存草稿写入共享草稿；发布生成正式版本。审计字段沿用 CreateBy/CreateDt/LastEditBy/LastEditDt、SavedBy/SavedDt、PublishedBy/PublishedDt。

力软首次接入地图代码需要增加相应 Compile 项，并核对引用及身份适配。后续地图入口不能依赖 BaseApi 白名单自动提供用户信息。

## 本地验证

使用 VS2022 MSBuild 对 Tests/WMS.Map.Tests.csproj 执行 /restore /t:Build，再运行 Tests/bin/Debug/WMS.Map.Tests.exe。该工程使用 net461 引用包，实际执行运行在本机已安装的 Framework 4.x 上；不连接真实数据库。
