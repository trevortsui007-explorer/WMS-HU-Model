# MES 成品包装 / 入库链路追踪

> 追踪日期：2026-08-18  
> MES 代码基线：`D:\com.delton.gz`  
> 约束：本次仅作静态、只读分析；未执行 MES/WMS 数据库查询、存储过程或任何写操作。  
> 证据标签：**FACT** = 代码或用户提供样本直接证明；**INFERENCE** = 由多个事实推出；**OPEN QUESTION** = 缺少 SP 定义、运行配置或运行数据，不能确认。

## 1. Executive Summary

- **FACT** BoxMore 是一条“人工输入/扫描周码 + 包装数量 → `PK_CreatePackBox` → 自动外箱打印”的现场路径。前端将 `DC1/DC2/DC3`、对应生产日期和数量传给 `CreatePackBox`；Service 将其原样传为 `@sDC1/@sDC2/@sDC3` 等参数。[BoxMore.js:116-138, 756-790](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxMore.js)、[PKBoxController.cs:442-475](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)、[PKBoxService.cs:880-944](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)
- **FACT** BoxStore 的“入库 SAP”按箱调用 SAP 包装信息接口；每箱返回成功后写 `PK_Box.WebApi` 与 `WebApiTime`。只有当前 `PK_PlanStock_Id` 下没有未成功箱时，才把 `PK_Box.status` 和 `PK_PlanStock.status` 写为“入库”，并写 `Stockby/Stockdt`。[BoxStore.js:152-237](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxStore.js)、[PKBoxController.cs:1082-1143](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)、[PKBoxService.cs:113-117, 1292-1325](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)
- **FACT** `PK_Packmaster.datecode` 被送入 SAP 载荷字段 `ZZM`；旧的 `PostPackInfoApiNew` 还把其解析成整数并拼为 SAP 批次 `CHARG`。因此 DateCode 不能被当作可随意格式化的显示字段。[ApiCall.cs:3417-3467](D:/com.delton.gz/Learun.Application.Web/Api/ApiCall.cs)
- **OPEN QUESTION** `PK_CreatePackBox`、`PK_BoxPrintTY`、`pr_pk_box_master_sp`、`PK_SplitBox`、`PK_SplitBoxPack` 的定义未随代码副本提供。不能确认三表实际 INSERT/UPDATE 顺序、`PrintInfo` 拼接、`DateCodeQty` 计算、最终打印列集合、或拆箱对已有关系的精确更新。

## 2. 完整调用链

### 2.1 BoxMore 装箱

1. **FACT** 操作员选择 WIP/订单，输入每个 DateCode 段的“满包包数、每包数、尾数、X、周期、生产日期”。`DC1` 回车时调用 `/MES/PKBox/GetDataRangeRuler`，该控制器从 `PkPackMasterService.GetDataRangeByProdno` 取得规则并返回日期；页面也显示 `/MES/Pack/GetDataRangeRuler` 返回的“标准周期格式”。[BoxMore.js:116-138, 1066-1079](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxMore.js)、[PkPackMasterService.cs:726-735](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PkPackMaster/PkPackMasterService.cs)
2. **FACT** 规则读取位置是 `md_flow` → `md_flowstation` → `md_stationattvalue.attvalue`，按 `prodno + prodrev` 取一条记录；这正是 MES 中人工维护的格式配置读取证据，而不是设备实际打印结果回读证据。[PkPackMasterService.cs:726-735](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PkPackMaster/PkPackMasterService.cs)
3. **FACT** 提交时 `BoxMore.js` 组装 `PK_BoxEntity` 并 POST `/MES/PKBox/CreatePackBox`。控制器反序列化后调用 BLL；成功且取得输出箱组号 `msg.str` 时立即调用 `PrintBoxTY(msg.str, "", "", plant)`。[BoxMore.js:756-831](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxMore.js)、[PKBoxController.cs:442-475](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)
4. **FACT** `PKBoxService.CreatePackBox` 调用 `PK_CreatePackBox`，传入厂别、料号、整/尾箱、返工、总 SET、箱数、单重、三组 `DC/Date/PackQty/QtyPerPack/QtyRem/QtyX`、订单项、包规、操作人和 `other1..3`；输出 `@sMsg` 与 `@iBoxGroupID`。[PKBoxService.cs:880-944](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)
5. **OPEN QUESTION** SP 内部是否创建 `PK_Packmaster`、`PK_Box`、`PK_PlanStock`，及三者的写入顺序、事务边界、`datecode` 与 `PrintInfo` 派生规则，必须以 `PK_CreatePackBox` 定义确认。

### 2.2 BoxStore 入库 SAP

1. **FACT** 页面展示 `PK_PlanStock` 和关联箱；界面把 `WebApi=true` 显示为“已入SAP”，`false` 显示为“预入库”。[BoxStore.js:392-435](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxStore.js)
2. **FACT** 用户触发后，控制器先以 `CheckPlantStatus(PK_PlanStock_Id)` 要求“预入库”，并以 `GetBoxID` 验证所选箱属于该批次。随后每箱查 `WebApi`，未成功时调用 `ApiCall.PostPackInfoApi(boxId)`，立即按 SAP 返回码更新 `WebApi/WebApiTime`。[PKBoxController.cs:1082-1124](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)、[PKBoxService.cs:113-117, 1249-1255](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)
3. **FACT** 所有箱均已 `WebApi=1` 后，旧路径还调用 `PostBoxtoSAP(PK_PlanStock_Id)`；成功后两条路径均调用 `PKBoxService.BoxToSAP`。该方法写 `PK_Box.status='入库'`，并将该 `PK_PlanStock` 写为“入库”、`Stockby=@userID`、`Stockdt=GETDATE()`。[PKBoxController.cs:1125-1137](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)、[PKBoxService.cs:1313-1325](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)
4. **FACT** 当前一般入口调用 `BoxToSAP`，而代码还保留 `BoxToSAPNew` 与 `BoxToSAP_OLD*`。`BoxToSAPNew` 改调 `PostPackInfoApiNew`，但 BoxStore.js 当前 URL 指向 `BoxToSAP`；因此不能假设现场在用 New 版本。[BoxStore.js:209-218](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxStore.js)、[PKBoxController.cs:1162-1215](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)

## 3. 三表 ER 关系与验证 SQL

| 关系 | 结论 | 证据 |
|---|---|---|
| `PK_PlanStock → PK_Box` | **FACT** 代码以 `PK_Box.PK_PlanStock_Id = @PK_PlanStock_Id` 更新和检查箱状态；用户样本也显示该字段为 GUID。 | `PKBoxService.BoxToSAP`、`CheckPlantSendStatus` |
| `PK_Box → PK_Packmaster` | **FACT（字段层）** `PK_Packmaster.boxID` 与 `PK_Box.boxID` 存在，样本两者均为数值业务号；装箱页/打印/SP 必须以此关联。 | 用户提供的三表字段样本；`PkPackMasterService` 查询字段 |
| Join 的 `plantCode` | **INFERENCE** `boxID` 是数值且代码查询常未加厂别；跨厂号冲突无法从现有约束证明不存在。集成读取与审计应同时加 `plantCode`，并先用下列 SQL 检查是否存在跨厂冲突。 | 实体 `boxID` 为 `int?`；样本含 1010/1020 |

以下 SQL 只读、仅供人工在 MES 数据库执行；不会由本任务执行：

```sql
DECLARE @PlanStockId UNIQUEIDENTIFIER = '替换为 PK_PlanStock_Id';

SELECT
    ps.PK_PlanStock_Id, ps.plantCode AS PlanPlant, ps.status AS PlanStatus,
    ps.Stockby, ps.Stockdt,
    b.PK_Box_Id, b.boxID, b.boxBarCode, b.CustboxID, b.status AS BoxStatus,
    b.WebApi, b.WebApiTime, b.PrintInfo, b.DateCodeQty,
    pm.PK_Packmaster_Id, pm.PackID, pm.CustPackID, pm.CustBarCode,
    pm.datecode, pm.SetQty, pm.PCSQty, pm.Status AS PackStatus
FROM dbo.PK_PlanStock AS ps
LEFT JOIN dbo.PK_Box AS b
  ON b.PK_PlanStock_Id = ps.PK_PlanStock_Id
LEFT JOIN dbo.PK_Packmaster AS pm
  ON pm.boxID = b.boxID
 AND pm.plantCode = b.plantCode
WHERE ps.PK_PlanStock_Id = @PlanStockId
ORDER BY b.boxID, pm.PackID;

SELECT boxID, COUNT(DISTINCT plantCode) AS PlantCount
FROM dbo.PK_Box
GROUP BY boxID
HAVING COUNT(DISTINCT plantCode) > 1;
```

## 4. 字段、状态和打印链路

### 4.1 `PK_Box` 与 `PK_Packmaster`

- **FACT** `PK_Box.boxID` 是 `int?` 的业务箱号；`boxBarCode`、`CustboxID` 均为独立字符串字段。BoxStore 的 SAP 操作向控制器提交 `boxIDArray` 与 `CustboxIDArray`，而 SAP 包装信息 API 接收 `boxId`。无法仅据此判定现场扫描的唯一字段。[PK_BoxEntity.cs](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PK_BoxEntity.cs)、[BoxStore.js:157-215](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Views/PKBox/BoxStore.js)
- **FACT** 样本的 `PrintInfo` 形如 `2629/60`、`2502/20`，并与箱 `DateCodeQty=1` 共现；这只是观测相关性，不足以证明拼接或数量规则。
- **OPEN QUESTION** `PacketQty/SetQty/UnitQty/DateCodeQty` 的精确定义与计算公式、`Bound` 的业务语义、`WMstatus` 生命周期、`boxBarCode/CustboxID` 的实际扫描优先级，需要 `PK_CreatePackBox`、查询 SP 和现场扫描日志证据。
- **FACT** `PK_Packmaster` 样本中的 `datecode` 含 `4825`、`2623`、`2203W2` 等不等长格式，说明字段存储原始字符串而非统一的四位周码。
- **OPEN QUESTION** `PackID`、`CustPackID`、`CustBarCode` 哪一个是 INNER_PACK 现场主扫码码，及 `SetQty/PCSQty` 分别的库存语义，未由 BoxMore/BoxStore 主链直接证明。WMS 导入不可先假定某字段为唯一条码。

### 4.2 DateCode 端到端

| 问题 | 结论 |
|---|---|
| DateCode 从何而来 | **FACT** BoxMore 的操作员输入 `DC1..3`；输入后前端用 MES 指示/规则配置计算或回填生产日期。`PK_CreatePackBox` 接收这些原始值。 |
| MES 指示库是否被读取 | **FACT** `GetDataRangeByProdno` 读取 `md_stationattvalue.attvalue`，BoxMore 用其显示“标准周期格式”并在输入后查询日期。 |
| `PK_Packmaster.datecode` 是否就是最终实际打印值 | **OPEN QUESTION** 其由 `PK_CreatePackBox` 写入的规则未知；打印 SP 返回的字段未知。只能确认它被 SAP 载荷原样作为 `ZZM`。 |
| 设备是否再计算/转换 | **OPEN QUESTION** 代码写入共享目录文本文件（或调用华为接口），但没有设备回传解析代码。 |
| 打印成功是否回传实际打印值 | **OPEN QUESTION** 普通打印路径在文本文件写完后直接返回“打印成功”；未发现设备回执处理。华为调用会检查 HTTP/API 返回码，但不构成已打印 DateCode 的字段回写证据。 |
| `datecode` 是计划值还是已实际打印值 | **INFERENCE** 在未取得 SP 和设备回传定义前，它应作为“MES 计划/传递的 DateCode”，不能声明为实物标签审计值。 |

### 4.3 打印

- **FACT** `CreatePackBox` 成功后调用 `PrintBoxTY`；它按厂别/客户选择打印目录，调用 `PK_BoxPrintTY(groupboxID, PK_BOX_ID, userID)`，将返回的列名和行值写到 `BOX_TY.txt`。随后调用 `pr_pk_box_master_sp`，其结果集交由 `Print(DataSet)` 写到 SP 给出的 `dirPath + fileName`。[PKBoxController.cs:488-627](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)
- **FACT** 重打会写 `PK_PackPintLog`（外箱、重打、操作人、时间）；普通首印路径未在 C# 中看到相同日志写入。[PKBoxController.cs:586-600](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)
- **OPEN QUESTION** `PK_BoxPrintTY` 的字段集合、标签模板选择和是否直接包含 `PK_Packmaster.datecode`；需 SP 定义和打印端模板/日志。

## 5. 拆箱、重工、托盘与 WebApi

- **FACT** BoxMore 有 `SplitBoxTY`，调用 `PK_SplitBoxPack(boxID, user, reason)`；`SplitBoxPack`/`SplitBox` 调用 `PK_SplitBox(CustboxID, user, reason)`，前者随后调用 `PackController.DividPack`。后者的输出包含 `PK_Packmaster_Id`，表明至少有内包级后续处理。[PKBoxController.cs:1339-1429](D:/com.delton.gz/Learun.Application.Web/Areas/MES/Controllers/PKBoxController.cs)、[PKBoxService.cs:1172-1245](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)
- **OPEN QUESTION** 拆箱/重工后是否重写 `boxID`、`datecode`、关系记录或只改状态，无法在没有上述 SP 的情况下确认。因此在 MES 已发 WMS 后，不应假设关系不可变。
- **INFERENCE** WMS v1 应先按“快照导入 + 集成冻结点”设计：仅在 MES 确认入库前接收完整结构；入库后发生拆箱/重工时，需拒绝 MES 原地覆盖并由后续 Packaging Change Event/人工差异流程处理。是否要实现事件，必须在 SP 证据确认后决定。
- **FACT** `PalletID` 同时出现在 `PK_Box`、`PK_Packmaster` 字段与查询投影中，但当前双入口没有写入它的逻辑；没有发现关联托盘表的直接证据。[PkPackMasterService.cs:37-49](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PkPackMaster/PkPackMasterService.cs)
- **OPEN QUESTION** MES 的托盘条码主表和 `PALLET → BOX` 关系尚未识别；不得将 `PK_PlanStock` 解释为托盘。
- **FACT** `WebApi` 当前用于每箱 SAP 包装信息发送成功标记，并写 `WebApiTime`；`PK_Packmaster.WebApi` 的写入者、重试机制与 WMS 用途未从主入口确认。失败箱在再次触发 BoxStore 时会再次尝试，因为仅跳过 `WebApi=1` 的箱。[PKBoxService.cs:113-117, 1249-1299](D:/com.delton.gz/Learun.Framework.Module/Learun.Application.Module/Learun.Application.TwoDevelopment/MES/PKBox/PKBoxService.cs)

## 6. MES → WMS Mapping Proposal

| MES 来源 | WMS 目标 | 建议与证据状态 |
|---|---|---|
| `PK_PlanStock.PK_PlanStock_Id` | 批次级 `SourceEntityId` / 请求幂等键 | **建议**；WMS `WMS_HU_Identifier` 已有 `SourceSystem`、`SourceEntityId`。 |
| `PK_Box.PK_Box_Id` | `OUTER_BOX` 源标识 | **建议**；同时保留 `boxID`、`boxBarCode`、`CustboxID` 为不同 IdentifierType，主扫描码待确认。 |
| `PK_Packmaster.PK_Packmaster_Id` | `INNER_PACK` 源标识 | **建议**；保留 `PackID`、`CustPackID`、`CustBarCode`，不先选定主码。 |
| `prodno` / `prodrev` | `MaterialCode` / `MaterialRevision` | **FACT（字段映射）**。 |
| `PK_Packmaster.datecode` | `WMS_HU_Item.DateCode` | **FACT（原文保留）**；解析结果须独立保存。 |
| `PCSQty` / `PK_Box.UnitQty` | `Qty` | **OPEN QUESTION**；数量单位必须先由 SP/包装规格确认，接口应显式传 `Uom`。 |
| `plantCode` | `PlantCode` / warehouse-plant mapping | **建议**；避免仅以数值箱号跨厂匹配。 |
| `WeekFormatCode/WeekYear/WeekNo/WeekSortKey/WeekRuleSource/WeekRuleValue/WeekResolvedTime` | `WMS_HU_Item` 已有字段 | **FACT（WMS schema）**；在 DateCode 真实来源确认前允许为 null，`WeekRuleSource` 应记录实际权威规则而非直接写“MES 指示库”。 |

WMS 现有 `WMS_HU_Identifier` 支持 `SourceSystem` 与 `SourceEntityId`，并对活动标识提供唯一索引；当前证据不要求新增 SourceMapping 表。[010_WeekCodeAndHuIdentifier.sql:135-277](D:/Code/WMS-HU-Model/docs/database/010_WeekCodeAndHuIdentifier.sql)

## 7. 批量接口与幂等建议

- **建议** 建立一个业务批次请求：`POST /api/wms/integration/mes/finished-goods/inbound`，请求体为一个 `PK_PlanStock_Id`、`plantCode`、`Boxes[]`、每箱 `InnerPacks[]`，由 WMS 单事务创建整棵 HU 结构。不要让 MES 对同一批数据循环调用 `CreateHU/AddItem/AttachHU/MoveHU`。
- **依据** 一个 MES 入库确认是以 `PK_PlanStock_Id` 聚合、以全部箱 SAP 成功为门槛、再一次性更新状态；将其拆成几十次 WMS 调用会制造部分成功与重复重试风险。
- **批量大小** **OPEN QUESTION**：未获得真实的单 `PK_PlanStock` 最大箱数、每箱内包数和字符长度，不能给出确定 Payload 上限。接口应记录请求大小和处理耗时；超出部署约定阈值时按 Box 分页，但同一批次需有可恢复的版本/完成标记。
- **幂等** 使用 `MES + PK_PlanStock_Id` 作为批次自然键，`MES + PK_Box_Id` / `MES + PK_Packmaster_Id` 作为 HU 自然键；同键同内容返回原结果，同键不同内容返回冲突，禁止静默覆盖。
- **重试** MES 只在获得 WMS 成功确认后设置其独立的 WMS 发送状态（不要复用现有 SAP `WebApi`）；网络失败可重发同一批次。SAP `WebApi` 是另一条集成的状态，不应作为 WMS 成功凭据。

## 8. 未确认问题与下一步

1. 提供并审阅：`PK_CreatePackBox`、`PK_BoxPrintTY`、`PK_BoxPrint`、`pr_pk_box_master_sp`、`PK_SplitBox`、`PK_SplitBoxPack`、`pk_boxmore_query`。
2. 提供 MES 实际打印目录中的标签模板/打印代理协议，以及一次成功打印的输入文件和设备回执（可脱敏），以确认实际标签 DateCode 与数据库字段是否一致。
3. 提供 `PalletID` 所关联表的 DDL/查询路径，确认真实托盘码和箱-托盘关系。
4. 用第 3 节只读 SQL 抽取一批“预入库”和一批“已入库”数据，验证箱/内包关系、状态和跨厂 `boxID` 唯一性。
5. 在上述证据齐备后，确定 WMS 导入冻结点与 Packaging Change Event 是否为必须项，再开始接口设计/实现。

## 9. 本次验证与限制

- 已执行：静态文件搜索与代码阅读；当前 WMS schema/migration 阅读。
- 未执行：MES/WMS 数据库 SQL、任何存储过程、接口调用、打印文件写入、构建或测试。
- 未修改：MES 代码、WMS 业务代码、数据库、Migration、Git 历史。

```text
TRACE_COMPLETE=False
DATECODE_SOURCE_CONFIRMED=False
PRINT_CHAIN_CONFIRMED=False
INBOUND_TRIGGER_CONFIRMED=True
PALLET_RELATION_CONFIRMED=False
BATCH_INTEGRATION_RECOMMENDED=True
```
