# Flow Canvas C# Bridge — Feature Map

Scope: `Services/FlowCanvasBridge.cs` (5,600 LOC), `UI/FlowCanvasForm.cs` (543 LOC), `UI/RunOutputWindowForm.cs` (254 LOC), cross-checked against the React protocol catalog `FlowCanvas/src/communication-message-types.ts` (91 LOC) and the Form1 wiring (`Form1.cs` ~2419–2512, ~6605–7360, ~12330–12370, ~13583–13760, ~13984, ~14072).

All paths below are relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

---

## 1. Feature inventory

### 1.1 YAML → Graph import (`TextToGraph`)
`Services/FlowCanvasBridge.cs:381–578`. Converts YAML script text to React Flow nodes/edges JSON.
- **Snippet round-trip core**: each top-level step node stores its original YAML verbatim in `props._yamlSnippet` (line 419), plus `_blankLinesBefore` (420–421), `_stepPath` (422), `_preview` (423–424). Export reassembles snippets, so untouched steps survive byte-for-byte.
- **Step splitting** is raw-text based: `SplitYamlSteps` (4655–4764) finds `steps:`, splits on top-level `- ` items, records blank-line counts, leading-comment runs (consecutive `#` lines merged into one multiline comment node, blank line breaks the run), and one inline trailing comment per step. Inline capture stops once a nested list item is seen (`enteredNestedBody`, 4674, 4747–4751) so container-body comments stay in the snippet verbatim.
- **Inline-comment splitter** `TrySplitTrailingComment` (4601–4631) is quote-aware (single/double, `''` escape, `\"` escape) and requires whitespace before `#`.
- **Properties panel data**: `ExtractStepProperties` (2634–~3195) flattens parsed `ScriptStep` fields into props for the React Properties panel; `GetStepPreview` (4535) + `GetDisplayLabel` (2611–2628, truncates >35 chars, special-cases `set var=`).
- **Container expansion**: `ExpandContainerChildren` (586–607) recursively expands if/foreach/while/repeat/try/switch/parallel into visual-only child nodes (no `_yamlSnippet`; carry `_isChildOf`, `_stepPath`, `_branchLabel`, `_branchColor`, `_depth` — 741–748). `GetBranches` (853–931) maps each container type to branch descriptors with scope paths matching the executor's `BranchTaken` vocabulary (`then`, `elif/{i}/then`, `cases/{i}/do`, `default`, `do`, `try/catch/finally`, `parallel/{i}`).
- **Layout placeholders only**: single-branch bodies indent right by `SingleBranchChildOffset = 220` (line 69, must stay in sync with `LAYOUT` in `hierarchicalLayout.ts`); multi-branch branches go in `MinColumnWidth = 330` columns (80, 683–709). The canvas recomputes layout on import; these positions are the host-only fallback.
- **Edge styling contract**: branch first-edges are dashed + labeled; nested-container continuations come from the `continue` sourceHandle with label `next` (505, 805–815, 839–841). Hardcoded hex edge colors at 83–92 (`#2ecc71`, `#e74c3c`, `#f0c040`, `#4a9eff`, `#1abc9c`, `#555`, `#666`).
- **Depth cap**: `MaxNestingDepth = 5` (74); containers nested deeper than 5 are NOT expanded into nodes (826) — their steps exist only inside the parent snippet (round-trip safe, but no canvas blocks, no `_stepPath` mapping, hence no debug highlight/output for those steps).
- **Start node + preamble**: everything above `steps:` (`ExtractPreamble`, 4789–4803) becomes the `__start__` node props via `ParsePreambleIntoProps` (4831–4890): name/description/version/environment/flags, `vars`/`imports` as read-only objects, raw `vars_yaml`/`imports_yaml`/`subroutines_yaml` sections, and the whole preamble as `_yamlSnippet` fallback. Header comment lines are stripped from the snippet (528–529) and emitted as `header`-anchored comment nodes (548–552) to avoid double emission.
- **Comments as nodes**: leading/inline comments per step become anchored comment nodes (`BuildCommentNode`, 335–357: anchor types `leading`/`inline`/`header`/`branch`). Branch-internal comments are recovered by `ScriptStep.LineNumber` walk-up in `CollectNestedComments` (653–681): below the branch keyword → `leading` (inside band), above the keyword → `branch` (above band).
- **Fallback `ToGraph(Script)`** (1028–1078): linear, lossy (no snippets/stepPaths). **No callers found — dead code.**

### 1.2 Graph → YAML export (`ExportGraphToYaml`)
`Services/FlowCanvasBridge.cs:1087–1388`. Returns `FlowCanvasExportResult` (45–61): YAML + `NodeToStepPathMap` + structured warning/error diagnostics with `NodeId` attribution; `Success` = no errors; failed exports return empty YAML (1385).
- **Ordering**: DFS from `__start__`'s edge target (`BuildChain`, 1696–1705). Unreachable nodes get a per-node warning and are excluded (1140–1152).
- **Three emission paths per top-level node**:
  1. **Container regeneration from graph** (1309–1324) when `_forceGraphExport` is set (prop edited), snippet missing (canvas-authored), branches were graph-authored (`HasGraphAuthoredContainerBranches`, 1403–1433), the imported container was structurally modified (`HasImportedContainerBeenModified`, 1481–1592 — detects deleted branch edges/children via `_stepPath`-derived branch keys, snippet branch-keyword scan `ExtractSnippetBranchKeys` 1648–1694, and the `else`↔`default` alias), or a canvas-authored comment targets a descendant (`ContainerHasDescendantComment`, 1451–1479).
  2. **Container snippet round-trip** (1328–1341) — verbatim, with a per-node warning "exported from its stored YAML snippet".
  3. **Leaf regeneration** `TryGenerateStepYaml` (3196–3287) — parses the old snippet's options (`TryParseSnippetOptions`, 3842), overlays edited props with alias mapping (`BlockPropAliasesByType`, 100–136; `BlockTypeToCommandKey`, 94–98), legacy-prop conversions (`TryHandleSpecialLegacyProp`, 3344–3465: portcheck `target`→host/port, writefile `append`→mode, foreach variable/expression→iterator, and hard rejections for send.delay / interactive.timeout / return.value), type normalization (Boolean/Integer/List/Dictionary option-key sets at 138–205), required-option enforcement (`RequiredOptionKeysByCommand`, 215–253; `TryEnsureRequiredOptions`, 4108), and canonical option re-ordering (`ReorderOptionsForSerialization`, 4256; per-command overrides at 255–267). Unsupported props fail the export with a named-property error (3273–3279).
- **Container regeneration** `TryGenerateContainerFromGraph` (1955–2289): per-type branch resolution from edge metadata (`data.branchPath`/`condition`/`caseValue`, sourceHandle `false`, label fallbacks `elif:`/`case:`/`default` — 2307–2329, 2174–2177). Branch membership comes from `CollectBranchChain` (1728–1798): metadata-driven (`_isChildOf` + immediate-child `_stepPath` index) for imported graphs, edge-following with convergence stop for pure canvas-authored graphs. Missing mandatory branches produce errors (e.g. "If block is missing a 'then' branch connection", 2038). Parallel branches export first node only, with a warning for multi-node branches (2266–2272). Empty branches emit `keyword: []` (1944).
- **Comment re-injection**: comments are indexed by `attachedToNodeId` (primary) or `anchor.stepPath` (fallback) into leading/inline/branch buckets + header list (1154–1225), from BOTH node-array comment nodes and the flat React `comments[]` payload (1213–1225). `CommentContext` (1857–1911) threads them into every emission path; `EmitBranch` (1919–1950) re-emits `branch` comments above the keyword; `BuildCommentedYaml` (2414–2427) handles nested nodes. Multiline comment texts are split so every line gets `#` (2555–2566).
- **Preamble export** `SerializeStartPropsToPreamble` (4895–4992): raw `*_yaml` editor sections win, then structured objects, then sections extracted from the stored snippet; unrecognized top-level sections are preserved via `ExtractUnrecognizedSections` (5106–5158). `steps:` header appended if absent (1241–1242).
- **Step-path map backfill** (1359–1380): nested children consumed by container regeneration still get their `_stepPath` recorded so debug correlation survives (fix for the "neon stops at the loop" bug).
- **`ToYaml`** (1390–1401) throws `InvalidOperationException` on diagnostics errors — convenience wrapper, used by tests/parity CLI.

### 1.3 Node↔step-path mapping for debug correlation
- `BuildNodeIdToStepPathMap(yamlText)` (5189–5230): regenerates the graph from YAML and maps node id → `_stepPath`; swallow-all catch returns empty map (5224–5227).
- `TryGetTopLevelStepIndex` (5248–5259); `BuildNodeIdToStepIndexMap` (5235–5246) — **no callers; dead** (Form1 has its own private equivalents at `Form1.cs:13699–13738`).
- `BuildIterationStackPayload` (5269–5290): converts executor `IterationFrame` stacks into `{loopId, i, label}` frames for the React iteration stepper; unresolvable frames are skipped individually, null when nothing resolves.
- Form1 keeps four live maps (`_nodeToStepPathMap`, `_stepPathToNodeIdMap`, `_nodeToStepIndexMap`, `_stepIndexToNodeIdMap`), refreshed on every apply/export (`Form1.cs:6971–6974`) and at run start (`Form1.cs:12332–12341`), and scoped down for test-step runs (`Form1.cs:7123–7129`).

### 1.4 Canvas layout persistence (per-preset Manual/Auto-flow)
`Services/FlowCanvasBridge.cs:5294–5597` + `Form1.cs:2419–2512, 6849–6884`.
- `ComputeStructureHash(nodes)` (5301–5322) and `ComputeStructureHashFromYaml` (5329–5355): SHA-256 over sorted `stepPath:blockType` tuples — value-edit-insensitive, structure-sensitive.
- `MergeLayout` (5383–5394): id-keyed position override, only used when the structure hash matches exactly.
- `TryMergeLayoutByTuple` (5403–5442): prefix-safe partial merge for Manual presets when structure changed — Safe only if every saved tuple survives; returns new-node ids for near-neighbor placement; otherwise caller clean-reflows (never mis-maps).
- `MergeAuxiliaryLayout` (5444–5523): re-applies disabled flags, expanded flags, and saved comments (reconciling against TextToGraph-emitted comment nodes by id).
- `ExtractLayout` (5529–5595): captures positions/comments/disabled/expanded + structure hash on Apply YAML (`Form1.cs:6989–7001`).
- **Autosave without Apply**: React `layout-autosave` → `Form1.ApplyLayoutAutosave` (2419–2502) overwrites positions (with per-node `stepPath`/`blockType` tuple keys), comments, disabled, expanded on the preset, computing the structure hash from the editor YAML if missing. `set-layout-mode` → `ApplySetLayoutMode` (2504–2512) persists the per-preset `LayoutMode`.
- **Load decision** (`Form1.LoadCurrentScriptIntoCanvas`, 6831–6890): effective mode = preset override else `WindowState.FlowCanvasDefaultLayoutMode` else AutoFlow; Manual + hash match → `MergeLayout` + `layoutAction:"keep"`; Manual + hash mismatch → tuple merge if safe; else `"reflow"`.

### 1.5 FlowCanvasForm — WebView2 host window
`UI/FlowCanvasForm.cs`.
- **Hosting**: modeless `Form`, reached via Form1 menu "Flow Canvas" (Ctrl+Shift+F, `Form1.cs:6605–6619`), single instance reused (`Form1.cs:6648–6655`). WebView2 with dedicated user-data dir `%LocalAppData%\SSH_Helper\WebView2\FlowCanvas` (117–122), dist served via virtual host `https://flowcanvas.local/index.html` (`SetVirtualHostNameToFolderMapping`, 173–179) from `FlowCanvasDistLocator.ResolveDistPath()` (`Utilities/FlowCanvasDistLocator.cs:16`). Missing dist shows searched-paths guidance in the status label (183–191). Status label until `NavigationCompleted` succeeds (145–159). Init runs on first `Shown` (92–107).
- **Handshake/queueing**: all `SendMessage` payloads marshal to the UI thread (`InvokeRequired`→`BeginInvoke`, 343–347) and queue in `ConcurrentQueue<string> _pendingMessages` until React posts `{type:'ready'}`; on `ready` the queue drains then `SendPersistedLayout()` runs (222–227, 352–362). A message sent before ready is never dropped; sent after dispose it is silently queued forever (354–361 — `IsDisposed` falls to the queue branch).
- **Inbound message dispatch** (`HandleHostMessage`, 214–310): `ready`, `apply-yaml`, `debug-action`, `test-step` (deprecated), `execute-canvas`, `breakpoint-toggle`, `run` / `run-request` (deprecated alias, logged), `disable-block`, `test-data-block`, `layout-save`, `pref-save`, `layout-autosave`, `set-layout-mode`, `browse-path`, `open-run-output-window`, `close-run-output-window`, `show-error` (modal via `DialogTheme.Show`), unknown types logged + ignored. 13 public `Action<JObject>` events (389–401), all wired in `Form1.OpenFlowCanvas` (6669–6815).
- **Display-settings persistence** (`SendPersistedLayout` 410–445, `SavePanelSizes` 447–494, `SaveReducedMotionPref` 496–508): `layout-restore` pushes ~14 `WindowState.FlowCanvas*` fields (panel sizes, heatmap, block width, text scale, density, default-expanded, snap, branch bands, compact comments, default layout mode, run-output color/wrap/follow); `pref-restore` pushes reduced-motion + iteration-history cap. `layout-save`/`pref-save` write back via `ConfigurationService.Update` with partial-field guards.
- **Window geometry**: static session cache `_lastLocation/_lastSize` (31–32); size persisted to `WindowState.FlowCanvasWidth/Height` on Dispose (518–534); **location is session-only, not persisted to config** (contrast RunOutputWindowForm which persists both).
- **Outbound helpers**: `SetTargetHost` (316–319, host bar payload or null), `SendRunOutputAppend`/`SendRunOutputClear` (322–332), `LoadGraph(nodes, edges, layoutMode, layoutAction, newNodeIds)` (371–382) + back-compat overload (385–386).

### 1.6 Debug/execution event forwarding (C# → React)
Wired in Form1 from `ScriptExecutor` events (via `_sshService`):
- Run start: `execution-started` (`Form1.cs:12342`) + pending breakpoints/disabled blocks filtered to mapped nodes and pushed into `ConfigureFlowCanvasDebugStateForRun` (12344–12361).
- `StepStarting` → `execution-update` `{stepId, state:"running"|"skipped", iterationStack}` (13601–13618).
- `StepCompleted` → `execution-update` `{stepId, state, duration, iterationCount, branchTaken, suppressedError, variables, iterationStack}` (13620–13650) + separate `step-output` `{stepId, output, iterationStack}` when output present (13652–13662). Matches `ExecutionUpdateMessage` in `communication-message-types.ts:65–81`.
- `DebugPauseStateChanged` → `debug-paused` `{stepId, lineNumber, variables, callStack}` / `debug-resumed` `{stepId, action, callStack}` (13665–13697). Call stack is a single-element subroutine name at most (13673–13676).
- `ExecutionCompleted` → `execution-finished` + a synthetic `debug-resumed` (13593–13599).
- Reverse direction: `debug-action` continue/step/stop (with deprecated `step-into`→step alias) drives `ActiveScriptContext.DebugState` / `_sshService.Stop()` (6719–6753); `breakpoint-toggle`/`disable-block` maintain `_pendingBreakpoints`/`_pendingDisabledBlocks` sets that survive between runs and sync into the live `DebugState` when a run is active (6756–6789).

### 1.7 Canvas-initiated execution
- `execute-canvas` `{mode, stepId}`: applies the graph first (`ApplyFlowCanvasGraph`, `Form1.cs:6914–7026`) — exports to YAML, updates `txtCommand` only when `graphChanged` (6932, 6966–6969), captures layout onto the active preset, replies `apply-result` `{success, errors, warnings, nodeStepMap, diagnostics[]}` (7028–7053) — then runs full (`ExecuteCanvasRun`) or test-step (6676–6695).
- **Test step** (`ExecuteCanvasTestStep`, 7055–7160): truncates the script through the selected top-level step, scopes node maps, auto-disables out-of-scope nodes, resolves the target host row, runs over SSH; failure responses go back as `test-step-result` (5 distinct early-out messages, 7062–7120). `ScriptPromptDialogRunner.AnchorFormOverride` anchors script prompt dialogs to the canvas window during the run (7145–7158).
- **Test data block** (`ExecuteCanvasTestDataBlock`, 7166+): runs a single data block (extract/parse/set/table/assert) against a supplied variable snapshot without SSH; replies `test-data-block-result`.
- `run` (and deprecated `run-request`) → `btnExecuteSelected.PerformClick()` (6698–6702).
- `browse-path` `{requestId, title, currentPath}` → native file/folder picker → `browse-path-result` `{requestId, canceled, path}` (6892–6912).

### 1.8 Run Output console + detachable window
- **Dock mirror**: Form1 mirrors all main-form output into the canvas Run Output tab: `SendRunOutputAppend` on every output append (13984), `SendRunOutputClear` on ClearOutput (14072), seeded with a buffered snapshot when the canvas opens (6827–6828).
- **Detachable window** `UI/RunOutputWindowForm.cs`: opened via React `open-run-output-window` → `Form1.OpenRunOutputWindow` (6621–6643). Second top-level Form with its **own WebView2 user-data dir** (`WebView2\RunOutputWindow`, 96–99, avoids profile lock contention) loading the same dist with `?panel=runoutput` (121) so React renders only `RunOutputView`. Owned and fed by Form1; independent of the canvas window; dark-only per design comment (15–19).
  - Same ready-queue handshake (136–165); on ready sends `layout-restore` with only the three run-output prefs (178–189); accepts `layout-save` writes for color/wrap/follow (191–205) — same `WindowState` fields as the canvas dock, so prefs stay in sync via config.
  - Mirror feed: `SendRunOutputAppend`/`SendRunOutputClear` (13984–13985, 14072–14073), seeded on open (6641–6642).
  - LIVE indicator: `SendRunState(bool)` reuses `execution-started`/`execution-finished` messages (176; sent at `Form1.cs:12369`, 12700, 13598).
  - Geometry fully persisted (size + location, `WindowState.FlowCanvasRunOutputWindow*`) using last-Normal-state values (207–240).
  - Close → `FormClosed` → Form1 sends `run-output-window-closed` so the React store docks the console back (6632–6637). Opening a fresh canvas closes an orphaned popped-out window (6657–6663).

### 1.9 Protocol catalog cross-check (`FlowCanvas/src/communication-message-types.ts`)
- React→C# (outgoing) — all 17 listed types handled in `FlowCanvasForm.HandleHostMessage`, plus deprecated `run-request`. **`show-error` is sent by React (`stores/messageBridge.ts:208`) and handled by C# (`FlowCanvasForm.cs:296`) but is absent from the catalog** — drift.
- C#→React (incoming) — all produced by C# except two: **`theme-sync` and `variables-snapshot` have React handlers (`messageBridge.ts:397, 454`) but no C# producer anywhere** (repo-wide grep) — dead inbound channels. Theme is fixed at construction (`darkMode` ctor arg); a mid-session app theme change never reaches an open canvas.

---

## 2. Integration points

| Connection | Direction | Mechanism / location |
|---|---|---|
| `ScriptExecutor` (`Services/Scripting/`) | events → React | `StepStarting`/`StepCompleted`/`DebugPauseStateChanged` forwarded as `execution-update`/`step-output`/`debug-paused`/`debug-resumed` (`Form1.cs:13601–13697`); `BranchTaken`/`IterationCount`/`StepPath` are the path-highlight contract |
| `ScriptParser` | bridge dependency | `TextToGraph` parses for step types/line numbers (381–391); option-key catalogs `GetKnownStepOptionKeysByCommand` etc. drive export validation (303–315, 3207–3220); `ComputeStructureHashFromYaml` (5329) |
| `SshService` / `ActiveScriptContext.DebugState` | React → C# | debug-action/breakpoint/disable handlers (`Form1.cs:6719–6789`); `ConfigureFlowCanvasDebugStateForRun` (12358) |
| `PresetManager` | layout persistence | `UpdateCanvasLayout` / `UpdateLayoutMode` (`Form1.cs:2501, 2509, 7000`); per-preset `CanvasLayout` + `LayoutMode` read on load (6856–6881) |
| `ConfigurationService` / `Models.WindowState` | display settings | ~20 `FlowCanvas*` fields round-tripped via `layout-restore`/`layout-save`/`pref-restore`/`pref-save` (`FlowCanvasForm.cs:410–508`) and run-output-window geometry (`RunOutputWindowForm.cs:178–240`) |
| `FlowCanvasDistLocator` (`Utilities/`) | asset resolution | exe-dir → project-root → embedded-extracted dist; both WebView2 hosts use it (`FlowCanvasForm.cs:165`, `RunOutputWindowForm.cs:117`) |
| `DialogTheme`, `AppDataPaths` | UI/conventions | dark title bar + themed error box (`FlowCanvasForm.cs:296–298, 403–408`); WebView2 data dirs under the app folder |
| `ScriptPromptDialogRunner` | dialog anchoring | `AnchorFormOverride = _flowCanvasForm` during canvas test-step runs (`Form1.cs:7145–7158`) |
| Host grid (CSV) | host bar | `SetTargetHost(BuildHostPayload(row))` on selection change (`Form1.cs:13491–13495`) |
| Main output pipeline | mirror | `AppendOutputText` → both canvas dock and detached window (13984–13985); `ClearOutput` → both (14072–14073) |
| Tests | consumers | `SSH_Helper.Tests/Services/FlowCanvasBridge*` (round-trip, nested export, tuple merge, layout persistence, parity), `SSH_Helper.Tests/UI/FlowCanvasForm*` + `RunOutputWindowForm*`; `FlowCanvas/tools/FlowCanvasParityCli` (second `InternalsVisibleTo`) |

---

## 3. Observed gaps & quirks

### Protocol / lifecycle
1. **Dead inbound channels**: `theme-sync` and `variables-snapshot` are declared and handled React-side but never sent by C# (no producer in the repo). Consequence: an open canvas never follows an app dark/light toggle (`_darkMode` fixed in ctor, `FlowCanvasForm.cs:34–36`), and the variables panel only updates via `execution-update`/`debug-paused`. Either implement the producers or prune the catalog.
2. **`show-error` missing from the protocol catalog** — sent at `FlowCanvas/src/stores/messageBridge.ts:208`, handled at `FlowCanvasForm.cs:296`, absent from `CANVAS_HOST_MESSAGES.outgoing`. The catalog header comment also documents `load-graph` as prose rather than a typed entry, so the "typed protocol" is partially honor-system.
3. **Messages queued after dispose are silently lost**: `PostOrQueue` (`FlowCanvasForm.cs:352–362`) enqueues when `IsDisposed` — nothing ever drains that queue. Harmless in practice (Form1 null-checks `_flowCanvasForm`), but a race between `FormClosed` and an in-flight `BeginInvoke` drops messages with no trace.
4. **No retry/timeout on the ready handshake**: if React never posts `ready` (JS crash, bad dist), queued messages accumulate forever and the user sees a blank canvas with no diagnostic beyond the nav-success label. There is no watchdog.
5. **`WebMessageReceived` failures are Debug-only** (`FlowCanvasForm.cs:208–211, 306–309`; `RunOutputWindowForm.cs:133`): a malformed message or handler exception is invisible in release builds.

### Import/export round-trip
6. **Branch-internal INLINE comments are dropped on container regeneration** — acknowledged in code (`FlowCanvasBridge.cs:1304–1308`, 4649–4653): they survive only via the snippet path; once a container regenerates (any prop edit sets `_forceGraphExport` on ancestors), nested inline comments vanish from the YAML. Deferred "Task 5b" per `docs/superpowers/plans/2026-06-04-flow-canvas-comment-flow.md`.
7. **Parallel branches export only their first node** with a warning (`FlowCanvasBridge.cs:2266–2272`) — a user who builds a multi-step parallel arm on the canvas silently loses steps 2+ unless they read the warning.
8. **`MaxNestingDepth = 5`** (`FlowCanvasBridge.cs:74, 826`): deeper nesting is invisible on the canvas and unmapped for debug events. No user-facing notice when the cap is hit.
9. **Preamble export normalizes key order** (`SerializeStartPropsToPreamble`, 4895–4992): emission order is fixed (name, description, version, …, vars, imports, subroutines, unrecognized), so an imported script whose preamble keys were ordered differently is canonicalized on export even when "unchanged" — only mitigated by Form1's `graphChanged` gate (6932). Also `version: 1` is captured (`!= 1`, 4837) but only re-emitted when `> 1` (4920) — a script with explicit `version: 1` loses the line on regeneration.
10. **`ExtractYamlSection` prefix match is loose** (`FlowCanvasBridge.cs:5084): `StartsWith(sectionKey)` after `TrimStart()` means a line like `vars:x` or a key beginning with the same prefix could be misdetected; combined with `ExtractUnrecognizedSections`' `StartsWith` known-key check (5135) a top-level key like `steps_extra:` would be treated as known and dropped.
11. **`EscapeYamlString` is heuristic** (5067–5073): quotes only on newline/colon/hash/edge-spaces; a value containing other YAML-significant leads (`*`, `&`, `[`, `{`, `>`...) round-trips unquoted via elif conditions / case values (2057, 2215).
12. **Duplicate `"max_seconds"` literal** in `IntegerOptionKeys` (lines 174 and 182) — harmless in a HashSet, but signals copy-paste accretion in the hand-maintained option-type tables (138–205), which must be kept in sync with the parser by hand.
13. **Dead public API**: `ToGraph(Script)` (1028) and `BuildNodeIdToStepIndexMap` (5235) have no callers (Form1 reimplements the index map privately at 13728). Step-index compatibility paths (`BuildStepIndexToNodeMap`, `TryGetTopLevelStepIndex` fallback in `TryResolveCanvasNodeId`) suggest a legacy correlation layer that could now be a single mechanism.
14. **Silent catch on canvas load**: `LoadCurrentScriptIntoCanvas` swallows all exceptions (`Form1.cs:6886–6889`) — a YAML script that parses in the editor but blows up `TextToGraph` yields a blank/stale canvas with zero feedback.
15. **`BuildNodeIdToStepPathMap` swallows all exceptions** (5224–5227) returning an empty map — a run on YAML the bridge can't graph silently loses all canvas highlighting (debug log only).

### Layout persistence
16. **Layout autosave races the active preset**: `ApplyLayoutAutosave` keys off `_activePresetName` at handler time (`Form1.cs:2421`); a debounced React autosave that arrives after the user switches presets would write the previous preset's positions onto the new active preset. No payload-side preset identity check.
17. **`MergeLayout` positions are id-keyed** (5383–5394) and node ids are regenerated `node-{counter}` on every `TextToGraph` (331) — correct only because the call site gates on an exact structure-hash match; the invariant is documented in a comment but not enforced.
18. **Window-geometry persistence is inconsistent**: FlowCanvasForm persists size only (location is session-static, `FlowCanvasForm.cs:31–32, 518–528`); RunOutputWindowForm persists both and clamps to last-Normal (223–240). Neither validates against current monitor bounds, so a saved position on a disconnected second monitor restores off-screen (RunOutputWindowForm path).
19. **FlowCanvasForm saves size in `Dispose`** (530–534) rather than `FormClosing` — config write happens during teardown; an app-crash path skips it (RunOutputWindowForm uses `FormClosing`, 73).

### Run output window
20. **Second full WebView2 environment per pop-out**: separate user-data folder/browser process (`RunOutputWindowForm.cs:96–101`) costs ~100MB+ working set for what is a text console; no reuse/pooling. Deliberate (lock contention), but worth noting for a tool that may run on jump boxes.
21. **No light theme**: the window is "dark-only (matches the console)" (15–19) but still receives the app `darkMode` flag and conditionally sets colors (68–69, 108) — half-implemented theme plumbing.
22. **Unknown message types in `RunOutputWindowForm.HandleHostMessage` are silently ignored** (136–150 has no default-case logging, unlike FlowCanvasForm 301–303).

### Debug forwarding
23. **Call stack is at most one frame** (`Form1.cs:13673–13676`): `debug-paused.callStack` carries only the current subroutine name — nested `call` chains aren't represented, though the React payload shape implies a real stack.
24. **`execution-finished` is always `success: true`** from `SshService_ExecutionCompleted` (13596); the richer path at 12696 sends a real result, so the canvas can receive a success-flagged finish for a failed/stopped run depending on which path fires.
25. **Pending breakpoints/disabled blocks are node-id keyed across graph reloads** (`_pendingBreakpoints`, 6756–6789): ids regenerate on every `TextToGraph`, so toggles made before a reopen/reload silently target ids that no longer exist (filtered out at 12346–12356 — fail-quiet). Memory note confirms `breakpoints` Set on the React side is also not cleared across preset switches.
