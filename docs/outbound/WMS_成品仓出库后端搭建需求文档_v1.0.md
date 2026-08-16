# WMS 成品仓出库、异常退回、库存预留与标签基础——后端搭建需求文档

版本：v1.0  
日期：2026-08-14  
适用项目：WMS-SAP 成品仓设计  
技术栈：C# / .NET Framework 4.7.2 / Nancy 2.0 / MSSQL

---

## 1. 文档目标

本阶段在已经完成的 HU、当前位置、移动日志、Repack、Putaway、FIFO 验证能力基础上，正式建立生产可用的出库业务骨架。

本阶段解决：

1. SAP 主动推送交货单到 WMS；
2. WMS 保存交货单头与多行项目；
3. 同一 `VBELN` 可以存在多个 `MATNR`、多个 `POSNR`；
4. SAP 推送只建立待拣货单，不立即锁库存；
5. 现场点击“开始拣货”后才执行正式 FIFO 分配；
6. 分配成功后建立 3 小时库存 Reservation；
7. 实际扫描后才发生 HU 移动；
8. 支持单箱异常退回、质量 Hold、人工 Release、补入替代箱；
9. RETURNED 为虚拟货位，实物放在现有备货区；
10. 为后续箱码/客户码一一对应、外箱标签和滚码系统预留统一身份模型；
11. 周码保留供应商原值，并在 `WMS_HU_Item` 固化标准排序键；
12. 正式出库推荐不再以入仓时间作为 FIFO 主依据。

本阶段不完成：

- SAP 最终 Picking/PGI 接口调用实现；
- LOADING / LOADED 现场状态；
- SHIPPED 后 SAP 冲销；
- 货位容量和智能上架推荐；
- 新箱滚码的真实条码规则；
- 客户标签模板和打印任务；
- SAP 交货单改单增量更新。

---

## 2. 已确认业务边界

### 2.1 HU 层级

```text
PALLET
  ↓
OUTER_BOX
  ↓
INNER_PACK
  ↓
WMS_HU_Item
```

职责：

- `PALLET`：入仓临时运输载体；
- `OUTER_BOX`：默认出库推荐、现场扫描、搬运和正式定位单元；
- `INNER_PACK`：成品仓最小不可分库存单元；
- `WMS_HU_Item`：产品库存属性。

### 2.2 INNER_PACK 不允许数量拆分

例如：

```text
I001 = 100 PCS
```

成品仓内不允许：

```text
60 PCS 出货
40 PCS 保留
```

若需要拆分 INNER_PACK 数量，必须退出成品仓，回内包装流程处理后重新入仓。

### 2.3 OUTER_BOX 可以拆，但不能拆 INNER_PACK 数量

例如：

```text
B001
├─ I001 40 PCS
├─ I002 30 PCS
└─ I003 30 PCS
```

需求 70 PCS 时：

```text
40 + 30 = 70
```

可以返回 `REPACK_REQUIRED`，拆外箱后按完整 INNER_PACK 重新装入新箱。

需求 60 PCS 时：

```text
40 / 30 / 40+30
```

不存在 60 PCS 的完整 INNER_PACK 组合，因此返回：

```text
NO_EXACT_PACK_COMBINATION
```

### 2.4 不允许超领

正常需求必须精确满足：

```text
NeedQty = 100
PickedQty = 100
```

赠送不是超领。

若正常数量 100 PCS，赠送 20 PCS，则：

```text
EffectiveRequiredQty = 120
```

WMS 必须精确准备 120 PCS。

赠送数量当前来源：

```text
pr_md_prodmaster_sp
@flag = 'SearchByZSSL'
```

---

## 3. SAP 出货单接收模型

### 3.1 SAP 主动推送

SAP 创建交货单后调用 WMS 接口。

WMS 接收后：

```text
SAP Push
   ↓
WMS_OutboundOrder
   ↓
Status = WAIT_PICK
```

此时：

- 不做库存移动；
- 不创建 Reservation；
- 不改变 HU CurrentLocation；
- 可以查询单据；
- 可以做动态预览推荐。

### 3.2 SAP 单据结构

一个 `VBELN`：

- 可以多个 `MATNR`；
- 可以多个 `POSNR`；
- 不跨 `WERKS`；
- 不跨 `LGORT`。

因此采用：

```text
WMS_OutboundOrder
  1
  ↓
N
WMS_OutboundOrderLine
```

### 3.3 SAP 推送 Header 字段

当前接口资料包含：

- VBELN
- LFART
- KUNNR
- KUNAG
- VKORG
- WADAT
- WAUHR
- VSART
- SDABW
- EXPVZ
- ERNAM
- ERDAT
- STREET
- TITLE_LET
- TEL_NUMBER

WMS 必须保存原始事实，不因后续推荐聚合而覆盖。

### 3.4 SAP 推送 Item 字段

当前接口资料包含：

- POSNR
- MATNR
- LFIMG
- MEINS
- PSTYV
- WERKS
- LGORT
- MFRGR
- BEZEI
- VGBEL
- VGPOS
- BSTKD
- ZKHLH
- LFIMG1
- BSTKD_E
- LFIMG2

当前 SAP 推送时 `LFIMG2` 无值，因此 WMS 在接单后通过 MES 规则读取 GiftQty，并固化到订单行。

### 3.5 第一版改单策略

第一版不支持 SAP 对同一 `VBELN` 做增量修改。

规则：

- 相同 `VBELN` + 相同报文：返回幂等成功；
- 相同 `VBELN` + 不同报文：拒绝更新；
- 如需修改，旧单作废，SAP 重新生成新单后推送。

---

## 4. 周码模型

### 4.1 周码不属于 OUTER_BOX 的单一属性

一个外箱可以混周码，例如：

```text
B9350
├─ I25590  DateCode = 2118
├─ I25601  DateCode = 2118
├─ I25700  DateCode = 2118
└─ I25701  DateCode = 2120
```

因此：

```text
OUTER_BOX.WeekCode
```

不能作为唯一事实字段。

周码事实必须保存在库存内容层：

```text
WMS_HU_Item
```

### 4.2 保留供应商原始周码

`DateCode` 继续保存供应商/MES 原始值。

不直接把：

```text
5221
```

覆盖成：

```text
2152
```

而是额外固化：

- WeekFormatCode
- WeekYear
- WeekNo
- WeekSortKey
- WeekRuleSource
- WeekRuleValue
- WeekResolvedTime

示例：

```text
DateCode        = 5221
WeekFormatCode  = WWYY
WeekYear        = 2021
WeekNo          = 52
WeekSortKey     = 202152
```

### 4.3 周码格式规则来源

当前权威来源仍然是 MES。

规则是：

```text
MATNR + Revision
    ↓
MES 指示库
    ↓
周期格式
```

实际 DateCode 来源：

```text
MES 外箱/内包数据
```

WMS 入仓时：

```text
读取原始 DateCode
    ↓
根据 MATNR + Revision 读取周期格式
    ↓
解析 WeekYear / WeekNo
    ↓
写入 WMS_HU_Item 快照
```

不建议每次出库时实时跨库重新解析。

未来标签系统迁入 WMS 后，可以替换 WeekCodeRuleProvider，而不改变库存表和出库算法。

---

## 5. 箱级 FIFO 规则

### 5.1 最小库存单元与默认拣货单元

必须区分：

```text
INNER_PACK
= 最小不可分库存单元

OUTER_BOX
= 默认推荐 / 扫码 / 搬运单元
```

### 5.2 混周码箱不会因为周码不同主动拆箱

例如：

```text
B001
├─ I001 2603
├─ I002 2605
└─ I003 2605
```

不会为了仅拿 2603 主动拆 B001。

箱级 FIFO 特征动态计算：

```text
BoxMinWeekSortKey
BoxMaxWeekSortKey
DistinctWeekCount
WeekSummary
```

其中：

```text
BoxEffectiveFIFOKey = MIN(WeekSortKey)
```

### 5.3 箱子只要存在最老周码，就按最老周码参与 FIFO

例如：

```text
B001 = 2603 + 2608
B002 = 2604
```

优先：

```text
B001
```

拿 B001 时允许连同箱内较新周码一起出，不因混周码触发 Repack。

---

## 6. 正式推荐算法

业务优先级固定：

```text
1. 最早周码
2. 尽量整箱
3. 精确满足
```

### 6.1 订单聚合

同一个 `MATNR` 在多个 `POSNR` 时：

```text
POSNR 000010  A 60
POSNR 000030  A 40
```

推荐阶段聚合为：

```text
A = 100
```

避免不同 POSNR 独立抢占同一 FIFO 库存。

推荐完成后，再按 SAP 行项目回填分配数量。

### 6.2 推荐结果收敛

正式算法结果只保留：

```text
EXACT_WHOLE_BOX
REPACK_REQUIRED
SHORTAGE
NO_EXACT_PACK_COMBINATION
```

#### EXACT_WHOLE_BOX

存在完整 OUTER_BOX 组合，数量精确等于需求。

#### REPACK_REQUIRED

完整外箱无法精确满足，但完整 INNER_PACK 组合可以精确满足。

本次 StartPicking 不直接执行库存移动，需要先完成 Repack，再重新分配。

#### SHORTAGE

总可用库存数量小于需求。

#### NO_EXACT_PACK_COMBINATION

总库存足够，但由于 INNER_PACK 不可分，无法组合出精确需求数量。

### 6.3 不再使用

正式流程废弃：

```text
OVERPICK
AllowOverPick
按 CreateTime FIFO
按 PCS 拆 INNER_PACK
```

---

## 7. Preview 与 StartPicking

### 7.1 Preview

Preview 只是动态计算：

```text
POST /api/wms/outbound/orders/{vbeln}/preview
```

特点：

- 不写 Reservation；
- 不锁库存；
- 不创建 MoveLog；
- 不更新 CurrentLocation；
- 返回当前时点推荐结果。

Preview 与实际 StartPicking 结果允许不同，因为两次之间库存可能变化。

### 7.2 StartPicking

现场点击：

```text
开始拣货
```

后端才执行正式分配。

建议前端按钮可以叫：

```text
开始拣货并打印
```

但后端顺序必须是：

```text
StartPicking
  ↓
分配成功
  ↓
创建 Reservation
  ↓
再打印
```

打印失败/重打不能重复创建库存分配。

### 7.3 订单级原子分配

一次 `StartPicking` 对整张 `VBELN` 执行。

流程：

1. 锁定订单；
2. 检查 `WAIT_PICK`；
3. 对所有 MATNR 分组计算；
4. 任意分组出现 SHORTAGE / REPACK_REQUIRED / NO_EXACT_PACK_COMBINATION，则整次 StartPicking 不建立 Reservation；
5. 全部 MATNR 都能 `EXACT_WHOLE_BOX` 时；
6. 创建一个 `WMS_OutboundAllocation`；
7. 每个物料组建立一个 `WMS_OutboundPickBatch`；
8. 对推荐 OUTER_BOX 建立 `WMS_HU_Reservation`；
9. 更新订单为 `ALLOCATED`；
10. 提交事务。

---

## 8. Reservation 规则

### 8.1 为什么需要 Reservation

箱子在开始拣货后仍可能物理位于：

```text
H201
```

但已经属于某张出货单。

因此：

```text
CurrentLocation
```

不能承担库存预留职责。

Reservation 单独表达：

```text
这个箱仍在 H201，
但已被 VBELN 8300xxxx 占用。
```

### 8.2 Reservation 粒度

第一版 Reservation 以：

```text
OUTER_BOX
```

为单位。

原因：

- 默认整箱拣货；
- 混周码不会主动拆箱；
- REPACK_REQUIRED 不直接 StartPicking；
- Repack 完成后重新运行正式分配。

### 8.3 一个箱只能有一个有效 Reservation

数据库必须通过过滤唯一索引保证：

```text
一个 HUId
最多一条 Status = ACTIVE Reservation
```

### 8.4 Reservation 3 小时有效

StartPicking 成功：

```text
AllocationTime = T
ExpireTime = T + 3 hours
```

如果 3 小时内：

```text
一个箱都没有实际扫描
```

则允许：

```text
ACTIVE
  ↓
EXPIRED / RELEASED
```

释放全部 Reservation：

```text
Order = WAIT_PICK
```

### 8.5 一旦开始实际拣货，不再自动释放

第一个箱扫描成功：

```text
Allocation.Status = PICKING
FirstPickTime = now
Order.Status = PICKING
```

此后 3 小时超时规则停止生效。

---

## 9. PickBatch 正式定义

一张 SAP 交货单可能多个 MATNR。

因此：

```text
1 OutboundOrder
    ↓
N PickBatch
```

正式定义：

```text
一个 PickBatch
= 一张 OutboundOrder 下一个物料聚合组的一次拣货执行
```

同 MATNR 多 POSNR：

```text
先聚合
↓
一个 PickBatch
↓
实际 PickDetail
↓
再分配到 POSNR
```

已有阶段验证版 `WMS_OutboundPickBatch` 不删除，正式版通过新增字段升级。

---

## 10. PickDetail 与 PickDetailItem

### 10.1 PickDetail

`WMS_OutboundPickBatchDetail` 表示：

```text
现场实际拿了哪个物理 OUTER_BOX
```

只有真正扫码成功后才创建 PickDetail。

StartPicking 阶段的推荐箱由 Reservation 表保存，不提前创建 PickDetail。

因此已有：

- MoveLogId
- OperationNo
- FromLocationId
- ToLocationId

仍然只对应真实物理动作。

### 10.2 PickDetailItem

新增：

```text
WMS_OutboundPickDetailItem
```

表示该实际箱中的库存如何分配给 SAP 行项目。

用途：

1. 保存 INNER_PACK / HU_Item 级周码快照；
2. 保存实际出库内容；
3. 同 MATNR 多 POSNR 时做 SAP 行项目分摊；
4. 为未来 SAP 出货报文提供来源；
5. 保证物理拣货和 SAP 行级数据分离。

---

## 11. 异常箱、RETURNED、Hold 与补箱

### 11.1 异常处理对象

普通异常按：

```text
单箱
```

处理。

不因一箱质量问题取消整个 PickBatch。

### 11.2 RETURNED

新增/正式启用虚拟货位：

```text
LocationCode = RETURNED
IsVirtual = 1
IsScannable = 0
```

现场无 RETURNED 条码。

实物放在现有：

```text
备货区
```

操作员通过界面：

```text
确认问题箱已退回备货区
```

系统执行位置事实：

```text
OUTBOUND-STAGING / 当前拣货位置
    ↓
RETURNED
```

并写 MoveLog / OperationLog。

### 11.3 质量异常

质量 NG：

```text
Box
  ↓
RETURNED
  ↓
IsHold = 1
HoldReason = QUALITY_FAILED
```

必须人工：

```text
Release
```

后才能重新参与 FIFO。

### 11.4 普通取消

例如：

- ORDER_CHANGED
- WRONG_PICK
- MANUAL_EXCEPTION

可以退到 RETURNED，但默认不 Hold。

### 11.5 Hold 与 Location 是两个维度

```text
Location
= 在哪里

IsHold
= 能不能使用
```

质量箱即使重新上到 H201：

```text
Location = H201
IsHold = 1
```

仍然不能参与出库。

### 11.6 补入替代箱

问题箱历史不得删除。

例如：

```text
B002 → QUALITY_RETURNED
B005 → Replacement
```

原异常记录保留，新箱作为替代库存重新 FIFO 分配。

Batch 的：

```text
EffectivePickedQty
```

扣除异常箱后下降。

补箱完成后重新达到：

```text
RequiredQty
```

### 11.7 HistoricalPickedQty

保留历史实际拿过的数量。

例如：

```text
RequiredQty = 100

第一次拿：
100

退回问题箱：
30

补新箱：
30
```

结果：

```text
EffectivePickedQty = 100
HistoricalPickedQty = 130
```

---

## 12. ReturnToStock

RETURNED 中的箱允许：

```text
返回原货位
```

或：

```text
扫描新货位
```

### 返回原货位

优先读取原 PickDetail：

```text
FromLocationId
```

检查原货位：

- ACTIVE；
- STORAGE；
- 厂别策略允许。

### 原货位不可用

当前容量规则尚未确认，因此第一阶段不做复杂容量推荐。

可以：

1. 返回“原货位不可用”；
2. 给出基础可用 STORAGE 货位；
3. 由操作员扫描新货位。

未来接入容量规则后再升级推荐。

---

## 13. SHIPPED

当前 WMS 最可靠的终态是：

```text
SHIPPED
```

暂不实现：

```text
LOADING
LOADED
```

这些状态未来如能从 SAP/TMS/装车系统取得，再扩展。

当前第一版：

```text
PICKED
  ↓
SHIPPED
```

SHIPPED 后的货物返仓涉及 SAP 发货冲销，留到 SAP-API/冲销阶段实现。

数据库和状态机保留扩展位，但本阶段不允许使用普通 CancelPick 直接回滚已正式 SHIPPED 的业务。

---

## 14. 箱码 / 客户码基础

### 14.1 不把所有条码塞进 WMS_HU

`WMS_HU.HUCode` 始终作为 WMS 主业务标识。

不同外部标识统一放：

```text
WMS_HU_Identifier
```

第一批类型：

```text
MES_BOX_ID
MES_BOX_BARCODE
CUSTOMER_BOX_CODE
CUSTOMER_BARCODE
```

### 14.2 一一对应

对同一个客户：

```text
一个有效 CUSTOMER_BOX_CODE
只能对应一个 HU
```

同一个 HU：

```text
同一客户、同一 IdentifierType
最多一个 Active 标识
```

### 14.3 未来扫码链

```text
RawBarcode
  ↓
BarcodeParser
  ↓
WMS_HU_Identifier
  ↓
HUId
  ↓
WMS_HU
```

以后 PDA 不需要针对内部码/客户码分别写库存业务逻辑。

---

## 15. SAP 出货回传边界

旧业务：

```text
delivery
= SAP Picking

delivery_send
= SAP 发货提交
```

未来 WMS PDA 会整合为一个现场界面。

当前核心 WMS 只保留：

```text
Order Received
↓
Allocation
↓
Picking
↓
PICKED
↓
SHIPPED
↓
SAP Post
```

具体 SAP Picking / PGI 接口调用、S0023 是否保留、新 S0089 映射，放到 SAP-API 模块专项设计。

### 当前接口资料中的出货字段

包括：

- VBELN
- POSNR
- LGORT
- BOXNO
- MATNR
- CHARG
- LFIMG
- LFIMG_BOX
- NTGEW
- BRGEW
- GEWEI
- VRKME
- BOXSIZE

当前业务口径：

```text
BOXNO → WMS_HU.HUCode
```

CHARG 当前暂按 HUCode 方向设计，但接口 Excel 将 CHARG 描述为批次号，因此最终字段映射必须在 SAP-API 阶段用真实成功报文再次确认。

核心 WMS 不把 CHARG 含义写死在 HU 库存模型中。

---

## 16. 正式状态机

### 16.1 OutboundOrder

```text
WAIT_PICK
   ↓
ALLOCATED
   ↓
PICKING
   ↓
PICKED
   ↓
SHIPPED
```

异常：

```text
PICKING / PICKED
   ↓
EXCEPTION
   ↓
问题处理完成
   ↓
PICKING / PICKED
```

作废：

```text
WAIT_PICK
   ↓
VOID
```

### 16.2 Allocation

```text
ACTIVE
├─ RELEASED
├─ EXPIRED
└─ PICKING
      ↓
   COMPLETED
```

### 16.3 Reservation

```text
ACTIVE
├─ CONSUMED
├─ RELEASED
├─ EXPIRED
└─ CANCELLED
```

### 16.4 PickDetail

```text
PICKED
├─ RETURNED
├─ QUALITY_HOLD
├─ CANCELLED
└─ SHIPPED
```

---

## 17. 事务边界

### SAP 接单

单事务：

```text
ReceiveLog
Order
OrderLine
GiftQty Snapshot
```

### StartPicking

单事务：

```text
锁Order
↓
重新计算全部物料
↓
全部满足 EXACT_WHOLE_BOX
↓
Allocation
↓
PickBatch(s)
↓
Reservations
↓
Order = ALLOCATED
```

任意物料失败：

```text
ROLLBACK ALL
```

### 扫描一箱

单事务：

```text
锁Reservation/HU/CurrentLocation
↓
验证箱属于当前Allocation
↓
MoveLog
↓
CurrentLocation → OUTBOUND-STAGING
↓
OperationLog
↓
Reservation = CONSUMED
↓
PickDetail
↓
PickDetailItem
↓
更新Batch/Line/Order数量
```

### 异常退回

单事务：

```text
锁PickDetail/HU
↓
移动到RETURNED
↓
MoveLog
↓
CurrentLocation
↓
Exception
↓
必要时Hold
↓
扣EffectivePickedQty
↓
Order/Batch → EXCEPTION
```

### 补箱

单事务建立替代 Reservation。

实际新箱扫码时再生成新的 PickDetail。

---

## 18. 幂等要求

所有写接口必须带：

```text
RequestId
```

关键唯一点：

- SAP ReceiveLog.RequestId；
- Allocation.RequestId；
- Reservation.ReservationNo；
- PickBatch.RequestId；
- Exception.RequestId；
- HoldLog.HoldRequestId / ReleaseRequestId；
- MoveLog / OperationLog 继续按现有规则防重。

不得仅依赖前端按钮禁用。

---

## 19. API 规划

本阶段仅定义职责，不在本次输出 C# 实现。

### SAP 单据接收

```text
POST {{localUrl}}/api/wms/integration/sap/outbound-orders
```

### 待拣货单

```text
GET {{localUrl}}/api/wms/outbound/orders
GET {{localUrl}}/api/wms/outbound/orders/{vbeln}
```

### Preview

```text
POST {{localUrl}}/api/wms/outbound/orders/{vbeln}/preview
```

### StartPicking

```text
POST {{localUrl}}/api/wms/outbound/orders/{vbeln}/start-picking
```

### 释放未开始分配

```text
POST {{localUrl}}/api/wms/outbound/allocations/{allocationNo}/release
```

仅：

```text
Allocation = ACTIVE
FirstPickTime IS NULL
```

允许。

### 扫箱

```text
POST {{localUrl}}/api/wms/outbound/pick-batches/{pickBatchNo}/scan
```

### 单箱异常退回

```text
POST {{localUrl}}/api/wms/outbound/pick-details/{detailId}/return
```

### 补箱

```text
POST {{localUrl}}/api/wms/outbound/pick-batches/{pickBatchNo}/replace
```

### ReturnToStock

```text
POST {{localUrl}}/api/wms/outbound/returned/{huCode}/putaway
```

### Hold / Release

```text
POST {{localUrl}}/api/wms/hus/{huCode}/hold
POST {{localUrl}}/api/wms/hus/{huCode}/release
```

---

## 20. 现有阶段验证代码的正式迁移

当前已验证：

```text
GET  /outbound/queue
POST /outbound/pick-next
POST /outbound/plan
POST /outbound/pick-batch
```

这些接口完成了模型验证，但正式出库阶段需要调整。

### 保留

- ADO.NET 事务模式；
- CurrentLocation；
- MoveLog；
- OperationLog；
- RequestId 幂等；
- OUTBOUND-STAGING；
- PickBatch / PickBatchDetail 历史表；
- FIFO 选择框架。

### 废弃/替换

```text
MIN(WMS_HU_Item.CreateTime)
```

替换为：

```text
MIN(WMS_HU_Item.WeekSortKey)
```

```text
AllowOverPick
OVERPICK
```

删除业务入口。

```text
直接 PickBatch(requiredQty)
```

替换为：

```text
SAP Order
→ Preview
→ StartPicking
→ Reservation
→ Scan
```

### PickNext

`PickNext` 可继续保留为内部测试/诊断能力，但不能作为生产出库主入口。

---

## 21. 数据库脚本

本方案提供：

```text
010_WeekCodeAndHuIdentifier.sql
011_OutboundOrder.sql
012_OutboundReservationAndPick.sql
013_OutboundExceptionAndHold.sql
```

执行顺序严格按照编号。

### 010

- 扩展 WMS_HU_Item 周码标准字段；
- 创建 WMS_HU_Identifier；
- 建立相关索引与唯一约束。

### 011

- 创建 WMS_OutboundOrder；
- 创建 WMS_OutboundOrderLine；
- 创建 WMS_OutboundOrderReceiveLog。

### 012

- 创建 WMS_OutboundAllocation；
- 创建 WMS_HU_Reservation；
- 升级已有 WMS_OutboundPickBatch；
- 升级已有 WMS_OutboundPickBatchDetail；
- 创建 WMS_OutboundPickDetailItem。

### 013

- 创建 RETURNED 虚拟货位；
- 创建 WMS_HU_HoldLog；
- 创建 WMS_OutboundPickException。

---

## 22. 下一开发顺序

建议后续按以下顺序进入 C#：

```text
1. WeekCode Resolver + HU Item 入仓周码固化
2. HU Identifier
3. SAP OutboundOrder Receive
4. Order Query
5. Order Preview Allocation
6. StartPicking + Reservation
7. Reservation 3小时释放
8. PDA Pick Scan
9. Box Exception Return
10. Hold / Release
11. Replacement
12. ReturnToStock
13. SHIPPED
14. SAP-API 模块
15. 外箱滚码 / 客户标签
```

其中第 1~12 项全部可以在不依赖 SAP 最终过账接口的情况下完成 WMS 内部业务闭环。
