# WMS Drawer Lifecycle Contract

## 1. Purpose and scope

`Wms.SideDrawer` is the current-iframe read-only detail surface. It is not a second page, Modal replacement, or global `top.$` component. Phase 1B covers Page 01 audit preview, Page 02 HU preview, Page 04 receipt detail, and Page 07 audit detail/request trace. Page 01 Recalculate Result remains a Drawer compatibility exception pending Phase 1C Modal migration.

```text
EXPLICIT_USER_INTENT_REQUIRED=True
PAGING_CLOSES_DRAWER=True
GRID_RELOAD_CLOSES_DRAWER=True
PROGRAMMATIC_SELECTION_CAN_OPEN_DRAWER=False
```

## 2. Foundation API

Static `Wms.SideDrawer` API:

- `open(options)`
- `close(id)`
- `setContent(id, content)`
- `setLoading(id, flag)`
- `destroy(id)`

Instance lifecycle API:

- `setContent(content, trustedHtml)`
- `setLoading(flag)` / `setError(message)`
- `close(force)` / `destroy()`
- `isOpen()`
- `getContext()` / `setContext(context)`
- `captureContext()` / `isContextCurrent(token)`

The equivalent state machine is `CLOSED → OPENING → OPEN → CLOSING → CLOSED`. `destroy()` removes DOM/events immediately; normal close may retain the 180ms close animation.

## 3. Explicit user intent

`Wms.Grid` keeps legacy `onRowClick` for selection consumers and adds `onUserRowClick` for detail navigation. A left-button mousedown on a non-interactive jfGrid data cell creates a one-shot intent token. The next jfGrid `onSelectRow` consumes it. `refreshdata`, reload, page changes, auto-selection, or other programmatic selection have no token and cannot call `onUserRowClick`.

Page-level `userRowIntentUntil` timing patches are prohibited. Interactive elements such as buttons and links are excluded from the row token and retain their own explicit click action.

## 4. Grid data lifecycle

`Wms.Grid.onBeforeDataChange(reason, grid)` runs for:

- `reload` — Query, filter refresh, tab reload
- `clear` — Reset without request
- `page` — Pager navigation
- `refresh` — current-page refresh

Page 02/04/07 always destroy the active Drawer in this callback. Page 01 does the same for Audit Preview; the `RECALCULATE_RESULT + refresh` exception preserves the existing Result Drawer until Phase 1C.

## 5. Async context guard

Every open carries `{ type, key }`. The caller captures a context token before starting detail I/O and applies success/error only when `drawer.isContextCurrent(token)` remains true. Opening B invalidates A; closing invalidates the active revision and clears context. A late response cannot reopen or render into a closed/replaced Drawer.

Loading and error belong to the Drawer. Success replaces local content and clears loading. Business/network errors leave the Drawer open with a local error state. A not-found response should use `记录已不存在或状态已变化` when the API exposes that distinction; current generic adapters preserve their existing server message.

## 6. Close, overlay and destroy

- X, overlay and existing ESC support use the same close path.
- Close clears context, invalidates pending responses, unbinds namespaced events, removes overlay and restores prior focus.
- Page iframe unload calls page `closeDrawer`, component `destroy`, and Grid destroy.
- Detail navigation to Page 03 closes the originating Drawer after `frameTab.open`.
- Page 07 Request Trace replaces the existing `auditCenterDrawer` context; it does not create a nested Drawer or navigation stack.

## 7. Usage matrix

| Page | Drawer used | Purpose | Open trigger | Close trigger | Grid related | Risk |
|---|---|---|---|---|---|---|
| 01 WeekCode Audit | Yes | Audit Preview; deferred Recalculate Result | user row click; recalculate success | data change/X/overlay/ESC/unload; Result refresh exception | Yes | Result Drawer conflicts with Phase 1B reload rule |
| 02 Finished Goods Inventory | Yes | HU quick preview | `onUserRowClick` | query/reset/page/reload/navigation/X/overlay/ESC/unload | Yes | async A/B and close-during-load guarded |
| 03 HU Detail | No | Full page detail/history | None | None | No | None |
| 04 Finished Goods Inbound | Yes | Receipt detail | `onUserRowClick` | query/reset/page/reload/navigation/X/overlay/ESC/unload | Yes | async A/B and close-during-load guarded |
| 05 Master Data | No | CRUD uses `Wms.Modal` | explicit CRUD buttons | Modal lifecycle | Grid selection only | Must not be migrated into Drawer |
| 06 Monitoring | No | Dashboard/monitoring | None | None | No | None |
| 07 Audit Log | Yes | Operation/Move/Hold detail and Request Trace | `onUserRowClick` or explicit RequestId | query/reset/page/tab/reload/navigation/X/overlay/ESC/unload | Yes | trace async context guarded; no nested Drawer |

All four product Drawer uses select Foundation `size:"large"`; no cross-page width inconsistency was found.

## 8. Verification

`agents/wms-drawer-lifecycle-harness.js` covers D01–D12. `agents/wms-drawer-page-integration-harness.js` reads the real Page 01/02/04/07 sources and checks user-intent binding, data-change close, contexts, stale guards and removal of page timing patches.

Human Runtime must use fresh Learun iframes after disabling cache. Order: Page 02, Page 04, Page 07, Page 01 Audit Preview. Page 01 Recalculate Result is not accepted as final UX in this phase.

```text
PAGE01_RESULT_DRAWER_LIFECYCLE_CONFLICT=True
PAGE01_RECALCULATE_MODAL_MIGRATION_PENDING=True
WMS_FOUNDATION_DRAWER_DEBT_CLOSED=PENDING_HUMAN_RUNTIME_GATE
```
