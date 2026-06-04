# Flow Canvas ↔ Script: Comment Round-Trip

**Date:** 2026-06-04
**Branch:** `flow-canvas-comment-flow` (experiment off `flow-canvas-blocks-bands-expansion`)
**Status:** Design approved, pre-implementation

## Problem

Comments a user writes in a preset/YAML script do not reach the Flow Canvas, and are
destroyed when the user edits the script visually. The bridge docstring claims the
snippet round-trip "preserves all properties, comments, and formatting" — this is false.
Comment fate today depends entirely on a line's shape relative to the `- ` step marker:

| Comment kind | Import → canvas | After editing that block | Net |
|---|---|---|---|
| File-header / preamble | Kept on Start node snippet | Fragile — can vanish if a preamble field is edited | Survives, fragile |
| Section label above a step (`# Get memory info`) | **Silently dropped** | Already gone | **Lost** |
| Inline / trailing (`cmd: x  # note`) | Kept inside step snippet | Dropped on regeneration | Lost on edit |
| Standalone between two steps | Glued to the *previous* step | Dropped on regeneration | Mis-attributed, then lost |
| Blank lines | Counted (`_blankLinesBefore`) | Re-emitted | Survives |

The worst case — a **section label above a step** — is the dominant real-world style in
this repo's own `ScriptSamples` (e.g. `bash/system_info.yaml` has 7+, `fortigate/block_ip.yaml`
uses a doc-header plus section labels). It is dropped before it ever reaches the canvas.

### Root cause

1. **Snippet boundaries are line-shape-based, not semantic.** `SplitYamlSteps` only recognises
   blank lines, `- ` items at step indent, and "deeper" continuations. A `#` line with no step
   open yet has no home and is skipped.
2. **Regeneration discards the snippet.** The snippet is the *only* comment carrier today, but it
   is deliberately bypassed whenever a block is authored/edited visually. There is no
   comment side-channel that survives regeneration.
3. **The parser strips comments** before structure is known, so the bridge cannot ask it where a
   comment belongs; it must scan raw text itself (which it currently does not).

## Goals (approved scope)

Full bidirectional comment flow:

1. **Preserve** — imported comments are never silently dropped, and survive editing/export.
2. **Show + edit** — comments render on the canvas and are editable there; changes flow back to YAML.
3. **Author** — users can create brand-new comments on the canvas that become real `#` lines in
   exported YAML, reconciled with the existing visual-only sticky-notes.

## Non-goals (v1)

- **Branch / edge path labels** (a comment annotating a specific branch edge). This is a net-new
  annotation feature, not a data-loss fix — clean follow-up.
- Pushing comment-awareness into the script engine (`ScriptStep`, validation, debug). The bridge
  round-trips to YAML text, and the Scintilla editor edits that same text, so comments surface
  there for free. Engine-level comment modelling is out of scope.

## Key decisions

1. **Rendering metaphor: pinned notes (option D).** Extend the existing `CommentNode` sticky-note
   rather than invent a parallel concept. Unifies the two comment worlds; reuses `attachedToNodeId`.
2. **Two explicit note kinds.** `kind: 'comment'` exports as `#`; `kind: 'sticky'` is visual-only
   (today's behavior). Two creation actions: **Add comment (#)** and **Add sticky**. No ambiguous
   default — chosen at creation. This keeps "visual changes never touch YAML export" true by
   construction. Imported `#` comments arrive as `comment`; existing stickies stay `sticky`.
3. **Anchor by `_stepPath`.** Each note's YAML position is described by an anchor keyed to the
   block's existing `_stepPath`, *not* x/y and *not* a fragile edge. This is what survives the
   export regeneration that already rebuilds nesting from `_stepPath`/`_isChildOf`, and is why
   branch-internal comments come nearly for free.
4. **v1 fidelity:** file-header + section labels (core), **inline trailing notes** (best-effort),
   **comments inside branches** (preserved and editable). Edge labels deferred. Anything not given
   full fidelity is still *preserved* — never silently dropped.

## Architecture

### Unified note model

A note is an extended `CommentNode`:

- `kind: 'comment' | 'sticky'` — exports vs visual-only.
- `anchor: { type: 'header' | 'leading' | 'inline'; stepPath?: string; lineOffset?: number }`
  - `header` → on the Start node (preamble / file header).
  - `leading` → above a step; `stepPath` e.g. `steps/0` or `steps/1/then/0`.
  - `inline` → trailing on a step's line; `stepPath` + best-effort `lineOffset` within the snippet.
- `attachedToNodeId` (already exists) — the block the note is pinned to, for rendering/placement.
- `text`, `color`, `commentId` — existing.

Existing stickies default to `kind: 'sticky'` with no anchor (free-floating).

### Round-trip flow

```
YAML  ──import──▶  unified notes (comment|sticky, anchored by _stepPath)  ──render──▶  Canvas
 ▲                                                                                       │
 └───────────── export: comment-kind re-injected as # at anchor; sticky skipped ◀── edit/author
```

## Data model changes

**React** (`FlowCanvas/src/nodes/CommentNode.tsx`, `stores/slices/commentSlice.ts`):
- Add `kind` and `anchor` to `CommentNodeData`.
- `commentSlice`: creation actions set `kind`; preserve `kind`/`anchor` through update; ensure both
  survive `layout-autosave` and the export payload.

**C#** (`FlowCanvasBridge.cs`):
- Extend `StepSnippetInfo` with `LeadingComments: string[]` and `InlineComment: string?`.
- Capture header comments onto the Start node.
- Emit comment notes into the graph JSON alongside the existing comment-node emission.

## Import (`FlowCanvasBridge`)

In `SplitYamlSteps` (the snippet splitter, ~`:4229-4319`; line numbers approximate — verify at
implementation time):
- Accumulate consecutive `#` lines into a pending buffer; flush onto the **next** step that opens
  as `leading` comment notes (closes the `!inStep` drop-hole that loses section labels).
- Split a trailing `# …` off a non-comment line into an `inline` note for that step.
- Header comments before `steps:` continue to ride `ExtractPreamble` onto the Start node, hardened
  so editing a preamble field does not nuke them.

Write the captured comments into `stepProps` next to `_yamlSnippet` (~`:377-383`), then materialise
them as `comment`-kind notes anchored to the produced node's `_stepPath`.

## Export (`FlowCanvasBridge.ExportGraphToYaml`)

Re-inject comments on **both** the round-trip path and the regeneration paths
(`TryGenerateStepYaml`, `TryGenerateContainerFromGraph` — ~`:1093`/`:1117`), so comments survive a
block edit:
- Before each step (after the `_blankLinesBefore` emission), prepend `comment`-kind `leading` notes
  whose anchor `stepPath` matches, as `#` lines at the step's indent.
- Append `inline` notes to the step's first line (best-effort; if the original line cannot be
  located after regeneration, degrade to a `leading` comment rather than drop).
- `sticky`-kind notes are skipped (as today, `:1053-1060`).
- Normalise comment text to one line-ending convention internally; re-emit per the document's
  prevailing ending to avoid content-hash / diff drift.

## Authoring UX

- Two actions (toolbar + canvas context menu): **Add comment (#)** and **Add sticky**.
- `comment`-kind notes render with a `#` badge in a block-accent color; `sticky` keeps today's look.
- Clutter fix: a run of `leading` comment notes collapses to slim `#` pills that expand on
  hover/click, so 7 section labels do not overwhelm the canvas.
- Both kinds editable in place and via the Properties panel.
- Imported notes auto-place near their anchor block without overlapping it.

## Invariants honored

- **Visual never touches YAML export** — guaranteed by `kind`: a `sticky` cannot export.
- **Snippet round-trip stays the fidelity contract** — comments ride a metadata side-channel so
  they survive *both* round-trip and regeneration (not only the snippet).
- **Regeneration rebuilds from `_stepPath`/`_isChildOf`** — comment anchors use `_stepPath`, so they
  re-place correctly.
- **CRLF normalization** — comment storage normalized to one convention to avoid spurious
  content-hash drift on `PresetInfo.Commands` / `JobDefinition.CustomPresetCommands`.
- **`ready` handshake** — new fields ride existing `load-graph` / `apply-yaml` messages; no new
  pre-ready message, no new handshake.
- **`branchPath` vocabulary untouched** — edge-label comments are deferred, so nothing overloads it.

## Affected files (initial map)

- C#: `FlowCanvasBridge.cs` (`SplitYamlSteps`, `ExtractPreamble`, `TextToGraph` snippet wiring,
  `ExportGraphToYaml` + regeneration paths, `StepSnippetInfo`).
- React: `nodes/CommentNode.tsx`, `stores/slices/commentSlice.ts`, `utils/exportGraph.ts`
  (preserve `kind`/`anchor` through `stripDefaultProps`/payload), `panels/Toolbar.tsx` +
  context menu (two creation actions), `panels/Properties.tsx` (edit), node rendering for pills.
- Per Development Guideline 8, both React and C# sides change together.

## Risks

- **Inline fidelity through regeneration** is the genuinely hard part; v1 accepts best-effort with
  graceful degradation to a leading comment.
- **Auto-placement** of many imported notes needs care to avoid overlap with the layout engine and
  branch bands.
- **`StepSnippetInfo` / line anchors** cited above are point-in-time; re-verify against current
  `FlowCanvasBridge.cs` before editing.

## Testing

- C#: unit tests for `SplitYamlSteps` capturing each comment kind; export re-injection on both the
  round-trip and regeneration paths; a full YAML→graph→YAML round-trip on `ScriptSamples` asserting
  comments are byte-stable.
- React: vitest for `kind`/`anchor` persistence through export payload and `stripDefaultProps`;
  creation actions producing the right `kind`.
- e2e (Playwright): import a sample with section labels, confirm pills render; edit a block, export,
  confirm the comment survives.
