# SSH_Helper Flow Canvas — Enhancement Scope

> A scoping document for making the Flow Canvas look **amazing / flashy / premium** *and* become genuinely **feature-rich** — without breaking the WebView2 / offline single-file / YAML round-trip constraints. Scoping only; no code changes.

## Executive Summary

The Flow Canvas is functionally deep but visually unfinished: 36 block types render as near-identical colored rectangles (the `icon` field every block declares is drawn nowhere), nodes bypass the half-built `--fc-*` token system entirely (so light mode leaves dark node bodies on a light canvas), edges are flat `#666` with no arrowheads, and the engine's 71 built-in functions, lambdas, and expression grammar are invisible. At the same time the codebase is *unusually well-positioned* for a premium leap: every flashy upgrade we want (icons, gradient edges, traveling data packets, glow halos, execution cinematics, replay scrubbing, heatmaps) is **visual-only or transient runtime state** that never touches the YAML export path — and the few high-value capability gaps (multi-host targeting, function discoverability, conditional breakpoints) reuse message patterns that already exist. This catalog scores **44 proposals** across seven themes, sequences them into four waves anchored by a load-bearing design-token foundation, and flags exactly where C# bridge work and round-trip verification are required so the team can ship aggressive polish *by default* instead of as a risky one-off.

---

## Current State Assessment

An honest read of where the canvas is today, grounded in the codebase.

### Visual layer — competent but dated

| Area | Reality today |
|------|---------------|
| **Block identity** | All 36 blocks read as same-shaped colored rectangles distinguished only by a text badge. `registry.ts` declares an `icon` per block (`ssh`, `if`, `for`, `http`, `vault`…) — **rendered nowhere** on the canvas. |
| **Design tokens** | `theme.ts` emits only ~8 `--fc-*` vars, consumed by **4 panels**. Nodes/edges/registry use raw hardcoded hex (~322–399 literals across ~25 files). The same `#4a9eff` / `#2ecc71` / `#e74c3c` triplet is duplicated in BaseBlock, AnimatedEdge, App.tsx, and registry. |
| **Light mode** | Half-baked: `theme.ts` has a light palette but nodes hardwire dark hex, so light mode is effectively broken for node bodies. `messageBridge` intentionally ignores theme-sync ("canvas is always dark"). |
| **Depth** | Flat single-color fill, 2px solid border, fixed 8px radius, shadow only when running/selected. No resting elevation, no surface ladder, no glass. `StartNode` is the one premium-feeling node (gradient + resting glow). |
| **Edges** | Flat `#666` smoothstep, **no arrowheads** (markerEnd passed through but never assigned a `MarkerType`), monochrome (ignores incoming branch stroke colors), marching-ants injected per-edge as duplicate inline `<style>`. |
| **Containers** | Children are flattened sibling nodes (not React Flow sub-flows). No visual containment/frame; branch membership (`_branchColor`/`_branchLabel`, already emitted by the bridge) is ignored by BaseBlock. |
| **Motion** | Two keyframes (`exec-pulse`, `spin`) + marching ants. No enter/exit, hover, or focus micro-interactions. **No animation library installed** and no `prefers-reduced-motion` handling anywhere. |
| **Chrome** | MiniMap/Controls/Background are stock React Flow with ad-hoc per-render hex; panels use flat fills. |

### Feature layer — a powerful engine, barely surfaced

- **Add/find is impoverished:** the only way to add a block is drag-from-Palette (no Ctrl+K, no quick-add); the Palette has no search/filter/favorites; finding a node means cycling Ctrl+F.
- **Engine power is hidden:** 71 functions + lambdas + the full expression grammar live only in C#. `code`-type property fields render as a plain `<input>`. No autocomplete, no signature help, no function catalog on the React side.
- **Multi-host identity is faked:** SSH_Helper's whole point is many-host execution, yet the canvas knows a single read-only `targetHost`; canvas runs are hardwired to one resolved row.
- **Observability is shallow:** `blockTimings` is wired into the badge but **never populated during live runs** (a real bug — duration arrives on the wire but is dropped into the timeline only). No run console, no heatmap, no loop/branch instrumentation, no conditional breakpoints, no durable replay.
- **No validation affordance on the canvas:** the only "this won't run" signal is a greyed-out Run button; C# already computes per-node diagnostics (with `NodeId`) but they're flattened to strings before reaching React.

### The good news (why this is tractable)

The export path (`exportGraph.ts` → `FlowCanvasBridge.ExportToYaml`) serializes **`node.data.props` only**, never styling, positions, comments, or transient execution state. A CSS-var sweep, icons, edge animation, glows, heatmaps, replay, and frames **cannot alter the exported payload** — and Playwright parity specs (`flow-canvas-parity.spec.ts`, `flow-canvas-preset-parity.spec.ts`) exist to prove it.

---

## Design Vision

**Target identity:** a dark-first *command center* — Linear/Raycast surface discipline, ComfyUI/n8n node anatomy, Unreal Blueprint execution-flow energy — that feels alive during a run and reads as a flagship IDE at rest. Flashy where it earns attention (running blocks, data flow, run completion), calm everywhere else.

### Visual system direction (the token foundation)

A single CSS-custom-property layer is the prerequisite for everything else. Proposed ~30-token set, authored in **OKLCH** (natively supported by WebView2's Chromium, plus `color-mix()`):

| Token group | Direction |
|-------------|-----------|
| **Surface ladder** | `--fc-surface-0` (canvas) → `-1` (blocks/panels) → `-2` (popovers) → `-3` (modals), each ~6% lighter. Elevation via lightness + 1px low-alpha hairline borders, **not** drop shadows. |
| **Text ramp** | `--fc-text` / `-secondary` / `-muted` — off-white, never pure white. |
| **State colors** | `--fc-running` / `-success` / `-error` / `-skip` — one source, consumed by nodes, edges, minimap, heatmap, celebration. |
| **Category hues** | 7 hues in OKLCH at constant L/C, rotating H, so `ssh`/`control`/`data`/`network`/`io`/`grid`/`timing` read with equal vividness. Each derives a body tint via `color-mix(in oklch, var(--fc-cat) 12%, var(--fc-surface-1))` plus a readable `badgeText`/body-text. |
| **Scales** | `--fc-r-sm/md/lg` radii, `--fc-glow` intensity scale, type scale + tabular-nums for duration badges. |

### Depth principles

- **Accent rail, not blob.** Category color becomes a 3–4px left rail + icon tint; node body neutralizes to a surface tone. Legible at any zoom.
- **Glass for chrome only.** `backdrop-filter: blur(12px) saturate(140%)` on Toolbar/MiniMap/Palette/panels — **never on nodes** (protects WebView2 framerate).
- **Glow is a signal.** Reserve halos/rings for active/selected/exec/breakpoint states; layer multi-shadow so they stack instead of fighting one box-shadow slot.

### Motion principles

- **Zero new animation library.** CSS `@property` animated gradients, keyframes, WAAPI (`element.animate`), SVG `offset-path`, and native `document.startViewTransition()` — all offline-safe in Chromium.
- **Compositor-only.** Animate `transform`/`opacity`/`box-shadow`; drive state via class/data-attribute toggles, never per-frame React re-renders.
- **A central kill switch.** One `.fc-reduced-motion` body class (auto-detected + manual toggle) disables every effect at once — the responsible enabler that makes aggressive motion shippable over RDP/software-GPU sessions.

---

## Enhancement Catalog

All 44 proposals, grouped by theme. Scorecard legend — **Impact** & **Wow** 1–5; **Effort** S/M/L/XL; **Risk** low/med/high; **Rec** = quick-win / big-bet / nice-to-have.

### Theme 1 — Visual Polish & Aesthetics

**Token Forge: A Real Design-Token Foundation**
Replace scattered hex with one OKLCH CSS-variable token layer (~30 tokens) that every node/edge/panel consumes; finally makes light mode and skins work. *Value:* coherent palette, working light mode, trivial future theming.
`Impact 4 · Wow 3 · Effort M · Risk low · Rec big-bet` — *load-bearing prerequisite; sequence first.*

**Iconic: Wake the Dead Icon Field**
Build an inline-SVG sprite keyed by the existing `def.icon` names; render a category-tinted icon chip in BaseBlock headers and Palette items. *Value:* instant at-a-glance block recognition; flagship-product feel.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *value lives in glyph craftsmanship.*

**Surface & Glass: Premium Node Cards**
Neutralized surface body + category gradient + inner top-highlight, slim left accent rail, frosted header band, layered multi-shadow for stacked states. *Value:* the canvas reads as a designed product; accent stays legible at zoom.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *reconcile with exec-pulse; drop the "dashed nesting frame" sub-claim (children are separate nodes).*

**Live Wires: Gradient Edges with Traveling Data Packets**
Arrowheads (`MarkerType.ArrowClosed`), source→target gradient strokes, 1–2 GPU data-packet dots via `offset-path` while running, optional value pills at the leading edge. *Value:* running a script becomes a living dataflow movie; flow direction unambiguous.
`Impact 4 · Wow 5 · Effort M · Risk low · Rec quick-win` — *per-color marker/gradient ids needed; cap packets, gate on running.*

**Branch Bands & Containment: Make Nesting Readable**
Activate dormant `_branchColor`/`_branchLabel` metadata as child accent stripes + then/else/catch chips, plus a translucent group frame behind each container's child cluster. *Value:* large if/foreach/try bodies read as cohesive color-coded regions.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *frame overlay must track viewport transform; ship rail+chip first.*

**Glass HUD & Living Aurora: Premium Canvas Chrome**
Frosted-glass Controls/MiniMap/panels + a breathing ambient aurora behind the dots that warms toward the exec color during runs. *Value:* modern command-center feel; minimap doubles as legend.
`Impact 3 · Wow 4 · Effort M · Risk med · Rec nice-to-have` — *GPU-bound; cap blur ≤14px, ≤3 orbs, gate motion; pure atmosphere, no new capability.*

### Theme 2 — Motion & Micro-interactions

**Choreographed Execution: CSS-Only State Cinematics**
`@property` conic-gradient glow halo on running blocks, success checkmark draw-in + pop, error shake + ripple, count-up duration badges, View-Transition morphs on layout/theme/panel changes. *Value:* the debugger feels flagship; smooth morphs, no bundle bloat.
`Impact 4 · Wow 5 · Effort M · Risk med · Rec quick-win` — *delete inline exec-pulse to avoid double-glow; scope view-transitions to theme/panel first.*

**Pop-In / Pop-Out Node Lifecycle**
WAAPI scale+fade on block add/delete, staggered cascade on script import so the graph "assembles itself." *Value:* tactile authoring; re-opening a script feels crafted.
`Impact 3 · Wow 4 · Effort M · Risk med · Rec quick-win` — *ship pop-in + cascade now; exit animation is the fiddly part (defer).*

**Cinematic Debug Spotlight + Camera Follow**
Radial-gradient mask follows the paused/executing block while the camera pans to keep it centered; vignette pulse on breakpoint hit. *Value:* never lose the active block; demo-ready.
`Impact 3 · Wow 5 · Effort M · Risk med · Rec nice-to-have` — *RDP perf risk; gate behind explicit toggle + "user-panned suspends follow" guard.*

**Inline Result Chips That Animate In**
On completion, animate a compact result chip into the node (duration count-up + send capture value + generic snippet in Phase 1; http/ping/branch/iteration in Phase 2). *Value:* read a whole run's results by scanning the canvas like a dashboard.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *Phase 1 rides existing data; structured chips need C# step-output enrichment.*

**Hover Lift + Floating Action Toolbar**
Hover lift + brighter border + `NodeToolbar` (bundled) with duplicate/disable/breakpoint/delete; handles grow on hover; breakpoint dot pulses. *Value:* common actions stop requiring right-click; wiring is less fiddly.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *only new piece is `duplicateNodes`; scope duplicate to non-containers in v1.*

**Run Progress Bar + Minimap Radar**
Shimmering top progress bar (step N/M) + MiniMap node glyphs recolored by live exec state. *Value:* whole-run situational awareness without scrolling.
`Impact 3 · Wow 3 · Effort S · Risk low · Rec quick-win` — *validate SVG-rect pulse in WebView2; match executable-node denominator to engine semantics.*

**Reduced-Motion Kill Switch (the responsible enabler)**
One uiSlice toggle + `prefers-reduced-motion` auto-detect → `.fc-reduced-motion` body class disabling all halos/packets/spotlights/aurora/shimmer. *Value:* RDP/VM users keep a smooth canvas; accessibility win.
`Impact 4 · Wow 2 · Effort S · Risk low · Rec quick-win` — *build first; persistence is NOT free via panel-size path (int-only) — needs a WindowState bool or localStorage.*

### Theme 3 — Canvas UX & Navigation

**Cmd-K Command Deck: Spawn Blocks, Jump, Run + Discover Engine Functions**
Ctrl/Cmd-K fuzzy launcher over 4 groups: spawn any of 36 blocks, jump to any node, fire commands, discover the 71 engine functions. *Value:* keyboard-first authoring; the single biggest premium-IDE upgrade.
`Impact 5 · Wow 4 · Effort L · Risk med · Rec big-bet` — *ship spawn/jump/commands first (round-trips free); function-catalog pillar needs net-new functions.json + focused-field state. No icon sprite exists yet.*

**Drag-to-Empty Quick-Add + Insert-on-Edge Splice**
Drop a wire on empty canvas → searchable picker creates+connects; hover "+" on an edge splices a node between two nodes with inherited branch metadata. *Value:* continuous authoring flow; ~halves clicks to extend a script.
`Impact 5 · Wow 4 · Effort L · Risk med · Rec big-bet` — *picker is net-new; splicing into imported containers needs `_forceGraphExport` to avoid half-snippet corruption.*

**Frames: Translucent Labeled Regions (Layout-Only)**
First-class resizable, color-tagged, titled regions behind blocks; rides the comment layout-only channel so it never enters YAML. *Value:* spatial organization for 60-block scripts.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *effort is M not S: persistence threads through ~8 spots (slice, node, autosave, CanvasLayoutData, Form1 parse, bridge merge/extract/hash). Ship v1 as pure backdrop; defer drag-children.*

**Outline Tree: A Navigable Spine for the Script**
Collapsible tree mirroring script structure (container nesting via `_isChildOf`), click-to-pan-and-select, live exec-state coloring, keyboard nav. *Value:* see a script's shape at a glance; doubles as a debug tracker.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *pure read of store; tree-build must nest by `_isChildOf` ancestry, not naive topo-walk.*

**Alignment Guides + Snap (Helper Lines)**
Vendor the React Flow Pro helper-lines util + overlay; smart object-snap + grid-snap persistence. *Value:* clean, professional hand-arranged layouts.
`Impact 3 · Wow 4 · Effort M · Risk low · Rec quick-win` — *snap toggle already exists; only persistence + helper lines are new. Keep per-drag scan O(local).*

**Multi-Select Action Bar + Bulk Ops**
Floating `NodeToolbar` on 2+ selection: enable/disable all, set/clear breakpoints, align, distribute. *Value:* bulk cleanup/debugging becomes single gestures.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *cut the fabricated "Group-into-Frame" (no frameSlice; no YAML repr) — split to a separate Frame proposal.*

**Searchable Palette with Icons, Favorites & Recents**
Sticky filter input, per-block icons, collapsible categories with counts, pinnable favorites + recents. *Value:* with 36 blocks/7 categories, type-to-filter + favorites removes everyday friction.
`Impact 4 · Wow 3 · Effort M · Risk med · Rec quick-win` — *icon sprite must be built (none exists); persist favorites in WebView2 localStorage, not the bridge.*

**Breadcrumb + Zen/Focus Mode for Nested Containers**
Breadcrumb from the `_isChildOf` chain (Start › foreach › if › send) + double-click-to-focus that dims everything outside a container's subtree and `fitView`s onto it. *Value:* deeply nested logic becomes navigable.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *mostly assembly of existing patterns; purely visual (opacity + camera).*

### Theme 4 — Authoring & Feature Richness

**Smart Cards: Multi-Field Previews & Inline Validation Badges**
Type-aware chip card (http: `[GET] url [bearer]`; foreach: `item in <collection>`; switch: `N cases`) + a corner badge on blocks missing required fields. *Value:* understand a block without opening Properties; catch misconfigs on the canvas.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *100% presentational over data that round-trips; ~12 per-type cases + fallback.*

**Expression Builder for 'code' Fields with Function Autocomplete + Live Validation**
Mini-editor for the 15 `code`-typed fields: operator buttons mirroring `ExpressionEvaluator`, function/variable/column typeahead, token highlighting, cheap client checks + C# deep-validate round-trip. *Value:* stop memorizing the grammar from C# source; catch errors before a run.
`Impact 5 · Wow 4 · Effort L · Risk med · Rec big-bet` — *string-in/string-out keeps round-trip safe; phase the contentEditable tokenizer + C# validation. No CodeMirror/Monaco.*

**Subroutine Library + Call Graph: Jump-to-Definition for 'call' Blocks**
Parse `subroutines_yaml` into a named list; turn the call field into a validated dropdown; flag undefined/recursive calls; animated jump-to-definition preview. *Value:* reusable subroutines become first-class, not stringly-typed.
`Impact 4 · Wow 3 · Effort L · Risk med · Rec nice-to-have` — *dropdown + lint is a clean quick slice; the call-graph card needs subroutine bodies (no JS YAML parser) → C#-pushed payload.*

**In-Canvas Environment & Multi-Host Targeting Bar**
Promote HostBar to an interactive env picker + multi-host chip chooser showing available `{{column}}` vars. *Value:* pick environment and target hosts without leaving the canvas; author against real columns.
`Impact 4 · Wow 3 · Effort L · Risk med · Rec big-bet` — *bridge/UI half is cheap; the multi-host execution rewire (off single `ResolveTargetHostRow`) is the real cost. Stage it.*

**Skins: Curated, Animated Theme Presets on the Token Layer**
Midnight / Graphite / Daylight / High-Contrast skins as token maps, switched via a View-Transition circular reveal. *Value:* tailor the canvas; the reveal is a signature wow.
`Impact 3 · Wow 4 · Effort L · Risk med · Rec nice-to-have` — *almost entirely a payoff layer on Token Forge — do NOT ship before it (399 hex would theme only chrome). Then it collapses to an S.*

### Theme 5 — Execution & Live Observability

**Flight Recorder: Scrub & Replay Completed Runs**
Upgrade TimelinePanel into a transport (play/pause/step, draggable playhead) that fully reconstructs the canvas at any moment — glows, edge flow, variable snapshot, output. *Value:* debug a slow side-effectful SSH run after the fact, zero re-execution.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *scaffolding exists; fix the step-entry variable snapshot off-by-one; output replay is partial today.*

**Run Heatmap & Bottleneck Spotlight**
Wire the **dead** `blockTimings` map (a real bug — duration is on the wire but dropped), then recolor nodes green→amber→red by duration / error-rate. *Value:* see "which step is killing my runtime / keeps failing" on the graph.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *one-line `setBlockTiming` fix ships value immediately; errors-mode needs a sessionStats map surviving clearExecution.*

**Conditional & Hit-Count Breakpoints**
Break only when an expression is true / after N hits, evaluated server-side by the existing `ExpressionEvaluator`. *Value:* on a 200-iteration foreach, stop only at the bad host — real-debugger territory.
`Impact 4 · Wow 3 · Effort M · Risk med · Rec big-bet` — *engine has the pause site + evaluator; widen the ephemeral breakpoint path (Set→Map, 3 readers) and reset hit counts per run. Round-trip-safe (breakpoints are session-only, NOT YAML).*

**Unified Run Console**
Bottom dock streaming all step output in execution order with timestamps, severity color, clickable step links + filter/search. *Value:* the first thing an operator reaches for — scroll the whole run, click to the failing block.
`Impact 4 · Wow 3 · Effort M · Risk low · Rec quick-win` — *TimelinePanel is the template; true severity needs a tiny additive C# emit of `ScriptOutputType`. Cap the log.*

**Loop & Branch Live Instrumentation**
foreach/while iteration counters + current item; if/switch tint the taken edge bright and dim the not-taken + a then/else/case chip. *Value:* the most error-prone constructs become observable.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *iteration index + branch decision already exist in the engine; additive execution-update fields. Land before Inline Result Chips.*

**Multi-Host Run Radar**
Per-host status strip + a host × block results matrix; click a host to filter exec glows. *Value:* closes the biggest gap between canvas and the multi-host engine — "host #37 failed at the firewall step" at a glance.
`Impact 5 · Wow 5 · Effort XL · Risk high · Rec big-bet` — *React side is clean; the engine relays many per-host executors onto ONE event stream with no host id + a global `ActiveScriptContext`. Phase: host-id plumbing → React hosts[] → matrix.*

**Variable Watch & Break-on-Change**
Pin vars to a sticky Watch list with value-history sparklines + highlight; optional break-on-change. *Value:* keep the 2–3 vars you care about front-and-center; break exactly when one mutates.
`Impact 4 · Wow 3 · Effort M · Risk med · Rec quick-win` — *Phase 1 (watch + sparkline) is pure-frontend off existing `changedKeys`; jump-to-setter + break-on-change are genuine engine work (over-claimed in the sketch).*

### Theme 6 — Signature Wow Moments & Delight

**First Light: A Cinematic Empty State & Onboarding**
A glass welcome card over the aurora with quick-start affordances + a hint arrow; cross-fades out when the first block lands. *Value:* turns the cold-start blank grid into a branded first impression.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *pure overlay gated on node count; "Open a sample" needs one new C# message reusing the import pipeline.*

**Run Complete: The Victory Lap**
On clean success: luminous ripple from the final node, progress fill, glass result toast ("12 steps · 3.4s · 0 errors"), optional CSS-particle burst + soft sound. Restrained red ripple on failure. *Value:* closure for long unattended runs; success/failure read from across the room.
`Impact 4 · Wow 5 · Effort M · Risk low · Rec quick-win` — *the hook is a 2-line no-op today; data is in-store. Do NOT add @tsparticles (singlefile inlines it anyway) — use the hand-rolled CSS burst.*

**Presentation Mode: Demo the Flow Like a Keynote**
One-key mode hides chrome, dims to a focused stage, enlarges fonts, and camera-follows the executing block. *Value:* makes "watch my automation run" genuinely impressive; great for GIFs.
`Impact 3 · Wow 4 · Effort M · Risk low · Rec nice-to-have` — *no existing spotlight/aurora to borrow (sketch is wrong); this would introduce the reusable spotlight/camera-follow helper.*

**Snapshot: Export the Canvas to a Beautiful Image**
Render the graph (or selection) to a crisp 2× PNG with optional aurora backdrop + title bar; save via the existing file-dialog bridge or copy to clipboard. *Value:* paste automation diagrams into tickets/runbooks; every share spreads the tool's identity.
`Impact 4 · Wow 4 · Effort M · Risk low · Rec quick-win` — *C# side mirrors the browse-path pattern exactly; real cost is reliably capturing inline-CSS DOM + SVG (provide a solid-background fallback).*

### Theme 7 — Foundations: Accessibility, Performance & Authoring Safety

**Accessible by Design: Keyboard-First Canvas + Screen-Reader & WCAG Foundation**
Arrow-key node traversal + Shift+? cheat-sheet, aria-live run-state region + node roles, WCAG-correct OKLCH palette + forced high-contrast variant. *Value:* procurement-grade accessibility; keyboard speed for power users.
`Impact 4 · Wow 3 · Effort L · Risk med · Rec big-bet` — *zero a11y exists today; hardest part is fighting React Flow's own focus model. Sequence after the token/contrast refactor.*

**Adaptive Performance Governor + Semantic-Zoom LOD**
Semantic-zoom LOD (hide text/badges when zoomed out, collapse to chips when tiny) + an auto-degrade governor that caps animations on large/janky graphs. *Value:* large graphs stay navigable; makes every flashy proposal safe to ship enabled-by-default.
`Impact 4 · Wow 4 · Effort L · Risk med · Rec big-bet` — *no perf-governance layer exists yet; the rAF sampler must self-suspend and write only on tier crossings, and `onlyRenderVisibleElements` needs container-child validation.*

**Problems Panel: Click-to-Fix Validation Router + Run-Block Explainer**
Turn the buried `exportStatus` diagnostics into an IDE Problems panel: grouped errors/warnings, each deep-linking to the offending node + a Run-button explainer popover. *Value:* the single biggest authoring-productivity win — stop hunting blindly behind a greyed-out Run button.
`Impact 5 · Wow 3 · Effort M · Risk low · Rec quick-win` — *~80% exists (diagnostics already carry NodeId); only fix is a richer wire shape (~10 LOC C#) + a panel. Defer true one-click fixes.*

**Smart Wires: Connection-Validity Guards + Glowing Drag Affordance**
`isValidConnection` rejects structurally invalid links (second edge off a single-output handle, illegal branch wiring, self-loops) with green/red handle feedback + a glowing snap connection line. *Value:* a correctness guardrail that prevents graphs that silently break ExportToYaml — and a premium drag feel.
`Impact 4 · Wow 3 · Effort M · Risk low · Rec quick-win` — *100% native React Flow; derive legality rules from the registry to avoid a second source of truth vs the bridge.*

**Engine Codex: Bundled 69-Function Catalog with In-Field Autocomplete & Signature Help**
Generate a read-only function catalog at build time from C# `FunctionRegistry`, bundle it, and drive in-field typeahead + signature help across all `code` fields + the Cmd-K palette + a reference drawer. *Value:* highest-leverage way to surface the hidden engine — `contains` / `regex_match` / `date_add` / `ip_in_cidr` / lambdas in-context.
`Impact 4 · Wow 4 · Effort L · Risk med · Rec big-bet` — *the catch: arity/help/examples are NOT machine-readable today — they must be authored by extending the registration API across 8 files + a generator. Phase metadata first, autocomplete second.*

**Diff Lens: Dirty-State Halos + Canvas-vs-Saved YAML Diff**
Per-node dirty halos (added/modified/deleted) + an on-demand side-by-side diff of canvas YAML vs the saved script (reusing `InlineDiffBuilder`). *Value:* see exactly what an Apply would change before committing; prevents accidental destructive edits to production scripts.
`Impact 4 · Wow 3 · Effort L · Risk med · Rec big-bet` — *Part 1 (halos) is a near-quick-win IF diff state lives in a uiSlice Map, NOT on node.data; Part 2 (true diff) needs a new bidirectional message + baseline retention.*

**Time Machine: Load & Replay Persisted Past Runs into the Canvas**
A run picker + `load-run-history` message replaying a prior execution into the timeline/glows/variables for post-mortem debugging. *Value:* reopen the canvas after a failed overnight job and watch what happened, no re-run against production.
`Impact 4 · Wow 4 · Effort XL · Risk high · Rec big-bet` — *premise is false today: NO per-step trace is persisted (only flat per-host output). Requires a foundational C# capture layer + versioned schema migration first.*

---

## Prioritization

### Quick Wins (high value / low effort, low risk)

| Proposal | Impact | Wow | Effort |
|----------|:------:|:---:|:------:|
| Reduced-Motion Kill Switch *(enabler — build first)* | 4 | 2 | S |
| Run Progress Bar + Minimap Radar | 3 | 3 | S |
| Iconic: Wake the Dead Icon Field | 4 | 4 | M |
| Surface & Glass: Premium Node Cards | 4 | 4 | M |
| Live Wires: Gradient Edges + Data Packets | 4 | 5 | M |
| Branch Bands & Containment | 4 | 4 | M |
| Choreographed Execution Cinematics | 4 | 5 | M |
| Inline Result Chips | 4 | 4 | M |
| Hover Lift + Floating Action Toolbar | 4 | 4 | M |
| Frames: Translucent Labeled Regions | 4 | 4 | M |
| Outline Tree | 4 | 4 | M |
| Alignment Guides + Snap | 3 | 4 | M |
| Multi-Select Action Bar | 4 | 4 | M |
| Searchable Palette | 4 | 3 | M |
| Breadcrumb + Focus Mode | 4 | 4 | M |
| Smart Cards: Previews + Validation Badges | 4 | 4 | M |
| Flight Recorder: Scrub & Replay | 4 | 4 | M |
| Run Heatmap & Bottleneck Spotlight | 4 | 4 | M |
| Unified Run Console | 4 | 3 | M |
| Loop & Branch Live Instrumentation | 4 | 4 | M |
| Variable Watch *(Phase 1)* | 4 | 3 | M |
| First Light: Empty State & Onboarding | 4 | 4 | M |
| Run Complete: The Victory Lap | 4 | 5 | M |
| Snapshot: Export Canvas to Image | 4 | 4 | M |
| Problems Panel: Validation Router | 5 | 3 | M |
| Smart Wires: Connection-Validity Guards | 4 | 3 | M |
| Pop-In Node Lifecycle *(pop-in half)* | 3 | 4 | M |

### Big Bets (high value / high effort)

| Proposal | Impact | Wow | Effort | Risk |
|----------|:------:|:---:|:------:|:----:|
| Token Forge *(prerequisite — sequence first)* | 4 | 3 | M | low |
| Cmd-K Command Deck | 5 | 4 | L | med |
| Drag-to-Empty Quick-Add + Edge Splice | 5 | 4 | L | med |
| Expression Builder for 'code' Fields | 5 | 4 | L | med |
| Engine Codex: Function Catalog + Autocomplete | 4 | 4 | L | med |
| In-Canvas Environment & Multi-Host Bar | 4 | 3 | L | med |
| Conditional & Hit-Count Breakpoints | 4 | 3 | M | med |
| Adaptive Performance Governor + LOD | 4 | 4 | L | med |
| Accessible by Design (keyboard/SR/WCAG) | 4 | 3 | L | med |
| Diff Lens: Dirty-State + YAML Diff | 4 | 3 | L | med |
| Multi-Host Run Radar | 5 | 5 | XL | high |
| Time Machine: Replay Persisted Runs | 4 | 4 | XL | high |

### Impact vs Effort overview

```
            LOW EFFORT (S/M)                  HIGH EFFORT (L/XL)
          ┌─────────────────────────────┬─────────────────────────────┐
   HIGH   │ ★ DO FIRST                  │ ★ PLAN & STAGE              │
  IMPACT  │ Problems Panel              │ Cmd-K Command Deck          │
  (4-5)   │ Smart Cards                 │ Quick-Add + Edge Splice     │
          │ Heatmap (+timing bugfix)    │ Expression Builder          │
          │ Live Wires / Data Packets   │ Engine Codex (functions)    │
          │ Exec Cinematics             │ Multi-Host Radar (XL)       │
          │ Inline Result Chips         │ Env & Multi-Host Bar        │
          │ Loop/Branch Instrumentation │ Conditional Breakpoints     │
          │ Hover Toolbar / Multi-Sel   │ Perf Governor + LOD         │
          │ Flight Recorder / Console   │ Accessibility Foundation    │
          │ Frames / Outline / Focus    │ Diff Lens / Time Machine    │
          │ Victory Lap / First Light   │ Token Forge (prereq, M)     │
          │ Connection Guards / Snapshot│                             │
          ├─────────────────────────────┼─────────────────────────────┤
   LOW    │ ◦ EASY POLISH               │ ◦ DEFER / NICE-TO-HAVE      │
  IMPACT  │ Run Progress + Radar (S)    │ Skins (after Token Forge)   │
  (≤3)    │ Kill Switch (S, enabler)    │ Subroutine call-graph half  │
          │ Searchable Palette          │ Presentation Mode           │
          │ Alignment Guides            │ Glass HUD & Aurora          │
          │ Pop-In Lifecycle            │ Debug Spotlight             │
          └─────────────────────────────┴─────────────────────────────┘
```

---

## Recommended Roadmap

Four sequenced waves. Each wave leaves the canvas shippable and de-risks the next.

### Wave 1 — Foundation & Safety Net
*Establish the token layer and the motion governor so everything after is a trivial token swap and ships safe-by-default.*

- **Token Forge** — the load-bearing prerequisite; unblocks node cards, edges, skins, high-contrast, aurora.
- **Reduced-Motion Kill Switch** — gate for all later motion; protects RDP/software-GPU sessions.
- **Run Heatmap timing bugfix** (the one-line `setBlockTiming` fix) + **Problems Panel** — bank two high-impact wins on infra that mostly exists, and revive the dead duration badge.
- **Smart Wires: Connection-Validity Guards** — fold in early so authoring can't produce graphs that break ExportToYaml.

*Why first:* nearly every later visual proposal references `--fc-*` tokens that don't exist yet, and every later motion proposal must be gateable. Re-run the Playwright parity specs after the token sweep to prove byte-stable export.

### Wave 2 — Node Redesign & Motion (the visual leap)
*Make the canvas look amazing; all visual-only, all round-trip-safe.*

- **Iconic** (icon sprite — also unblocks Palette/Cmd-K/Outline) → **Surface & Glass: Premium Node Cards** → **Branch Bands & Containment**.
- **Live Wires: Gradient Edges + Data Packets** + **Loop & Branch Instrumentation** (the data producer for chips).
- **Choreographed Execution Cinematics** + **Inline Result Chips (Phase 1)** + **Run Complete: Victory Lap**.
- **Hover Lift + Floating Action Toolbar**, **Pop-In Lifecycle**, **First Light empty state**, **Run Progress + Minimap Radar**.

*Why this order:* icons before everything that renders them; node surface before edges/branches that accent off it; instrumentation before result chips; celebration/empty-state as the bookends that make a run feel alive.

### Wave 3 — Authoring Power (feature richness)
*Surface the engine and accelerate building.*

- **Searchable Palette** + **Cmd-K Command Deck** (spawn/jump/commands pillars) + **Drag-to-Empty Quick-Add + Edge Splice** — the authoring-loop trifecta.
- **Smart Cards** + **Engine Codex (function catalog)** + **Expression Builder** — turn the canvas into a discoverable scripting surface.
- **Outline Tree**, **Frames**, **Breadcrumb + Focus Mode**, **Multi-Select Action Bar**, **Alignment Guides** — navigation and organization for large scripts.
- **In-Canvas Environment & Multi-Host Bar (read-only + env-dropdown slice)** — capture authoring value before the execution rewire.

*Why third:* these depend on the icon sprite (Wave 2) and a function-catalog pipeline, and they're where the "more feature-rich" goal is met without atmosphere-only changes.

### Wave 4 — Live Observability & the Big Wow
*Flagship debugging + the demo-stopping moments, including the staged big bets.*

- **Flight Recorder (scrub & replay)** — built **single-session, in-memory** first (decision #8) + **Unified Run Console** + **Variable Watch (Phase 1)** + **Conditional Breakpoints** — a credible single-host debugger for preset authoring.
- ~~Multi-Host Run Radar + multi-host execution rewire~~ — **CUT (decision #5):** the canvas is a preset-builder, not the multi-host run surface; the existing read-only `targetHost` is sufficient.
- **Adaptive Performance Governor + Semantic-Zoom LOD** + **Accessibility Foundation** (dark-only contrast audit; light/high-contrast token-swap deferred per decision #4) — ship before/with the heaviest effects so nothing regresses perf or contrast.
- **Snapshot image export**, **Diff Lens**, plus **Skins** (now an S after Token Forge) and the nice-to-haves (**Glass HUD & Aurora**, **Presentation Mode**, **Debug Spotlight**) as polish. **Time Machine / durable cross-session replay is deferred** (decision #8) until single-session Flight Recorder proves its worth.

*Why last:* the high-risk items (durable replay) need foundational C#/engine work, and the perf/a11y governors are most valuable once the flashy effects they protect have all landed.

---

## Technical Considerations & Risks

### Offline single-file / bundle size
- The SPA is built single-file via Vite and embedded as an assembly resource — **no runtime CDN/network**. Every asset must inline.
- **An animation library is allowed if it bundles offline** (decision #2): a lean runtime such as `motion`/Framer Motion may be used for spring/orchestration on chrome and node lifecycle. Per-frame and canvas-heavy effects (edges, data packets, glows) still prefer CSS `@property`, keyframes, WAAPI, SVG `offset-path`, and native `document.startViewTransition()` — all native to WebView2's Chromium — to protect framerate and bundle size.
- **Do not lazy-import @tsparticles** for the Victory Lap: under single-file the "lazy" import inlines anyway and just adds KB. Use a hand-rolled ~12-particle CSS burst.
- Icons must be a **bundled inline-SVG sprite**, not an icon font/CDN.
- New OKLCH palette + `color-mix()` + `backdrop-filter` are all supported by the pinned WebView2 Chromium runtime.

### YAML round-trip (the hard gate)
- Export serializes **`node.data.props` only**. Visual-only changes (tokens, icons, edges, glows, frames-via-layout-channel, heatmap, replay, dirty halos) **cannot** alter the payload.
- **Diff-state must live in a uiSlice Map, not on `node.data`** — `buildExecutableGraphPayload` does not whitelist, so transient fields on node.data would leak.
- Anything that changes node semantics (Quick-Add node creation, Edge Splice) must reuse the existing `addNode`/`onConnect` paths and be proven through Import→Export→re-import parity (the e2e specs gate this).
- Frames ride the **comment layout-only channel** (stripped from executable payload, excluded from the structure hash) — they must mirror the comment exclusion in `exportGraph.ts` *and* `ComputeStructureHash`.

### C# bridge / message additions
The following proposals require coordinated `communication-message-types.ts` + `FlowCanvasForm.cs` + `Form1.cs` edits (no schema version negotiation exists — stale builds silently drop unknown messages):

| Proposal | New / changed bridge work |
|----------|---------------------------|
| Problems Panel | Richer `apply-result` carrying `{nodeId, severity, message}` (~10 LOC; diagnostics already carry NodeId) |
| Unified Run Console / Inline Result Chips (structured) | Additive `severity` / structured fields on `step-output` / `execution-update` |
| Loop & Branch Instrumentation | `StepExecutionEventArgs` + `execution-update` gain `iteration`/`iterationTotal`/`branchTaken` |
| Conditional Breakpoints | `condition`/`hitCount` on the breakpoint-toggle payload + `DebugState` value type |
| Engine Codex | Build-time generator emitting `functions.generated.json` from an enriched registration API |
| ~~Env & Multi-Host Bar / Run Radar~~ | **CUT (decision #5)** — the canvas is a preset-builder, not the multi-host run surface; no multi-host targeting/run/bridge work |
| Snapshot export, First Light "open sample" | New `save-image` / `request-sample-script` mirroring the `browsePath` request/result pattern |
| Time Machine | **Deferred (decision #8)** — single-session in-memory replay first (no persistence layer); revisit a durable per-step trace only if replay proves valuable |

All outbound C#→React posts must be marshaled to the UI thread via `SendMessage` (queued until React posts `ready`).

### Performance with many nodes
- Nodes are `React.memo`'d; execution state lives in Maps replaced wholesale on each update — **keep new per-node state behind fine-grained selectors** and write to the store only on band/tier crossings (never per frame).
- Confine `backdrop-filter` to a few chrome surfaces; cap simultaneous animated edges/glows (running path only); cap blur radii ≤14px and aurora orbs ≤3.
- The **Adaptive Performance Governor + LOD** is the systemic mitigation — its rAF sampler must self-suspend when idle/hidden or it becomes the jank it prevents. Validate `onlyRenderVisibleElements` against off-screen container children + edge routing.

### Accessibility
- **Zero a11y exists today** (no `role`, `aria`, `tabIndex`, or `prefers-reduced-motion`).
- The OKLCH palette must be contrast-audited (4.5:1 body / 3:1 large) before a high-contrast variant — several current grays/blues are borderline.
- `prefers-reduced-motion` is unreliable inside WebView2 (server/RDP OS setting often unset) — the **explicit uiSlice toggle is the load-bearing safety**, not the media query.
- Real screen-reader focus must coexist with React Flow v12's own focus/selection/keyboard model — the hardest part of the a11y bet.

---

## Decisions (Resolved 2026-05-29)

These answers are the **contract**. Where they conflict with the catalog/prioritization above, the decision wins.

1. **Scope & sequencing — all four waves, one at a time, starting with Wave 1.** Full roadmap approved; execute and ship wave-by-wave with approval between waves.
2. **Animation library — allowed if it bundles offline.** A lean runtime (e.g. `motion`/Framer Motion) may be used for spring/orchestration on chrome and node lifecycle. Per-frame/canvas-heavy effects (edges, data packets, glows) still prefer CSS/WAAPI to protect framerate.
3. **Theme direction — proposed OKLCH dark-first surface-ladder + accent-rail; accent = indigo/azure** (Claude's pick — continues the existing blue running/active signal + `StartNode` gradient). No external brand color to honor.
4. **Light / high-contrast — dark-only for now, architected for later.** Token Forge keeps light/high-contrast a future *token-swap*, not a rewrite: **no hardcoded hex outside the token layer.**
5. **Multi-host — CUT.** The Flow Canvas is a **preset-builder**, not the normal multi-host run experience. No Multi-Host Run Radar, no Env/Multi-Host targeting bar, no execution rewire. The existing read-only `targetHost` is sufficient; observability features target single-host preset debugging.
6. **Engine surfacing — extend the C# `FunctionRegistry`** registration API with metadata as the single source of truth; build-time generate `functions.generated.json` for the Engine Codex.
7. **Persistence — all new UI prefs survive restarts** (reduced-motion, skin, snap-to-grid, favorites, …) via `AppConfiguration`/`WindowState`, not ephemeral WebView2 localStorage.
8. **Durable replay — deferred (Claude's decision).** Build **Flight Recorder as single-session, in-memory** replay first. Defer the per-step trace persistence layer + schema migration (and full cross-session Time Machine) until single-session replay proves its value.
9. **Glyphs — adopt a vendored stroke set (Lucide SVGs, inlined).** No bespoke 30-glyph hand-drawing; map the 36 block types to Lucide icons, bundled as inline SVG.
10. **Round-trip posture — strict.** Every semantic-touching change must pass extended Import→Export→re-import parity specs **before merge**, even at the cost of slower delivery.
