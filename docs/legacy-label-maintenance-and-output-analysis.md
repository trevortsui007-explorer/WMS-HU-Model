# WMS-HS 标签维护与输出链路分析

## 1. 文档目的与结论

本文分析以下两个既有大界面及其后端实现：

- `WMS_LabelTemplate`：标签模板维护。
- `WMS_LabelParamImport`：标签自定义参数批量导入。

分析基于 `D:\Code\WMS-HS\Developer_Chenhong18` 下的前端、Controller、BLL、Service、Entity 及相关发货打印代码进行静态追踪；没有连接数据库、没有调用 BarTender 服务、没有向打印目录写文件，也没有执行真实打印。

核心结论如下：

1. 标签由三层内容共同组成：
   - `.btw` 文件决定版式、条码和 BarTender Named Data Source 名称；
   - `WMS_PrintingDataMaint` 决定每个 Named Value 的取值方式；
   - `HS_LabelMapperMaint` 保存“模板 + 料号 + 参数名”的定制值。
2. `.btw` 上传时，WMS 调用配置键 `BtwUpload` 对应的外部服务。外部服务保存模板并返回模板内的 Named Values，WMS 再同步生成 `WMS_PrintingDataMaint` 行。
3. 预览不是浏览器本地渲染。WMS 计算参数后调用 `BtwExportImg` 外部服务，取得 Base64 图片并显示。
4. 正式业务打印主要不是直接调用打印机，而是把一个 UTF-8、无 BOM 的 `.txt` 任务文件写入 `HS_LabelPrinterPath.PrinterPath`。从代码边界看，后续应由部署在打印侧的 BarTender/监听程序消费该文件并完成物理打印；监听程序本身不在本仓库中，因此这是有代码依据的架构推断，而不是已验证的运行事实。
5. `WMS_LabelParamImport` 自身只写 `HS_LabelMapperMaint`。当前 C# 标签计算器没有直接读取该表；定制值要真正出现在标签上，必须由 `ValueType=2` 所选择的数据库存储过程间接查询它。相关存储过程定义不在代码库中，因此“导入值是否及如何覆盖最终字段”不能仅凭当前源码完全证明。

## 2. 总体结构

```text
BarTender Designer
    |
    | 设计 .btw，并定义 Named Values
    v
WMS_LabelTemplate（模板维护）
    |-- 保存模板元数据/二进制 ------------> WMS_LabelTemplate
    |-- 调 BtwUpload 上传 .btw ----------> 外部 BarTender 服务
    |                                      |
    |<------ 返回 Named Values ------------+
    |
    +-- 同步字段及取值规则 --------------> WMS_PrintingDataMaint
                                               |
WMS_LabelParamImport / 页面下半区              | ValueType 决定取值
    +-- 维护料号定制值 ------------------> HS_LabelMapperMaint
                                               |
业务打印（发货客户标签/内包标签）              |
    +-- generateDynamicLabelData <-------------+
    |       0 默认值 / 1 主数据列 / 2 存储过程 / 3 手工值
    |
    +-- 预览：BtwExportImg -> Base64 图片 -> 浏览器
    |
    +-- 打印：生成 [Bartender]+[NamedValues] 文本任务
            -> HS_LabelPrinterPath.PrinterPath
            -> 外部打印监听/BarTender（仓库外，待运行验证）
```

## 3. 关键数据模型

### 3.1 `WMS_LabelTemplate`：模板主档

| 字段 | 含义 | 维护/使用方式 |
| --- | --- | --- |
| `LabelTemplateId` | 模板主键 | 新增时由 Entity `Create()` 生成 |
| `Name` | `.btw` 文件名 | 由上传文件名自动带出；前端禁止中文名 |
| `FileData` | `.btw` 二进制 | 保存于数据库，供模板下载；上传时同时发往外部服务 |
| `LabelType` | 标签类型 | `1=客户标签`，`2=内包标签` |
| `CusType` | 客户别 | 页面必填；属于模板检索/说明信息 |
| `ShipAddress` | 出货地 | 页面必填；属于模板检索/说明信息 |
| `Description` | 描述 | 页面维护 |
| `IsBlock` | 是否停用 | 列表仅显示 `IsBlock=0`，页面没有直接维护控件 |
| `OqcCheck` | OQC 状态 | `0=未确认`，`1=已确认` |
| `OqcUser/OqcDate` | OQC 确认人/时间 | 确认或取消确认时更新 |
| `LabelTags/LabelDesc` | 旧式标签字段定义 | 当前主维护页面已注释；只在遗留 `CustomPrint` 路径中使用 |

### 3.2 `WMS_PrintingDataMaint`：模板 Named Value 取值模型

每个 `.btw` Named Value 对应一行。

| 字段 | 含义 |
| --- | --- |
| `PrintingDataId` | 行主键 |
| `LabelTemplateId` | 所属模板 |
| `NamedValue` | 必须与 `.btw` Named Data Source 名称匹配 |
| `NamedNotes` | 页面可读说明 |
| `ValueType` | 取值类型：0 默认、1 可选主数据、2 数据库函数、3 手工 |
| `DefaultValue` | `ValueType=0` 时直接输出的固定值 |
| `DynamicExpression` | `ValueType=1` 时选择的主数据列名 |
| `FuncExpression` | `ValueType=2` 时执行的存储过程名 |
| `LastEditUser/LastEditDate` | 最后维护信息 |

`ValueType` 的实际求值逻辑为：

| 类型 | 页面名称 | 实际来源 | 运行方式 |
| --- | --- | --- | --- |
| `0` | 默认值 | `WMS_PrintingDataMaint.DefaultValue` | 原样输出 |
| `1` | 可选值 | `HS_Product`、`HS_ProductProperty` | 按 `model.Product` 查一行，再读取 `DynamicExpression` 指定列 |
| `2` | 函数 | 数据库存储过程 | 执行 `FuncExpression`，输入参数名、交货单、料号、外箱号，读取输出参数 `@resultData` |
| `3` | 手动 | 调用页面传入的 `manualData` | 按 `NamedValue` 精确匹配；客户发货打印未传手工集合 |

页面允许的 `DynamicExpression` 固定为：`ProductCode`、`UnitArea`、`UnitWeight`、`CustomerPN`、`CustomerModel`、`BaseUom`、`Layer`。

### 3.3 `HS_LabelMapperMaint`：模板/料号定制参数

| 字段 | 含义 |
| --- | --- |
| `LabelMapperMaintId` | 主键 |
| `LabelTemplateId` | 模板 |
| `ProductId` | 成品料号主键 |
| `NamedValue` | 模板变量名 |
| `DefaultValue` | 此模板、此料号、此变量的定制值 |
| `Description` | 备注 |
| `LastEditBy/LastEditDate` | 最后维护信息 |

逻辑唯一键由代码按 `(LabelTemplateId, ProductId, NamedValue)` 查找，但是否存在数据库唯一约束未验证。

需要特别注意：C# 的 `generateDynamicLabelData` 不直接查询 `HS_LabelMapperMaint`。因此这张表不是一个由应用层自动套用的“全局覆盖层”。较合理且与现有结构吻合的设计是：某些 `HS_LabelFunction` 中登记的存储过程读取 `HS_LabelMapperMaint`，再通过 `ValueType=2` 返回值；是否确实如此需要读取目标数据库中的存储过程定义确认。

### 3.4 其他关联表

| 表 | 作用 |
| --- | --- |
| `HS_LabelFunction` | 提供数据建模页面中的存储过程候选项，核心字段为 `FuncName/FuncNotes` |
| `HS_LabelPrinterPath` | 打印位置主档，`Guid/Name/PrinterPath`；正式打印按 `Guid` 找物理/共享目录 |
| `WMS_PackingLineMaint` | 模板维护页“手工打印”选择装箱线，并使用其 `PrinterFilePath` |
| `HS_Product` | 料号存在性校验、动态字段来源、内包排版数 `PcspersetQty` 来源 |
| `HS_ProductProperty` | 客户料号、客户型号、层数等动态字段来源 |
| `HS_PackingRule` | 可把产品/包装规则关联到 `LabelTemplateId`，属于业务侧模板选择的一部分 |
| `WMS_PrinterLabelHistory` | 正式客户/内包标签任务文件历史，保存用户、时间、IP、类型、原始任务文本、请求值、打印路径、模板名等 |
| `WMS_RePrinterLabelHistory` | 补打历史 |

## 4. `WMS_LabelTemplate` 界面拆解

### 4.1 主界面布局

主界面分上下两块：

1. 上半区“模板信息”
   - 过滤条件：标签类型、模板名关键字。
   - 操作：新增、编辑、删除、数据建模、模板下载、手工打印、内包标签打印、OQC 确认/取消确认。
   - 列：模板名、图片预览、类型、客户别、出货地、描述、修改人/时间、OQC 状态/人员/日期。
2. 下半区“自定义参数信息”
   - 选择上方模板后，以 `LabelTemplateId` 查询该模板的料号参数。
   - 可按料号过滤。
   - 可新增、编辑、删除 `HS_LabelMapperMaint`。

上下区的关系是主从关系：上方当前选中的模板决定下方显示哪个模板的料号定制参数。

### 4.2 新增/修改模板

前端流程：

1. 选择 `.btw` 文件，浏览器把文件名写入只读的 `Name`。
2. 输入模板类型、客户别、出货地、描述。
3. 前端拒绝包含中文字符的文件名。
4. 通过 `ajaxFileUpload` 把文件和序列化后的实体提交到 `POST /WMS/WMS_LabelTemplate/SaveForm`。

后端流程：

1. Controller 从 `Request.Files["uploadFile"]` 读取二进制到 `entity.FileData`。
2. Service 开启数据库事务。
3. 新增或更新 `WMS_LabelTemplate`，并将 OQC 重置为未确认。
4. 若本次带有文件：
   - 从 `WMS_InterfaceConfig` 取得 `BtwUpload` 地址；
   - POST `{ Name, Content }` 到外部 BarTender 服务；
   - 将服务返回的逗号分隔 Named Values 与数据库现有行比较；
   - 删除模板中已不存在的变量；
   - 为模板中新出现的变量插入 `WMS_PrintingDataMaint`，初始 `ValueType=0`；
   - 已存在且仍同名的变量保留原取值配置。
5. 外部上传成功且数据同步成功后提交事务，否则回滚数据库事务。

这意味着 `.btw` 是变量集合的源头：数据建模页面不能自由新增一个 `.btw` 中不存在的字段，只能配置上传服务识别出的 Named Values。

### 4.3 标签数据建模

入口为主界面的“数据建模”。页面读取 `GET /WMS/WMS_LabelTemplate/GetPrintingDataList` 并逐行维护：

- 数据名称 `NamedValue`：只读，来自 `.btw`。
- 数据描述 `NamedNotes`：人工维护。
- 使用类型 `ValueType`：默认值/可选值/函数/手动。
- 默认值 `DefaultValue`。
- 可选值 `DynamicExpression`。
- 函数 `FuncExpression`。

提交前，前端只校验：

- 类型 1 必须选择动态表达式。
- 类型 2 必须选择函数。

后端逐行更新 `WMS_PrintingDataMaint`，然后再次把模板 OQC 状态重置为未确认。也就是说，模板版式改变或字段取值规则改变后，都要求重新进行 OQC 确认。

### 4.4 料号自定义参数维护

主界面下半区实际复用了 `HS_LabelMapperMaint` 模块：

1. 先选模板。
2. 再选成品料号。
3. 参数名下拉框来自该模板的 `WMS_PrintingDataMaint.NamedValue`。
4. 输入参数值和描述。
5. 保存为 `HS_LabelMapperMaint`。

编辑时，料号、模板和参数名在前端被锁定，只能修改参数值/描述；新增时模板也会固定为上半区当前选择。

这套单条维护与 `WMS_LabelParamImport` 写入的是同一张表。

### 4.5 预览

有三类预览：

1. 空模板/手填字段预览
   - 主列表点“预览”进入 `ViewLabel`。
   - 页面先只传模板 ID；也可手填每个 `DataValue` 后再次预览。
   - Controller 调 `BtwExportImg`，请求体为 `{ Name: 模板文件名, Params: Hashtable }`。
   - 外部服务返回 Base64 图片，浏览器放入 `<img>`。
2. 发货客户标签预览
   - 位于 `WMS_DeliveryShip/ViewCusLabel`，不是模板维护主页面本身。
   - 对选中的交货单外箱，先调用 `generateDynamicLabelData` 计算全部 Named Values，再逐箱调用 `BtwExportImg`。
3. 内包标签预览
   - 在模板维护页选择 `LabelType=2` 的模板后进入。
   - 根据总 SET 数、包规和排版数生成多条内包数据；`Qty` 被换算成 `本包 SET 数 × PcspersetQty`。
   - 手工字段来自 `ValueType=3` 的建模项，其中 `Qty` 会被系统分包结果覆盖。
   - 每一条内包数据分别计算 Named Values 并调用 `BtwExportImg`。

预览链路只返回图片，不写打印任务文件，也不写打印历史。

### 4.6 OQC 确认

- “确认”写 `OqcCheck=1`，并记录当前账号和时间。
- “取消确认”写 `OqcCheck=0`，代码仍会写当前账号和时间，但列表在未确认状态下隐藏人员/日期。
- 修改模板或提交数据建模都会清除确认状态。
- 正式客户发货打印 `SubmitDeliveryPrint` 明确拦截未 OQC 确认模板。
- 模板维护页的手工打印、内包标签打印、通用脚本生成路径没有看到同样的 OQC 检查。

因此 OQC 当前不是所有打印入口统一执行的全局门禁。

## 5. `WMS_LabelParamImport` 界面拆解

### 5.1 页面职责

该页面不维护 `.btw`，也不维护 `WMS_PrintingDataMaint` 的类型规则。它只用于批量新增或更新 `HS_LabelMapperMaint`。

主列表展示：标签名、料号、参数名称、参数值、描述、更新人、更新时间。查询关键字匹配模板名或料号。

### 5.2 Excel 解析流程

1. 点击“Excel导入”打开预览窗。
2. 在预览窗点击“导入Excel资料”，复用通用 `CommonWin/ImportForm` 上传组件。
3. 上传文件由 `WMS_LabelParamImportController.ExcelImport` 调用 `ExcelHelper.ExcelImport` 解析。
4. Excel 列标题必须是：
   - `标签模板名`
   - `料号`
   - `参数名`
   - `参数值`
   - `备注`
5. Controller 将每行转换为预览对象，回调 `ImportCallBack` 加到 `grid_import`。
6. 用户点击“保存数据”后，整个预览列表提交到 `SaveExcelImportData`。

代码中存在“下载模板”入口和固定文件名 `标签参数导入模板.xlsx`，但对应按钮在 cshtml 中为 `hidden`。

### 5.3 保存/覆盖规则

Service 在一个事务对象中逐行处理：

1. 按 `ProductCode` 查 `HS_Product`；不存在则返回失败。
2. 按完整 `Name` 查 `WMS_LabelTemplate`；不存在则返回失败。
3. 按 `(LabelTemplateId, ProductId, NamedValue)` 查 `HS_LabelMapperMaint`。
4. 已存在：只更新 `DefaultValue`、`LastEditBy`、`LastEditDate`。
5. 不存在：插入新行，并写入 `Description`。
6. 全部行处理完后提交事务。

实际含义是“upsert”：同一模板、料号、参数名重复导入时覆盖参数值，而不是追加另一条版本。

当前实现没有显式验证：

- `NamedValue` 是否确实属于该模板的 `WMS_PrintingDataMaint`；
- 参数名是否为空；
- 参数值是否为空或符合数据格式；
- 同一个导入文件是否含重复逻辑键；
- 模板是否停用或是否经过 OQC；
- 已有行重新导入时，Excel 中的新备注是否应覆盖旧描述（当前不会覆盖）。

## 6. Named Value 最终如何求值

业务打印会构造 `DeliveryLabelModel`：

```text
BtwName        = 模板文件名
DeliveryNumber = 交货单号（内包标签为空）
Masterpack     = 外箱号（内包标签为空）
Product        = 成品料号
```

随后 `generateDynamicLabelData` 按 `WMS_PrintingDataMaint` 的每一行求值：

```text
ValueType 0 -> DefaultValue
ValueType 1 -> HS_Product/HS_ProductProperty 的指定列
ValueType 2 -> EXEC FuncExpression
               @namedvalue = 当前 NamedValue
               @delivery   = 交货单号
               @product    = 料号
               @masterpack = 外箱号
               @resultData = 输出值
ValueType 3 -> 从本次调用的 manualData 按名称寻找值
```

结果是一个有序的 `KeyValuePair<string,string>` 列表，最终被序列化为：

```ini
[Remark]
Description=...
PrintUser=...
[Bartender]
BtwFileName=C:\LabelTemplates\Example.btw
[NamedValues]
PN=04S880351A
Qty=100
CustomerPN=...
```

BarTender 侧模板必须存在于其本地 `C:\LabelTemplates\`，而 `.txt` 中的键必须与 `.btw` 的 Named Values 精确一致。

### 6.1 `HS_LabelMapperMaint` 的真实接入点

源代码能够证明：

- 两个维护界面会写 `HS_LabelMapperMaint`；
- `ValueType=2` 会执行一个数据库存储过程，并提供模板字段名、交货单、料号和外箱号上下文。

源代码不能证明：

- 哪些 `FuncExpression` 会查询 `HS_LabelMapperMaint`；
- 发生匹配和不匹配时，存储过程采用定制值、模板默认值还是空值；
- 多条重复数据时采用哪一条。

因此若要形成可实施的新系统规则，必须补充只读导出：

1. `HS_LabelFunction` 当前数据；
2. 其中每个 `FuncName` 对应的存储过程定义；
3. `HS_LabelMapperMaint` 是否存在唯一约束/索引；
4. 一组实际模板、料号和最终打印文本样本。

## 7. 最终输出路径

### 7.1 客户发货标签

入口：`WMS_DeliveryShip/ViewCusLabel`。

流程：

1. 读取交货单外箱列表。
2. 用户选择客户模板、打印位置和一个或多个外箱。
3. `SubmitDeliveryPrint` 校验打印路径可访问、模板存在、模板已 OQC 确认、数据建模存在。
4. 每个外箱执行动态求值并生成一份任务文本。
5. 文件写入 `HS_LabelPrinterPath.PrinterPath`：`{MasterPack}_{yyyyMMddHHmmss}.txt`。
6. 写 `WMS_PrinterLabelHistory`，`LabelType=Delivery`，`RequestValue=外箱号`。
7. 外部监听程序消费 `.txt` 并驱动 BarTender 打印。

预览走相同的动态求值逻辑，但最后调用 `BtwExportImg`，不落 `.txt`。

### 7.2 内包标签

入口：模板维护页的“内包标签打印”。

流程：

1. 只允许选择 `LabelType=2` 的模板。
2. 用户选择料号，系统读取 `HS_Product.PcspersetQty` 作为每 SET 排版数。
3. 用户维护包规和所有手工参数，`Qty` 表示本次总 SET 数。
4. 页面按包规拆成多包，并把每包标签数量转换为 `SET 数 × 排版数`。
5. 用户勾选要打印的内包行和打印位置。
6. Service 对每行执行动态求值，其中手工 `Qty` 被当前行自动计算值替换。
7. 文件写入 `HS_LabelPrinterPath.PrinterPath`：`{模板名}_{时间}_{序号}.txt`。
8. 写 `WMS_PrinterLabelHistory`，`LabelType=InnerCode`。
9. 外部监听程序消费任务文件并打印。

当前内包 Service 在目录存在性检查后紧接着把结果强制设为 `true`，所以目录不可访问时不会在校验处友好返回，而可能在 `File.WriteAllText` 才抛异常。

### 7.3 模板维护页“手工打印”

该入口与正式业务打印不同：

1. 页面展示所有 `WMS_PrintingDataMaint` 行，但用户直接填写 `DataValue`。
2. 选择 `WMS_PackingLineMaint` 的装箱线。
3. Controller 不执行 `ValueType` 求值，而是把用户输入原样生成 `[NamedValues]`。
4. 文件写入该装箱线的 `PrinterFilePath`，文件名为 `{模板名}_{yyyyMMddHHmmss}.txt`。
5. 当前代码没有写 `WMS_PrinterLabelHistory`，也没有 OQC 拦截。

### 7.4 遗留 `CustomPrint`

Controller 还保留一条 `CustomPrint` 路径，生成 BarTender 传统 Trigger File：

- 文件写入配置项 `BarTender_ScriptPath`；
- 模板路径取 `BarTender_TempPath`；
- 内容使用 `%BTW% ... /P /C=1`、`LabelTags` 和 `#` 分隔值；
- 网络目录通过配置账号访问。

当前两个主界面没有调用该接口，它应视为兼容性遗留路径，不应与主业务 `[Bartender]/[NamedValues]` 任务格式混为一谈。

## 8. 接口清单

### 8.1 模板维护相关

| 方法 | 路径 | 作用 |
| --- | --- | --- |
| GET | `/WMS/WMS_LabelTemplate/GetPageList` | 模板列表 |
| POST | `/WMS/WMS_LabelTemplate/SaveForm` | 保存元数据和 `.btw` |
| POST | `/WMS/WMS_LabelTemplate/DeleteForm` | 删除模板及数据建模行 |
| POST | `/WMS/WMS_LabelTemplate/DataCheck` | OQC 确认/取消 |
| GET | `/WMS/WMS_LabelTemplate/GetPrintingDataList` | 读取 Named Value 建模 |
| POST | `/WMS/WMS_LabelTemplate/SubmitPrintingData` | 保存取值规则 |
| POST | `/WMS/WMS_LabelTemplate/GetBartenderPrintingPicture` | 普通模板图片预览 |
| POST | `/WMS/WMS_LabelTemplate/GetDeliveryPrintingPicture` | 客户标签批量预览 |
| POST | `/WMS/WMS_LabelTemplate/GetInnerPackingPrintingPicture` | 内包标签批量预览 |
| POST | `/WMS/WMS_LabelTemplate/ManualLabelPrinter` | 页面手工值输出到装箱线目录 |
| POST | `/WMS/WMS_LabelTemplate/SubmitInnerPackLabelPrint` | 内包标签正式输出 |
| POST | `/WMS/WMS_LabelTemplate/DownLoadLabelTemplate` | 下载数据库中的 `.btw` |
| GET | `/WMS/WMS_LabelTemplate/GetLabelFunctionList` | 数据建模函数候选项 |
| GET | `/WMS/WMS_LabelTemplate/GetLabelPrinterPathList` | 打印位置列表 |

### 8.2 料号参数相关

| 方法 | 路径 | 作用 |
| --- | --- | --- |
| GET | `/WMS/HS_LabelMapperMaint/GetPageList` | 当前模板下的料号参数 |
| GET | `/WMS/HS_LabelMapperMaint/GetLabelParamList` | 模板 Named Value 下拉项 |
| POST | `/WMS/HS_LabelMapperMaint/SaveForm` | 单条新增/修改 |
| POST | `/WMS/HS_LabelMapperMaint/DeleteForm` | 单条删除 |
| GET | `/WMS/WMS_LabelParamImport/GetPageList` | 全部导入参数列表 |
| POST | `/WMS/WMS_LabelParamImport/ExcelImport` | 解析 Excel 到预览数据 |
| POST | `/WMS/WMS_LabelParamImport/SaveExcelImportData` | 批量 upsert |

### 8.3 业务打印相关

| 方法 | 路径 | 作用 |
| --- | --- | --- |
| POST | `/WMS/WMS_DeliveryShip/SubmitDeliveryPrint` | 客户发货标签正式输出 |
| POST | `/WMS/WMS_LabelTemplate/SubmitInnerPackLabelPrint` | 内包标签正式输出 |

## 9. 关键行为与风险

### 9.1 维护一致性

- `.btw` 上传外部服务发生在数据库事务提交之前。如果外部服务已经替换模板，而后续数据库同步失败并回滚，外部模板与数据库可能短暂或长期不一致；这不是分布式事务。
- 编辑模板时如果不重新上传文件，是否会因通用 ORM `Update(entity)` 把未提交字段（尤其 `FileData`）覆盖为空，需要结合 Repository 实现或运行测试确认。
- 模板删除会删除 `WMS_LabelTemplate` 和 `WMS_PrintingDataMaint`，但没有看到同时清理 `HS_LabelMapperMaint`；可能形成料号参数孤儿数据，取决于数据库外键约束。
- 上传返回的 Named Values 通过逗号拆分；字段名若包含逗号会被错误拆分。

### 9.2 参数导入

- 导入存在性失败时在事务内部直接 `return`，没有显式 `Rollback()`；最终是否安全回滚依赖 Repository/连接释放行为。
- 非 `ExceptionEx` 异常分支也没有在重新抛出前显式回滚。
- 导入不验证参数名属于模板，Excel 拼写错误可能生成永远不会被打印逻辑使用的数据。
- 已有记录导入时不更新 `Description`。
- 页面宣称可编辑预览表，但列没有逐列配置 edit 控件，实际可编辑能力应以浏览器验证为准。

### 9.3 求值与打印

- `ValueType=3` 在 `manualData=null` 时会抛异常。客户发货打印调用时传入的是 `null`，所以客户模板不应配置手工类型，当前页面和保存接口却没有阻止。
- `ValueType=2` 把数据库中维护的 `FuncExpression` 作为存储过程名执行，正确性和权限完全依赖函数主档与数据库过程。
- `ValueType=1` 动态列名通过字符串拼入 SQL；页面候选是固定白名单，但后端没有再次校验白名单。
- OQC 只在客户发货正式打印中明确强制；内包打印和手工打印可以绕过。
- 正式打印先写文件，再以独立数据库调用写历史，两者不是同一原子事务；可能出现“文件已输出但历史未记”或历史记录异常被吞掉。
- 客户标签历史没有在主打印分支写 `LabelName`；内包分支会写。
- 内包历史的 `RequestValue` 来自 `model.Masterpack`，而内包流程中该值为 `null`，因此无法依靠该字段追踪具体内包行。
- 内包目录存在性校验被 `oo = true` 覆盖，是明确的失效校验。
- 文件名主要使用秒级时间；并发或快速重复提交存在文件名冲突/覆盖风险，尤其手工打印同模板同秒输出。
- API 返回“生成标签文件成功”只证明 WMS 完成文件写入，不等价于打印机已出纸。系统内没有看到监听确认或打印机回执闭环。

### 9.4 安全与可运维性

- 配置文件中存在打印共享目录访问凭据的明文配置；本文不记录具体值。应迁移到受保护配置，并立即审查现有凭据是否需要轮换。
- `GetPageList`、`getLabelTemplateManualData`、`GetProductLayeroutNum`、`HS_LabelMapperMaint` 多处把请求值直接拼进 SQL，存在 SQL 注入与特殊字符失败风险。
- 模板名称既用作数据库业务键，又进入文件路径与 BarTender 本地模板路径；只禁止中文不足以防止路径分隔符、相对路径和其他非法文件名字符。
- 外部 `BtwUpload/BtwExportImg` 的超时、重试、身份认证和审计策略在本次分析范围的代码中不清晰。

## 10. 推荐的维护操作顺序

1. 用 BarTender Designer 完成 `.btw`，确保每个动态内容都定义为清晰、稳定、无逗号的 Named Value。
2. 在模板维护中上传文件，确认外部上传成功。
3. 打开“数据建模”，逐字段维护说明及取值类型。
4. 对需要料号定制的字段，确认其函数存储过程确实读取 `HS_LabelMapperMaint`，再通过页面下半区或 Excel 导入维护值。
5. 使用普通预览或业务预览核对每个字段，不只核对版式。
6. 重新进行 OQC 确认。
7. 在业务打印页选择目标外箱/内包和打印位置，先小批量输出。
8. 同时核对打印目录任务文件、BarTender 日志、实物标签和 `WMS_PrinterLabelHistory`，不要只依据网页成功提示。

## 11. 推荐验收用例

### 11.1 模板维护

- 新增含 4 种 ValueType 的测试模板，核对 Named Values 是否完整同步。
- 替换 `.btw`：删除一个字段、新增一个字段、保留一个已配置字段，核对删除/新增/保留行为。
- 只改元数据不上传文件，核对数据库 `FileData` 是否仍完整。
- 数据建模保存后核对 OQC 自动重置。
- 删除模板后检查 `HS_LabelMapperMaint` 是否残留。

### 11.2 参数导入

- 同一逻辑键首次导入与二次覆盖。
- 不存在料号、不存在模板、错误参数名、空参数值、文件内重复键。
- 中间行失败时确认前序行没有被提交。
- 已有行备注变更时确认产品期望与当前“不更新备注”行为是否一致。

### 11.3 求值

- 类型 0 固定值。
- 类型 1 的七个允许列及缺失主数据。
- 类型 2 的每个实际存储过程，特别是 `HS_LabelMapperMaint` 命中/未命中场景。
- 类型 3 在内包流程有值、客户发货流程配置错误时的行为。
- `.btw` Named Value 与数据库 `NamedValue` 大小写及拼写一致性。

### 11.4 输出闭环

- 客户标签未 OQC 时应拒绝，确认后应输出。
- 内包/手工打印是否也应强制 OQC，先确认业务要求再整改。
- 打印目录不可访问、BarTender 服务停止、打印机离线、同秒重复任务。
- 核对文件内容编码为 UTF-8 无 BOM。
- 核对任务文件被消费后是否移动/删除、失败是否重试、是否可能重复打印。
- 核对 WMS 历史数量与 BarTender/打印机实际出纸数量。

## 12. 主要代码索引

### 12.1 前端

- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/Index.cshtml`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/Index.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/Form.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/LabelModule.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/ViewLabel.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/ViewInnerPackageLabel.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelTemplate/PrintForm.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelParamImport/Index.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_LabelParamImport/Form.js`
- `Learun.Application.Web/Areas/WMS/Views/HS_LabelMapperMaint/Form.js`
- `Learun.Application.Web/Areas/WMS/Views/WMS_DeliveryShip/ViewCusLabel.js`

### 12.2 Web Controller

- `Learun.Application.Web/Areas/WMS/Controllers/WMS_LabelTemplateController.cs`
- `Learun.Application.Web/Areas/WMS/Controllers/WMS_LabelParamImportController.cs`
- `Learun.Application.Web/Areas/WMS/Controllers/HS_LabelMapperMaintController.cs`
- `Learun.Application.Web/Areas/WMS/Controllers/WMS_DeliveryShipController.cs`

### 12.3 业务与数据层

- `Learun.Application.TwoDevelopment/WMS/WMS_LabelTemplate/WMS_LabelTemplateService.cs`
- `Learun.Application.TwoDevelopment/WMS/WMS_LabelParamImport/WMS_LabelParamImportService.cs`
- `Learun.Application.TwoDevelopment/WMS/HS_LabelMapperMaint/HS_LabelMapperMaintService.cs`
- `Learun.Application.TwoDevelopment/WMS/WMS_API/WMSService.cs`
- `Learun.Application.TwoDevelopment/WMS/WMS_DeliveryShip/WMS_DeliveryShipService.cs`
- `Learun.Application.TwoDevelopment/WMS/WMS_LabelTemplate/WMS_LabelTemplateEntity.cs`
- `Learun.Application.TwoDevelopment/WMS/WMS_LabelTemplate/WMS_PrintingDataMaintEntity.cs`
- `Learun.Application.TwoDevelopment/WMS/HS_LabelMapperMaint/HS_LabelMapperMaintEntity.cs`
- `Learun.Application.TwoDevelopment/WMS/WMS_PrinterLabel/WMS_PrinterLabelHistoryEntity.cs`

## 13. 分析边界与待确认项

本次没有把以下内容当成已验证事实：

- `BtwUpload` 和 `BtwExportImg` 的真实 URL、服务实现、部署位置及当前可用性；
- BarTender 目录监听程序的实现、轮询规则、成功/失败处理和重复打印保护；
- `HS_LabelFunction` 当前生产数据及其存储过程定义；
- `HS_LabelMapperMaint` 在数据库函数中的实际优先级；
- 真实数据库约束、索引、触发器以及孤儿数据清理规则；
- 任一真实模板的最终打印效果。

因此，本文已经完整说明代码中的维护和输出链路，但若要将其迁移为新 WMS 的正式规格，仍应先完成上述只读数据库与打印侧证据采集。

## 14. `BtwUpload` / `BtwExportImg` 调用链深挖

### 14.1 可以由当前源码确认的事实

在指定的 `Learun.Application.TwoDevelopment/WMS` 范围内，`BtwUpload` 不是 C# 方法名，也不是本工程的 Controller 路由，而是数据库接口配置键。

地址解析链路为：

```text
WMS_LabelTemplateService.SaveEntity
  -> new WMS_InterfaceConfigBLL().GetAddressByKey("BtwUpload")
  -> WMS_InterfaceConfigService.GetAddressByKey
  -> 查询 WMS_InterfaceConfig.InterfaceKey == "BtwUpload"
  -> 返回 WMS_InterfaceConfig.InterfaceAddress
```

模板上传调用为：

```text
WMS_LabelTemplateService.SaveEntity
  -> 构造 { Name = entity.Name, Content = entity.FileData }
  -> JsonConvert.SerializeObject
  -> NetHelper_WMS.RestSharpPost
  -> HTTP POST 到 BtwUpload 配置地址
  -> 反序列化为 BartenderBaseResult
```

`NetHelper_WMS.RestSharpPost` 的实际行为是：

- RestSharp POST；
- `Content-Type: application/json`；
- 请求体是上述 JSON；
- Timeout 设置为无限等待；
- 添加 Basic Authorization；但此处使用 `new NetHelper_WMS("")`，构造函数没有为该类型设置账号/密码，因此生成的是空账号密码的 Basic 值；
- 调用方没有先检查 HTTP 状态、网络错误或空响应，直接读取 `response.Content`。

响应契约为：

```json
{
  "success": true,
  "code": "...",
  "info": "...",
  "data": "PN,Qty,CustomerPN"
}
```

其中 `data` 被 `ToString().Split(',')` 解析为字段名列表。这能够证明外部服务返回的是 `.btw` 中识别出的字段，但不能证明外部服务内部使用了哪个 SDK 类。

### 14.2 当前工程没有 BarTender SDK 的证据

`Learun.Application.TwoDevelopment.csproj` 中可见 RestSharp 引用和标签相关源码引用，但没有：

- `Seagull.BarTender.Print`；
- `BarTender.Print`；
- `Engine` / `LabelFormatDocument`；
- BarTender SDK NuGet/package 引用。

指定 WMS 源码范围内也没有 `Seagull`、`LabelFormatDocument`、`NamedSubStrings` 等 SDK 调用。因此链路在 HTTP 边界处结束：

```text
本仓库能够证明                        本仓库无法证明
-------------------------------      -------------------------------
WMS读取接口地址                       BtwUpload服务部署在哪台机器
WMS发送模板名和二进制                 服务端使用哪个BarTender版本
WMS接收字段名字符串                   是否使用.NET SDK/COM/Automation
WMS同步WMS_PrintingDataMaint          模板保存目录和覆盖策略
                                     字段枚举的具体SDK方法
```

### 14.3 `BtwExportImg` 与上传服务的关系

预览使用另一个接口配置键 `BtwExportImg`。三条预览入口最终都使用相同请求形态：

```json
{
  "Name": "CustomerLabel.btw",
  "Params": {
    "PN": "04S880351A",
    "Qty": "100"
  }
}
```

区别仅在于 `Params` 如何生成：

- 普通模板预览：使用页面直接填写的 `DataValue`；
- 客户发货预览：先调用 `generateDynamicLabelData`，使用交货单、产品和外箱上下文；
- 内包预览：先结合手工数据和分包数量调用 `generateDynamicLabelData`。

外部服务返回 `BartenderBaseResult.data`，前端将其作为 Base64 JPEG 显示。当前源码没有在 WMS 进程中打开 `.btw` 或渲染图片，因此渲染同样发生在外部服务。

### 14.4 `HS_LabelMapperMaint` 链路断点

对指定 WMS 目录的全部 C# 引用检查结果是：

- `WMS_LabelParamImportService` 新增/更新 `HS_LabelMapperMaint`；
- `HS_LabelMapperMaintService` 提供列表、单条维护和删除；
- `generateDynamicLabelData` 不查询 `HS_LabelMapperMaint`；
- 其他标签输出代码也没有直接查询 `HS_LabelMapperMaint`。

`generateDynamicLabelData` 在 `ValueType=2` 时只执行 `FuncExpression` 指向的数据库存储过程，并传入：

```text
@namedvalue
@delivery
@product
@masterpack
@resultData OUTPUT
```

所以料号定制参数能否参与最终标签，只能在存储过程层面确认。当前代码只能支持如下条件性结论：如果被配置的存储过程查询了 `HS_LabelMapperMaint`，导入值才会进入最终 Named Values；否则这张表只是被维护但没有被该打印链路消费。

### 14.5 复刻建议

复刻时建议保留“WMS 与 BarTender 服务分离”的边界：

```text
WMS（业务服务器）
  -> 管理模板元数据、版本、字段规则、OQC
  -> 计算业务值
  -> 调用受控HTTP接口

BarTender Adapter（Windows打印服务器）
  -> 安装与现场版本匹配的BarTender和官方SDK
  -> 打开.btw
  -> 枚举Named Data Sources
  -> 设置Named Values
  -> 导出预览图
  -> 可选：直接打印
```

建议 Adapter 至少提供：

1. `POST /templates/inspect`：接收 `.btw`，返回结构化 `namedValues[]`、文件 SHA-256 和模板兼容性信息；
2. `POST /templates/publish`：原子发布到正式模板目录；
3. `POST /previews`：接收模板版本与 Named Values，返回 PNG；
4. 可选 `POST /print-jobs`：若不继续使用 Integration Builder 目录监听，则由 Adapter 直接提交打印任务。

不要复制旧接口中的无限超时、空 Basic 凭据、逗号分隔字段和“外部模板已覆盖但数据库事务可能回滚”的行为。更安全的顺序是：临时上传并检查 -> WMS保存模板版本和字段差异 -> OQC发布 -> Adapter原子切换正式版本。

### 14.6 找到外部实现所需的下一份证据

要准确回答旧 `BtwUpload` 使用的是 SDK、COM 还是其他组件，最短路径是：

1. 只读查询 `WMS_InterfaceConfig` 中 `BtwUpload`、`BtwExportImg` 的 `InterfaceAddress`；
2. 根据主机和端口定位 IIS站点、Windows Service 或独立程序；
3. 检查部署目录的 `web.config`、`.csproj`、`packages.config` 或 DLL 引用；
4. 搜索 `Seagull.BarTender.Print.dll`、`BarTender.Print.dll`、`LabelFormatDocument`、`Engine.Documents.Open`；
5. 核对安装的 BarTender 版本、授权能力和服务运行账号。

后续已在 `D:\Code\WMS-HS\SwaggerWebApi` 找到该外部服务源码。第 16 节给出源码级结论，并取代本节关于“SDK实现尚未确认”的阶段性结论。

## 15. X02 标签数据建模与内部 BarTender API 实证

### 15.1 截图字段与数据库字段对应关系

截图标题为 `[X02.btw]标签数据建模`，每一行就是 `WMS_PrintingDataMaint` 中一个 Named Value：

| 页面列 | 数据库字段 | 含义 |
| --- | --- | --- |
| 数据名称 | `NamedValue` | `.btw` 中的 Named Data Source 名称 |
| 数据描述 | `NamedNotes` | 给维护人员看的说明，不参与求值 |
| 使用类型 | `ValueType` | 决定运行时使用哪条求值分支 |
| 默认值 | `DefaultValue` | 类型 0 使用 |
| 可选值 | `DynamicExpression` | 类型 1 使用，实际是产品主数据列选择器 |
| 函数 | `FuncExpression` | 类型 2 使用，实际保存数据库存储过程名 |
| 更新人 | `LastEditUser` | 最近保存账号 |
| 更新时间 | `LastEditDate` | 最近保存时间 |

截图中的 X02 配置可还原为：

| 数据名称 | 类型 | 页面显示的来源 |
| --- | --- | --- |
| `banben` | 函数 | X02截取客户型号后3位 |
| `shuliang` | 函数 | 成品外箱PCS数量 |
| `zhouqi` | 函数 | 成品外箱周期 |
| `2` | 默认值 | 截图中默认值为空 |
| `changneiliaohao`（截图被截断） | 可选值 | 成品料号 |
| `kehuxinghao`（截图被截断） | 可选值 | 客户型号 |
| `3` | 默认值 | 截图中默认值为空 |
| `shengchanriqi`（截图被截断） | 函数 | X02获取周期日期 |
| `1` | 默认值 | 截图中默认值为空 |
| `liushui` | 函数 | X02获取10位流水号 |

页面中的 `1/2/3` 不是 ValueType 编号，而是 `.btw` 自己定义的 Named Value 名称。这样的名称技术上可以工作，但可读性较差；复刻时建议使用有业务含义的英文名。

### 15.2 X02.btw 字段读取实证

2026-09-08 对用户提供的内部接口执行了只读调用：

```http
POST http://10.8.10.2:8888/api/BarTender/LoadFileds?fileName=X02.btw
```

服务返回成功，字段集合为：

```text
kehuxinghao
banben
shengchanriqi
zhouqi
shuliang
3
2
1
changneiliaohao
liushui
```

该结果与截图的十行完全对应，证明建模行确实源自 `X02.btw` 的 Named Data Sources，而不是维护人员在 WMS 中手工创建的任意字段。

字段顺序与截图顺序不同，说明页面/数据库不依赖 `.btw` 返回顺序；字段关联依赖名称精确匹配。

### 15.3 “默认值”如何设置和运行

页面选择“默认值”时保存：

```text
ValueType = 0
DefaultValue = 页面输入内容
```

打印求值直接执行：

```csharp
namedValueDatas.Add(
    new KeyValuePair<string, string>(item.NamedValue, item.DefaultValue));
```

因此它是模板级固定值，不根据料号、交货单或外箱变化。截图中的 `1/2/3` 虽配置为默认值，但默认值列看起来为空；实际输出将是空值，除非数据库中存在截图未显示出的内容。

适用示例：固定厂别、固定国家、固定标签版本文案。不适合数量、周期、流水号等业务动态字段。

### 15.4 “可选值”如何设置和运行

旧页面名称“可选值”容易误导。它不是让用户维护任意候选列表，而是从硬编码白名单中选择一个产品主数据字段：

```text
ProductCode   成品料号
UnitArea      单元面积
UnitWeight    单元重量
CustomerPN    客户料号
CustomerModel 客户型号
BaseUom       单位
Layer         层数
```

保存后：

```text
ValueType = 1
DynamicExpression = 所选字段名
```

打印时按当前 `DeliveryLabelModel.Product` 查询：

```sql
SELECT TOP 1 ls.{DynamicExpression}
FROM (
    SELECT p.ProductCode,
           p.BaseUom,
           p.UnitArea,
           p.UnitWeight,
           pp.CustomerPN,
           pp.CustomerModel,
           pp.Layer
    FROM HS_Product p
    LEFT JOIN HS_ProductProperty pp ON pp.prodno = p.ProductCode
    WHERE p.ProductCode = @PN
) ls;
```

X02 示例：

```text
changneiliaohao -> ProductCode
kehuxinghao     -> CustomerModel
```

所以同一个 X02 模板打印不同料号时，这两个字段会随产品主数据变化。

### 15.5 “函数”如何设置

数据建模页面初始化时调用：

```text
GET /WMS/WMS_LabelTemplate/GetLabelFunctionList
```

Service 读取全部 `HS_LabelFunctionEntity`，页面将：

```text
下拉值 id   = FuncName
下拉显示 text = FuncNotes
```

因此截图函数列看到的“X02截取客户型号后3位”“成品外箱PCS数量”等是 `FuncNotes`，用于展示；真正保存在 `WMS_PrintingDataMaint.FuncExpression` 中的是对应的 `FuncName`，即存储过程名。

提交保存后：

```text
ValueType = 2
FuncExpression = HS_LabelFunction.FuncName
```

### 15.6 “函数”运行时如何实现

打印时 `generateDynamicLabelData` 使用 Dapper 动态参数调用：

```text
ExecuteByProc(FuncExpression, parameters)
```

每个函数必须遵循统一签名：

```sql
@namedvalue  NVARCHAR/... INPUT,
@delivery    NVARCHAR/... INPUT,
@product     NVARCHAR/... INPUT,
@masterpack  NVARCHAR/... INPUT,
@resultData  NVARCHAR(200) OUTPUT
```

上下文含义：

| 参数 | 来源 | X02 可用于 |
| --- | --- | --- |
| `@namedvalue` | 当前 `.btw` 字段名 | 一个通用过程按字段分支 |
| `@delivery` | 当前交货单号 | PO、客户、出货日期等 |
| `@product` | 当前成品料号 | 客户型号、厂内料号等 |
| `@masterpack` | 当前外箱号 | 外箱数量、周期、流水号等 |
| `@resultData` | 存储过程输出 | 写入最终 Named Value |

X02 函数的页面说明可以推测其业务意图，但当前仓库没有 `HS_LabelFunction` 实际数据和存储过程定义，所以不能仅凭说明还原 SQL。例如“X02截取客户型号后3位”可能读取 `HS_ProductProperty.CustomerModel` 后取末三位，也可能使用客户定制表；“X02获取10位流水号”是否会更新序列表尤其必须读取过程定义，不能按纯查询函数处理。

### 15.7 “手动”如何设置和运行

页面选择“手动”时保存 `ValueType=3`。运行时从调用方传入的 `manualData` 按 `NamedValue` 查找并输出。

当前主要由内包标签页面使用：页面列出所有手动字段，用户输入后提交；`Qty` 会被分包计算结果覆盖。客户发货打印传入 `manualData=null`，因此客户模板配置手动类型会导致运行异常，旧页面没有提前阻止。

### 15.8 四类配置的完整决策表

| ValueType | 页面名称 | 配置字段 | 是否随料号变化 | 是否随业务单/外箱变化 | 执行位置 |
| ---: | --- | --- | --- | --- | --- |
| 0 | 默认值 | `DefaultValue` | 否 | 否 | C# 直接取值 |
| 1 | 可选值 | `DynamicExpression` | 是 | 间接，仅通过当前料号 | C# 拼查询、数据库读取主数据 |
| 2 | 函数 | `FuncExpression` | 由过程决定 | 由过程决定 | SQL Server 存储过程 |
| 3 | 手动 | 本次请求 `manualData` | 由用户输入决定 | 是 | 页面输入 + C# 名称匹配 |

每个 Named Value 独立配置类型，所以一个 `.btw` 可以同时混用四种方式；X02 正是“函数 + 可选值 + 默认值”的混合模板。

### 15.9 两个 8888 接口是不是自己实现的

对 `http://10.8.10.2:8888/swagger/docs/v1` 进行了只读检查，Swagger 返回：

```text
Swagger 2.0
title: WebApi测试文档
host: 10.8.10.2:8888
```

公开的 BarTender Controller 路由包括：

| 路由 | 方法 | Swagger说明 | 请求模型 |
| --- | --- | --- | --- |
| `/api/BarTender/LoadFileds` | POST | 读取btw文件所有变量名 | query `fileName` |
| `/api/BarTender/ExportImgStream` | POST | 导出btw文件图片的二进制流 | `DataModel` |
| `/api/BarTender/ExportBtwStream` | POST | 导出btw文件的二进制流 | query `fileName` |
| `/api/BarTender/UploadFile` | POST | 根据文件返回文件二进制流 | `FileModel` |
| `/api/BarTender/CreateFile` | POST | Swagger未写说明 | `PostData` |

模型与 WMS 调用完全吻合：

```text
FileModel: Name + Content(byte/base64)
DataModel: Name + Params(object)
BaseResult: success + code + info + data
```

可以据此作出高可信结论：这两个 URL 是项目方自行封装的 ASP.NET Web API，不是 BarTender 产品原生规定的固定 REST URL。依据包括自定义控制器名和动作名、内部 IP/端口、测试文档标题、与 WMS 自定义 `BartenderBaseResult` 契约完全一致，以及同一控制器下存在项目特有的 `CreateFile`。

该阶段仅凭 Swagger 只能证明“外层 API 是自建的”。后续已在 `D:\Code\WMS-HS\SwaggerWebApi` 找到服务端源码，确认其内部使用 `Interop.BarTender.dll` 的 COM Automation；详见第 16 节。

### 15.10 当前建模链路

```text
X02.btw
  -> 自建 BarTender API 保存模板
  -> 自建 API 读取 Named Data Sources
  -> 返回十个字段名
  -> WMS 同步 WMS_PrintingDataMaint
  -> 用户逐字段选择：默认值/产品字段/存储过程/手工值
  -> 保存 FuncExpression 等配置并清除 OQC
  -> 打印时 generateDynamicLabelData 逐字段执行
  -> 生成 NamedValues
  -> 预览：自建 ExportImgStream
  -> 正式打印：写 BarTender 任务文本到监听目录
```

## 16. SwaggerWebApi 服务端源码确认

### 16.1 最终技术结论

`D:\Code\WMS-HS\SwaggerWebApi` 就是运行在 `10.8.10.2:8888` 的 BarTender 适配服务源码。它不是 Seagull 新式 `.NET Print SDK` 封装，而是引用：

```text
Interop.BarTender.dll
Assembly Version: 11.2.1.1
Namespace: BarTender
```

并通过 COM Automation 对象工作：

```csharp
new BarTender.Application()
btApp.Formats.Open(...)
btFormat.NamedSubStrings
btFormat.SetNamedSubStringValue(...)
btFormat.ExportToFile(...)
```

项目为 ASP.NET Web API、.NET Framework 4.7.2、AnyCPU，主要源码是：

- `SwaggerWebApi/WebAPI/Controllers/BarTenderController.cs`
- `SwaggerWebApi/WebAPI/Common/BarTenderHelper.cs`
- `SwaggerWebApi/WebAPI/Models/FileModel.cs`
- `SwaggerWebApi/WebAPI/Models/DataModel.cs`
- `SwaggerWebApi/WebAPI/Web.config`
- `SwaggerWebApi/WebAPI/WebAPI.csproj`

### 16.2 UploadFile 的真实实现

Controller：

```text
POST /api/BarTender/UploadFile
  -> BarTenderController.UploadFile(FileModel)
  -> BarTenderHelper.CreateBtw(data.Name, data.Content)
```

`FileModel` 为：

```text
Name    string
Content byte[]
```

`CreateBtw` 的执行顺序：

1. 如果文件名不含斜杠，拼接 `Web.config` 的 `Template_Path`，当前源码配置为 `C:\LabelTemplates\`；
2. 如果同名模板已经存在，先删除；
3. 将请求中的二进制写成 `.btw` 文件；
4. 立即调用 `LoadAllFiled(fullName)`；
5. 打开刚保存的模板并返回所有 Named SubStrings，逗号拼接。

因此原 WMS 所谓 `BtwUpload` 实际同时完成了：

```text
覆盖发布模板 + COM打开模板 + 提取字段
```

它没有模板版本、哈希、备份或原子切换。同名上传会直接删除旧文件再创建新文件。

### 16.3 .btw 字段如何解析

真实代码为：

```text
BarTender.Application
  -> Formats.Open(fullName, false, "")
  -> Format.NamedSubStrings.Count
  -> NamedSubStrings.GetSubString(i)
  -> SubString.Name
```

索引从 1 开始遍历，名称重复时只保留一次，最终使用逗号连接。

这证明旧系统解析的准确对象是 BarTender `NamedSubStrings`。WMS 中的 `NamedValue` 就是 `SubString.Name`。

### 16.4 ExportImgStream 的真实实现

Controller：

```text
POST /api/BarTender/ExportImgStream
  -> BarTenderController.ExportImgStream(DataModel)
  -> BarTenderHelper.ExportImgStream(model.Name, model.Params)
```

`DataModel` 为：

```text
Name   string
Params Hashtable
```

Helper 的执行顺序：

1. 拼接 `Template_Path` 并检查 `.btw` 存在；
2. `btApp.Formats.Open` 打开模板；
3. 再次枚举模板中的全部 `NamedSubStrings`；
4. 只有请求 `Hashtable` 中存在的同名字段才调用 `SetNamedSubStringValue`；
5. 生成临时 JPG 文件名；
6. 调用 `Format.ExportToFile`，格式为 JPG、24-bit color、printer resolution；
7. 关闭模板并退出 BarTender Application；
8. 读取 JPG 为 `byte[]`；
9. 删除临时 JPG；
10. Web API JSON序列化 `byte[]`，WMS前端把它作为 Base64 图片显示。

因此预览确实由 BarTender COM Automation 打开并渲染 `.btw`，不是 WMS 或浏览器自行绘制。

### 16.5 其他接口

| 接口 | 实现 |
| --- | --- |
| `LoadFileds` | 打开服务器现有 `.btw`，返回 Named SubStrings |
| `ExportBtwStream` | 直接读取模板文件并返回 `byte[]`，不调用 BarTender引擎 |
| `CreateFile` | 向 `TemplateLabel_Path` 写入 `.txt` 打印任务，不直接调用打印机 |

### 16.6 资源与并发风险

这份实现可以解释旧系统如何工作，但不适合原样复制：

- `_btApp` 和 `btFormat` 都是静态字段。多个 ASP.NET 请求可能互相覆盖同一个 Format，存在并发串值、关闭错对象和 COM 状态竞争风险。
- `LoadAllFiled`、`ExportImgStream` 正常路径会 `Close` 和 `Quit`，异常路径没有 `finally`，可能残留 `bartend.exe` 或 COM对象。
- 调用 `btApp.Quit` 后没有把 `_btApp=null`；下一请求可能继续使用已经退出的 COM代理。
- 没有显式释放 COM对象，例如 `Marshal.FinalReleaseComObject`。
- `UploadFile` 先删除正式文件再创建；写入或解析失败时旧模板已经丢失。
- 允许传入包含 `/` 或 `\` 的路径，可能绕过 `Template_Path`；Controller只校验非空，没有做根目录约束、扩展名或文件名校验。
- 图片临时文件使用时间戳命名，缺少真正的随机任务 ID；文件流/`BinaryReader` 没有 `using` 释放，并在文件仍打开时执行删除，行为依赖 Windows共享删除语义。
- `ExportToFile` 使用 `btSaveChanges`，随后又以 `btDoNotSaveChanges` 关闭；是否会改变模板状态应做真实版本测试。
- WebApi全局 Basic授权过滤器被注释，Controller也没有授权特性；从源码看这些模板上传和文件创建接口没有应用级身份校验。
- `CreateFile` 使用秒级文件名，可能碰撞；返回“完成”只表示文本文件已写入，不代表打印机出纸。
- 项目为 AnyCPU，而 COM注册位数取决于现场 BarTender 安装；部署时必须明确 IIS应用池32/64位设置。

### 16.7 复刻时的建议

如果必须兼容同一版本 BarTender，可继续使用 `Interop.BarTender`，但应把它放在专用 Windows进程中，单线程串行管理 COM生命周期，而不是在多线程 IIS请求里共享静态对象。

更稳妥的适配器行为是：

```text
每个任务进入有界队列
  -> 专用STA工作线程创建/复用Application
  -> 打开单独Format
  -> try/finally关闭Format
  -> 必要时释放COM代理
  -> 返回结构化字段数组或PNG
```

模板发布应改为：临时文件写入 -> SDK打开验证 -> 计算 SHA-256 -> 保存版本 -> 原子替换当前版本。接口还应加入认证、请求大小限制、文件名白名单、根目录约束、超时和审计。
