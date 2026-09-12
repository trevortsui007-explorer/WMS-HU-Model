# DT 标签正式打印链路

## 1. 实施范围

本阶段在 DT 标签模板维护模块中补齐正式发送链路。WebApi 在最终发送时计算 Named Values，将数据库中的 BTW 发布到配置目录，并向旧监听程序的任务目录写入 UTF-8 with BOM 的 BarTender Print Command Script TXT。它不直接调用 BarTender SDK 打印，也没有打印机出纸回执，因此 `SENT` 只表示任务文件已发送。

数据库迁移候选为 `D:\com.delton.gz\Learun.Application.WebApi\Modules\WMS\DT_LabelTemplate\Sql\002_DT_LabelPrinting.sql`。脚本已登记到工程，但未执行。

## 2. 数据模型

`DT_LabelPrinter` 一行绑定一个用户可选打印机及其路由：显示名称、物理打印机名称、TXT 任务目录、BTW 模板目录、启用/删除状态及审计字段。删除为软删除；未删除显示名称由 filtered unique index 保证唯一。

`DT_LabelPrintJob` 保存不可变快照：客户端 RequestId、模板版本与 SHA-256、打印机和目录、业务输入、最终字段 JSON、完整 TXT、任务文件名、状态和错误。历史没有到模板或打印机配置的级联外键，因此删除配置或模板不会删除历史。

状态含义：

- `PENDING`：已登记请求，正在求值或写文件。
- `SENT`：TXT 已原子改名为正式 `.txt`，监听程序可以读取。
- `FAILED`：求值、目录、BTW 发布、TXT 写入或历史更新链路发生异常。

## 3. 字段求值顺序

对模板字段按 `SortNo` 计算：

1. 先查询 `(TemplateId, ProductCode, NamedValue)` 的 `DT_LabelMaterialValue`；存在时覆盖字段类型。
2. 类型 0 读取 `DefaultValue`。
3. 类型 1 只允许 `ProductCode/CustomerPN/CustomerModel/BaseUom/UnitArea/UnitWeight/Layer`，使用固定参数化 SQL 查询 `HS_Product` 与 `HS_ProductProperty`。
4. 类型 2 根据 `DT_LabelFunction` 找到启用且真实存在的存储过程，以 `CommandType.StoredProcedure` 调用 `@namedvalue/@delivery/@product/@masterpack/@resultData OUTPUT`。
5. 类型 3 从请求 `ManualValues` 按 Named Value 获取；请求没有该键时报错。

最终集合必须覆盖全部字段，Named Value 不得重复，每个字段必须有唯一的 `BtwDatabaseFieldName`。数据库字段名或值不得包含竖线和换行符。函数只在正式发送时调用；测试预览仍只注入人工测试值。

## 4. 文件发布

BTW 目标为 `TemplateDirectory + 数据库模板文件名`。目标 SHA-256 相同时跳过；否则先写同目录临时文件并复核 SHA-256，再使用同卷原子替换/改名。

TXT 使用 CRLF、UTF-8 with BOM，先写 `{文件基本名}.tmp`，完成后改名为：

```text
{模板基本名}_{yyyyMMddHHmmssfff}_{JobId无横线}.txt
```

内容示例：

```text
%BTW% /AF="D:\Templates\X02.btw" /D="" /PRN="TSC TE4503" /R=3 /P /C=1
%END%
FIELD1|FIELD2|FIELD3
ABC|120|2636
```

`/AF` 使用发布后的 BTW 完整路径，`/PRN` 使用物理打印机名称，`/D=""` 按现有监听配置固定。表头按 `BtwDatabaseFieldName` 自然序输出，数据行与之逐列对应；编号断档不补列。

`PrinterName` 仅供页面显示和历史追溯，监听路由仍由 `TaskDirectory` 决定，没有向旧协议增加未知打印机字段。

## 5. API

统一前缀为 `/api/wms-fg/label-templates`：

- `GET /printers`：普通用户可读取启用打印机，响应不含路径。
- `POST /{id}/print`：校验 OQC、幂等 RequestId、计算字段并发送一份任务。
- `GET /printer-configs`、`POST /printer-configs/save`、`POST /printer-configs/{id}/delete`、`POST /printer-configs/{id}/test`：仅 System。
- `GET /print-jobs`、`GET /print-jobs/{id}`：仅 System，查看历史与原始证据。

管理权限从请求上下文的 Learun `account` Header 取得工号，严格按 `System` 忽略大小写判断；Body 不接收操作人工号。全部接口使用 `BaseApi` 已有白名单前缀 `/api/wms-fg`。重复 RequestId 返回现有任务，不再次写 TXT。该 Header 不是密码学身份凭据，正式上线前应由反向代理/MVC 服务端覆盖该 Header，或统一接入可验证的 WebApi token；不能仅依赖前端隐藏按钮作为安全边界。

## 6. 页面行为

模板摘要区新增“打印”：只有已选择模板、已 OQC 且存在启用打印机时可用。弹窗收集打印机、必填料号、可选交货单/外箱号和类型 3 字段。成功提示使用“打印任务已发送”，并显示文件名与 JobId。

System 用户可见“打印配置”，报表弹窗含：

- 打印机配置：新增、编辑、启停、软删除、两个目录的临时探针写入/删除测试。
- 打印历史：日期、状态、料号、外箱号、发起人筛选，以及最终字段 JSON、TXT 原文和错误详情。

## 7. 部署与验收顺序

1. 人工备份并审批后执行 `002_DT_LabelPrinting.sql`；本次实现没有执行它。
2. 以 System 登录，配置专用测试任务目录和 BTW 目录，先执行“测试路径”。
3. 确认测试模板 OQC，分别验证默认、动态、函数、手工及料号覆盖。
4. 检查 BTW SHA-256、TXT CRLF/UTF-8 BOM、数据库字段自然序及文件名。
5. 使用同一 RequestId 重放，确认只产生一个 TXT。
6. 最后由人工同时核对 TXT、监听程序日志和实物标签。

当前未验证项：迁移执行、真实数据库/存储过程、网络共享权限、监听程序、浏览器运行态和物理打印。
