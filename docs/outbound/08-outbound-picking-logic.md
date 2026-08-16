# 08 - Outbound Picking Logic

## 背景

当前 PDA 出货页面来自旧模式：现场先拿到已经打印好的 SAP 交货单，通过 PDA 扫描交货单号，再向 SAP 查询交货单明细，之后人工扫描箱码完成数量确认。

新 WMS 模式下，出货单据应由 SAP 主动推送到 WMS。WMS 接收 SAP 出货需求后，在 WMS 内部基于 HU 库存推荐拣货箱，并由现场按推荐结果执行出库。

核心变化：
- SAP 不再指定具体哪一箱。
- SAP 只需要告诉 WMS 出货需求，例如型号、数量、工厂、库存地点等业务维度。
- WMS 根据 HU 库存、周码、箱数量自动推荐拣货方案。
- 周码用于 FIFO 排序，不再按入仓时间排序。
- 系统尽量推荐整箱组合，减少现场拆箱。

## 旧 PDA 链路

### 交货单查询

旧页面 `CPC_delivery.cshtml` 的查询入口：

```text
GET /H5TPM/PostSapData
url=/RESTAdapter/PDA/S0021PDAreadDN
postdata={
  "IMPWERKS": "1020",
  "IMPVBELN": "8300261975"
}
```

链路：

```text
PDA 扫描 10 位交货单号
  -> /H5TPM/PostSapData
    -> ApiCall.PostSapData
      -> SapApiHost + /RESTAdapter/PDA/S0021PDAreadDN
        -> SAP 返回 TABOUTDN
```

典型返回：

```json
{
  "E_CODE": 1,
  "E_MSG": "读取交货单成功",
  "TABOUTDN": [
    {
      "VBELN": "8300261975",
      "POSNR": "000010",
      "MATNR": "M4W080013D",
      "LFIMG": 30360,
      "WERKS": "1020",
      "LGORT": "12C3",
      "CHARG": "B00002630",
      "ZDYSE": 30,
      "ZZHJH": "X"
    }
  ]
}
```

旧页面实际依赖字段：

| 字段 | 含义 | 旧逻辑用途 |
| --- | --- | --- |
| `VBELN` | 交货单号 | 页面显示、提交 |
| `POSNR` | 行项目 | 页面显示、提交 |
| `MATNR` | 料号/型号 | 箱码匹配 |
| `LFIMG` | 需求数量 | 数量校验 |
| `WERKS` | 工厂 | 页面明细字段 |
| `LGORT` | 库存地点 | 提交 SAP |
| `CHARG` | SAP 批次 | 箱码匹配、提交 SAP |
| `ZZHJH` | 齐数标识 | 页面展示 |
| `ZDYSE` | 返回字段 | 当前前端未直接使用 |

### 赠送数量查询

旧页面在 SAP 明细返回后，会按 `MATNR` 查询赠送数量：

```text
GET /H5TPM/GetDataSet
sqlStoredName=pr_md_prodmaster_sp
pms={
  "flag": "SearchByZSSL",
  "plantcode": loginInfo.plant,
  "cols": "M4W080013D"
}
```

旧逻辑允许：

```text
需求数量 <= 已扫数量 <= 需求数量 + 赠送数量
```

新 WMS 逻辑不再沿用这个超量容忍方式。系统推荐拣货时应刚好满足出货需求数量，避免现场靠赠送数量手工拆箱和调整。

### 箱码扫描匹配

旧页面扫描箱码后，使用以下字段：

```text
arr[1]  = MATNR，料号
arr[2]  = 本箱数量
arr[5]  = CHARG，批次
arr[10] = 箱号，取 split("-")[0]
arr[12] = 净重
arr[13] = 毛重
```

旧匹配条件：

```text
箱码 MATNR == 交货单行 MATNR
箱码 CHARG == 交货单行 CHARG
```

旧模式下，SAP 返回 `CHARG`，前端认为该交货单行必须匹配该批次。

## 新 WMS 出货链路

### 单据接收

新模式下，SAP 有出货单时调用 WMS 接口推送出货需求，WMS 保存为出货单和出货单行。

建议链路：

```text
SAP 创建/变更出货单
  -> 调用 WMS 出货单接收接口
    -> WMS 保存出货单头、出货单行
      -> WMS PDA/页面显示待拣货任务
        -> WMS 根据 HU 库存推荐拣货箱
          -> 现场按推荐箱码执行扫描
            -> WMS 校验并完成出库
              -> WMS 汇总结果回传 SAP
```

### SAP 推送建议字段

单头建议字段：

| 字段 | 含义 | 必要性 |
| --- | --- | --- |
| `VBELN` | 出货单/交货单号 | 必填 |
| `WERKS` | 工厂 | 必填 |
| `LGORT` | 库存地点 | 建议必填 |
| `CustomerCode` | 客户编码 | 可选 |
| `ShipTo` | 收货方 | 可选 |
| `DeliveryDate` | 交货日期 | 可选 |
| `SourceSystem` | 来源系统 | 建议 |

单行建议字段：

| 字段 | 含义 | 必要性 |
| --- | --- | --- |
| `VBELN` | 出货单/交货单号 | 必填 |
| `POSNR` | 行项目 | 必填 |
| `MATNR` | 料号/型号 | 必填 |
| `LFIMG` | 需求数量 | 必填 |
| `UOM` | 单位 | 必填 |
| `WERKS` | 工厂 | 必填 |
| `LGORT` | 库存地点 | 建议必填 |
| `Version` | 版本号 | 可选，当前规则不参与排序 |
| `Remark` | 备注 | 可选 |

当前已确认：新拣货排序按 `MATNR` 聚合，不要求 SAP 指定具体 `CHARG` 或箱号。

## HU 库存字段

WMS 需要在 HU 维度维护周码。该字段在入仓建立 HU 时同步写入。

HU 库存推荐至少依赖以下字段：

| 字段 | 含义 | 说明 |
| --- | --- | --- |
| `HUCode` | HU/箱号 | 现场扫描对象 |
| `MATNR` | 料号/型号 | 出货需求聚合维度 |
| `Qty` | 当前可用数量 | 推荐和扣减依据 |
| `UOM` | 单位 | 需要和出货单一致 |
| `WeekCode` | 周码 | FIFO 排序依据 |
| `WERKS` | 工厂 | 库存范围过滤 |
| `LGORT` | 库存地点 | 库存范围过滤 |
| `Status` | HU 状态 | 仅可用库存参与推荐 |
| `IsFullBox` | 是否整箱 | 可由当前数量和标准箱数推导 |
| `StandardBoxQty` | 标准箱数量 | 判断整箱、拆箱 |
| `BatchNo` | WMS 批次/内部批次 | WMS 自维护，可用于追溯 |
| `InboundTime` | 入仓时间 | 保留追溯，不作为 FIFO 主排序 |

周码排序规则：

```text
WeekCode 越小，优先级越高。
例如 2603 早于 2605，2605 早于 2606。
```

## 新拣货推荐规则

### 聚合维度

出货需求按 `MATNR` 聚合推荐。

如果一张出货单中同一个 `MATNR` 有多行，可以先聚合成总需求，再按推荐结果回填到具体行项目；也可以按行逐行推荐。建议先按 `MATNR` 聚合，避免同型号多行互相抢占库存。

基础过滤条件：

```text
HU.MATNR == 出货需求 MATNR
HU.Status == 可用
HU.Qty > 0
HU.WERKS == 出货需求 WERKS
HU.LGORT == 出货需求 LGORT，若业务允许跨库位则该条件可放宽
```

### 优先级

已确认优先级：

```text
1. 尽量使用最早周码
2. 在满足周码优先的前提下，尽量不拆箱
3. 必须刚好满足需求数量
```

解释：
- 周码是 FIFO 主排序字段，不按入仓时间排序。
- 允许跨周码凑箱。
- 当早周码小箱和较晚周码小箱能刚好凑够需求时，优先整箱凑够，避免拆完整箱。
- 不使用赠送数量做超量容忍，推荐数量应等于需求数量。

### 示例

需求：

```text
MATNR = A
NeedQty = 100 PCS
```

可用 HU：

| 箱号 | 周码 | 数量 |
| --- | --- | ---: |
| Box1 | 2603 | 50 |
| Box2 | 2605 | 100 |
| Box3 | 2606 | 50 |

推荐：

```text
Box1 + Box3 = 100 PCS
```

原因：
- Box1 是最早周码，优先使用。
- Box1 单箱不足，需要补足 50。
- Box3 可与 Box1 刚好凑够 100。
- 选择 Box1 + Box3 可以避免拆箱。
- Box2 虽然单箱刚好 100，但会跳过最早周码 Box1，不符合优先使用早周码的目标。

## 推荐算法建议

### 输入

```text
MATNR
NeedQty
WERKS
LGORT
UOM
AvailableHU[]
```

### 输出

```text
RecommendedHU[]
NeedSplit
ShortageQty
Reason
```

推荐明细：

| 字段 | 含义 |
| --- | --- |
| `HUCode` | 推荐箱号 |
| `MATNR` | 料号 |
| `WeekCode` | 周码 |
| `PickQty` | 推荐拣货数量 |
| `OriginalQty` | 原箱可用数量 |
| `IsSplit` | 是否拆箱 |
| `POSNR` | 可选，关联出货单行 |

### 处理步骤

1. 查询同 `MATNR` 的可用 HU。
2. 按 `WeekCode` 升序排序。
3. 从最早周码开始处理。
4. 优先寻找不拆箱且总数量刚好等于需求的组合。
5. 如果不能整箱刚好满足，再考虑拆箱。
6. 如果总可用数量不足，返回缺料信息。
7. 推荐结果生成后锁定库存，避免多人并发重复推荐同一 HU。

### 组合选择原则

推荐逻辑可以分为两层：

第一层，必须覆盖最早可用周码：

```text
从最早 WeekCode 的 HU 开始纳入候选。
只要最早周码还有可用 HU，推荐结果不能完全跳过该周码。
```

第二层，在候选基础上寻找刚好满足需求的整箱组合：

```text
优先整箱组合 Sum(Qty) == NeedQty。
如果存在多个组合，选择周码序列整体更早的组合。
如果周码序列相同，选择箱数更少的组合。
```

如果不存在整箱组合：

```text
按 WeekCode 升序取整箱，直到下一箱会超过需求。
最后对最早可拆的 HU 取剩余数量。
```

拆箱时要记录：

```text
原 HU 数量
拣出数量
剩余数量
新 HU 或剩余 HU 的处理方式
操作人
操作时间
出货单号
```

## 箱码解析

当前旧 PDA 只支持一种 `#` 分隔、14 段的箱码格式。新系统需要支持多种箱码格式。

建议增加箱码解析层：

```text
RawBarcode
  -> BarcodeParser
    -> ParsedBarcode
      -> WMS 校验
```

统一解析结果：

| 字段 | 含义 |
| --- | --- |
| `BarcodeType` | 箱码类型 |
| `HUCode` | 箱号 |
| `MATNR` | 料号 |
| `Qty` | 数量 |
| `WeekCode` | 周码 |
| `BatchNo` | 批次 |
| `NetWeight` | 净重 |
| `GrossWeight` | 毛重 |
| `RawText` | 原始码 |

不同箱码类型只影响解析，不应影响后续拣货校验。后续校验统一使用 `ParsedBarcode`。

扫描校验建议：

```text
1. 箱码能识别为已支持类型。
2. 箱码解析出的 HUCode 在 WMS 存在。
3. HU 状态为已推荐/可拣货。
4. HUCode 属于当前出货任务推荐结果。
5. MATNR 与出货需求一致。
6. WeekCode 与 WMS HU 库存记录一致。
7. 扫描数量与推荐数量一致。
```

## 出库提交

WMS 完成现场拣货后，应按 WMS 实际拣货结果回传 SAP。

建议回传维度：

| 字段 | 含义 |
| --- | --- |
| `VBELN` | 出货单号 |
| `POSNR` | 行项目 |
| `MATNR` | 料号 |
| `Qty` | 实际出货数量 |
| `UOM` | 单位 |
| `WERKS` | 工厂 |
| `LGORT` | 库存地点 |
| `BatchNo` | WMS 批次/可映射 SAP 批次 |
| `HUCode` | 箱号 |
| `WeekCode` | 周码 |

是否仍调用旧 SAP 接口 `S0023PrdBatchSplitPicking`，需要结合 SAP 新接口方案确认。新模式下更合理的是由 WMS 提交实际出库明细，SAP 负责过账或单据状态更新。

## 边界场景

### 可用库存不足

返回缺料，不生成可执行推荐：

```text
NeedQty = 100
AvailableQty = 80
ShortageQty = 20
```

### 无法整箱刚好满足

如果整箱组合无法刚好等于需求，则允许拆箱，但拆箱应是最后选择。

示例：

```text
NeedQty = 100
Box1 2603 60
Box2 2605 60
```

推荐：

```text
Box1 拣 60
Box2 拆 40
```

### 存在多个整箱组合

选择更符合周码 FIFO 的组合。

示例：

```text
NeedQty = 100
Box1 2603 50
Box2 2604 50
Box3 2605 100
```

推荐：

```text
Box1 + Box2
```

### 最早周码单箱数量大于需求

如果最早周码只有一箱且数量超过需求，无法通过其他箱组合刚好满足，则拆最早周码。

示例：

```text
NeedQty = 100
Box1 2603 120
Box2 2605 100
```

推荐：

```text
Box1 拆 100
```

原因：周码 FIFO 优先级高于不拆箱。

## 待确认点

以下事项还需要后续和 SAP/WMS 表结构设计继续对齐：

- SAP 推送出货单接口的最终字段名。
- WMS 内部 `WeekCode` 字段类型，建议使用可排序字符串或整数。
- 不同箱码类型的格式清单，以及每种格式中料号、数量、周码、箱号的位置。
- WMS 批次 `BatchNo` 和 SAP 批次 `CHARG` 是否需要映射。
- 跨 `LGORT` 拣货是否允许。
- 出库完成后回传 SAP 的接口和幂等键。
- 推荐库存锁定策略，避免同一 HU 被多个任务同时推荐。

