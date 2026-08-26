# WMS Seven Pages Visual Audit — Phase 2A

## 1. Executive Summary

Phase 2A is a source-based visual audit. It does not redesign or bulk-edit Page01–Page07, and it does not claim browser Runtime PASS.

- Target language: 70–80% Learun native, 20–30% WMS-scoped enhancement; dense, enterprise, square-ish and low-shadow.
- Strong existing contracts: native 28px jfGrid header/row, 31px WMS thin pager, shared 36px Drawer header, shared DetailPanel/StatusTag/AuditDiffCell, and Page01/02/04 absolute-fill guard.
- Primary baseline: Page02 for a standard list page. Page04 is a structural co-reference, but its single fixed query row must be corrected before it can be the baseline.
- Detail/CRUD/monitoring references: Page03, Page05 and Page06 remain legitimate specialized patterns; each needs visual alignment rather than conversion into a list page.
- Highest static Runtime risks: Page04 and Page07 query-area fit at 1366px; Page05 and Page06 Grid Hosts using absolute top/bottom while inheriting Foundation `height:100%` without the shared modifier.
- Human Runtime measurement at 1366×768 and 1920×1080 remains the release gate.

Alignment grades are intentionally coarse: A = aligned, B = mostly aligned, C = mixed, D = outlier.

| Page | Grade | Summary |
|---|---|---|
| Page01 WeekCode Audit | B | Native list shell and shared Drawer/Grid; result Modal has stronger typography and spacing. Last to change. |
| Page02 Finished Goods Inventory | B | Best current standard list baseline; native density, shared Grid/Drawer/Pager and controlled absolute fill. |
| Page03 HU Detail / History | C | Legitimate split detail layout, but bordered panes/history blocks create visual fragmentation. |
| Page04 Inbound Records | C | Strong list/Grid/Drawer structure; fixed one-row query content is at risk of clipping at 1366px. |
| Page05 Master Data | C | Correct CRUD specialization, but title/tabs/44px toolbar and 28/30px action buttons form a separate local shell. |
| Page06 Monitoring | C | Legitimate monitoring exception; MetricCard/trend density needs Human review and recent Grid has absolute-height risk. |
| Page07 Audit Center | C | Business hierarchy is correct; 62px card-like wrapped search/tabs shell and dense audit content need alignment. |

## 2. Learun Reference Pages

All references are current local project sources; no internet screenshots or third-party templates were used.

| Path | Purpose | Pattern adopted |
|---|---|---|
| `Areas/LR_SystemModule/Views/Module/Index.cshtml` | Standard query + Grid | `_Index`, `lr-layout`, 55px native toolbar, left query/right grouped actions, native jfGrid. |
| `Areas/LR_AuthorizeModule/Views/FilterIP/Index.cshtml` | Compact action toolbar + Grid | Native refresh/add/edit/delete grouping, square Bootstrap/Learun buttons, dense Grid body. |
| `Areas/LR_AuthorizeModule/Views/FilterIP/Form.cshtml` | CRUD Form | `_Form`, `lr-form-wrap`, `lr-form-item`, fixed label column, native validation/input treatment. |
| `Areas/MES/Views/EAP2_ApiUpload/Form_ExecutionLog.cshtml` | Query + native pager + technical detail | 5–10px padding, 28px controls, compact query spacing, native jfGrid pager and secondary raw detail. |
| `Areas/LR_NewWorkFlow/Views/NWFProcess/MonitorDetailsIndex.cshtml` | Tabs + timeline/detail | Native `lr-form-tabs`, Bootstrap tabs and `lrtimeline` with restrained borders. |

No mature native Learun side Drawer equivalent was found in these reference families. `Wms.SideDrawer` therefore remains a justified WMS-scoped enhancement; it should visually inherit Learun colors, borders and density.

## 3. Foundation Current Patterns

| Pattern | Current contract | Audit conclusion |
|---|---|---|
| Page shell | `.wms-page`, transparent background, 12px inherited font | Aligned; page padding should normally be 5px or native zero with 5–10px inner padding. |
| SearchPanel | Lifecycle wrapper plus 28px controls | Functional but lacks a thin, explicit spacing/height contract. |
| Grid | Native jfGrid surface; Foundation host/state/pager | Strong baseline. Native header and row are both 28px. Do not restyle jfGrid source. |
| Pager | 31px slot, 30px content, 25px controls | Closed contract; preserve geometry and viewport invariant. |
| StatusTag | 18–20px, 2px radius, light borders/backgrounds | Appropriate lightweight emphasis; no colorful pill wall found. |
| DetailPanel | 12px section title, 45px two-column items | Good base; page-local section/table headings still duplicate the visual contract. |
| SideDrawer | 36px header, 10px body, 39px optional footer; 360/480/640 tiers | Strong shell. Page01/02/04/07 all currently use `large`, so actual page width variants = 1. |
| Modal | Shared iframe/layer shell with medium/large usage | Two legitimate size variants; align shell/footer, not business content. |
| AuditDiffCell | Shared single-line Before → After renderer | Reused by Page01 and Page07: `AUDIT_DIFF_VISUAL_REUSE=True`. |
| Timeline / JsonViewer | Shared dense timeline and 12px monospace JSON | Correct technical-secondary intent; nested scroll height needs a shared rule. |
| MetricCard | 70px, 2px radius, no large shadow | Restrained relative to modern dashboards, but remains a Page06 exception requiring Human density review. |
| Foundation Showcase | Search/Grid/Status/Detail/Drawer/Modal/Confirm | Missing explicit HuTree, Timeline, JsonViewer, MetricCard, AuditDiff and runtime geometry examples. |

Static CSS inventory:

- `IMPORTANT_RULE_COUNT=6`: 2 justified absolute-fill rules, 3 suspicious page-layout overrides, 1 legacy layer-shell integration override.
- `INLINE_STYLE_COUNT=4`, all in Page05 Form markup.
- `CUSTOM_BUTTON_STYLE_COUNT=12` under the audit selector definition; most are link/copy/toggle affordances, not alternate button skins.
- Font sizes: 10, 11, 12, 13, 14, 15, 16, 17, 18px — 9 variants.
- Border radii: 0, 2px, 4px — 3 variants.
- Shadow declarations: 3 including `none` and Bootstrap-like focus treatment. No heavy non-Learun page shadow was found: `NON_LEARUN_SHADOW_COUNT=0`.

## 4. Visual Dimension Matrix

Values are authored/static evidence. “Runtime” means the true computed value must be captured in the Learun iframe.

| Dimension | Foundation / Learun Baseline | P01 | P02 | P03 | P04 | P05 | P06 | P07 | Recommendation |
|---|---|---|---|---|---|---|---|---|---|
| Page padding | Native 0; dense inner 5–10px | 5 | 5 | body 8 | 5 | 5 | 5 | inner 8 | Standardize shell choice, not an average. |
| Content gap | 5–10px | 5–12 | 5–12 | 8–12 | 5–12 | 4–5 | 5–10 | 5–10 | Use thin shared spacing values only. |
| Main layout | Native absolute Learun shell | Absolute list | Absolute list | Flex split detail | Absolute list/tabs | Absolute tabs/CRUD | Absolute monitoring | Flex toolbar + absolute panes | Keep legitimate page archetypes. |
| Search / toolbar height | Native 55px; compact 38px is acceptable | 55 | 58 | native 55 | 38 | 44 | 38 | 62 | Phase 2B should define standard/compact/two-row variants. |
| Control / button height | 28px controls, Learun/Bootstrap small buttons | 28 | 28 | 28 | 28 | controls 28; actions 28/30 | 28/xs activity | 28 | Remove unexplained Page05 28/30 split. |
| Tabs | Native form 44px; list tabs about 35px | N/A | N/A | native-like 35 | 35 | 36 | 35 | custom 28 | Define list/detail tab variants; preserve business tabs. |
| Grid header | Native 28px | 28 inherited | 28 | N/A tables | 28 | 28 | 28 | 28 | No jfGrid override required. |
| Grid row | Native 28px | 28 inherited | 28 | N/A tables/timeline | 28 | 28 | 28 | 28 | Static variant count = 1; verify computed Runtime. |
| Pager | Shared 31px | shared | shared | N/A | shared | not server pager | no pager | shared | Preserve closed contract and geometry probe. |
| Drawer | 36px header / 10px body / large 640 | large | large | N/A | large | N/A | N/A | large | Actual width variant count = 1; no inconsistency. |
| Modal | Shared medium/large shell | large result | N/A | N/A | N/A | medium CRUD | N/A | N/A | Keep 2 semantic sizes; align shell only. |
| Detail section | 12px title, 1px separator, 45px item | shared + custom preview | shared + table | custom blocks/timeline | shared + table | form | tables/issues | shared + technical collapse | Promote a thin section contract. |
| Status | 18–20px light tag | shared | shared | shared | shared | shared | shared | shared | Keep current tone semantics. |
| Typography | 12px body, restrained 11/14 hierarchy | up to 18 | mostly 12/14 | 12/17 code | 12/14 | 12/15 | 10–18 metrics | 12/14 | Limit display sizes by archetype; do not add a large token system. |
| Border radius | 0/2/4px | 2/4 inherited | inherited | inherited | inherited | inherited | 2 inherited | 0/2 inherited | Square-ish baseline; no large pill/radius. |
| Shadow | Native low/no shadow | none local | none local | none local | none local | none local | none local | none local | Drawer/focus shadows remain the only scoped cases. |
| Scroll | Grid/Drawer owns scrolling | Grid + Drawer/Modal | Grid + Drawer | tree + tab content | Grid + Drawer/table | Grid + Modal | pane + Grid | Grid + Drawer/JSON | Avoid adding another body scroll owner. |
| 1366 iframe fit | Must be measured | likely | likely | likely | search risk | Grid height risk | Grid height risk | search risk | Run the read-only probe before Phase 2B approval. |

## 5. Page-by-Page Findings

### Page01 — B, Mostly Aligned

- Native 55px toolbar, native-density Grid, shared 31px Pager and large Drawer.
- `wms-grid-absolute-fill` correctly controls the proven height conflict.
- Recalculate Modal uses 17/18px headings/numbers, 12–16px padding and several nested result blocks. This is readable but visually stronger than Learun list/detail pages.
- Page01 is `LAST_TO_CHANGE=True`; Phase 2C may only adjust spacing/typography after other baselines pass.

### Page02 — B, Mostly Aligned

- Best standard list candidate: 5px shell, 28px controls, inherited 28px Grid rows, shared Pager and Drawer.
- 58px search host is close to native 55px and accommodates a deliberate advanced-filter collapse.
- Keep Page02 as Phase 2B list reference; use Page04 as a co-reference only after its query fit is corrected.

### Page03 — C, Mixed

- Legitimate 300px tree + flexible detail split; native `lrtree`, tabs, Timeline and JsonViewer are preserved.
- Background panel, borders, history cards, tables and notes create more fragmentation than the native workflow detail reference.
- Scroll ownership is explicit, but Page03 needs a shared detail-section hierarchy before local polish.

### Page04 — C, Mixed

- Grid/Drawer/Pager structure is otherwise a strong standard list implementation.
- Twelve query controls/actions are authored inside one fixed 37px nowrap row with hidden overflow. At 1366px, later technical filters can be clipped rather than intentionally collapsed.
- Do not increase toolbar height ad hoc in Phase 2A; Phase 2B must select the accepted compact/two-row search pattern.

### Page05 — C, Mixed

- Correct master-data archetype with Warehouse/Mapping/Location tabs and medium CRUD Modal.
- Local shell uses title 34 + tabs 36 + toolbar 44 and extensive absolute positioning. Left buttons are 28px while right action buttons are 30px.
- Three Grid Hosts use absolute top/bottom without `wms-grid-absolute-fill`; this repeats the proven structural risk even though Page05 has no thin server pager.
- Four inline styles and one page `!important` should be reduced only during Page05 rollout, not through a broad Foundation override.

### Page06 — C, Mixed / Monitoring Exception

- Metric cards remain restrained: 70px, 2px radius, no large shadow. They are a legitimate monitoring exception, not a new global card pattern.
- At widths below 1100px, metrics reflow to two rows and content top changes; Human should verify density and transition behavior.
- `recentReceiptGrid` is an absolute top/bottom Grid Host without the height-auto modifier and requires Runtime geometry measurement.

### Page07 — C, Mixed

- Shared Grid/Pager/Drawer/DetailPanel/Timeline/JsonViewer/AuditDiff are correctly reused; technical evidence remains secondary.
- The fixed 62px, bordered search panel combines wrapped labeled filters, actions and tabs. Static width indicates possible overlap/clipping at 1366px.
- Two layout `!important` rules override Learun shell geometry. They are suspicious, not automatically wrong; Phase 2C should remove them only if the accepted two-row shell makes them unnecessary.

## 6. Absolute Layout Risk Matrix

| Page | Absolute structure | Height conflict status | Classification |
|---|---|---|---|
| P01 | Grid top/right/bottom/left | Controlled by `wms-grid-absolute-fill` | Justified runtime fix |
| P02 | Grid top/right/bottom/left | Controlled by `wms-grid-absolute-fill` | Justified runtime fix |
| P03 | Tree and tab content absolute inside bounded panes | No Foundation Grid Host collision | Legitimate detail layout |
| P04 | Grid top/right/bottom/left | Controlled by `wms-grid-absolute-fill` | Justified runtime fix |
| P05 | Three Grid Hosts top/right/bottom/left | Uncontrolled inherited `height:100%` risk | Runtime layout risk |
| P06 | Recent activity Grid Host top/right/bottom/left | Uncontrolled inherited `height:100%` risk | Runtime layout risk |
| P07 | Pane is absolute; Grid child is height 100% | No same-element top/bottom + height conflict found | Flex/absolute shell still needs Runtime fit check |

`ABSOLUTE_HEIGHT_CONFLICT_RISK_PAGES=[Page05, Page06]`. This is a static risk finding, not a claim that Human Runtime has failed.

## 7. Scroll Ownership

| Page | Page scroll owner | Grid/content scroll owner | Drawer scroll owner | Modal scroll owner |
|---|---|---|---|---|
| P01 | none; shell hidden | jfGrid | `.wms-drawer-body` | result iframe `.wms-recalculate-result`; result list is bounded nested detail |
| P02 | none | jfGrid | `.wms-drawer-body` | N/A |
| P03 | none | `.wms-hu-tree` and `.wms-hu-tab-content` | N/A | N/A |
| P04 | none | jfGrid | `.wms-drawer-body`; bounded drawer table | N/A |
| P05 | none | jfGrid | N/A | Wms.Modal iframe / Learun form body |
| P06 | none | active monitoring pane; recent jfGrid | N/A | N/A |
| P07 | none | jfGrid | `.wms-drawer-body`; JsonViewer is bounded technical scroll | N/A |

No page has `UNKNOWN` scroll ownership. Phase 2B/C must not add iframe body scrolling on top of these owners.

## 8. Runtime Acceptance Contract

Run `agents/wms-visual-runtime-probe.js` manually in the active WMS iframe console at 1366×768 first, then 1920×1080. The probe is read-only: it does not click, query, open a Drawer/Modal or perform network calls.

Required invariants:

1. `pageRect.top >= 0`, `pageRect.right <= iframe.innerWidth`, `pageRect.bottom <= iframe.innerHeight`.
2. Every visible Pager satisfies `pagerRect.top >= 0` and `pagerRect.bottom <= iframe.innerHeight`.
3. Toolbar/search precedes Grid; Grid does not cover search or Pager.
4. `documentElement.scrollWidth <= iframe.innerWidth + 1`; no body horizontal overflow.
5. An explicitly opened Drawer fits the iframe and its body owns vertical scroll.
6. Page01/Page05 Modal footer remains reachable without changing business lifecycle.
7. Runtime values use `window.innerWidth/innerHeight` inside the iframe, never `window.top.innerHeight`.

`VISUAL_STATIC_AUDIT_PASS` proves only source structure and audit invariants. It does not imply `VISUAL_RUNTIME_PASS`.

## 9. Visual Debt Register

| ID | Page | Component / issue | Classification | Severity | Foundation candidate | Runtime risk | Phase |
|---|---|---|---|---|---|---|---|
| V001 | P04 | Fixed nowrap query row can clip late filters at 1366px | RUNTIME_LAYOUT_RISK | HIGH | Search shell | Yes | 2C V1 |
| V002 | P07 | Fixed 62px wrapped filters/actions/tabs can overlap or clip | RUNTIME_LAYOUT_RISK | HIGH | Search shell | Yes | 2C V2 |
| V003 | P05 | Absolute Grid Hosts inherit height 100% without modifier | RUNTIME_LAYOUT_RISK | HIGH | Existing absolute-fill rule | Yes | 2C V3 |
| V004 | P06 | Recent activity Grid Host inherits height 100% without modifier | RUNTIME_LAYOUT_RISK | HIGH | Existing absolute-fill rule | Yes | 2C V3 |
| V005 | P06 | Two-row metric reflow below 1100px changes content geometry | RUNTIME_LAYOUT_RISK | MEDIUM | No | Yes | 2C V3 |
| V006 | Shared | SearchPanel has no explicit standard/compact/two-row spacing contract | FOUNDATION_GAP | MEDIUM | Yes | Indirect | 2B |
| V007 | P03/P04/P05/P06/P07 | Tabs repeat page-local height/padding rules | FOUNDATION_GAP | MEDIUM | Yes | No | 2B |
| V008 | P02/P03/P04/P07 | Detail section/table headings repeat similar borders and spacing | FOUNDATION_GAP | MEDIUM | Yes | No | 2B |
| V009 | P01/P07 | Technical-detail collapse uses separate local shells | FOUNDATION_GAP | LOW | Yes | No | 2B |
| V010 | P03/P07 | JsonViewer inside page/Drawer can create nested technical scrolling | FOUNDATION_GAP | MEDIUM | Yes | Yes | 2B |
| V011 | Shared | Nine authored font-size variants lack an archetype-level hierarchy | FOUNDATION_GAP | LOW | Thin typography values | No | 2B |
| V012 | Showcase | Missing HuTree/Timeline/JsonViewer/MetricCard/AuditDiff/runtime geometry examples | FOUNDATION_GAP | LOW | Showcase | No | 2B |
| V013 | Shared | Toolbar action order and query/reset grouping are convention only | FOUNDATION_GAP | MEDIUM | Yes | No | 2B |
| V014 | P01 | Result Modal heading/summary sizes are stronger than Learun detail pages | PAGE_LOCAL_DEVIATION | MEDIUM | No | No | 2C V4 |
| V015 | P02 | 58px advanced search is slightly outside native 55px shell | PAGE_LOCAL_DEVIATION | LOW | No | No | 2C V1 |
| V016 | P03 | Multiple bordered history/table/note blocks fragment the detail hierarchy | PAGE_LOCAL_DEVIATION | MEDIUM | Detail section first | No | 2C V2 |
| V017 | P05 | Title + tabs + 44px toolbar consumes more vertical space than native CRUD list | PAGE_LOCAL_DEVIATION | MEDIUM | No | Yes | 2C V3 |
| V018 | P05 | Left query buttons are 28px; right action buttons are 30px | PAGE_LOCAL_DEVIATION | MEDIUM | Button convention | No | 2C V3 |
| V019 | P05 | Four inline styles and one layout `!important` increase local specificity | PAGE_LOCAL_DEVIATION | LOW | No | Indirect | 2C V3 |
| V020 | P07 | Bordered white SearchPanel reads more like a card than native toolbar | PAGE_LOCAL_DEVIATION | MEDIUM | Search shell first | No | 2C V2 |
| V021 | P07 | Fixed four-column capability row is rigid at narrow widths | PAGE_LOCAL_DEVIATION | LOW | No | Yes | 2C V2 |
| V022 | P03 | Tree + detail split is required for HU hierarchy | LEGITIMATE_BUSINESS_EXCEPTION | MEDIUM | No | No | Preserve |
| V023 | P04 | Record/exception tabs and scope hint are required list context | LEGITIMATE_BUSINESS_EXCEPTION | LOW | No | No | Preserve |
| V024 | P06 | Metric/trend/issues layout is a monitoring-specific pattern | LEGITIMATE_BUSINESS_EXCEPTION | MEDIUM | No global card pattern | No | Preserve |
| V025 | P07 | Dense raw evidence and Request Trace exceed normal business-page density | LEGITIMATE_BUSINESS_EXCEPTION | MEDIUM | Shared technical shell only | No | Preserve |
| V026 | Shared | Learun V7 absolute shells, inline-era forms and square pager constrain cleanup | LEGACY_LEARUN_CONSTRAINT | LOW | No | Indirect | Document |

Register totals:

- `FOUNDATION_GAP_COUNT=8`
- `PAGE_LOCAL_DEVIATION_COUNT=8`
- `LEGITIMATE_BUSINESS_EXCEPTION_COUNT=4`
- `RUNTIME_LAYOUT_RISK_COUNT=5`
- `HIGH_VISUAL_DEBT_COUNT=4`
- `MEDIUM_VISUAL_DEBT_COUNT=14`
- `LOW_VISUAL_DEBT_COUNT=8`

## 10. Phase 2B Recommendation

Proceed only after Human accepts the references and baselines. Phase 2B should remain thin:

1. Define standard 55px, compact 38px and controlled two-row search/toolbar variants using Learun structure.
2. Define list/detail tab spacing without changing Bootstrap/Learun behavior.
3. Consolidate detail-section and technical-collapse shell styling.
4. Document 28px Grid header/row, 31px Pager and absolute-fill modifier as locked geometry.
5. Add at most a few spacing/control/border/muted-background values; do not build a large token system.
6. Extend Foundation Showcase with missing component and geometry examples.
7. Keep Drawer/Modal sizes semantic: small/medium/large; current production uses one Drawer width and two Modal sizes.

`PHASE_2B_RECOMMENDED=True` after Human Review.

## 11. Phase 2C Rollout Plan

1. **V1 Page02 + Page04:** hold Page02 as baseline; fix Page04 query fit and then align shared list spacing.
2. **V2 Page03 + Page07:** align detail sections, tabs, technical evidence and Page07 two-row search density.
3. **V3 Page05 + Page06:** apply the existing absolute-fill modifier where proven, normalize Page05 toolbar/buttons, review monitoring density.
4. **V4 Page01:** final spacing/typography alignment only; preserve its Human-passed Drawer → Modal lifecycle.

## 12. Human Review Gate

Human must confirm: the five Learun references; Page02 list baseline; search/toolbar variants; 28px Grid density; 31px Pager visual; Drawer and Modal shells; detail-section baseline; Page06 monitoring density; Page07 audit density; 1366×768 as first target; and the exact Phase 2B Foundation boundary.

