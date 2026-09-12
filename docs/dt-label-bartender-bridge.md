# DT 标签 BarTender Bridge 部署说明

## 1. 架构

`Learun.Application.WebApi`保留模板、字段、料号参数、OQC、四类字段求值、TXT任务和历史等业务能力，不再引用 `Seagull.BarTender.Print.dll`。

BTW解析、详细对象数据源识别、图片预览和 SDK健康检查迁移到独立的 `DT.BarTenderApi`：

```text
Learun.Application.WebApi (.NET Framework 4.7.2)
    -> HTTP + X-DT-BarTender-Key
DT.BarTenderApi (.NET Framework 4.8 / x64)
    -> Seagull.BarTender.Print.dll 11.4
    -> BarTender 11.4 Automation
```

两个 API没有项目或程序集引用关系，可以独立构建、发布和回退。

## 2. 路径和解决方案

- Bridge项目：`Learun.Framework.Module/Learun.Application.Module/DT.BarTenderApi/DT.BarTenderApi.csproj`
- Bridge专用解决方案：`DT.BarTenderApi.sln`
- 主解决方案中项目位于“03 二次开发模块”，只有 `ActiveCfg`，没有默认 `Build.0`，普通生成解决方案不会编译 Bridge。

## 3. 配置

主 WebApi：

```xml
<add key="BarTenderApiEnabled" value="true" />
<add key="BarTenderApiUrl" value="https://bartender-server/api/bartender" />
<add key="BarTenderApiKey" value="部署密钥" />
<add key="BarTenderApiTimeoutSeconds" value="60" />
```

源码模板默认 `BarTenderApiEnabled=false` 且密钥为空，避免普通开发环境误调用。Bridge的 `Web.config` 设置同一个 `BarTenderApiKey`；密钥为空时服务拒绝所有请求。真实密钥不得提交 SVN。

Bridge内部仍可配置本机 BarTender Actions API：

```xml
<add key="BarTenderMetadataApiUrl" value="http://localhost:5159/api/actions" />
```

## 4. API

- `POST /api/bartender/templates/parse`：Base64 BTW，返回字段及诊断结果。
- `POST /api/bartender/templates/preview`：Base64 BTW和测试值，返回 Base64 JPEG。
- `GET /api/bartender/health`：返回 SDK程序集、版本、Engine和 Actions API状态。

Bridge不访问 WMS数据库。Learun WebApi收到解析结果后，仍由现有 Repository事务保存模板和字段。

## 5. 构建和发布

普通开发人员无需安装 BarTender，可直接构建主 WebApi。

Bridge构建机必须安装 BarTender 11.4 SDK：

```powershell
msbuild DT.BarTenderApi.csproj /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /p:BarTenderSdkDir="C:\Program Files\Seagull\BarTender 11.4\SDK\Assemblies"
```

Bridge应用池使用 CLR v4.0、Integrated、`Enable 32-Bit Applications=False`。部署后依次验证 health、实际 BTW解析、FIELD映射和图片预览。

## 6. SVN

提交 `.sln/.csproj/.cs/.config`模板和文档；排除 `bin/obj/.vs/*.user`、Seagull DLL、许可证和真实密钥。Bridge目录当前需人工审查后执行 `svn add`。当前工作副本已有无关修改及冲突，不应执行 `svn commit .`。
