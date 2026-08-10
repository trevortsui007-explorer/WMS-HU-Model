# WMS-HU-Model

基于 **C# / .NET Framework 4.7.2 / Nancy / SQL Server** 构建的成品仓 WMS 核心模型验证项目。

当前项目重点围绕 **HU（Handling Unit）模型、仓库货位、HU 层级关系、HU 位置管理及移动轨迹** 进行设计与实现，用于验证成品仓从现有包装管理逐步演进到 WMS 管理模式的可行性。

---

## 1. 项目目标

本项目用于构建 PCB 成品仓 WMS 的核心基础能力。

当前设计目标：

- 使用 HU 模型统一管理托盘、外箱、内包
- 管理 HU 之间的包装层级关系
- 管理 HU 内部实际库存内容
- 管理成品仓物理仓库、区域及货位
- 管理 HU 当前所在位置
- 保存 HU 完整位置移动历史
- 保存 HU 生命周期及业务操作日志
- 为后续上架、移库、拆箱、合箱、拣货、出库、SAP 对接提供统一基础模型

WMS 负责仓库现场执行，SAP 继续保留库存账务及相关业务账。

---

## 2. 技术栈

| 技术 | 版本 / 说明 |
|---|---|
| C# | .NET Framework |
| .NET Framework | 4.7.2 |
| Web Framework | Nancy 2.0 |
| Host | ASP.NET / IIS Express |
| Database | Microsoft SQL Server |
| 数据访问 | System.Data.SqlClient |
| API Test | Postman |
| IDE | Visual Studio 2022 |

---

## 3. Solution 结构

当前解决方案主要包含两个项目：

```text
WMS-HU-Model
│
├─ WMS
│  ├─ Modules
│  │  ├─ HealthModule.cs
│  │  ├─ LocationModule.cs
│  │  ├─ HuModule.cs
│  │  └─ HuLocationModule.cs
│  │
│  └─ Web.config
│
└─ WMS-HU-Model
   ├─ Infrastructure
   │  └─ Database
   │     └─ DbConnectionFactory.cs
   │
   ├─ Models
   │  ├─ Common
   │  ├─ MasterData
   │  └─ HU
   │
   ├─ Repositories
   ├─ Services
   └─ Sql
```

### WMS

Nancy Web API 宿主项目。

主要负责：

- HTTP 请求接收
- Route / Query / Body 参数转换
- Request Model 绑定
- 调用 Service
- 返回统一 API Result

Nancy Module 不直接处理复杂业务逻辑，也不直接编写数据库操作。

### WMS-HU-Model

核心业务项目。

主要负责：

```text
Models
    ↓
Services
    ↓
Repositories
    ↓
SQL Server
```

职责包括：

- WMS 领域模型
- HU 业务规则
- 仓储位置规则
- 数据访问
- 数据库事务
- 操作日志
- 移动历史

---

## 4. 核心模型

### 4.1 HU 层级

当前支持三种 HU 类型：

```text
PALLET
  │
  └─ OUTER_BOX
       │
       └─ INNER_PACK
```

| HUType | 说明 |
|---|---|
| PALLET | 托盘，主要作为运输及收货载体 |
| OUTER_BOX | 外包装箱，成品仓主要仓储管理单位 |
| INNER_PACK | PCB 内包装 |

允许的包装关系：

```text
PALLET → OUTER_BOX
OUTER_BOX → INNER_PACK
```

当前禁止：

```text
PALLET → INNER_PACK
OUTER_BOX → PALLET
INNER_PACK → OUTER_BOX
```

---

## 5. HU 核心数据表

### WMS_HU

HU 主数据。

主要保存：

- HUCode
- HUType
- PlantCode
- PackageSpec
- LifecycleStatus
- IsHold
- SourceSystem
- SourceBusinessNo

HU 表只表示 HU 本身身份，不直接保存货位。

### WMS_HU_Relation

HU 包装关系及关系历史。

例如：

```text
P001
└─ B001
   └─ I001
```

对应：

```text
P001 → B001
B001 → I001
```

关系变化时不直接覆盖历史。

旧关系：

```text
IsActive = 0
EffectiveTo = ...
```

新关系重新 INSERT。

一个 Child HU 同一时间只允许存在一个有效 Parent HU。

### WMS_HU_Item

HU 内部实际库存内容。

当前库存明细主要挂在：

```text
INNER_PACK
```

例如：

```text
I001
└─ MaterialCode = 14A890336C
   ProductionOrderNo = 1000216328
   BatchNo = TEST-BATCH-001
   Qty = 100 PCS
```

---

## 6. 仓库及货位模型

当前基础模型包括：

```text
WMS_Warehouse
WMS_WarehousePlant
WMS_Zone
WMS_Location
WMS_PackageSpec
```

### Warehouse

表示物理仓库。

当前测试仓：

```text
FG01
```

### WarehousePlant

表示物理仓库允许存储的厂别。

当前测试数据：

```text
1010
1020
9010
```

一个物理成品仓可以同时服务多个 Plant，因此 Warehouse 与 Plant 是两个不同维度。

### Zone

当前包括：

```text
STORAGE
RECEIVE
RETURN
STAGING
SYSTEM
```

### Location

当前使用实际可维护粒度的货位编码。

例如：

```text
H201
H202
```

暂不细分：

```text
H201-01
H201-02
```

数据库通过 `ParentLocationId` / `LocationLevel` 预留未来扩展能力。

---

## 7. 虚拟货位

系统使用虚拟货位表达业务节点。

当前包括：

```text
INBOUND-PENDING
RETURN-PENDING
OUTBOUND-STAGING
UNLOCATED
```

例如：

```text
外部包装区
    ↓
INBOUND-PENDING
    ↓
H201
```

位置未知时不使用 `Location = NULL`，而使用：

```text
UNLOCATED
```

保证库存位置状态始终明确。

---

## 8. HU 当前位置

### WMS_HU_CurrentLocation

保存 HU 当前直接所在位置。

一个 HU 同一时间只允许一个直接位置，因此 `HUId` 直接作为主键。

例如：

```text
B001 → H201
```

---

## 9. DirectLocation 与 EffectiveLocation

系统区分：

```text
DirectLocation
EffectiveLocation
```

### DirectLocation

HU 自己直接占据的货位。

### EffectiveLocation

HU 实际所在位置。

例如：

```text
P001 → INBOUND-PENDING

P001
└─ B001
   └─ I001
```

此时：

```text
P001
DirectLocation    = INBOUND-PENDING
EffectiveLocation = INBOUND-PENDING
```

B001：

```text
DirectLocation    = NULL
EffectiveLocation = INBOUND-PENDING
LocationSourceHU  = P001
```

I001：

```text
DirectLocation    = NULL
EffectiveLocation = INBOUND-PENDING
LocationSourceHU  = P001
```

子 HU 默认继承有效父 HU 的位置。

---

## 10. MoveLog

### WMS_HU_MoveLog

记录 HU 的位置移动历史。

核心职责：

> HU 从哪里移动到了哪里。

例如：

```text
NULL → INBOUND-PENDING
INBOUND-PENDING → H201
H201 → H202
H202 → OUTBOUND-STAGING
```

主要字段：

```text
MoveNo
HUId
FromLocationId
ToLocationId
MoveType
ScannedLocationCode
BusinessNo
TaskNo
RequestId
ReasonCode
Operator
MoveTime
Remark
```

MoveLog 只 INSERT：

```text
禁止 UPDATE 历史移动记录
禁止 DELETE 正常移动记录
```

---

## 11. OperationLog

### WMS_HU_OperationLog

记录 HU 生命周期中的业务操作。

例如：

```text
CREATE_HU
ATTACH_HU
DETACH_HU
ADD_ITEM
MOVE
HOLD
RELEASE
CLOSE
VOID
```

区别：

```text
OperationLog = HU 做过什么
MoveLog      = HU 在空间上怎么移动
```

所有 Move 都属于一种 HU Operation，但 ATTACH / DETACH / HOLD / RELEASE 不一定产生 MoveLog。

---

## 12. CurrentLocation / MoveLog / OperationLog

三者职责：

```text
                  HU
                   │
       ┌───────────┼───────────┐
       │           │           │
       ▼           ▼           ▼

CurrentLocation  MoveLog   OperationLog

现在在哪里      怎么走的     做过什么
```

例如一次：

```text
B001
H201 → H202
```

事务内需要：

```text
INSERT WMS_HU_MoveLog
UPDATE / INSERT WMS_HU_CurrentLocation
INSERT WMS_HU_OperationLog
```

必须同时成功或同时回滚。

---

## 13. RequestId 幂等设计

HU Move 支持 `RequestId`，用于防止：

- PDA 重复提交
- 网络重试
- Postman 重复发送
- 前端重复点击

同一个 RequestId 再次提交时：

```text
不重复执行移动
直接返回原移动结果
```

数据库层 `WMS_HU_MoveLog.RequestId` 建立唯一约束。

---

## 14. 当前支持的 MoveType

| MoveType | 说明 |
|---|---|
| RECEIVE | 收货进入 WMS |
| PUTAWAY | 上架 |
| RELOCATION | 移库 |
| RETURN | 退货移动 |
| STAGE | 移到备货 / 暂存区 |
| CORRECTION | 位置纠正 |

---

## 15. Nancy 参数编码规范

### Route 参数

禁止直接传给 Service：

```csharp
_service.Get(
    args.huCode);
```

禁止一行完成转换：

```csharp
string huCode =
    Convert.ToString(
        args.huCode);
```

统一使用两步转换：

```csharp
object rawHuCode =
    args.huCode;

string huCode =
    Convert.ToString(
        rawHuCode);

_service.Get(
    huCode);
```

### Query 参数

Query 参数同样执行两步转换：

```csharp
object rawTop =
    Request.Query["top"];

string topText =
    Convert.ToString(
        rawTop);
```

再转换成具体类型：

```csharp
int top;
int.TryParse(
    topText,
    out top);
```

### Service 以下禁止 dynamic

从 Service / Repository / Infrastructure 开始全部使用强类型：

```text
string
int
long
bool
decimal
DTO
Request
Command
OperationResult<T>
```

禁止 `dynamic`。

---

## 16. API 返回格式

成功：

```json
{
    "code": 200,
    "message": "success",
    "data": {}
}
```

失败：

```json
{
    "code": 400,
    "message": "错误信息",
    "data": null
}
```

---

## 17. 本地开发环境

推荐：

```text
Visual Studio 2022
.NET Framework 4.7.2
IIS Express
```

启动项目：

```text
WMS
```

---

## 18. 数据库

本地数据库：

```text
WMS_DEV
```

连接配置位于：

```text
WMS/Web.config
```

示例：

```xml
<connectionStrings>
  <add name="WmsDb"
       connectionString="Data Source=.;Initial Catalog=WMS_DEV;Integrated Security=True;TrustServerCertificate=True;"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

根据本机 SQL Server 实例调整 `Data Source`。

---

## 19. 数据库脚本

SQL 脚本位于：

```text
WMS-HU-Model/Sql
```

当前版本：

```text
001_Init.sql
003_CreateMasterData.sql
004_CreateHUCore.sql
005_CreateHULocation.sql
```

数据库版本通过 `WMS_SchemaVersion` 记录。

---

## 20. Postman 配置

Postman Environment：

```text
localUrl
```

当前开发环境示例：

```text
https://localhost:44375
```

所有测试地址统一使用：

```text
{{localUrl}}/api/xxx
```

---

## 21. 健康检查

```http
GET {{localUrl}}/api/health
```

```http
GET {{localUrl}}/api/health/db
```

---

## 22. Location API

```http
GET {{localUrl}}/api/wms/locations
GET {{localUrl}}/api/wms/locations?warehouseCode=FG01
GET {{localUrl}}/api/wms/locations/FG01/H201
POST {{localUrl}}/api/wms/locations
PUT {{localUrl}}/api/wms/locations/FG01/H201
```

---

## 23. HU API

创建 HU：

```http
POST {{localUrl}}/api/wms/hus
```

建立包装关系：

```http
POST {{localUrl}}/api/wms/hus/attach
```

增加库存内容：

```http
POST {{localUrl}}/api/wms/hus/I001/items
```

查询 HU Tree：

```http
GET {{localUrl}}/api/wms/hus/P001/tree
```

示例：

```text
P001
└─ B001
   └─ I001
      └─ 14A890336C / 100 PCS
```

---

## 24. HU Location API

查询位置：

```http
GET {{localUrl}}/api/wms/hus/B001/location
```

HU 移动：

```http
POST {{localUrl}}/api/wms/hus/B002/move
```

查询移动历史：

```http
GET {{localUrl}}/api/wms/hus/B002/moves
GET {{localUrl}}/api/wms/hus/B002/moves?top=20
```

---

## 25. 当前业务规则

有父 HU 时禁止独立 Move。

例如：

```text
P001
└─ B001
```

B001 的位置应继承 P001。若 B001 需要单独上架，必须先 Detach，再 Move。

后续正式上架流程将通过 Putaway Service 在一个业务事务中完成。

---

## 26. 项目开发原则

Nancy Module：

```text
参数转换
Request绑定
调用Service
返回结果
```

Service：

```text
业务规则
业务校验
领域动作
```

Repository：

```text
数据库访问
SQL
事务
锁
```

历史数据不可直接覆盖：

```text
包装关系变化：
关闭旧 Relation
新增新 Relation

位置变化：
新增 MoveLog
更新 CurrentLocation
```

一个完整领域动作涉及多表时必须使用事务。

---

## 27. 当前已完成

- [x] Nancy + IIS Express 基础环境
- [x] SQL Server 本地数据库连接
- [x] Health API
- [x] Warehouse
- [x] WarehousePlant
- [x] Zone
- [x] Location
- [x] PackageSpec 基础模型
- [x] Location CRUD
- [x] HU 主模型
- [x] HU Relation
- [x] HU Item
- [x] HU Tree
- [x] OperationLog
- [x] CurrentLocation
- [x] EffectiveLocation 继承
- [x] MoveLog
- [x] HU Move
- [x] RequestId 幂等
- [x] Move 并发位置校验

---

## 28. 下一阶段

计划实现 Putaway。

目标流程：

```text
托盘 P001 到达成品仓
        ↓
INBOUND-PENDING
        ↓
扫描 P001
        ↓
扫描 B001
        ↓
扫描目标货位 H201
        ↓
关闭 P001 → B001 Relation
        ↓
B001 → H201
        ↓
记录 MoveLog
        ↓
记录 OperationLog
```

后续继续实现：

- Detach HU
- 拆托
- 拆箱
- 合箱
- 换箱
- 上架任务
- Location Reservation
- 移库
- Hold / Release
- 盘点
- Picking
- Outbound
- SAP Posting
- SAP Interface Outbox

---

## 29. Git Commit 规范

推荐：

```text
feat(project):初始化.NET Framework 4.7.2与Nancy WMS项目基础架构
feat(master-data):实现仓库、厂别、区域、货位及包装规格基础模型
feat(hu-core):实现HU、包装层级关系、库存明细及HU树模型
feat(hu-location):实现HU当前位置及父级位置继承模型
feat(hu-movement):实现HU移动流水、当前位置更新及移动幂等控制
fix(webdav):修复IIS WebDAV导致Nancy PUT接口返回405问题
refactor(nancy):统一Nancy参数两步强类型转换规范
```

Commit 格式：

```text
type(scope):中文说明
```

---

## 30. 项目状态

当前项目处于：

```text
WMS核心模型验证 / 基础能力建设阶段
```

现阶段重点不是完整替代 SAP，而是先建立稳定的：

```text
HU
+
Relationship
+
Inventory
+
Location
+
Movement
+
Audit
```

核心领域模型。

在核心模型稳定后，再逐步接入成品仓真实业务流程及 SAP 接口。
