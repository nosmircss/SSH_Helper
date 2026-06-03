# Night Run Status — Flow Canvas: Bigger Blocks, Labeled Lanes & Expandable Settings

**Date:** 2026-06-02 (overnight autonomous run)
**Branch:** `flow-canvas-blocks-bands-expansion` (NOT pushed, NOT merged — left for your review)
**Base:** plan commit `e43f4f0` (off 0.51.22 lineage)
**Result:** ✅ **All 5 phases / all 18 plan tasks complete.** All required verification green except two *pre-existing* environmental flakes that are unrelated to this work (detailed below). Final independent code review: **APPROVE**.

Execution: subagent-driven (fresh subagent per task, TDD red→green→commit, one task per commit) per `superpowers:subagent-driven-development`. 22 commits total (18 plan tasks + 4 follow-up fixes from final verification & review).

---

## TL;DR for your coffee

- The feature works as specified: blocks are 330/300, branch bands are labeled lanes (pill + soft border + left accent + brighter nested tint), any block expands in place to a read-only settings summary, and the auto-layout is height-aware (expanded blocks push neighbors and grow their lanes; toggling reflows).
- Zero changes to edges/edge-routing/colors/arrowheads or YAML import/export semantics (verified, including a C# test proving `expanded` never reaches exported YAML).
- 4 things to glance at: (1) one intentional plan deviation on the band-depth formula, (2) the reflow-on-toggle = Option A tradeoff you already approved, (3) two pre-existing test flakes that are NOT mine, (4) a couple of optional polish items. All under "Needs your attention".

---

## Final verification (actual results, run at HEAD `7055da6`)

| Command | Result |
|---|---|
| `cd FlowCanvas && npm run build` | ✅ Clean — `tsc` + `vite build` succeed (only the pre-existing chunk-size advisory; no errors) |
| `cd FlowCanvas && npm test` | ✅ **93 passed** (12 files), 0 failed |
| `cd FlowCanvas && npx playwright test` | ✅ **103 passed**, 0 failed, 1 *flaky* (see note A) |
| `dotnet build SSH_Helper.sln` | ✅ **0 Errors** (13 pre-existing warnings) |
| `dotnet test SSH_Helper.Tests/...` | ⚠️ **2394 passed / 2 failed** in the full parallel run — both failures are **pre-existing parallel-execution flakes that PASS in isolation** and do not touch Flow Canvas (see note B). FlowCanvasBridge-scoped tests: **99/99 pass** (incl. the 3 new persistence tests). |

**Note A — Playwright flaky:** exactly one spec flakes between runs (`flow-canvas-preset-parity` or `flow-canvas-preset-negative`), always passing on the automatic retry. Root cause: the e2e harness builds the `FlowCanvasParityCli` C# tool on first use (`ensureParityCliBuilt` runs `dotnet build`); on a cold session this races with other dotnet builds and fails once, then succeeds. I verified the CLI **builds standalone with 0 errors**, so this is pre-existing harness fragility, not a code defect. All 103 functional tests pass.

**Note B — C# full-suite "2 failed":** The two are `PresetManagerTests.Export_IncludesFolder` (`config.json ... being used by another process` — a parallel test-isolation file-lock race) and `ReadFileCommandTests.ExecuteAsync_SelectFileTrue_PathOnly_UsesNativeFileDialogByDefault` (the native-file-dialog test that hung the baseline run). **Both pass when run in isolation** (verified: 1/1 each). Neither touches Flow Canvas code. This matches the known project gotcha ("WinForms UI/dialog tests flake/hang under full parallel `dotnet test`, pass in isolation"). The bare `dotnet test` was never cleanly green even at baseline on `master` — at baseline it *hung* the test host on the same native-dialog test. This work did not introduce or worsen it.

Baseline at start of run (for comparison): vitest 72/72, npm build clean, dotnet build 0 errors, e2e harness working (auto-layout spec 2/2), dotnet test 2296 pass then host hang on the native-dialog test.

---

## Phases & tasks (all complete)

### Phase 1 — Block sizing (330 / 300) + density
| Task | Commit | Notes |
|---|---|---|
| 1.1 nodeSize.ts module | `84e345c` | New single-source dims (330/300/52 + summary metrics) |
| 1.2 BaseBlock widths + density | `f88d455` | 330/300, icon 20, header 6/9, label 13, preview 12 |
| 1.3 StartNode width 330 | `5f24a71` | |
| 1.4 Layout column width (TS) | `5b65618` | `CHILD_NODE_MAX_WIDTH` 300 → cols 330 |
| 1.5 Mirror constants in C# | `61227c0` | + new `FlowCanvasBridgeLayoutPersistenceTests` |
| 1.6 e2e threshold + YAML round-trip | `4c80f79` | e2e 260→300; YAML round-trip confirmed via 97 C# bridge tests (per your instruction, no WinForms launch) |

### Phase 2 — Labeled lanes (branch bands)
| Task | Commit | Notes |
|---|---|---|
| 2.1 branchBands geometry + pill labels + 18px pad | `6e5b247` | Geometry from real dims |
| 2.2 BranchBandsLayer labeled lanes | `3510178` | pill + border + accent + nested tint; **corrected depth formula** (see Deviations) |

### Phase 3 — `expandedNodes` state + persistence
| Task | Commit | Notes |
|---|---|---|
| 3.1 expandedNodes in debugSlice | `dbbd389` | Mirrors `disabledBlocks` |
| 3.2 layout-autosave serialize + restore | `4cde304` | |
| 3.3 C# `ExpandedNodeIds` extract/merge | `784c8a2` | + Clone test + **export-ignores-expanded** test. (Threaded in `Form1.cs`, not `FlowCanvasForm.cs` — see Deviations.) |

### Phase 4 — Read-only summary + chevron
| Task | Commit | Notes |
|---|---|---|
| 4.1 blockSummary.ts helper | `9c222d1` | required + non-default rows; masking; "— not set" |
| 4.2 chevron + summary in BaseBlock | `5fff11c` | preview replaced when expanded; "Edit in Properties" selects node |

### Phase 5 — Height-aware layout (integration)
| Task | Commit | Notes |
|---|---|---|
| 5.1 expanded height estimate | `761f086` | `estimateNodeHeight` |
| 5.2 height-aware vertical advance | `cbba0a9` | `advanceFor`; `placeComments` deliberately untouched |
| 5.3 bands wrap expanded children | `38a83f2` | |
| 5.4 reflow on toggle (**Option A**) | `3fcb757` | always re-runs `computeHierarchicalLayout`; real behavioral test (expand pushes successor 252→308) |
| 5.5 e2e expansion push + lane growth | `e6a1850` | 2 e2e cases pass |

### Follow-up fixes (from final verification + independent review)
| Commit | Why |
|---|---|
| `ca174fc` | **Bug fix:** `BranchBandsLayer` mixed the `border` shorthand with `borderLeft` → React console.error ("can lead to styling bugs", left accent clobbered on rerender). Switched to longhand `borderTop/Right/Bottom` + `borderLeft`. (Came verbatim from the plan's Step 2 snippet.) |
| `3605237` | **Drift fix (tests):** two pre-existing e2e specs encoded the OLD behavior I intentionally changed — `flow-canvas-edge-geometry` pinned the Start node to ~280px (now 330) and `flow-canvas-branch-bands` asserted the band's left border was the *pure* branch color (now `mix(branch,70%)`). Updated both to the new contract. These weren't in the plan's file list. |
| `65de167` | **Fidelity fix (found by final review):** `blockSummary.isRequired` for `interactive` blocks dropped the panel's `max_seconds`/`max_lines` "required when window hidden and neither set" branch. Ported it faithfully + added a test. Affects only the read-only summary (no YAML/validation/execution impact). |
| `7055da6` | **Docs:** corrected stale `// 290` column-width comments to 330 in `hierarchicalLayout.ts` and `FlowCanvasBridge.cs`. |

---

## Deviations from the plan (all intentional, all documented)

1. **Band nesting-depth formula (Task 2.2).** The plan suggested `depth = stepPath.split('/').length - 2`. That is wrong: every branch child's stepPath has ≥4 segments (e.g. `steps/0/then/0`), so `length-2` is ≥2 for *every* band, making `nested = depth >= 1` always true and the non-nested styling dead code — which defeats the spec's entire "nested reads brighter than outer" goal. I implemented a correct `branchDepth` that counts branch-keyword segments and subtracts 1 (`steps/0/then/0` → 0 = outermost; `steps/0/then/0/else/0` → 1 = nested), with TDD tests locking it in. The independent review confirmed this is correct.

2. **C# threading site (Task 3.3).** The plan said thread `expandedNodeIds` through `UI/FlowCanvasForm.cs`. In reality that file only relays the autosave message via an event; the disabled-blocks threading actually lives in `Form1.cs` (two sites: `ApplyLayoutAutosave` and the `ExtractLayout` call). I edited `Form1.cs` to mirror `disabledBlocks` and left `FlowCanvasForm.cs` untouched. `ExtractLayout` got an **optional** `expandedNodeIds` param (backward-compatible; one caller).

3. **Reflow-on-toggle = Option A (Task 5.4).** Per your explicit instruction, implemented Option A exactly: toggling expand always re-runs `computeHierarchicalLayout`. Tradeoff (as the plan's open decision noted, and the review re-flagged): on a *manually-arranged* canvas (`hasUserLayout`), expanding a block re-derives all positions and then autosaves them, discarding manual nudges. This is the accepted v1 behavior for a preset-builder (canvases are mostly auto-laid). If you later want to preserve manual layouts, that's the Option B follow-up.

4. **Longhand borders (follow-up `ca174fc`).** Deviated from the plan's literal `border` + `borderLeft` to avoid the React shorthand/longhand conflict. Visual is identical.

No other deviations. Nothing in scope touched edges, edge routing, colors/arrowheads, or YAML import/export.

---

## Independent final code review — APPROVE

A full-branch review (feature-dev:code-reviewer, opus) verified all six critical invariants **PASS**:
1. No edge changes (`AnimatedEdge.tsx` etc. untouched). 2. `expanded` never leaks to YAML (export reads `node.data.props` only; proven by the `Export_ignores_expanded_flag` test). 3. TS↔C# layout parity (300/330 in both). 4. No raw hex outside the token layer. 5. `expandedNodes` mirrors `disabledBlocks` end-to-end without breaking the disabled path. 6. No import cycles (`blockSummary` does not import `nodeSize`).

The review's one "Important" finding (the `interactive` summary gap) was fixed in `65de167`. Its minor notes (stale comments) were fixed in `7055da6`. Remaining minor observations are non-blocking (see below).

---

## Needs your attention (morning review)

1. **Visual sign-off (only thing not automatable here).** Per your instruction I did NOT launch the WinForms app. Please eyeball in the running app: 330/300 block sizes, the THEN/ELSE/LOOP/CASE pills, nested-lane brighter tint, the expand chevron → read-only summary, and "Edit in Properties" focusing the panel.

2. **Minor cosmetic — chevron placement.** Per the plan, the expand chevron uses `marginLeft: 4` after the exec-indicator. On an idle block (no exec-indicator) it therefore sits just right of the label rather than far-right. Trivial to change to `marginLeft: 'auto'` if you'd prefer it pinned to the header's right edge. Left as-planned to avoid unrequested cosmetic drift.

3. **Option B (manual-layout preservation)** — see Deviation #3. Only relevant if users hand-arrange canvases and expect nudges to survive an expand toggle.

4. **Pre-existing test flakes (NOT introduced by this work)** — see Notes A & B above. If you want the bare `dotnet test` to be cleanly green, the `PresetManagerTests.Export_IncludesFolder` test needs an isolated config path (it uses the real `%LocalAppData%` config and races), and the native-file-dialog `ReadFileCommandTests` test needs a headless guard. Both are out of scope for this branch.

5. **Minor (non-blocking, from review):** `blockSummary.toBool` is slightly narrower than the panel's `toBoolean` (doesn't accept `yes`/`no`/`1`/`0`), but the props it gates only ever carry boolean/`"true"`/`"false"`, so no behavioral divergence today.

---

## How to pick this up

```bash
git switch flow-canvas-blocks-bands-expansion
git log --oneline e43f4f0..HEAD        # 22 commits
cd FlowCanvas && npm run build && npm test && npx playwright test
cd .. && dotnet build SSH_Helper.sln
dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridge"   # 99/99, avoids the parallel-flake hang
```

Working tree is clean. Nothing pushed, nothing merged.
