# Start Block for Flow Canvas

**Date:** 2026-03-24
**Status:** Approved

## Problem

Script-level properties (name, description, debug, nobanner, environment, vars, imports, etc.) are stored in a hidden `__preamble__` metadata node at (-9999, -9999) with no visual UI in the Flow Canvas. Users cannot see or edit these properties without writing raw YAML. Additionally, flows have no explicit visual entry point — root nodes are inferred by finding nodes with no incoming edges, which is fragile and ambiguous.

## Solution

A mandatory, non-deletable **Start Block** that serves as:
1. The visual entry point for every flow
2. The editing surface for script-level properties

It replaces the hidden `__preamble__` node entirely.

## Decisions

- **Mandatory**: Every flow always has exactly one Start Block. It cannot be deleted.
- **Visual style**: Header block style (Option B) — same rectangular shape as step blocks but with a blue accent (`#4a9eff`), showing script name and active flags as badges.
- **Connected**: Start Block has a source handle (bottom). The user draws an edge from Start to the first step block, making execution order explicit and deterministic.
- **Approach**: New React Flow node type (`StartNode.tsx`), not a special entry in the block registry.

## Design

### 1. New Node Type: `StartNode.tsx`

Registered in `App.tsx` alongside existing node types:

```ts
const nodeTypes = { block: BaseBlock, comment: CommentNode, start: StartNode };
```

**Rendering:**
- Rectangular, 8px border-radius (matches step blocks)
- Blue accent color (`#4a9eff`), dark blue background (`#0d1a2a`)
- Header: `START` badge + script name (or "Untitled Script")
- Preview area: small badges for active boolean flags (debug, nobanner, etc.) + counts for vars/imports
- Source handle only (bottom) — no target handle
- Cannot be deleted (delete key is a no-op on this node)

**Node ID convention:** `__start__`

**Node data structure:**

```ts
interface StartNodeData {
  blockType: '_start';
  label: string;  // script name
  props: {
    name?: string;
    description?: string;
    environment?: string;
    version?: number;
    debug?: boolean;
    nobanner?: boolean;
    suppress_missing_column_warning?: boolean;
    library?: boolean;
    vars?: Record<string, unknown>;  // read-only summary
    imports?: string[];               // read-only summary
    _yamlSnippet?: string;           // round-trip fallback for unrecognized keys
  };
}
```

### 2. Properties Panel

**Integration point:** In `Properties.tsx`, add a check for `blockData?.blockType === '_start'` **before** the existing `!node || !def` guard (line 456). Since `_start` is not in the block registry, `def` will be `null` — the early return prevents falling through to the "Select a block" empty state. The Start form is a self-contained render path that does not depend on a `BlockDef`.

When `blockData?.blockType === '_start'`, renders a custom form:

| Field | Control Type | Notes |
|-------|-------------|-------|
| Name | Text input | Displayed in node header |
| Description | Textarea | |
| Environment | Text input | |
| Version | Number input | Defaults to 1 |
| Debug | Toggle (checkbox) | Badge shown on node when active |
| No Banner | Toggle (checkbox) | Badge shown on node when active |
| Suppress Missing Column Warning | Toggle (checkbox) | Badge shown on node when active |
| Library | Toggle (checkbox) | Badge shown on node when active |
| Vars | Read-only count | "3 variables defined" — full editing deferred |
| Imports | Read-only count | "1 import" — full editing deferred |

### 3. Bridge Changes (`FlowCanvasBridge.cs`)

**Import (`TextToGraph`):**
- Extracts preamble via `ExtractPreamble()` (unchanged)
- Creates a visible `start` node (type `"start"`, id `"__start__"`) at position (250, 40) instead of the hidden `__preamble__` node
- Parses preamble YAML into individual `props` fields (name, debug, nobanner, etc.)
- Stores `_yamlSnippet` as fallback for unrecognized preamble keys (round-trip safety)
- Creates an edge from `__start__` to the first step node

**Export (`ExportGraphToYaml`):**
- Finds the `__start__` node by ID
- Reconstructs preamble YAML from `props` by serializing known keys in canonical order: `name`, `description`, `version`, `environment`, `debug`, `nobanner`, `suppress_missing_column_warning`, `library`, then `vars` and `imports` blocks. Only non-default values are emitted (e.g., `debug: true` is emitted, `debug: false` or missing is omitted). This is new serialization logic in the bridge.
- Appends `_yamlSnippet` content for unrecognized keys (preserves round-trip fidelity)
- `__start__` is **excluded** from `orderedIds` (it has no YAML step representation)
- The outgoing edge target of `__start__` becomes the single root for `BuildChain`. Other disconnected subgraphs are **not emitted as YAML** — they produce a warning diagnostic only.
- Chain-building logic remains the same after identifying the first step

**New/empty flow:**
- Creates a default Start node at (250, 40) with empty props

### 4. Store Changes (`useFlowStore.ts`)

- **`graphSlice`**: Ensure Start node is created on store initialization (empty canvas case)
- **Delete protection (two paths)**:
  1. `removeNodes`: Filter `__start__` out of the ID list before processing
  2. `onNodesChange`: Filter out `remove`-type changes where `id === '__start__'` before passing to `applyNodeChanges`. This covers React Flow's internal deletion mechanisms (multi-select + delete key, `deleteKeyCode` prop).
- **Copy/paste/duplicate**: Exclude `__start__` from clipboard operations. When copying selected nodes, filter it out. Paste never creates a second Start node.
- **Selection**: Start node is selectable (for properties editing) but immune to deletion
- **Undo/redo**: Start node persists through all snapshots

### 5. Palette

No changes. The Start Block is not in the palette — it is auto-created and permanent.

### 6. Edge Cases

| Scenario | Behavior |
|----------|----------|
| Disconnected Start (no outgoing edge) | Export produces valid YAML with preamble + empty `steps:` |
| Unreachable nodes (not connected from Start) | Export emits a warning diagnostic |
| Import script with no preamble | Start Block created with default (empty) props |
| Import script with unrecognized preamble keys | Round-tripped via `_yamlSnippet` |
| User tries to delete Start Block | No-op; node stays |
| User tries to connect an edge into Start's top | No target handle exists; connection rejected |
| Right-click context menu on Start Block | Hide "Delete" option; other options (if any) remain |
| Ctrl+A then delete | Start node filtered out of removal; other selected nodes deleted |
| Copy/paste with Start selected | Start excluded from clipboard; other nodes copied normally |
| User drags Start to new position | Position persisted in graph JSON but reset to (250, 40) on YAML reimport (position is not stored in YAML) |

## Files to Create

- `FlowCanvas/src/nodes/StartNode.tsx` — new React Flow node component

## Files to Modify

- `FlowCanvas/src/App.tsx` — register `start` node type
- `FlowCanvas/src/panels/Properties.tsx` — add Start Block property form
- `FlowCanvas/src/stores/useFlowStore.ts` (or relevant slices) — Start node creation, delete protection
- `Services/FlowCanvasBridge.cs` — import/export logic for Start node replacing `__preamble__`

## Migration

**YAML import path (primary):** No migration needed. `TextToGraph` always re-parses YAML from scratch and will create the new `__start__` node instead of the old `__preamble__` node.

**Persisted graph JSON (layout positions):** If graph JSON is saved/restored (e.g., layout persistence via WebView2 messages), old graphs may contain a `__preamble__` node but no `__start__` node. On load, detect the old format: if `__preamble__` exists but `__start__` does not, create the `__start__` node at (250, 40) and migrate preamble data. Remove the `__preamble__` node. This migration runs once in the React store's graph load handler.

## Naming Convention

Underscore-prefixed `blockType` values (e.g., `_start`, `_preamble`) are reserved for system nodes — non-step nodes that are not in the block registry and not user-creatable. This convention prevents collisions with user-facing block types.

## Test Plan

| Test | Type | Verifies |
|------|------|----------|
| `StartNode` renders correct badges for active boolean props | Unit (React) | Visual rendering |
| `StartNode` renders "Untitled Script" when no name set | Unit (React) | Default state |
| Properties panel renders Start form when `_start` selected | Unit (React) | Panel integration |
| Properties panel renders normal form for regular blocks | Unit (React) | No regression |
| `removeNodes` with `__start__` in ID list does not remove it | Unit (store) | Delete protection path 1 |
| `onNodesChange` with remove change for `__start__` does not remove it | Unit (store) | Delete protection path 2 |
| Copy/paste excludes `__start__` | Unit (store) | Clipboard protection |
| Empty canvas always has exactly one Start node | Unit (store) | Initialization |
| C# bridge `TextToGraph` creates `__start__` node from preamble | Unit (C#) | Import |
| C# bridge `ExportGraphToYaml` serializes Start props to preamble | Unit (C#) | Export |
| Round-trip: YAML → TextToGraph → ExportGraphToYaml produces equivalent YAML | Integration (C#) | Fidelity |
| Round-trip with unrecognized preamble keys preserved | Integration (C#) | `_yamlSnippet` fallback |
| Disconnected Start produces valid YAML with empty `steps:` | Integration (C#) | Edge case |

## Out of Scope

- Full vars editor (key-value editing UI) — deferred to a future iteration
- Full imports editor (file picker + alias) — deferred to a future iteration
- Subroutines editing in the Start Block — subroutines are a separate structural concept
