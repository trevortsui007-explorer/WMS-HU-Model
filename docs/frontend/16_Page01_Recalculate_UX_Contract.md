# Page01 Recalculate UX Contract

## Fixed responsibility boundary

```text
Grid
  → read-only Preview Drawer
  → scope Confirm
  → iframe Result Modal executes once
  → Modal renders final evidence
  → Modal remains open for review
  → user closes Modal
  → parent Grid refreshes
```

Drawer only previews current Audit evidence. Modal owns execution, loading, errors, final result and RequestId traceability. The parent owns the existing Grid and its filter/page state. Foundation owns Drawer/Modal/Grid lifecycle mechanics; Page01 owns WeekCode/Hold result semantics.

## Preview contract

Preview must answer why the selected box is anomalous, how it is currently parsed, what candidate the Audit API currently reports, and what scope will execute. It reuses `Wms.AuditDiffCell` and never implements a client WeekCode resolver.

Candidate completeness affects only preview wording. Recalculate eligibility is:

`permission visible AND plantCode present AND materialCode present AND materialRevision present`

This allows a stale `RULE_MISSING` Audit row to execute after MES configuration has been corrected.

## Execution contract

Confirm text identifies the full Plant + Material + Revision scope and the execution-time MES source of truth. The iframe receives scope/operator/requestId only. Its camelCase POST allowlist is:

```text
plantCode
materialCode
materialRevision
operator
requestId
```

Week rule, candidate and calculated week values are forbidden request fields. `executionStarted` guards the one automatic POST. There is no Modal retry path.

## Result contract

The renderer consumes the real V1.2 result DTO:

- Result: plantCode, materialCode, materialRevision, requestId, ruleSource, ruleValue, boxCount, updatedBoxCount, unchangedBoxCount, failedBoxCount, updatedItemCount, releasedHoldCount, items.
- Box: boxHUId, boxCode, result, beforeIsHold, afterIsHold, updatedItemCount, releasedWeekCodeHold, errorCode, errorMessage, itemResults.
- Item: dateCode and old/new WeekRuleSource, WeekRuleValue, WeekFormatCode, WeekYear, WeekNo, WeekSortKey.

Presentation state is derived only for UI:

- failed boxes plus updated/unchanged boxes: `PARTIAL / 部分箱码处理完成`.
- all returned boxes failed: `FAILED / 重新计算未完成`.
- updated boxes and no failures: `SUCCESS / 周码重新计算完成`.
- no updates and no failures: `UNCHANGED / 检查完成，无需更新`.

Only `releasedWeekCodeHold=true` authorizes the release label. A remaining Hold is explicitly preserved and never described as released.

## Parent/Modal lifecycle

The parent attaches a one-shot completion callback to the actual iframe DOM element in `Wms.Modal.onOpen`. The child sequence is fixed:

1. POST resolves successfully, including partial box success.
2. Modal renders scope, result status, summary, boxes, errors, Before → After, Hold evidence and resolved-row explanation.
3. Child calls the parent callback with `refreshRequired` and the result-specific close-button text; it does not refresh immediately.
4. The Modal remains readable until the user closes it.
5. On close, the parent calls the existing `grid.refresh()` once only for SUCCESS/PARTIAL.
6. Grid preserves current filters/page; Grid data change keeps Preview Drawer closed.

UNCHANGED, known business failures, unknown business failures and network/server unknown outcomes set `refreshRequired=false`. Closing a pending Modal does not cancel a request already sent and does not refresh Grid.

## Business Message Hierarchy

The first screen answers result, action performed, WMS data impact and suggested next action. Plant, Material, Revision and MES周期格式 are explicit business fields; the slash-compressed scope and “执行后返回” placeholder are forbidden.

SUCCESS, PARTIAL, UNCHANGED and FAILED are presentation states only. Business title/message/action and impact statements appear before counts, box evidence or technical codes. SUCCESS/PARTIAL keep compact counts and box detail; a pre-execution failure does not render an empty result grid.

## Technical Detail

Technical detail is collapsed by default. It contains one copyable execution number, ErrorCode, raw MES rule source and preserved backend message. It is not a raw JSON dump and must never expose connection strings, stack traces, authorization values or cookies.

RequestId is not repeated in the business header. Known codes remain available for Page07 traceability without becoming the primary business message.

## Known Business Failures

Real backend scanning confirms the pre-execution request codes `WEEK_RULE_NOT_FOUND` and `WEEK_RULE_AMBIGUOUS`. Both occur before inventory reads/writes, so their UI may state that recalculation was not executed, WMS week data was not modified and no WeekCode Hold was released.

`WEEK_RULE_NOT_FOUND` displays “无法重新计算周码”, MES周期格式 “未维护”, explains the missing MES rule and recommends maintaining “周期格式”. The raw code/message remain unchanged in technical detail.

Real HTTP-200 box codes are `WEEK_RULE_MISSING`, `MISSING_DATECODE`, `INVALID_DATECODE`, `WEEK_ANCHOR_REQUIRED`, `UNSUPPORTED_WEEK_RULE`, `INVALID_WEEK`, `PERSISTENCE_FAILED` and fallback `PARSE_FAILED`. They are rendered as box-level business reasons; `PERSISTENCE_FAILED` remains a box failure inside a known aggregate result and is not converted into a network failure page.

## Unknown Outcome and Network Failure Semantics

Unknown business errors preserve the backend message, recommend contacting the WMS administrator with the execution number, and do not invent an “unknown error” label or no-write guarantee.

Network failure, timeout, aborted connection, session loss and proxy failure use “无法确认重新计算结果”. Generic server errors use “重新计算发生系统异常”. Both explicitly state that partial processing cannot be confirmed, recommend list/log review, and warn against immediate duplicate submission. They never claim WMS data was not modified.

## Refresh Button Semantics

The Page01 Modal hides the duplicate Foundation secondary button for this view and uses one primary action:

- SUCCESS: `关闭并刷新列表`, then `grid.refresh()` on close.
- PARTIAL: `关闭并刷新列表`, then `grid.refresh()` on close.
- UNCHANGED: `关闭`, no refresh.
- FAILED/unknown outcome: `关闭`, no refresh.

Refresh is derived from the actual result, never from a checkbox. The view contains no “确认并关闭窗口” control and no retry action.

## Removed legacy contract

Page01 no longer has snapshotAuditScope, direct parent POST, ResultPanel/Result Drawer, `RECALCULATE_RESULT` Drawer context, post-refresh anomaly trace, or Last Result state. Result history remains traceable through RequestId and Page07 Audit Log Center.

## Verification contract

`wms-page01-recalculate-ux-harness.js` covers R01–R17. `wms-page01-business-readability-harness.js` executes known/network/server/unknown message mappings and checks business hierarchy, technical collapse, button semantics and compact failure layout. `wms-page01-parent-modal-integration-harness.js` covers Preview close, Modal open/execute/render, result-specific refresh, Drawer closed state, Modal persistence and close callback.

Harness results are not Human UI evidence. No real POST may be executed by Codex; Human Runtime remains the acceptance gate.
