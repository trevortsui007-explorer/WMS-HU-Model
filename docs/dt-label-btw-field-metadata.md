# DT 标签 BTW 字段技术元数据

## 1. 字段来源

上传或替换 BTW 时，WebApi 以 BarTender Print SDK 的 `LabelFormatDocument.SubStrings` 为基础字段清单，并优先通过 BarTender 11.4.1+ Actions API 的 BTXML `GetFormatProperties IncludeObjectDataSources="true"` 补充详细技术元数据：

- `SubString.Name` -> `NamedValue`
- `SubString.Type.ToString()` -> `BtwSourceType`
- `SubString.Value` -> 解析期样本值 `CurrentValue`（只用于诊断，不写数据库）
- 字段在 SDK 枚举中的顺序 -> `SortNo`

详细解析通过 Windows Integrated Authentication 调用 `http://localhost:5159/api/actions`。只有 XML 中能够从 Named Data Source 的 ID/Name 明确到达对象数据源，并最终唯一到达 `DatabaseFieldName`、`DatabaseField` 或 `FieldName` 时，才写入 `BtwDatabaseFieldName`。仅有 `Used=True` 的数据库字段列表时不会按位置推断。REST 不可用时退回 Print SDK 基础解析，上传仍可完成，但未确认的数据库字段保持空值。

BarTender 官方说明：[`GetFormatProperties`](https://help.seagullscientific.com/12.0/en/Subsystems/BTXML/Content/GetFormatProperties_Tag.htm) 可返回 Named Data Sources、对象数据源和数据库设置；其数据库 Schema 中的 `Field` 只表示字段存在或被使用，不天然证明它与某个 Named Value 的一一对应关系。

## 2. 脱敏 XML 结构与当前验证状态

2026-09-10 只读检查目标库，确认实际样本 `HelloWorldTrevor.btw` 包含 `IDBar`；数据库中该字段原有 `FieldDescription/BtwSourceType/BtwDatabaseFieldName` 均为空。BTW 二进制仅写入系统临时目录，探针结束后删除，没有更新数据库。

本机实际 SDK 探针未能进入文档打开阶段：64 位 Windows PowerShell/.NET Framework 调用 `Engine.Start()` 返回 COM `0x800706BA`。因此本次没有取得可以诚实标记为“实际输出”的 `format.SubStrings.XML` 或 GetFormatProperties 响应，不能把下列契约示意冒充实际样本：

```xml
<!-- 契约示意，不是本机实际输出；Value、路径和连接信息必须脱敏 -->
<SubStrings>
  <SubString Name="IDBar" Type="Database" FieldName="FIELD3">
    <Description>工号条形码</Description>
    <Value>***</Value>
  </SubString>
</SubStrings>
```

修复 BarTender COM/System Database 后必须重新运行实际上传验证，并用真实 XML 替换本节示意。验收目标是：

```text
NamedValue=IDBar
FieldDescription=工号条形码
BtwSourceType=Database
BtwDatabaseFieldName=FIELD3
```

若实际 XML 只能证明 `IDBar` 是 `Database` 类型，不能证明对应 `FIELD3`，则 `BtwDatabaseFieldName` 必须保持空值。

2026-09-10 已使用以下脱敏结构夹具验证新版引用图解析器；这是结构测试，不冒充 BarTender 11.8 实际输出：

```xml
<NamedDataSources>
  <DataSource ID="N1" Name="IDBar" />
</NamedDataSources>
<Objects>
  <Object Name="Barcode 1">
    <DataSource Type="Database">
      <NamedDataSourceID>N1</NamedDataSourceID>
      <DatabaseFieldName>[RECORDSET].[TABLE].[FIELD3]</DatabaseFieldName>
      <Description>工号条形码</Description>
    </DataSource>
  </Object>
</Objects>
```

夹具结果为 `IDBar / Database / FIELD3 / 工号条形码 / Exact`。增加第二个 `FIELD4` 后结果为 `Ambiguous` 且数据库字段留空；仅在 `LastKnownSchema` 中加入 `FIELD3 Used=true` 时结果为 `Unresolved`，证明解析器没有按集合位置推断。

## 3. 同步语义

字段同步与模板文件保存共用一个数据库事务：

1. 继续使用 `DT_LabelFieldNameList` 同步 `NamedValue/SortNo`。
2. 同名字段保留 `ValueType/DefaultValue/DynamicFieldName/FunctionName`；解析到唯一且非空的数据库字段时覆盖原映射，未解析或有歧义时保留原人工映射。
3. BTW 返回非空描述时更新 `FieldDescription`；返回空描述时保留数据库原值。
4. 每次替换都刷新 `BtwSourceType`；`BtwDatabaseFieldName` 只有精确解析为非空值时才覆盖，新字段未解析时保持为空。
5. 新字段以 `ValueType=0` 创建；已删除字段及其料号参数继续删除。
6. 原有文件版本递增和 OQC 重置逻辑保持不变。

字段建模保存接口允许手工维护 `BtwDatabaseFieldName`，但忽略客户端提交的 `BtwSourceType`。非空数据库字段会校验长度、分隔符和不区分大小写的唯一性；空输入表示保留数据库原值，不提供清空操作。保存与其他业务配置处于同一事务并重置 OQC。

不提供手工重新解析接口；BarTender 解析只在首次上传或替换 BTW 文件时执行。`GET /api/wms-fg/label-templates/{id}/metadata-diagnostics` 仅由 System 账号读取已保存字段元数据，不启动 BarTender：有数据库字段时显示“已保存”，否则显示“未解析”。

## 4. 部署

部署脚本为 `003_DT_LabelTemplateField_BtwMetadata.sql`。脚本通过 `COL_LENGTH` 幂等新增两列，并包含显式事务及 `SET XACT_ABORT ON`。本次未执行该脚本，也未执行任何 DDL/DML。

详细解析的应用配置：

- `BarTenderMetadataApiEnabled=true`
- `BarTenderMetadataApiUrl=http://localhost:5159/api/actions`
- `BarTenderMetadataApiTimeoutSeconds=30`

目标 BarTender 为 11.8 Automation 64 位，最低支持详细元数据的版本为 11.4.1。工程优先检测 `C:\Program Files\Seagull\BarTender 11.8\SDK\Assemblies`，未安装时继续使用当前 2022 SDK路径，也可通过 `/p:BarTenderSdkDir=...` 覆盖。

当前机器截至 2026-09-10 仍为 2022 R8 / SDK 11.3.8.1；下载目录中没有 11.8 安装程序，也未取得许可升级权益，因此本轮没有伪造“已升级”结论。安装 11.8 后必须在 Administration Console 为 WebApi/IIS 身份授予 `API User` 和 `Administer Integrations`，再用真实 BTW调用“解析详情”，并把脱敏后的真实响应补入本文件。
