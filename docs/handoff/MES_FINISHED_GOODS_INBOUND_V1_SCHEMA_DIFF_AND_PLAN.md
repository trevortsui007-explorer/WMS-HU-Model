# Finished Goods Inbound Core V1 — WMS Schema Diff and Implementation Handoff

## Executive Summary

Implemented the WMS Finished Goods Inbound Core and the MES `PLAN_STOCK` adapter. The MES reader/repository, snapshot builder, WeekCode rule lookup, receive orchestration, and HTTP APIs are implemented; MES live-schema review and database integration verification remain deferred. No migration was executed and WMS_DEV was not modified.

Frozen WMS schema evidence supports PALLET, OUTER_BOX, INNER_PACK, active containment, inherited locations, active alternate identifiers, and all required WeekCode item fields. It does not contain a top-level inbound source fact, so migration draft `015_FinishedGoodsInboundCore.sql` adds `WMS_FinishedGoodsInboundReceipt`.

## Design-to-Schema Mapping

| Design field | Target | Current status | Gap / decision |
| --- | --- | --- | --- |
| SourceSystem, SourceType, SourceEntityId, SnapshotHash, HashVersion, ReceiveTime | `WMS_FinishedGoodsInboundReceipt` | No suitable existing entity | Required new receipt table and source-key unique index. |
| Concrete HU source identity | `WMS_HU.SourceSystem`, `HUType`, `SourceEntityId` | Fields exist, no suitable unique index | Canonical location; draft filtered unique index. Do not duplicate into identifier table. |
| Box alternate code | `WMS_HU_Identifier` | Compatible (`varchar(200)`, active global unique index) | Insert only if distinct from `HUCode`. |
| PALLET → BOX → INNER_PACK | `WMS_HU`, `WMS_HU_Relation` | Compatible | Active-child unique index prevents one child having multiple active parents. |
| Material, quantity, UOM, raw DateCode, resolved WeekCode | `WMS_HU_Item` | Compatible | `Qty decimal(18,4)`, all WeekCode fields and FIFO index exist. |
| Root inbound location | `WMS_HU_CurrentLocation` and `WMS_HU_MoveLog` | Schema compatible | `INBOUND-PENDING` master data is `RUNTIME_SCHEMA_UNVERIFIED`. |

## Source Identity and Idempotency

- `WMS_FinishedGoodsInboundReceipt` is the top-level source fact. It remains valid when `Snapshot.Pallet` is null.
- Receipt natural key: `(SourceSystem, SourceType, SourceEntityId)`.
- Hash algorithm version: `FGI_V1`; duplicate requires equal hash and equal version. A changed hash or version returns conflict.
- Concrete box/inner/pallet identity is stored once in `WMS_HU`; identifiers represent real alternate/scannable values only.
- Repository uses a serializable transaction and receipt-key `UPDLOCK, HOLDLOCK`. The receipt unique key remains the final concurrency safeguard.

## WeekCode Compatibility

`WMS_HU_Item` already has `DateCode`, `WeekFormatCode`, `WeekYear`, `WeekNo`, `WeekSortKey`, `WeekRuleSource`, `WeekRuleValue`, and `WeekResolvedTime` with range checks/index support.

Inbound Core retains the legacy resolver/provider contract and adds a source-neutral overload. It supports `YYWW`, `WWYY`, `YWW`, plus `YY-WW`, `WW-YY`, `WW/YY`, and `WW.YY` aliases. It returns explicit missing-rule, unsupported-rule, invalid-DateCode/week, and missing-anchor failures. MES rule selection is implemented through the `md_flow` chain; missing or ambiguous rules enter the exception-inbound path and retain the original DateCode without fabricating a WeekCode.

## Persistence and Logging

- With pallet: pallet receives direct `INBOUND-PENDING`; boxes and inners inherit by containment.
- Without pallet: each outer box receives direct `INBOUND-PENDING`; inners inherit.
- Direct location records are preceded by a move log because `LastMoveLogId` is required.
- Each root receives `RECEIVE`; no-pallet snapshots never select an arbitrary representative box. Every operation shares the receipt business number and request correlation. Move logs use a derived unique request key because the existing move-log request ID is unique.
- Receipt, HUs, alternate identifiers, relations, items, move logs, current locations, and operation logs are written in one transaction or entirely rolled back.

## Migration 015 Review Gate

The draft is rerunnable and does not update, normalize, delete, or otherwise repair historical HUs. It checks for pre-existing populated HU source-identity duplicates before creating the filtered unique index and throws `HUMAN_REVIEW_REQUIRED` if found.

Before any execution: human review → live WMS_DEV schema verification → `INBOUND-PENDING` master-data verification → database backup/snapshot → explicit approval. Migration execution is not authorized by this handoff.

## Deferred Scope

- `MES_SCHEMA_REVIEW=DEFERRED`：未对 MES 实库执行查询或写入。
- MES 适配仅支持 `PLAN_STOCK` 触发，未实现持续轮询、消息队列或 MES 回写。
- 未执行 WMS 实库写入测试；`INBOUND-PENDING` 位置主数据仍待人工确认。
- No real database write test was run.

## Verification

- Unit/fake tests cover structural and prepared validation, WeekCode aliases/YWW anchor, canonical hash version, duplicate/conflict orchestration, MES graph mapping, rule lookup, preview, request preparation, and receive orchestration.
- Build verification: Solution Debug and Release builds pass; console regression tests pass 36 cases.
- Recommended next order: human review migration → verify live schema/location master → backup → explicitly approve migration execution → execute once → execute a second time only for idempotency verification → run database integration tests.
