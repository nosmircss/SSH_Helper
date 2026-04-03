# FlowCanvas Bug Fix Plan (Verified)

This plan keeps only issues that were confirmed in the current codebase.

Confirmed issues: 4
Files touched: 5

---

## Confirmed Bug 1 (High): Side effects inside `set()` updater

**File**: `FlowCanvas/src/stores/slices/debugSlice.ts`  
**Lines**: 47-63

**Problem**  
`toggleDisabled` performs `get().updateNodeData(...)` and `messageBus.send(...)` inside the Zustand updater callback. This mixes side effects with state transition logic and can create ordering surprises.

**Fix**  
Compute `nowDisabled` in the updater, return state only, then do side effects after `set()` returns.

```ts
toggleDisabled: (nodeId) => {
  let nowDisabled = false;
  set((s) => {
    const next = new Set(s.disabledBlocks);
    nowDisabled = !next.has(nodeId);
    if (nowDisabled) next.add(nodeId);
    else next.delete(nodeId);
    return { disabledBlocks: next };
  });

  get().updateNodeData(nodeId, { execState: nowDisabled ? 'disabled' : 'idle' });
  messageBus.send({
    type: CANVAS_HOST_MESSAGES.outgoing.disableBlock,
    stepId: nodeId,
    disabled: nowDisabled,
  });
},
```

---

## Confirmed Bug 2 (Medium): Variable highlight timer overlap

**File**: `FlowCanvas/src/stores/slices/variableSlice.ts`  
**Lines**: 44-48

**Problem**  
`setVariablesWithChanges` creates a new `setTimeout` each call without canceling prior timers. Under rapid updates, earlier timers clear `changed` flags too early.

**Fix**  
Track one timer at module scope and reset it on each call.

```ts
// module scope
let changeHighlightTimer: ReturnType<typeof setTimeout> | null = null;

// inside setVariablesWithChanges
if (changeHighlightTimer) clearTimeout(changeHighlightTimer);
changeHighlightTimer = setTimeout(() => {
  changeHighlightTimer = null;
  set((s) => ({
    variables: s.variables.map((v) => ({ ...v, changed: false })),
  }));
}, 800);
```

---

## Confirmed Bug 3 (Medium): Dirty-state gaps for user graph mutations

**Files**  
- `FlowCanvas/src/stores/slices/graphSlice.ts`
- `FlowCanvas/src/hooks/useKeyboardShortcuts.ts`
- `FlowCanvas/src/hooks/useAutoLayout.ts`

**Problem**  
`setNodes()` and `setEdges()` do not mark `isDirty`. User actions that call them directly (paste and auto-layout) can leave `isDirty === false`, which affects `graphChanged` in run/test/apply payloads.

**Fix (recommended)**  
Add an optional `markDirty` flag to `setNodes` and `setEdges`, default `false`.

```ts
setNodes: (nodes, opts) => set({
  nodes,
  ...(opts?.markDirty ? { isDirty: true } : {}),
  ...clearedExportStatusState(),
}),
setEdges: (edges, opts) => set({
  edges,
  ...(opts?.markDirty ? { isDirty: true } : {}),
  ...clearedExportStatusState(),
}),
```

Then call with `markDirty: true` from:
- Ctrl+V paste path (`useKeyboardShortcuts.ts`)
- Auto-layout action (`useAutoLayout.ts`)

Keep host load path (`messageBridge.ts`) without `markDirty` and keep `clearDirty()` after load.

---

## Confirmed Bug 4 (Low/Perf): Duplicate `<style>` tag per node

**File**: `FlowCanvas/src/nodes/BaseBlock.tsx`  
**Lines**: 146-157

**Problem**  
Each rendered block injects an identical `<style>` tag. Large canvases duplicate CSS in DOM repeatedly.

**Fix**  
Move the keyframes/search classes into a CSS file loaded once (for example `FlowCanvas/src/nodes/baseblock.css`) and remove inline `<style>` from `BaseBlock`.

---

## Removed From This Plan (Not Confirmed As Active Bugs)

- Former "race in `toggleBreakpoint`": not a confirmed correctness bug in current synchronous store flow.
- Former "stale undo snapshot (`pushSnapshot`/`undo`/`redo`)": not confirmed as an active bug in current flow.
- Former "timeline updates all open entries": potential hardening idea, but not reproduced with current host message ordering.
- Former "`setBlockTiming` `end === 0` check": semantic cleanup only; currently low impact and no active call sites found.

---

## Execution Order

1. `debugSlice.ts`: Fix side effects in `toggleDisabled`.
2. `variableSlice.ts`: Add timer cancellation for variable highlights.
3. Dirty tracking cross-cut:
   - update `graphSlice.ts` setter signatures/implementation
   - update `useKeyboardShortcuts.ts` paste call
   - update `useAutoLayout.ts` layout call
4. `BaseBlock.tsx` + `nodes/baseblock.css`: extract shared CSS.
5. Build: `cd FlowCanvas && npm run build`.
6. Verify: `npm run test:e2e` (or targeted spec).

---

## Verification Checklist

- Toggling disable updates node visual and sends one `disable-block` message.
- Rapid variable updates keep highlight visible until 800ms after the latest update.
- Ctrl+V marks graph dirty and subsequent run/test payloads include `graphChanged: true`.
- Auto-layout marks graph dirty and subsequent run/test payloads include `graphChanged: true`.
- Only one definition of `exec-pulse`/`spin`/search classes exists in DOM-loaded CSS.
