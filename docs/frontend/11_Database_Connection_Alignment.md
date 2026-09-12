# WMS Frontend / Learun Database Connection Alignment

日期：2026-08-22  
性质：Investigation + SELECT-only runtime preflight

## Configuration Source

Learun Web 的数据库配置入口为：

- `D:\com.delton.gz\Learun.Application.Web\Web.config`
- `D:\com.delton.gz\Learun.Application.Web\XmlConfig\database.config`

`Web.config` 第 17 行使用标准 .NET Framework 外部配置段：

```xml
<connectionStrings configSource="XmlConfig\database.config" />
```

因此 `database.config` 不是 Learun 自定义 XML 格式；它是标准 `connectionStrings` section 的外部内容。完整 XML 严格解析通过，根节点为 `<connectionStrings>`，`MesDb` 和 `WmsDb` 都是该根节点下的直接 `<add>` 子节点，各精确出现一次，没有大小写变体。

配置文件与抽样的 `ioc.config/system.config/log4net.config` 均为 UTF-8 with BOM，XML declaration 为 UTF-8。本轮没有修改任何配置文件，也没有输出或记录完整连接串。

## Learun Database Config Loader

没有独立的 Learun `database.config` loader 类。真实调用链为：

1. ASP.NET/.NET Framework 根据 `Web.config` 的 `configSource` 加载 connectionStrings section。
2. `System.Configuration.ConfigurationManager.ConnectionStrings[name]` 以 `<add name="...">` 的 `name` 作为 key 读取 `ConnectionStringSettings`。
3. `Learun.DataBase.Repository.DbFactory.GetIDatabase(string name)` 读取 `ProviderName`。
4. `DbFactory.GetDbType(string providerName)` 将 `System.Data.SqlClient` 映射到 `DatabaseType.SqlServer`。
5. `DbFactory.GetIDatabaseByIoc(...)` 通过 `UnityIocHelper` 和 `XmlConfig/ioc.config` 创建 `Learun.DataBase.SqlServer.Database`。
6. `Learun.DataBase.SqlServer.Database.Database(string connString)` 再以 key 读取 `ConfigurationManager.ConnectionStrings[connString].ConnectionString`，创建 EF `DatabaseContext`；`Repository.getDbConnection()` 返回其 `DbConnection`。

真实证据文件与方法：

- `D:\com.delton.gz\Learun.Framework.Module\Learun.Db\Learun.DataBase.Repository\DbFactory.cs`
  - `GetIDatabase(string name)`
  - `GetDbType(string providerName)`
  - `GetIDatabaseByIoc(string name, string connString)`
- `D:\com.delton.gz\Learun.Framework.Module\Learun.Db\Learun.DataBase.Repository\RepositoryFactory.cs`
  - `BaseRepository(string name)`
- `D:\com.delton.gz\Learun.Framework.Module\Learun.Db\Learun.DataBase.Repository\Repository.cs`
  - `getDbConnection()`
- `D:\com.delton.gz\Learun.Framework.Module\Learun.Db\Learun.DataBase.EF.Sqlserver\Database.cs`
  - `Database(string connString)`
  - `getDbConnection()`
- `D:\com.delton.gz\Learun.Application.Web\XmlConfig\ioc.config`
  - `IDatabase` name `SqlServer` → `Learun.DataBase.SqlServer.Database`

`providerName` 会实际决定 Unity 解析的数据库实现，不是无效属性。完整 connection string 最终原样交给 `System.Data.SqlClient`，因此 `TrustServerCertificate=True` 会进入真实 SqlConnection 配置。

`ConfigurationManager`/ASP.NET 会按 AppDomain 缓存配置 section。外部 configSource 变化通常触发应用配置重载；为了避免已有 IIS Express/AppDomain 仍持有旧值，修改后仍建议显式停止并重新启动目标 IIS Express 站点再做验证。

## Effective Connection Keys

```text
MES_DB_CONNECTION_KEY=MesDb
WMS_DB_CONNECTION_KEY=WmsDb
```

Learun Web `database.config` 中两个 key 均存在且结构合法。Learun Solution 内没有第二个 `MesDb/WmsDb` 定义或大小写变体。

独立 WMS Host 的 `D:\Code\WMS-HU-Model\WMS\Web.config` 也各有一个同名 key，并指向相同测试目标。这不是同一 ConfigurationManager scope 内的重复冲突：Learun Web 与 WMS Host 是两个独立应用，各自只读取自己的配置。对 WeekCode API 真正生效的是 WMS Host 的配置，而不是 Learun Web 的 `database.config`。

## MesDb

- Server：`.\SQLEXPRESS`
- Database：`adms705`
- Authentication：Integrated Security
- Provider：`System.Data.SqlClient`
- Trust server certificate：True
- Environment：Development/Test

### Read test

通过临时、未纳入项目的预检 harness 调用真实 Learun：

```text
RepositoryFactory.BaseRepository("MesDb")
→ DbFactory.GetIDatabase("MesDb")
→ Learun.DataBase.SqlServer.Database
→ DbConnection.Open()
```

配置 key、provider、server、database、Integrated Security 和 TrustServerCertificate 均被正确解析；但当前终端身份 `DELTON\953818` 在 `Open()` 阶段失败：

```text
System.Data.SqlClient.SqlException
目标主体名称不正确。无法生成 SSPI 上下文。
```

因此规定的 `DB_NAME/SYSTEM_USER/ORIGINAL_LOGIN/sys.tables` SELECT 没有执行，不能把 Learun 进程连接标记为通过，也不能把表计数 0 解释为表不存在。

独立 WMS IIS Runtime 的只读 Audit GET：

```text
GET http://localhost:52602/api/wms/weekcode/audit?plantCode=1020&page=1&pageSize=1
HTTP 200 / Code 200 / TotalCount 230 / PackCreatedTime 非空
```

该链路在 WMS Runtime 内先读取 WmsDb，再由 `MesFinishedGoodsInboundRepository.GetPackCreatedTimes()` 读取 MesDb 的 `PK_Packmaster`，证明独立 WMS Host 当前能够读取 MesDb。但现有 API 不返回 MesDb 的 `DB_NAME/SYSTEM_USER/ORIGINAL_LOGIN`，所以不能替代规定的完整 MesDb preflight。

Required tables：规定的 sys.tables 查询受 SSPI 阻断；当前只由成功的 WMS Runtime 路径间接确认 `PK_Packmaster` 可读，其余 5 张未按本 Gate 精确验证。

## WmsDb

- Server：`.\SQLEXPRESS`
- Database：`WMS_DEV`
- Authentication：Integrated Security
- Provider：`System.Data.SqlClient`
- Trust server certificate：True
- Environment：Development/Test

### Read test

Learun Factory harness 与 MesDb 一样，在终端身份 `DELTON\953818` 的 `DbConnection.Open()` 阶段命中 SSPI failure，规定的 identity/table SELECT 未执行。

已有 WMS Development health endpoint 被安全复用：

```text
GET http://localhost:52602/api/health/db
HTTP 200
Code 200
Connected True
Server BT-ME-PC024\SQLEXPRESS
Database WMS_DEV
```

这证明当前运行中的独立 WMS IIS Runtime 能通过它自己的 `WmsDb` 建立只读连接，并验证真实数据库名为 `WMS_DEV`。随后 Audit GET 成功执行 `WeekCodeAuditRepository.QueryFacts()`，其 SQL 实际访问 `WMS_HU/WMS_HU_Relation/WMS_HU_Item/WMS_HU_HoldLog`。

现有 health endpoint 没有返回 `SYSTEM_USER/ORIGINAL_LOGIN`，也没有执行规定的 7 表 sys.tables 查询；因此完整身份和 7/7 表计数仍需 Human-approved development-only diagnostic 或受控 SQL 工具补验。

## WeekCode Audit Runtime Path

静态代码证明当前页面不是 Learun Controller proxy，也不直接访问数据库：

```text
Browser
→ Learun MVC Razor page /WMS/FG_WeekCodeAudit/Index
→ WeekCodeAuditApi.js / Wms.Api
→ relative HTTP /api/wms/weekcode/audit or /recalculate
→ deployment same-origin route/reverse proxy
→ independent WMS Nancy Host
→ WeekCodeAuditModule / WeekCodeRecalculateModule
→ Service
→ WMS repositories using WMS Host WmsDb
→ MES repositories using WMS Host MesDb
```

`FG_WeekCodeAuditController.Index()` 只返回 View；没有 proxy action、Repository 或 ConfigurationManager。页面 JS 没有 connection string、SqlConnection 或数据库 helper。

当前源码未包含把 Learun origin 的相对 `/api/wms/*` 转发到独立 WMS Host 的 proxy/reverse-proxy 配置。直接访问 WMS Host 的 52602 端口成功，但 Learun 页面同源 URL 的部署映射仍需 Human Runtime Review。

## Direct DB Access Decision

```text
FRONTEND_DIRECT_DATABASE_ACCESS=False
LEARUN_DIRECT_DB_CONNECTION_REQUIRED=False
```

WeekCode 页面正确边界是 HTTP API。不要因为 Learun `database.config` 已加入 `MesDb/WmsDb` 就在 Learun Controller 中新增 Repository 或绕过 WMS API。对当前 WeekCode 功能，Learun Web 中这两个 key 是未消费的备用配置；真正必须存在的是独立 WMS Host 配置。

## Existing Connection Diagnostic

Learun 已有 `DatabaseLinkController.TestConnection` → `DatabaseLinkBLL.TestConnection` → `DatabaseLinkService.TestConnection`，但它接收完整连接串/数据库类型并调用 `BeginTrans()`/`Commit()`，不是按 connection key 的 SELECT-only preflight，而且会把所有异常压缩为布尔失败。本轮没有调用它。

WMS 已有 `/api/health/db`，只读且不暴露完整连接串，本轮已复用。它当前仅覆盖 WmsDb，并缺少 Windows process identity、SQL login/original login 和 required-table inventory。若要补齐本 Gate，建议 Human 审批后扩展为仅 Development 可用、固定 key allowlist、固定 SELECT、响应脱敏的 diagnostic；不要提供任意 SQL 或完整 connection string。

## SSPI / Runtime Notes

- Terminal/Learun harness process identity：`DELTON\953818`。
- 当前可见两个 IIS Express 进程，但受现有进程查询权限限制，无法读取其 token owner/command line；不能把终端身份静默等同于 IIS Runtime identity。
- WMS IIS Runtime 能连接 WMS_DEV 并完成含 MesDb 读取的 Audit GET；Terminal/Learun harness 对相同目标命中 SSPI failure，说明身份/token/进程生命周期差异是关键证据。
- SQL Server `MSSQL$SQLEXPRESS` 服务正在运行，服务身份为 `NT Service\MSSQL$SQLEXPRESS`。
- 本轮没有切换 SQL 用户名密码、没有加 `Encrypt=False`、没有改 server alias 或 connection string。
- 失败分类：`SSPI_FAILED`。不是 CONFIG_NOT_FOUND、CONFIG_PARSE_FAILED、DATABASE_NOT_FOUND 或 TABLE_NOT_FOUND。

## Final Decision

```text
DATABASE_CONFIG_FILE_FOUND=True
DATABASE_CONFIG_STRUCTURE_COMPATIBLE=True
MES_DB_CONFIG_FOUND=True
WMS_DB_CONFIG_FOUND=True

WMS_SERVICE_RUNTIME_WMSDB_READ=True
WMS_SERVICE_RUNTIME_MESDB_READ=True
LEARUN_FACTORY_TERMINAL_MESDB_READ=False
LEARUN_FACTORY_TERMINAL_WMSDB_READ=False

DATABASE_CONNECTION_ALIGNMENT_COMPLETE=False
```

Gate 未完成的原因不是配置结构或 connection key，而是：

1. Learun Factory/terminal identity 的两条连接均被 SSPI 阻断；
2. 现有 runtime diagnostic 没有输出规定的 SQL identities 和完整 required-table inventory；
3. Learun 页面相对 `/api/wms/*` 到独立 WMS Host 的同源路由尚未验证。

本轮所有数据库 SQL 均为 SELECT；没有业务 POST、数据库写入、schema 修改或业务代码修改。

## Build Verification

`Learun.Application.Web.csproj` Debug build passed with `BUILD_EXIT=0`. Two referenced projects reported `MSB3101` warnings because existing `obj\Debug\*.AssemblyReference.cache` files were not writable, but all affected assemblies and `Learun.Application.Web.dll` were generated successfully.

## API Runtime Route Alignment

The database conclusion for the WeekCode frontend is now separated from API route readiness:

```text
DATABASE_CONFIG_ALIGNMENT_PASS=True
WMS_RUNTIME_WMSDB_READ_PASS=True
WMS_RUNTIME_MESDB_READ_PASS=True
LEARUN_DIRECT_DB_CONNECTION_REQUIRED=False
LEARUN_FACTORY_DB_PREFLIGHT_BLOCKING=False
```

The independent WMS runtime is reachable at `http://localhost:52602`, but the active Learun development origin `http://localhost:20471` returns HTTP 404 for the relative `/api/wms/weekcode/audit` request. No IIS rewrite/ARR rule, Learun server proxy, WMS service BaseUrl setting, or direct cross-origin configuration was found. Therefore the database alignment is accepted for this frontend boundary, while API route alignment remains a separate failed gate. See `docs/frontend/12_WMS_API_Route_Alignment.md`.
