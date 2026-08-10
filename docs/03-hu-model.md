# 03 - HU Model

## HU 层级

```text
PALLET
└─ OUTER_BOX
   └─ INNER_PACK
```

## 当前规则
允许：
- PALLET → OUTER_BOX
- OUTER_BOX → INNER_PACK

禁止跨层直接挂载以及反向挂载。

## HU_Relation
包装关系使用有效期模型：
- EffectiveFrom
- EffectiveTo
- IsActive

关系变更时关闭旧关系、新增新关系，不直接覆盖历史。

## HU_Item
当前库存明细主要挂在 INNER_PACK。

## OperationLog
用于记录 HU 全生命周期操作：
- CREATE_HU
- ATTACH_HU
- DETACH_HU
- ADD_ITEM
- MOVE
- HOLD
- RELEASE
- CLOSE
- VOID
