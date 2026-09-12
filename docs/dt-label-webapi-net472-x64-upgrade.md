# DT 标签 WebApi .NET Framework 4.7.2 / x64 升级记录

## 已实施配置

- `Learun.Application.WebApi` 目标框架升级为 `.NET Framework 4.7.2`。
- Debug/Release 均使用 `PlatformTarget=x64`、`Prefer32Bit=false`。
- IIS Express 固定使用 64 位；正式 IIS 应用池需设置 `Enable 32-Bit Applications=False`。
- BarTender SDK 基线为 64 位 BarTender 2022 R8、`Seagull.BarTender.Print 11.3.x`，本机程序集版本为 `11.3.8.1`。
- 已移除未使用且仅支持 x86 的 `Pechkin 0.5.8.1` 工程引用。
- `System.ValueTuple` 统一为程序集版本 `4.0.5.0`；`NPOI.OOXML` 统一为 `2.7.5.0`。
- DT 标签后端 C# 和 SQL/迁移工具已登记到经典 WebApi 工程。SQL 仅作为内容文件，未执行。

## 构建与位数检查

在开发者命令行执行：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  'D:\com.delton.gz\Learun.Application.WebApi\Learun.Application.WebApi.csproj' `
  /t:Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /m:1
```

工程的解决方案平台名称仍为 `AnyCPU`，但 Debug/Release 属性中的编译目标已经固定为 x64。构建后执行：

```powershell
& 'C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.7.2 Tools\CorFlags.exe' `
  'D:\com.delton.gz\Learun.Application.WebApi\bin\Learun.Application.WebApi.dll'
```

合格结果为：`PE=PE32+`、`32BITREQ=0`、`32BITPREF=0`。

## 本机 IIS Express

1. 关闭当前解决方案对应的 IIS Express 实例，不能批量终止其他项目的实例。
2. 在 Visual Studio 中启用“对网站和项目使用 64 位版 IIS Express”。工程也已设置 `Use64BitIISExpress=true`。
3. 启动 WebApi 后在任务管理器确认进程路径为 `C:\Program Files\IIS Express\iisexpress.exe`，不能是 `Program Files (x86)`。
4. 先访问普通 WebApi 接口，再访问 `/api/wms-fg/label-templates/sdk-health`。

## 当前 BarTender 阻塞与处理

64 位 .NET Framework 进程加载 `11.3.8.1` SDK 成功，但 `Engine.Start()` 返回 `0x800706BA`。同一时间 Windows Application 日志记录：

```text
MSSQL$BARTENDER 18456
Login failed for user 'DELTON\953818'.
Failed to open the explicitly specified database 'Datastore'.
```

所有 BarTender、Licensing、Print Scheduler、System Service 和 `MSSQL$BARTENDER` 服务均在运行，因此应先修复 System Database，而不是降级框架或重新启用 32 位 IIS：

1. 以管理员身份打开 BarTender Administration Console。
2. 在 System Database 页面检查 `Datastore` 是否存在、在线以及当前连接账户。
3. 重新连接/修复现有数据库并授予 WebApi 运行身份所需权限。不要直接删除或重建数据库；需要重建时先备份并人工确认。
4. 再次运行 SDK health。若数据库事件已消失但 COM 仍失败，使用 BarTender 2022 安装程序执行 Repair，重新注册 64 位组件。

通过标准：`AssemblyVersion=11.3.8.1`、`VersionCompatible=true`、`EngineStarted=true`，且模板打开和图片导出成功。

## 回归顺序

1. 登录、鉴权、普通数据库查询。
2. Excel/NPOI 导入导出、文件上传下载、现有 PDF/报表功能。
3. X02 BTW 上传并确认解析出 10 个 Named Values。
4. 人工测试值图片预览，包括中文、条码、空值和特殊字符。
5. 并发预览，确认字段和模板不串用。
6. 经数据库安全门禁批准后，才允许执行 DT 标签建表和样例迁移。

## 回退

恢复 WebApi `.csproj`、`Web.config` 和上一版发布目录，并恢复原 IIS 宿主设置。本次升级未执行数据库变更，不需要数据库回滚。
