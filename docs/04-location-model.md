# 04 - Location Model

## 核心概念

### DirectLocation
HU 自己直接占据的货位。

### EffectiveLocation
HU 实际所在的位置；若自身无直接位置，则继承最近有效父 HU 的位置。

示例：

```text
P001 → INBOUND-PENDING
└─ B001
   └─ I001
```

B001 / I001 的 DirectLocation 为 NULL，但 EffectiveLocation 为 INBOUND-PENDING。

## CurrentLocation
回答“现在在哪里”。

## MoveLog
回答“怎么移动过来的”。

## OperationLog
回答“HU 做过什么”。

## 虚拟货位
- INBOUND-PENDING
- RETURN-PENDING
- OUTBOUND-STAGING
- UNLOCATED
