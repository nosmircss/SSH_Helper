# Flow Canvas UI (React) — Feature Map

Scope: `FlowCanvas/src/` React layer of the visual script editor hosted in WebView2 (`UI/FlowCanvasForm.cs` is the C# host, out of scope here). Entry: `main.tsx` routes by URL query — `?panel=runoutput` renders `RunOutputWindowApp` (detached console), otherwise `App` (`panelMode.ts:4`). All state lives in one Zustand store (`stores/useFlowStore.ts`) composed from slices; WebView2 transport is the singleton `messageBus` (`MessageBus.ts:114`) wired to the store by `stores/messageBridge.ts:124` (full canvas) or `stores/runOutputWindowBridge.ts:12` (popped-out console only).

---

## 1. Feature inventory

### 1.1 Canvas shell & rendering (`App.tsx`)
- **ReactFlow surface** with custom node types `block`/`comment`/`start` and the single edge type `animated` (`App.tsx:47-56`). Dark-only; all colors come from the token layer (`styles/tokens.css`, `utils/tokens.ts`); `themeSync` from the host is intentionally ignored (`messageBridge.ts:454-456`), and `uiSlice.setTheme/toggleTheme` exist but are never surfaced in UI (dead path, `uiSlice.ts:158-159`).
- **MiniMap** (non-interactive, `pointerEvents:none`, category-colored nodes via `resolveCssVar` because SVG attrs can't take `var()` — `App.tsx:331-339, 405-415`), **Controls**, **dot-grid Background**.
- **Snap-to-grid** honors `snapToGrid` + `gridSize` (fixed 20, no UI to change grid size — `uiSlice.ts:128`).
- **Inverse-zoom UI scale**: `syncUiZoomScale` quantizes `1/zoom` to 0.05 steps capped at 2.25 (`uiSlice.ts:27,368-372`); drives `--fc-ui-scale` (handles/pills) and `connectionRadius = 28 * uiScale` (`App.tsx:130-132, 391`).
- **Selection model**: click=single select (also flips output pane to Block tab, `App.tsx:239-252`), shift-click=toggle multi-select, drag-rectangle (`selectionOnDrag`, pan on middle/right button `App.tsx:392-393`), Escape clears. Multi-select shows only a "N blocks selected" placeholder in Properties (`Properties.tsx:1577-1594`) — no bulk edit/align/disable.
- **Drag & drop from palette**: `onDrop` reads `application/flowcanvas-block`, populates schema defaults, honors `defaultBlockExpanded` (`App.tsx:187-227`).
- **Undo snapshot on drag start**, layout-autosave on drag stop (`App.tsx:170-179`).
- **Connection validation**: `isValidConnection` + `onConnectEnd` rejection notices (`App.tsx:136-167`); rules in `utils/connectionRules.ts:9-55` — no self-loop, nothing into Start, no duplicates, **no fan-in** (one incoming edge per block), per-handle uniqueness (containers may emit multiple bottom-handle branch edges; plain blocks one successor), cycle detection (`canReach` DFS). Rejection reason shows in `ConnectionNotice` toast, auto-dismissed after fixed 2500ms (`panels/ConnectionNotice.tsx:10`).
- **Comments render behind blocks** and with content-sized hit boxes so they never hijack block drags (`utils/displayNodes.ts:11-32`, applied at `App.tsx:307-317`).

### 1.2 Palette (`panels/Palette.tsx`)
- Fixed 180px left rail listing all blocks grouped by 7 categories in hardcoded order (`Palette.tsx:12-14`). Items are HTML5-draggable, tooltip = block description. **No palette search/filter, no collapse-by-category, no favorites** — with ~37 block defs the rail is a long scroll.
- Block schema source: `blockDefs/registry.ts` — 37 `BlockDef`s (send, interactive, sftp, if, foreach, while, repeat, switch, parallel, try, break, continue, call, return, exit, extract, set, sethistorylabel, parse, table, assert, ping, dns, portcheck, http, webhook, browser_callback, vault, print, input, choose, multiselect, confirm, readfile, writefile, exists, playsound, log, notify, localcmd, updatecolumn, updateenvironment, wait). Each has property schema (type/required/defaults/help/group), `previewKey`, `isContainer`, `outputs`. Note `parallel`/`try` have **empty property lists** (`registry.ts:188-189`). Header comment says "36 commands" — count drifted (`registry.ts:2`).

### 1.3 Block visuals (`nodes/BaseBlock.tsx`, `baseblock.css`, `execution-cinematics.css`, `styles/justPlaced.css`)
- Category-tinted card: icon chip, uppercase type badge, label, expand chevron; width driven by `blockWidth` setting (children inset by `BLOCK_WIDTH_INSET`), text by `textScale` (`BaseBlock.tsx:199-246`).
- **Expanded summary view**: `summarizeBlock` shows required + non-default props ("N fields at default" footer), masks `password`/`token` keys with bullets (`utils/blockSummary.ts:10,53-73`); collapsed view shows `previewKey` text (falls back to import-time `_preview`, `BaseBlock.tsx:172-178`).
- **Execution state visuals**: idle neon ring / running breathing-glow class + sweeping comet halo (`fc-run-halo`, reduced-motion gated, `BaseBlock.tsx:333-335`) / error shake + glow / success check-draw + DONE / skipped / disabled (0.5 opacity, strike-through, "⏭ DISABLED" badge `BaseBlock.tsx:322-326`).
- **Live duration ticker** while running (rAF, re-render only on text change `BaseBlock.tsx:65-86`); settled ms/s badge; **loop ×N badge**; **branch-taken badge** (`then`, `elif #2`, `case #1`… `BaseBlock.tsx:53-60, 285-320`).
- **Heatmap tint**: opt-in (settings); blocks ringed cold→hot by duration relative to max; render-only, never touches export (`BaseBlock.tsx:38-44, 95-103, 165-168`). NB: every block subscribes to a full `blockTimings` scan when heatmap is on — O(N²) per store change on large graphs (`BaseBlock.tsx:95-103`).
- **Breakpoint gutter dot** on every block including nested children; click toggles + notifies host (`BaseBlock.tsx:350-364`, `debugSlice.ts:41-56`).
- **Iteration-scoped display**: when an ancestor loop has a selected iteration, badge/state/duration show that iteration only (`BaseBlock.tsx:104-118, 156-160`).
- **Handles**: top target; bottom source (shifted to 75% for containers); right `false` handle for if (`outputs===2`); accent square `continue` handle for containers (`BaseBlock.tsx:337-470`). Geometry invariants are load-bearing for edge routing (comments in file).
- `_justPlaced` one-shot entrance pulse for new blocks, cleared on animationEnd (`BaseBlock.tsx:28-34, 135-139`).

### 1.4 Edges (`nodes/AnimatedEdge.tsx`, `EdgeMarkers.tsx`, `animatededge.css`)
- Single renderer for all edges. Spine edges (aligned, downward, |dx|<0.5) get straight paths; offset corridors get smoothstep (`AnimatedEdge.tsx:28-34`).
- **Live run packet**: one travelling glow dot rides the deepest running edge (`selectEdgeIsRunningFrontier`, `stores/selectors/edgePath.ts:83-90`); reduced-motion gated.
- **Persistent execution-path overlay**: edges classify as on-path (neon bloom, branch arms keep their hue, spine promotes to traversed cyan), untaken (faded), idle (`selectEdgePathStatus`, `edgePath.ts:106-194`). Handles both canvas-built edges (`data.branchPath`) and imported edges (correlated via child `_stepPath`/`_isChildOf`) — the dual-origin seam documented in file comments. Survives run end; "Clear Path" toolbar button hides it (also resets iteration selections, `executionSlice.ts:114-117`).
- Per-edge gradient + arrowhead markers from the token set (`EdgeMarkers.tsx:8-28`); on-path edges get a per-edge marker matching the glow hue (`AnimatedEdge.tsx:93-117`).

### 1.5 Branch bands & iteration stepper (`nodes/BranchBandsLayer.tsx`, `nodes/IterationCluster.tsx`, `utils/branchBands.ts`)
- Toggleable tinted rectangles wrapping each container branch subtree, with draggable label pills (drag moves the whole band's members, undo-snapshotted, autosaved — `BranchBandsLayer.tsx:24-50, 85-120`). Band color from `--fc-branch-*` tokens via `branchColorVar`; nested bands tinted brighter.
- **Post-run iteration stepper** per loop band (`branchKey === 'do'`): `[ALL] [◀ label · 3/12 ▶] [⚠ n]` chips, failed-iteration jump, eviction notice ("of N total"), and a tick scrubber when >20 visible iterations (bucketed above 60 ticks, pointer-drag scrubbing — `IterationCluster.tsx:7-13, 86-160`). Selecting an iteration re-scopes path overlay, badges, durations, Block Output, and Variables via `iterationSlice` + `selectors/iterationScope.ts` (seq-keyed records, parent-chain consistency: inner-pulls-outer, descendant reset — `iterationSlice.ts:168-212`).
- Iteration history capped per loop (`iterationHistoryCap`, default 500, user-settable 1–100,000 in settings; eviction tolerated by consumers — `iterationSlice.ts:8, 138-143`). Variable snapshots sanitized: heavy keys dropped, strings/objects truncated at 2000 chars (`iterationSlice.ts:11-32`).

### 1.6 Comments & stickies (`nodes/CommentNode.tsx`, `panels/CommentProperties.tsx`, `stores/slices/commentSlice.ts`)
- Two kinds: `comment` (exports as `#` YAML comment) and `sticky` (visual only). Compact mode renders anchored comments as `#`-pills (`CommentNode.tsx:27-89`); double-click edits inline (Escape cancels, blur commits).
- Added from the block context menu ("Add Comment (#)" / "Add Sticky" at hardcoded offset +200/−20 from the block, `BlockContextMenu.tsx:66-92`); anchored comments reserve layout space and trigger reflow on add/remove via `anchorReservesLayoutSpace` gating (`commentSlice.ts:36-43, 73-84`).
- Properties pane for a selected comment: text, kind dropdown, read-only anchor info (`CommentProperties.tsx`). **No color picker** despite `data.color` being modeled and exported (`exportGraph.ts:127`, default `#e0c040` must stay byte-identical to C# — `tokens.ts:6`).

### 1.7 Start node (`nodes/StartNode.tsx`, `Properties.tsx:1221-1526`)
- Synthesized on load if missing (adopts `_preamble` props, wires to first root — `messageBridge.ts:32-72`); source-only handle (invariant guarded by e2e).
- Card shows script name + chips for active flags / vars count / imports count. `StartProperties` form edits name, description, environment, version, 5 boolean flags (debug, nobanner, compact_errors, suppress_missing_column_warning, library), and **raw-YAML textareas** for `vars`, `imports`, `subroutines` (`Properties.tsx:1432-1489`) — free-text YAML with **no client-side YAML validation**; errors surface only via host round-trip.

### 1.8 Properties panel (`panels/Properties.tsx`, `panels/RightPanel.tsx`)
- Right rail, drag-resizable 200–600px, width persisted via `layout-save` (`RightPanel.tsx:27-56`; default 600 in `uiSlice.ts:20-23`).
- Schema-driven editors per `PropertyDef.type`: text, number, textarea, select (touched-guard against spurious commits `Properties.tsx:608-707`), boolean checkbox, code (monospace single-line), file-browse (`Browse...` → host `browse-path` request/response correlated by UUID `requestId`, `Properties.tsx:65-71, 573-606`).
- **Buffered inputs** (`useBufferedInput`, `Properties.tsx:18-63`): local state while focused, commit-on-change+blur, external updates (undo/import) only applied when unfocused — prevents stale blur commits.
- **Insert variable…** dropdown under text/code/textarea fields, fed by the live `variables` slice; appends `${var}` tokens (`Properties.tsx:618-649, 207-220`).
- **Grouped sections**: Core / Advanced / On Error — group from `propDef.group`, else `on_error` key, else a hardcoded `ADVANCED_PROPERTY_KEYS` set (`Properties.tsx:925-965`).
- **Conditional required logic** per block (readfile select_file/path_only, http auth modes, interactive show_window bounds — `Properties.tsx:870-923`, duplicated in `blockSummary.ts:22-43`); inline validation messages + red borders; `choose` default-not-in-options warning (`Properties.tsx:1027-1038`).
- **ChoiceOptionsEditor** for choose/multiselect options: "From Variable" (token-validated source) vs "Static Options" (label/value rows with add/remove/reorder; serializes strings or `{label,value}` objects — `Properties.tsx:222-511`).
- **Branch chip**: shows `_branchLabel` tinted by branch color for imported children (`Properties.tsx:1633-1656, 1742-1757`).
- **Test Block (data blocks)**: extract/parse/set/table/assert get a "Test Block" button that sends `test-data-block` with current props + live variables (disabled while running or when no variables yet); result panel shows success/error, output, changed-key chips, relative timestamp (`Properties.tsx:822, 1048-1211`).
- Edits push **debounced undo snapshots** (one per 500ms burst, `Properties.tsx:1551-1557`) and propagate `_forceGraphExport` up the `_isChildOf` ancestor chain so export regenerates edited containers (`graphSlice.ts:510-562`).

### 1.9 Toolbar (`panels/Toolbar.tsx`)
- **▶ Run** — sends `execute-canvas {mode:'run', graphChanged:isDirty, nodes, edges, comments, disabledBlocks}`; disabled when running / export errors / **no target host** (tooltip explains; `Toolbar.tsx:101-115`). Payload built by `buildExecutableGraphPayload` (`utils/exportGraph.ts:110-146`) which splits comments out, strips schema-default props, and attaches `disabledBlocks`.
- **⏩ Test Step** — `execute-canvas {mode:'test-step', stepId}` for the single selected block (`Toolbar.tsx:65-74`). Not gated on target host (unlike Run).
- **Debug cluster** (visible while running/paused): PAUSED/RUNNING chip + Continue / Step (F10) / Stop → `debug-action` messages (`Toolbar.tsx:128-176`, `debugSlice.ts:119-132`).
- **Undo/Redo** buttons (snapshot stack ×100, deep JSON clones — `undoSlice.ts:5, 30-98`; undo/redo also clear export status+diagnostics).
- **⊞ Layout** — explicit hierarchical auto-layout (undoable; threads live sizing — `hooks/useAutoLayout.ts`).
- **Layout-mode toggle** — 🔒 Manual / ✨ Auto-flow per preset; switching to auto reflows, switching to manual flushes an immediate layout autosave freeze (`uiSlice.ts:199-211`).
- **🔍 search toggle**, **Expand All / Collapse All** (batched carrier-flag write + single reflow, `debugSlice.ts:92-105`), **⚙ Settings popover**, **Apply YAML** (graph → host → Scintilla editor), **Vars / Output / Timeline panel toggles**, **⌫ Clear Path**, **⚠ Problems** toggle with error-count pill (`Toolbar.tsx:219-270`).
- Cosmetic: version tag "Flow Canvas v2"; dead ternary `variablesVisible ? '🔍 Vars' : '🔍 Vars'` (`Toolbar.tsx:227`).

### 1.10 Host bar (`panels/HostBar.tsx`)
- Read-only target strip: 🎯 TARGET + Host/Port(≠22 only)/User chips + up to 4 extra CSV-column variable chips (suppressing `Host_IP`,`port`,`username`,`password` — **not** `vault_path` — `HostBar.tsx:4-7,41-45`); "No target host — select a host in the main grid" empty state. Host selection happens only in the WinForms grid (`set-target-host` message, `messageBridge.ts:434-451`); no host switching from the canvas.

### 1.11 Keyboard shortcuts (`hooks/useKeyboardShortcuts.ts`)
- Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z (undo/redo), Ctrl+C / Ctrl+V (internal clipboard module — `utils/clipboard.ts`; copy excludes Start, paste offsets +30/+30 with remapped ids), Ctrl+F (search), Delete/Backspace (edges first, then nodes), Escape (search → context menus → selection), Ctrl+Enter (test selected step), F5 (run), F10 (debug step). All text-input-guarded except F5/F10/Ctrl+Enter.
- Gaps vs toolbar parity: **F5 ignores the no-target-host gate** that disables the Run button, and neither F5 nor Ctrl+Enter sends `graphChanged` (toolbar sends `graphChanged:isDirty`) — `useKeyboardShortcuts.ts:111-146` vs `Toolbar.tsx:65-83`. Ctrl+Enter also doesn't check `isRunning`. No Ctrl+X cut, Ctrl+D duplicate, Ctrl+A select-all, zoom/fit shortcuts, or keyboard node navigation.

### 1.12 Context menus
- **BlockContextMenu** (`panels/BlockContextMenu.tsx`): Toggle Breakpoint (non-Start), Add Comment/Sticky, Expand/Collapse All, Delete Block/Comment. **No Disable Block, Duplicate, Copy/Paste, Test Step, or "go to YAML" items.**
- **EdgeContextMenu** (`panels/EdgeContextMenu.tsx`): for edges sourced from if/try/switch/parallel (foreach/while fall through to a fixed `do` mode), a branch metadata editor — mode dropdown (Then/Elif/Else, Do/Catch/Finally, Case/Default, Branch), numeric branch index, elif condition, case value, Save → `updateEdgeBranchMetadata` re-derives label/color (`graphSlice.ts:564-597`); plus Delete Connection. **No duplicate-index validation** (two edges can both claim `elif/0`), and the try editor offers mode `do` but commits `branchPath:'try'` (`EdgeContextMenu.tsx:87, 250-255`) — vocabulary mismatch worth verifying against the exporter.

### 1.13 Search (`panels/SearchOverlay.tsx`, `uiSlice.ts:226-273`)
- Ctrl+F overlay (top-right of canvas, hardcoded `right:320`): live query over label, blockType, def label, and `JSON.stringify(props)` (`uiSlice.ts:229-242`) — the props serialization means **hidden metadata matches** (`_stepPath`, `_yamlSnippet`, `_isChildOf` values can produce false positives). Enter/Shift+Enter cycle results; "n of m" counter; matches get `search-match`/`search-current` classNames (`App.tsx:307-317`). **No viewport navigation** — cycling results never pans/zooms to the match (contrast `ProblemsPanel` which calls `setCenter`).

### 1.14 Settings popover (`panels/SettingsPopover.tsx`, `stores/slices/settingsSlice.ts`)
- Sizing: Block width slider 300–2000px (live-preview drag, persist-on-release), Text size 90–250%, Canvas density segmented (Tight/Normal/Roomy — vertical only), Loop history cap number field, Default block state (Collapsed/Expanded — applies immediately to the open graph and to all future loads `settingsSlice.ts:78-85`).
- View: Snap to grid, Branch bands, Compact comments (reflows), Default layout mode (Auto-flow/Manual), Heatmap, Reduced motion (also seeded from OS `prefers-reduced-motion` before ready — `messageBridge.ts:504-506`).
- "↺ Reset to defaults" (`settingsSlice.ts:86-94`). All persist to WinForms `WindowState` via `layout-save`/`pref-save`; restores arrive via `layout-restore`/`pref-restore` with no echo.

### 1.15 Output pane: Block Output + Run Output (`panels/OutputPreview.tsx`, `panels/RunOutputView.tsx`)
- Bottom dock, drag-resizable 80–600px (persisted), tab strip **Block Output | Run Output** with unread dot on Run while hidden (`OutputPreview.tsx:176-197`).
- **Block Output**: selected block's own per-step output (executor's attribution contract — blank if the block carried none, never a neighbor's; `App.tsx:288-299`); per-block output history pager (◀ ▶ n/m) for loops; Copy button; iteration-scoped view with honest empty states — "(not reached in this iteration)" vs "(no output in this iteration)" vs loop-container hint (`OutputPreview.tsx:60-108, 288-316`); iteration-context chip `⏱ 3/12 · label`.
- **Run Output console** (`RunOutputView.tsx`): live mirror of the main form's output box. LIVE dot while running; toolbar: Find (substring count + `<mark>` highlights — **no next/prev navigation, no regex/case toggle**, `RunOutputView.tsx:29-39, 93-114`), Follow (stick-to-bottom, auto-released on scroll-up), Wrap, Color (heuristic per-line classification: `#…#` banners teal, error-regex lines red — `utils/runOutputClassify.ts:4-7`, "cosmetic only" by design), Copy all, Pop out.
- Buffer capped at 5000 lines with **silent truncation** (`executionSlice.ts:4, 119-127`); auto-switches dock to the Run tab on `execution-started` unless popped out (`messageBridge.ts:221`).
- **Detached Run Output window**: `openRunOutputWindow` → host opens a second WebView2 at `?panel=runoutput` (`uiSlice.ts:335-345`); window renders only `RunOutputView` with its own minimal bridge (run-output stream, run state, pref seed — `runOutputWindowBridge.ts`); main dock switches to Block; clicking the Run tab while popped out re-docks; host notifies `run-output-window-closed` on external close. Pref toggles in the two windows don't live-sync to each other (each echoes to host persistence only).

### 1.16 Variable inspector (`panels/VariableInspector.tsx`)
- Docked under Properties (max 40% height), name filter, click-to-expand long values (JSON pretty-print, 200px scroll), yellow flash + bold on changed variables (800ms timer — `variableSlice.ts:45-52`).
- **Secret masking**: names matching password/secret/token/api-key/etc. regex render `"********"` and are **not expandable** (`VariableInspector.tsx:6-8, 165-168`) — note values are still present in the JS store/messages, masking is display-only.
- **Iteration snapshot mode**: when a loop iteration is selected anywhere, shows that iteration's frozen end-of-iteration variable snapshot with a banner (`⏱ Iteration 3/12 · label · ⚠ failed`) and a "Live" button to return (`VariableInspector.tsx:38-130`); honest "(no variable snapshot recorded)" empty state.

### 1.17 Timeline (`panels/TimelinePanel.tsx`, `stores/slices/timelineSlice.ts`)
- Toggleable strip of per-step bars (width ∝ duration 20–80px, state-colored), hover tooltip, click to **scrub**: selects the block and restores that entry's variable snapshot into the inspector (`timelineSlice.ts:52-63`); click again to stop. Cleared per run.
- Bug-ish: entries are created with `nodeLabel: blockType` (`messageBridge.ts:253-256`) so tooltips show the block *type*, never the user's label.

### 1.18 Debug experience (`panels/DebugPanel.tsx`, `debugSlice.ts`, toolbar cluster)
- Floating panel (hardcoded `left:190, bottom:10` — pinned to the 180px palette width, `DebugPanel.tsx:16-17`) appears while running/paused: state dot, Continue/Step/Stop, **call stack** list (top frame highlighted). Host messages `debug-paused`/`debug-resumed` carry stepId + callStack + variables (`messageBridge.ts:408-431`); paused block forced to `running` visual.
- Breakpoints: gutter dot + context menu toggle; set notified to host per toggle. **Breakpoints Set is never cleared/restored on load-graph** (`messageBridge.ts:130-184` restores disabled + expanded only; `debugSlice.ts` has no clear/restore for breakpoints) — switching presets in an open canvas can leak stale breakpoint ids (collidable `node-N` ids).
- **Disabled blocks**: `toggleDisabled` exists with full host wiring (`disable-block` message, layout-autosave, restore-on-load — `debugSlice.ts:58-76`, `messageBridge.ts:169-177`) **but no UI invokes it** — `toggleDisabled` has zero call sites outside the slice (grep). BaseBlock renders the disabled visual and export carries `disabledBlocks`, but a user cannot disable/enable a block from the canvas.

### 1.19 Problems panel (`panels/ProblemsPanel.tsx`)
- Floating bottom-left list of export diagnostics (errors sorted before warnings); row click selects + `setCenter`-animates to the offending block (reduced-motion honored). Renders only when toggled **and** diagnostics exist — toggling it with zero diagnostics gives no feedback at all (`ProblemsPanel.tsx:12`). Diagnostics arrive only via `apply-result` (`messageBridge.ts:188-212`) and are wiped by any graph mutation (`clearedExportStatusState`, `graphSlice.ts:23`), so the list is empty until the next Apply/Run round-trip.

### 1.20 Graph mutation engine (`stores/slices/graphSlice.ts`, `utils/childMembership.ts`)
- `onConnect` infers default branch metadata + visuals for container edges, confers `_isChildOf`/`_stepPath` membership on newly wired blocks (incl. re-homing orphans, with sibling renumbering — `graphSlice.ts:346-420`).
- All removal paths renumber `_stepPath` to stay contiguous (executor correlation invariant — `graphSlice.ts:310-314, 447-457, 460-476`).
- Comment cascade on block delete, anchored-comment drag-follow with delta propagation (`graphSlice.ts:247-308`).
- Dirty tracking + export-status clearing on every mutation; `isDirty` drives the `graphChanged` run flag.

### 1.21 Layout system (referenced; engine itself in `utils/layout/`)
- Per-preset Auto-flow vs Manual; `reflowLayout` (`stores/reflow.ts`) gates every automatic reflow: auto → full hierarchical layout with `keepOrphans`, manual → only re-anchor comments. Reflow triggers: expand/collapse, settings changes, compact-comments toggle, anchored-comment add/remove, space-reserving removals. Layout autosave (300ms debounce) ships positions + comments + disabled + expanded to the host with `stepPath:blockType` tuples for prefix-safe merge (`utils/layoutAutosave.ts:25-70`).

---

## 2. Integration points (React ↔ host)

| Direction | Message | Producer/consumer (React) | Purpose |
|---|---|---|---|
| in | `load-graph` | `messageBridge.ts:130-184` | nodes/edges + `layoutMode`/`layoutAction`/`newNodeIds`; synthesizes Start; resets session state; restores disabled/expanded |
| in | `apply-result` | `messageBridge.ts:188-212` | export success/errors/warnings + per-node diagnostics; failure echoes a `show-error` message back to host |
| in | `execution-started/-finished/-update` | `messageBridge.ts:215-313` | run lifecycle; per-step state/duration/variables/changedKeys/iterationCount/branchTaken/iterationStack |
| in | `step-output` | `messageBridge.ts:316-330` | per-block output + iteration attribution (`outputIdx`) |
| in | `run-output`, `run-output-clear`, `run-output-window-closed` | `messageBridge.ts:333-354`, `runOutputWindowBridge.ts` | run console mirror + detached-window lifecycle |
| in | `test-step-result`, `test-data-block-result` | `messageBridge.ts:357-394` | single-step / data-block test results |
| in | `variables-snapshot`, `debug-paused`, `debug-resumed` | `messageBridge.ts:397-431` | inspector + debug UI |
| in | `set-target-host`, `theme-sync` (ignored), `layout-restore`, `pref-restore`, `browse-path-result` | `messageBridge.ts:434-499`, `Properties.tsx:575-590` | host grid selection, persisted settings, file dialog result |
| out | `ready` | `messageBus.sendReady()` (`messageBridge.ts:509`) | drains host's `_pendingMessages` queue — nothing before this is delivered |
| out | `apply-yaml`, `execute-canvas` (run / test-step), `debug-action`, `breakpoint-toggle`, `disable-block`, `test-data-block` | Toolbar/shortcuts/slices | execution + editing surface (C# `OnApplyYaml`/`OnExecuteCanvas`/`OnDebugAction`/`OnBreakpointToggle`/`OnDisableBlock`/`OnTestDataBlock`) |
| out | `layout-save`, `layout-autosave`, `pref-save`, `set-layout-mode`, `browse-path`, `open-/close-run-output-window`, `show-error` | uiSlice/settingsSlice/layoutAutosave | persistence into `WindowState` + window management |
| — | deprecated alias `run-request` retained in `communication-message-types.ts:49-51` | | legacy route to `OnRunRequest` |

Contracts that must not drift: `branchTaken` vocabulary (`then|else|elif/{i}/then|cases/{i}/do|default`) ↔ `edge.data.branchPath`; `_stepPath` contiguity; `comments[]` as a **flat array separate from nodes[]** in every outbound graph payload; `DEFAULT_COMMENT_COLOR` byte-identical to the C# default.

---

## 3. Observed gaps & quirks

**Functional gaps**
1. **Disable Block has no UI entry point.** `debugSlice.toggleDisabled` (`debugSlice.ts:58-76`) and the whole host pipeline exist, but nothing calls it — not the block context menu (`BlockContextMenu.tsx:72-121`), not a shortcut. Disabled state is import-only/restore-only today.
2. **Clipboard paste doesn't remap structural metadata.** `pasteNodes` (`utils/clipboard.ts:44-67`) remaps edge endpoints but copies `node.data` verbatim — pasted children keep `props._isChildOf`/`_stepPath` pointing at the *original* container, so pasting a container+children can attach clones to the source container's band/branch and duplicate step paths (layout, bands, and export regeneration all key off these). Paste also ignores cursor position (+30/+30 offset) and confers no membership cleanup.
3. **Keyboard/toolbar parity drift**: F5 run skips the `targetHost` gate and the `graphChanged:isDirty` flag; Ctrl+Enter test-step skips `isRunning` and `graphChanged` (`useKeyboardShortcuts.ts:111-146`). If the host treats absent `graphChanged` as false, a dirty graph run via F5 could execute stale YAML.
4. **Breakpoints leak across preset loads** — load-graph restores disabled+expanded but never clears/restores `breakpoints` (`messageBridge.ts:169-183`); ids collide between presets (`node-1`, `node-2`…). (Known in project memory as "breakpoints Set still uncleaned".)
5. **Search doesn't navigate** — Enter cycles `searchIndex` but nothing pans/zooms to the current match (`SearchOverlay.tsx`, `App.tsx:281`); on a large graph the highlight is frequently off-screen. Also matches hidden metadata via `JSON.stringify(props)` (`uiSlice.ts:236`) → false positives on `_stepPath`/`_yamlSnippet` content.
6. **Run Output find has no navigation** — count + highlight only, no next/prev/scroll-to (`RunOutputView.tsx:29-39`); buffer truncates silently at 5000 lines (`executionSlice.ts:4`) with no "output truncated" indicator; no save-to-file/export of the console.
7. **No multi-select operations** — Properties shows a count placeholder only (`Properties.tsx:1577-1594`); no bulk delete confirmation, alignment, or group property edit (delete does work via keyboard).
8. **Problems toggle is silent when clean** (`ProblemsPanel.tsx:12`) and diagnostics evaporate on any edit (`clearedExportStatusState` on every mutation) — no live/lint-style validation between Apply round-trips, even though required-field validation exists per-field in Properties.
9. **Comment color is modeled + exported but uneditable** (`CommentProperties.tsx` has text/kind only).
10. **No host switching from the canvas** — HostBar is read-only by design, but there's also no affordance to *open* the main grid or pick among hosts; multi-host execution (the app's core feature) is single-target-only in the canvas (canvas is a preset-builder per project decision — still, the bar offers no "change host" action beyond a hint string).

**Inconsistencies / correctness risks**
11. **EdgeContextMenu try-branch vocabulary**: options list `do` (`EdgeContextMenu.tsx:87`) but commit writes `branchPath:'try'` (`:251`); bands/markers use `try` tokens — needs a single authoritative key, and the exporter's expectation should be verified.
12. **Branch index free-typing** allows duplicate/skipping indices (two `elif/0/then` edges; `cases/3` with no `cases/0..2`) with no validation (`EdgeContextMenu.tsx:331-348`).
13. **Timeline `nodeLabel` is the blockType**, never the user label (`messageBridge.ts:255`), making the tooltip/title less useful than intended (`TimelinePanel.tsx:131,145`).
14. **Undo restores node data but not derived Sets** — `undoSlice` snapshots nodes/edges only; `breakpoints`, `disabledBlocks`, `expandedNodes` Sets are not snapshotted, so undoing past a breakpoint/disable/expand leaves Set↔node.data desync (e.g. `data.breakpoint` true but Set empty; host never re-notified).
15. **Sensitive-value masking is display-only and name-based** (`VariableInspector.tsx:6`, `blockSummary.ts:10`) — values still cross the bridge and sit in store/iteration snapshots; a password in a non-matching variable name renders in clear; block summary masks only exact keys `password`/`token` (e.g. `passphrase`, `api_key` props unmasked).
16. **Heatmap selector scans all timings per block per store change** (`BaseBlock.tsx:95-103`) — O(N²); fine at tens of blocks, risky at hundreds.
17. **Hardcoded geometry**: DebugPanel `left:190` couples to the 180px palette (`DebugPanel.tsx:16`); SearchOverlay `right:320` (`SearchOverlay.tsx:49`); comment spawn offset `+200/−20` (`BlockContextMenu.tsx:69`); HostBar max 4 var chips with silent truncation (`HostBar.tsx:7,43`).
18. **`useBufferedInput` commits on every keystroke** (`commitIfNeeded` inside `onChange`, `Properties.tsx:52-56`) — "buffered" mainly guards external overwrites; each keystroke is a store write + `_forceGraphExport` ancestor walk; only undo granularity is debounced (500ms).
19. **Number property commit** maps `''→undefined` but `Number('1e3')`/locale input edge cases are uncaught; invalid text in `type=number` inputs yields `NaN` commit potential (`Properties.tsx:556-566`).
20. **Toolbar dead ternary** (`Toolbar.tsx:227`) and `DebugPanel`'s re-exported `DebugState` "for compatibility" (`DebugPanel.tsx:102-106`) look like leftovers; stray temp file `stores/slices/__tests__/graphSlice.rewireProbe.test.ts.tmp` is checked in.
21. **Registry count drift**: header claims 36 commands, file defines 37 defs (`registry.ts:2`); `keyvalue` PropertyDef type is declared (`registry.ts:22`) but has no editor case in `PropertyField` (falls through to the generic text input, `Properties.tsx:800-819`).
22. **Run console error heuristic** is intentionally regex-cosmetic (`runOutputClassify.ts:7`) — `\bfail(?:ed)?\b` will tint legit lines (e.g. "0 failed") red; acceptable per comment but a per-host/step structured stream would be more truthful.
23. **`updateNodeLabel`/`updateNodeProp` don't push their own undo snapshots** — they rely on Properties' debounced snapshot; programmatic callers (e.g. StartProperties name sync) get coalesced history, and two rapid edits to *different* nodes inside 500ms share one snapshot label "Edit property" (`Properties.tsx:1551-1557`).
24. **Detached Run Output window prefs don't live-sync** between windows (each store is independent; only host persistence converges them on next open).

**Test-surface notes** (for downstream auditors)
- Component tests exist (vitest+jsdom) for nodes/panels/slices/utils (`__tests__` throughout); e2e specs live outside src. Known pre-existing e2e failures from a stale reduced-motion selector are documented in project memory — don't chase as regressions.
