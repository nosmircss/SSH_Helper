# Container Continuation Handle Design

**Date**: 2026-03-25
**Status**: Draft
**Scope**: Flow Canvas — all container block types (if, foreach, while, try, switch, parallel)

## Problem

Container blocks (IF, FOREACH, WHILE, TRY, SWITCH, PARALLEL) have branch outputs for their child steps, but no UI affordance for adding blocks **after** the container in the main sequential flow. Users must manually wire convergence edges from each branch-end node to a downstream target — unintuitive and error-prone.

## Solution

Add a **diamond-shaped continuation handle** at the bottom-left of every container block. Dragging from this handle creates a "next" edge to the block that should execute after the container finishes. The same visual model applies to both hand-authored and imported flows.

## Design

### 1. Frontend — Handle Rendering

**File**: `FlowCanvas/src/nodes/BaseBlock.tsx`

All blocks where `def.isContainer === true` render a third source handle:

- **Handle ID**: `"continue"`
- **Position**: Bottom-left of the block, offset ~15px from the left edge
- **Shape**: 10x10px square rotated 45deg (diamond), `#4a9eff` blue fill
- **Hitbox**: Larger invisible area (~20x20px) for easier clicking
- **Visibility**: Always rendered on container blocks, regardless of whether an edge is connected

Existing handles remain unchanged:
- Top center: target (input)
- Bottom center: source (branch output — then, do, try, etc.)
- Right side (IF only): source with `id="false"` (else branch)

### 2. Frontend — Connection Logic

**File**: `FlowCanvas/src/stores/slices/graphSlice.ts`

In `onConnect`, the current code unconditionally calls `inferDefaultBranchMetadata` for all container blocks:

```typescript
const isContainer = !!def?.isContainer;
const branchMetadata = isContainer
  ? inferDefaultBranchMetadata(...)
  : {};
```

**Change**: Add an early check for `connection.sourceHandle === "continue"` **before** the `isContainer` branch. When the source handle is `"continue"`:
- Set `branchMetadata = {}` (no `branchPath`)
- Do **not** set `edgeProps.data = branchMetadata` (the `isContainer` guard on line 323 must also be bypassed)
- Apply continuation edge styling directly:
  - Solid stroke (no dash), color `#4a9eff`
  - Label: `"next"`, font size 9px, color `#4a9eff`, weight 600

The full conditional becomes:

```typescript
const isContinuation = connection.sourceHandle === 'continue';
const isContainer = !!def?.isContainer;
const branchMetadata = (isContainer && !isContinuation)
  ? inferDefaultBranchMetadata(...)
  : {};
```

And the edge props / styling assignment:

```typescript
if (isContinuation) {
  // Continuation edges get explicit styling — bypass getBranchVisual
  edgeProps.style = { stroke: '#4a9eff' };
  edgeProps.label = 'next';
  edgeProps.labelStyle = { fill: '#4a9eff', fontSize: 9, fontWeight: 600 };
  // No data assignment — continuation edges carry no branch metadata
} else {
  const branchVisual = isContainer
    ? getBranchVisual(blockType, branchMetadata)
    : { style: { stroke: '#666' } };
  edgeProps.style = branchVisual.style;
  if (branchVisual.label) edgeProps.label = branchVisual.label;
  if (branchVisual.labelStyle) edgeProps.labelStyle = branchVisual.labelStyle;
  if (isContainer) edgeProps.data = branchMetadata;
}
```

This ensures continuation edges created interactively from the diamond handle render with `#4a9eff` blue, solid stroke, and a `"next"` label — not the default gray returned by `getBranchVisual`.

No changes to `BlockDef` interface or `registry.ts` — the existing `isContainer` flag identifies which blocks get the handle.

### 3. Backend Export

**File**: `Services/FlowCanvasBridge.cs`

**Color constant**: Define `private const string ColorContinue = "#4a9eff";` alongside existing constants like `ColorElse`. Use this wherever the continuation color appears in the backend.

#### TryGenerateContainerFromGraph — Filter out continuation edges

**Critical**: Every `foreach (var edge in nodeEdges)` loop in `TryGenerateContainerFromGraph` iterates ALL outgoing edges from the container node. The `"continue"` edge has no `branchPath`, so without filtering it falls through to fallback/catch-all logic:

- **IF** (line ~1296): Would be assigned as `thenTarget` or fabricated as an elif branch
- **FOREACH/WHILE** (line ~1381): `?? nodeEdges.FirstOrDefault()` could pick it as the `do` edge
- **TRY** (line ~1434): Falls through to `doEdge ??= edge`
- **SWITCH** (line ~1525): Fabricated as a case branch
- **PARALLEL** (line ~1593): Fabricated as a parallel branch

**Fix**: Before the container-type-specific branch routing, filter the edge list:

```csharp
var nodeEdges = outgoing.TryGetValue(nodeId, out var edgesFromNode)
    ? edgesFromNode.Where(e => e.SourceHandle != "continue").ToList()
    : new List<EdgeInfo>();
```

This single filter at the top of the method protects all container types.

#### HasGraphAuthoredContainerBranches — Explicit guard

This method currently skips edges with empty `branchPath` (which happens to exclude `"continue"` edges). Add an explicit `SourceHandle == "continue"` check for robustness:

```csharp
if (string.Equals(edge.SourceHandle, "continue", StringComparison.OrdinalIgnoreCase))
    continue;
if (string.IsNullOrWhiteSpace(edge.BranchPath))
    continue;
```

#### CollectBranchChain — Already handles continuation edges

In `CollectBranchChain` (line ~1199-1210), the existing filter `string.IsNullOrEmpty(ei.SourceHandle)` at line 1205 only follows edges with a null/empty `SourceHandle`. Since continuation edges have `SourceHandle == "continue"`, they are **already excluded** by this filter. No code change needed here.

This naturally prevents the branch chain from consuming the continuation target, even when it has `incomingCount == 1` (e.g., IF with only a `then` branch and no `else`).

#### BuildChain — Traversal is benign

`BuildChain` follows ALL outgoing edges unconditionally via DFS. With `"continue"` edges, it will traverse from the container to the continuation target. This is correct — the continuation target should appear in `orderedIds`. The `visited` set prevents duplicates when the same node is also reachable through the main sequential chain. No change needed.

#### Existing convergence detection preserved

The `incomingCount > 1` check at line 1193 remains as a fallback for graphs saved before this change. Old graphs without `"continue"` edges continue to export correctly.

#### No changes to YAML generation

The continuation target appears in `orderedIds` as the next top-level step after the container — already how sequential flow works.

### 4. Backend Import

**File**: `Services/FlowCanvasBridge.cs`

#### TextToGraph — Single continuation edge from container

In the main import loop (line ~347-374), after `ExpandContainerChildren` returns branch-end node IDs:

**Before** (current):
```csharp
foreach (var be in branchEnds)
    pendingConnections.Add(new PendingEdge(be));
```

**After**:
```csharp
pendingConnections.Add(new PendingEdge(nodeId, "continue", "#4a9eff", "next"));
```

This creates a single edge from the container's diamond handle to the next sequential step, instead of multiple convergence edges from branch ends.

#### PendingEdge — Add dashed flag

Add a `bool Dashed` property (default `false` — solid edges, preserving existing behavior for sequential and convergence edges). Labeled branch edges (like the IF-without-else skip edge) must explicitly pass `Dashed = true`.

Updated `PendingEdge` class signature:

```csharp
private sealed record PendingEdge(
    string NodeId,
    string? SourceHandle = null,
    string? Color = null,
    string? Label = null,
    bool Dashed = false);  // true only for dashed branch edges (e.g., IF-without-else skip)
```

Continuation edges are created as:
```csharp
new PendingEdge(nodeId, "continue", ColorContinue, "next", Dashed: false)
```

The current edge creation code (line ~338-339) applies `strokeDasharray` based on whether `pe.Label != null`. The continuation edge has `Label = "next"` (non-null), so it would incorrectly get dashes under the current logic. **Change the dasharray decision to use `pe.Dashed` instead of `pe.Label != null`**:

```csharp
// Before:
if (pe.Label != null)
{
    edge["label"] = pe.Label;
    edge["style"]!["strokeDasharray"] = "5,5";
    ...
}

// After:
if (pe.Label != null)
{
    edge["label"] = pe.Label;
    edge["labelStyle"] = new JObject { ... };
    edge["type"] = "smoothstep";
}
if (pe.Dashed)
{
    edge["style"]!["strokeDasharray"] = "5,5";
}
```

This separates label rendering from dash styling, allowing the continuation edge to have a `"next"` label with a solid stroke.

#### IF-without-else skip edge

The existing logic that adds `PendingEdge(nodeId, "false", ColorElse, "else")` for IF blocks without else/elif **remains unchanged**. This draws the else-skip edge from the right handle. The convergence to the next step now goes through the diamond handle instead of from the then-branch's last node.

### 5. Edge Cases & Compatibility

| Scenario | Behavior |
|----------|----------|
| **Existing saved graphs** (no `"continue"` edges) | Export works via `incomingCount > 1` fallback. No migration needed. |
| **Nested containers** (e.g., IF inside FOREACH) | Inner container gets its own diamond handle. Import uses the same `PendingEdge(nodeId, "continue", ...)` pattern recursively. |
| **Container as last block** | Diamond handle rendered but no edge drawn. Same as unused bottom handles today. |
| **Foreach/While** (single branch) | Bottom center = `do` branch, bottom-left diamond = continuation after loop. |
| **Switch/Parallel** (multiple branches) | Bottom center = indexed branches, bottom-left diamond = continuation after all branches. |
| **Try** (try/catch/finally) | Branch edges from bottom/right for try/catch/finally, bottom-left diamond = continuation after try block. |
| **User deletes continuation edge** | Blocks after the container become disconnected. Export warns about unreachable nodes. |

### 6. Visual Summary

```
Container Block (e.g., IF)
┌──────────────────────────┐
│  ● target (top center)   │
│                          │
│  [IF]  If                │  ● else (right, id="false")
│  abc == true             │
│                          │
│  ● then (bottom center)  │
└──────────────────────────┘
 ◆ next (bottom-left, id="continue")

Handle shapes:
  ● = circle (8px, branch handle)
  ◆ = diamond (10px rotated 45deg, continuation handle, #4a9eff blue)
```

### Files Modified

| File | Change |
|------|--------|
| `FlowCanvas/src/nodes/BaseBlock.tsx` | Add diamond handle for `isContainer` blocks |
| `FlowCanvas/src/stores/slices/graphSlice.ts` | Handle `"continue"` sourceHandle in `onConnect` — no branch metadata, blue solid edge |
| `Services/FlowCanvasBridge.cs` | `TryGenerateContainerFromGraph`: filter `"continue"` edges from `nodeEdges`. `HasGraphAuthoredContainerBranches`: explicit `"continue"` guard. `CollectBranchChain`: skip `"continue"` edges. `TextToGraph`: single continuation edge from container. `PendingEdge`: add `Dashed` flag. Edge creation: separate label from dash logic. Add `ColorContinue` constant. |
| `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` | Tests for continuation handle export/import round-trip. Key cases: (1) IF with `"continue"` edge + `then` branch — continuation target NOT consumed as elif. (2) FOREACH with `"continue"` edge — NOT consumed as `do` branch. (3) Import-then-export round-trip — continuation edge survives without duplicate steps. |
