# Flow Canvas Auto-Organize Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the structure-blind dagre "Auto-organize" with one structure-aware TypeScript layout engine that lays out the container→branch→child tree (smart-hybrid: spine vertical, loops indented, multi-branch fanned into columns), and drive both the Auto-organize button and preset import through it.

**Architecture:** A new `utils/layout/` module reconstructs the layout tree from the graph (`treeBuilder.ts`, using `_isChildOf`/`_stepPath` metadata with an edge-based fallback) and positions it (`hierarchicalLayout.ts`, a faithful port of the C# `ExpandContainerChildren` math). The Auto-organize button (`useAutoLayout.ts`) always runs it; import (`messageBridge.ts`) runs it only when the host says there is no saved arrangement (`hasUserLayout`). dagre is deleted.

**Tech Stack:** TypeScript, React 19, `@xyflow/react` (React Flow) v12, Zustand v5, Vitest v4 (jsdom), Playwright. C# / .NET 8 WinForms host (`FlowCanvasBridge.cs`, `FlowCanvasForm.cs`, `Form1.cs`).

**Spec:** `docs/superpowers/specs/2026-05-31-flow-canvas-auto-layout-overhaul-design.md`

---

## File Structure

| File | Responsibility |
| --- | --- |
| `FlowCanvas/src/utils/layout/types.ts` (NEW) | `LayoutTreeNode`, `LayoutBranch`, `LayoutTree`, `Point` interfaces |
| `FlowCanvas/src/utils/layout/branchScope.ts` (NEW) | derive a branch's scope path + left-to-right sort rank from `_stepPath` or an edge `branchPath` |
| `FlowCanvas/src/utils/layout/treeBuilder.ts` (NEW) | `buildLayoutTree(nodes, edges)` — reconstruct the tree (metadata first, edge fallback, cycle guard, orphans) |
| `FlowCanvas/src/utils/layout/hierarchicalLayout.ts` (NEW) | constants + `placeTree()` + `computeHierarchicalLayout(nodes, edges)` |
| `FlowCanvas/src/utils/branchBands.ts` (MODIFY) | export `branchKeyFromStepPath` for reuse |
| `FlowCanvas/src/hooks/useAutoLayout.ts` (MODIFY) | call the new engine instead of dagre |
| `FlowCanvas/src/stores/messageBridge.ts` (MODIFY) | run the engine on import when `!hasUserLayout` |
| `FlowCanvas/src/communication-message-types.ts` (MODIFY) | document `hasUserLayout` on `load-graph` |
| `FlowCanvas/src/utils/autoLayout.ts` (DELETE) | the dagre layout |
| `FlowCanvas/package.json` (MODIFY) | remove `@dagrejs/dagre` + `@types/dagre` |
| `UI/FlowCanvasForm.cs` (MODIFY) | add `hasUserLayout` to `LoadGraph` + `load-graph` message |
| `Form1.cs` (MODIFY) | compute and pass `hasUserLayout` |
| `Services/FlowCanvasBridge.cs` (MODIFY, Phase 3) | retire dead position math |
| `FlowCanvas/src/utils/layout/__tests__/*.test.ts` (NEW) | unit tests |
| `FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts` (NEW) | e2e |

**How to run tests (from `FlowCanvas/`):**
- One unit file: `npx vitest run src/utils/layout/__tests__/treeBuilder.test.ts`
- All unit: `npm test`
- One e2e file: `npm run build:main` then `npx playwright test e2e/flow-canvas-auto-layout.spec.ts` (see existing e2e config; `installHostMessageCapture` serves the built app)
- C# tests: from repo root `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridge"`

---

## Phase 1 — Engine + button

### Task 1: Export the branch-key helper for reuse

**Files:**
- Modify: `FlowCanvas/src/utils/branchBands.ts` (the `function branchKeyFromStepPath` declaration)

- [ ] **Step 1: Export the existing helper**

In `FlowCanvas/src/utils/branchBands.ts`, change the declaration from private to exported:

```typescript
export function branchKeyFromStepPath(stepPath: string | undefined, branchLabel: string | undefined): string {
```

(Only the `export ` keyword is added; the body is unchanged.)

- [ ] **Step 2: Verify nothing broke**

Run: `npx vitest run` (from `FlowCanvas/`)
Expected: PASS (same count as before — this is a pure export change).

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/utils/branchBands.ts
git commit -m 'refactor(flow-canvas): export branchKeyFromStepPath for layout reuse'
```

---

### Task 2: Layout types + branch-scope helpers

**Files:**
- Create: `FlowCanvas/src/utils/layout/types.ts`
- Create: `FlowCanvas/src/utils/layout/branchScope.ts`
- Test: `FlowCanvas/src/utils/layout/__tests__/branchScope.test.ts`

- [ ] **Step 1: Create the types**

`FlowCanvas/src/utils/layout/types.ts`:

```typescript
import type { Node } from '@xyflow/react';

export interface Point {
  x: number;
  y: number;
}

/** One branch of a container (e.g. then/else/elif-0/case-1/catch/parallel-0). */
export interface LayoutBranch {
  /** Full branch scope, e.g. 'then', 'else', 'cases/1/do', 'parallel/0'. Distinguishes sibling branches. */
  scope: string;
  /** Left-to-right ordering rank (then < elif* < else; try < catch < finally; cases in order). */
  sortRank: number;
  children: LayoutTreeNode[];
}

export interface LayoutTreeNode {
  id: string;
  node: Node;
  isContainer: boolean;
  /** Non-empty only for containers that have children. */
  branches: LayoutBranch[];
}

export interface LayoutTree {
  /** Top-level sequence, excluding the start node. */
  spine: LayoutTreeNode[];
}
```

- [ ] **Step 2: Write the failing test for branch-scope helpers**

`FlowCanvas/src/utils/layout/__tests__/branchScope.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { branchScopeFromStepPath, branchScopeFromBranchPath, branchSortRank } from '../branchScope';

describe('branchScopeFromStepPath', () => {
  it('extracts the branch scope between container path and child index', () => {
    expect(branchScopeFromStepPath('steps/0/then/0', 'steps/0')).toBe('then');
    expect(branchScopeFromStepPath('steps/1/cases/2/do/0', 'steps/1')).toBe('cases/2/do');
    expect(branchScopeFromStepPath('steps/0/else/3', 'steps/0')).toBe('else');
  });

  it('falls back to dropping only the trailing index when the prefix does not match', () => {
    expect(branchScopeFromStepPath('then/0', undefined)).toBe('then');
  });
});

describe('branchScopeFromBranchPath', () => {
  it('passes canvas-built branch paths through unchanged', () => {
    expect(branchScopeFromBranchPath('then')).toBe('then');
    expect(branchScopeFromBranchPath('cases/1/do')).toBe('cases/1/do');
    expect(branchScopeFromBranchPath('parallel/0')).toBe('parallel/0');
  });
});

describe('branchSortRank', () => {
  it('orders if branches then < elif < else', () => {
    expect(branchSortRank('then')).toBeLessThan(branchSortRank('elif/0/then'));
    expect(branchSortRank('elif/0/then')).toBeLessThan(branchSortRank('else'));
  });
  it('orders switch cases numerically', () => {
    expect(branchSortRank('cases/0/do')).toBeLessThan(branchSortRank('cases/1/do'));
  });
  it('orders try < catch < finally', () => {
    expect(branchSortRank('try')).toBeLessThan(branchSortRank('catch'));
    expect(branchSortRank('catch')).toBeLessThan(branchSortRank('finally'));
  });
});
```

- [ ] **Step 3: Run to verify it fails**

Run: `npx vitest run src/utils/layout/__tests__/branchScope.test.ts`
Expected: FAIL — "Cannot find module '../branchScope'".

- [ ] **Step 4: Implement the helpers**

`FlowCanvas/src/utils/layout/branchScope.ts`:

```typescript
/**
 * Branch scope = the path segment(s) identifying which branch of a container a child
 * belongs to, e.g. 'then', 'else', 'cases/1/do', 'parallel/0'. Sibling branches have
 * distinct scopes (so switch cases / parallel arms / elifs do not collapse together).
 */
export function branchScopeFromStepPath(
  childStepPath: string,
  containerStepPath: string | undefined,
): string {
  let rest = childStepPath;
  if (containerStepPath && childStepPath.startsWith(`${containerStepPath}/`)) {
    rest = childStepPath.slice(containerStepPath.length + 1);
  }
  // Drop the trailing child index ('/0', '/3', ...).
  return rest.replace(/\/\d+$/, '');
}

/** Canvas-built edges already carry the branch scope as their branchPath. */
export function branchScopeFromBranchPath(branchPath: string): string {
  return branchPath;
}

const HEAD_RANK: Record<string, number> = {
  then: 0, do: 0, loop: 0, try: 0, parallel: 0,
  elif: 1, catch: 1,
  else: 2, finally: 2,
  cases: 3, case: 3,
  default: 4,
};

/** Left-to-right ordering key for a branch scope. */
export function branchSortRank(scope: string): number {
  const segs = scope.split('/');
  const head = segs[0];
  const idx = segs.length > 1 && /^\d+$/.test(segs[1]) ? Number(segs[1]) : 0;
  const rank = HEAD_RANK[head] ?? 9;
  return rank * 1000 + idx;
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `npx vitest run src/utils/layout/__tests__/branchScope.test.ts`
Expected: PASS (all cases).

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/utils/layout/types.ts FlowCanvas/src/utils/layout/branchScope.ts FlowCanvas/src/utils/layout/__tests__/branchScope.test.ts
git commit -m 'feat(flow-canvas): layout types + branch-scope helpers'
```

---

### Task 3: Tree builder — metadata path (imported presets)

**Files:**
- Create: `FlowCanvas/src/utils/layout/treeBuilder.ts`
- Test: `FlowCanvas/src/utils/layout/__tests__/treeBuilder.test.ts`

This is the primary, reliable reconstruction: imported graphs carry `_isChildOf` (parent id) and `_stepPath` on every child.

- [ ] **Step 1: Write the failing test**

`FlowCanvas/src/utils/layout/__tests__/treeBuilder.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { buildLayoutTree } from '../treeBuilder';

// Imported if/else: container at steps/0 with one then-child and one else-child.
function ifElseGraph(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _stepPath: 'steps/0' } } } as Node,
    { id: 'then-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } } as Node,
    { id: 'else-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } } as Node,
    { id: 'tail-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/1' } } } as Node,
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' } as Edge,
    { id: 'e1', source: 'if-1', target: 'then-1' } as Edge,
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false' } as Edge,
    { id: 'e3', source: 'if-1', target: 'tail-1', sourceHandle: 'continue' } as Edge,
  ];
  return { nodes, edges };
}

describe('buildLayoutTree (metadata)', () => {
  it('puts top-level steps on the spine, excluding the start node', () => {
    const { nodes, edges } = ifElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    expect(tree.spine.map((n) => n.id)).toEqual(['if-1', 'tail-1']);
  });

  it('attaches then/else as separate, correctly ordered branches of the container', () => {
    const { nodes, edges } = ifElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    const ifNode = tree.spine.find((n) => n.id === 'if-1')!;
    expect(ifNode.isContainer).toBe(true);
    expect(ifNode.branches.map((b) => b.scope)).toEqual(['then', 'else']);
    expect(ifNode.branches[0].children.map((c) => c.id)).toEqual(['then-1']);
    expect(ifNode.branches[1].children.map((c) => c.id)).toEqual(['else-1']);
  });

  it('does not put branch children on the spine', () => {
    const { nodes, edges } = ifElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    expect(tree.spine.map((n) => n.id)).not.toContain('then-1');
    expect(tree.spine.map((n) => n.id)).not.toContain('else-1');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx vitest run src/utils/layout/__tests__/treeBuilder.test.ts`
Expected: FAIL — "Cannot find module '../treeBuilder'".

- [ ] **Step 3: Implement the metadata path**

`FlowCanvas/src/utils/layout/treeBuilder.ts`:

```typescript
import type { Edge, Node } from '@xyflow/react';
import { blockDefMap } from '../../blockDefs/registry';
import { branchScopeFromStepPath, branchSortRank } from './branchScope';
import type { LayoutBranch, LayoutTree, LayoutTreeNode } from './types';

const START_ID = '__start__';

function blockTypeOf(node: Node): string {
  return ((node.data as { blockType?: string } | undefined)?.blockType) ?? '';
}
function propsOf(node: Node): Record<string, unknown> {
  return ((node.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};
}
function isContainerNode(node: Node): boolean {
  return !!blockDefMap.get(blockTypeOf(node))?.isContainer;
}
function isComment(node: Node): boolean {
  return node.type === 'comment';
}
function childIndexOf(node: Node): number {
  const sp = propsOf(node)['_stepPath'] as string | undefined;
  if (!sp) return 0;
  const last = sp.split('/').pop()!;
  const n = Number(last);
  return Number.isFinite(n) ? n : 0;
}

export function buildLayoutTree(nodes: Node[], edges: Edge[]): LayoutTree {
  const layoutable = nodes.filter((n) => n.id !== START_ID && !isComment(n));

  // parentId -> child nodes (imported metadata)
  const metaChildren = new Map<string, Node[]>();
  for (const n of layoutable) {
    const parentId = propsOf(n)['_isChildOf'] as string | undefined;
    if (parentId) {
      const arr = metaChildren.get(parentId);
      if (arr) arr.push(n);
      else metaChildren.set(parentId, [n]);
    }
  }

  const claimed = new Set<string>(); // nodes that belong to some container branch
  const building = new Set<string>(); // cycle guard

  function toTreeNode(node: Node): LayoutTreeNode {
    const isContainer = isContainerNode(node);
    let branches: LayoutBranch[] = [];
    if (isContainer && !building.has(node.id)) {
      building.add(node.id);
      branches = buildBranchesMeta(node);
      building.delete(node.id);
    }
    return { id: node.id, node, isContainer, branches };
  }

  function buildBranchesMeta(container: Node): LayoutBranch[] {
    const kids = metaChildren.get(container.id);
    if (!kids || kids.length === 0) return [];
    const containerStepPath = propsOf(container)['_stepPath'] as string | undefined;

    // Group by full branch scope so sibling branches (cases/elifs/parallel arms) stay separate.
    const groups = new Map<string, Node[]>();
    for (const k of kids) {
      const sp = (propsOf(k)['_stepPath'] as string | undefined) ?? '';
      const scope = branchScopeFromStepPath(sp, containerStepPath);
      const arr = groups.get(scope);
      if (arr) arr.push(k);
      else groups.set(scope, [k]);
    }

    const branches: LayoutBranch[] = [];
    for (const [scope, groupKids] of groups) {
      const ordered = [...groupKids].sort((a, b) => childIndexOf(a) - childIndexOf(b));
      ordered.forEach((k) => claimed.add(k.id));
      branches.push({ scope, sortRank: branchSortRank(scope), children: ordered.map(toTreeNode) });
    }
    branches.sort((a, b) => a.sortRank - b.sortRank);
    return branches;
  }

  // Resolve all container branches first so `claimed` is fully populated.
  const treeNodes = new Map<string, LayoutTreeNode>();
  for (const n of layoutable) treeNodes.set(n.id, toTreeNode(n));

  // Spine = top-level nodes (not claimed by any container), in document order.
  const spine = layoutable.filter((n) => !claimed.has(n.id)).map((n) => treeNodes.get(n.id)!);

  return { spine };
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `npx vitest run src/utils/layout/__tests__/treeBuilder.test.ts`
Expected: PASS (3 cases).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/treeBuilder.ts FlowCanvas/src/utils/layout/__tests__/treeBuilder.test.ts
git commit -m 'feat(flow-canvas): tree builder metadata path'
```

---

### Task 4: Tree builder — spine ordering, edge fallback, cycle guard, orphans

The metadata path leaves the spine in array order and ignores canvas-built containers (children without `_isChildOf`). Make the spine follow edges from the start node, add an edge-based fallback for unclaimed container children, guard loop back-edges, and keep orphans.

**Files:**
- Modify: `FlowCanvas/src/utils/layout/treeBuilder.ts`
- Test: `FlowCanvas/src/utils/layout/__tests__/treeBuilder.test.ts`

- [ ] **Step 1: Add failing tests (canvas-built + spine order + cycle)**

Append to `treeBuilder.test.ts`:

```typescript
// Canvas-built if/else: structure lives on edges (branchPath), no _isChildOf metadata.
function canvasIfElseGraph(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: {} } } as Node,
    { id: 'then-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    { id: 'else-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' } as Edge,
    { id: 'e1', source: 'if-1', target: 'then-1', data: { branchPath: 'then' } } as Edge,
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
  ];
  return { nodes, edges };
}

describe('buildLayoutTree (edge fallback + robustness)', () => {
  it('reconstructs branches from edges when metadata is absent', () => {
    const { nodes, edges } = canvasIfElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    const ifNode = tree.spine.find((n) => n.id === 'if-1')!;
    expect(ifNode.branches.map((b) => b.scope)).toEqual(['then', 'else']);
    expect(tree.spine.map((n) => n.id)).toEqual(['if-1']); // children not on spine
  });

  it('orders the spine by following edges from the start node', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'b', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/1' } } } as Node,
      { id: 'a', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/0' } } } as Node,
    ];
    const edges: Edge[] = [
      { id: 'e0', source: '__start__', target: 'a' } as Edge,
      { id: 'e1', source: 'a', target: 'b' } as Edge,
    ];
    expect(buildLayoutTree(nodes, edges).spine.map((n) => n.id)).toEqual(['a', 'b']);
  });

  it('does not loop forever on a while back-edge', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'w', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'while', props: {} } } as Node,
      { id: 'body', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    ];
    const edges: Edge[] = [
      { id: 'e0', source: '__start__', target: 'w' } as Edge,
      { id: 'e1', source: 'w', target: 'body', data: { branchPath: 'do' } } as Edge,
      { id: 'e2', source: 'body', target: 'w' } as Edge, // back-edge
    ];
    const tree = buildLayoutTree(nodes, edges);
    expect(tree.spine.map((n) => n.id)).toEqual(['w']);
    expect(tree.spine[0].branches[0].children.map((c) => c.id)).toEqual(['body']);
  });

  it('keeps orphan (disconnected) nodes on the spine', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'orphan', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    ];
    expect(buildLayoutTree(nodes, []).spine.map((n) => n.id)).toEqual(['orphan']);
  });
});
```

- [ ] **Step 2: Run to verify the new cases fail**

Run: `npx vitest run src/utils/layout/__tests__/treeBuilder.test.ts`
Expected: FAIL — canvas-built `if-1` has no branches; spine-order test may pass by luck but the edge-fallback/cycle cases fail.

- [ ] **Step 3: Implement edge fallback, edge-ordered spine, cycle guard, orphans**

Replace the body of `buildLayoutTree` in `treeBuilder.ts` with the version below (adds `outBySource`, edge-based branch resolution, and an edge-ordered spine). Imports stay the same plus add `branchScopeFromBranchPath`:

```typescript
import { branchScopeFromBranchPath, branchScopeFromStepPath, branchSortRank } from './branchScope';
```

```typescript
export function buildLayoutTree(nodes: Node[], edges: Edge[]): LayoutTree {
  const layoutable = nodes.filter((n) => n.id !== START_ID && !isComment(n));
  const byId = new Map(nodes.map((n) => [n.id, n] as const));

  const metaChildren = new Map<string, Node[]>();
  for (const n of layoutable) {
    const parentId = propsOf(n)['_isChildOf'] as string | undefined;
    if (parentId) {
      const arr = metaChildren.get(parentId);
      if (arr) arr.push(n);
      else metaChildren.set(parentId, [n]);
    }
  }

  const outBySource = new Map<string, Edge[]>();
  for (const e of edges) {
    const arr = outBySource.get(e.source);
    if (arr) arr.push(e);
    else outBySource.set(e.source, [e]);
  }

  const claimed = new Set<string>();
  const building = new Set<string>();

  function branchPathOf(edge: Edge): string | undefined {
    return (edge.data as { branchPath?: string } | undefined)?.branchPath;
  }

  function toTreeNode(node: Node): LayoutTreeNode {
    const isContainer = isContainerNode(node);
    let branches: LayoutBranch[] = [];
    if (isContainer && !building.has(node.id)) {
      building.add(node.id);
      branches = buildBranchesMeta(node);
      if (branches.length === 0) branches = buildBranchesEdges(node);
      building.delete(node.id);
    }
    return { id: node.id, node, isContainer, branches };
  }

  function buildBranchesMeta(container: Node): LayoutBranch[] {
    const kids = metaChildren.get(container.id);
    if (!kids || kids.length === 0) return [];
    const containerStepPath = propsOf(container)['_stepPath'] as string | undefined;
    const groups = new Map<string, Node[]>();
    for (const k of kids) {
      const sp = (propsOf(k)['_stepPath'] as string | undefined) ?? '';
      const scope = branchScopeFromStepPath(sp, containerStepPath);
      const arr = groups.get(scope);
      if (arr) arr.push(k);
      else groups.set(scope, [k]);
    }
    const branches: LayoutBranch[] = [];
    for (const [scope, groupKids] of groups) {
      const ordered = [...groupKids].sort((a, b) => childIndexOf(a) - childIndexOf(b));
      ordered.forEach((k) => claimed.add(k.id));
      branches.push({ scope, sortRank: branchSortRank(scope), children: ordered.map(toTreeNode) });
    }
    branches.sort((a, b) => a.sortRank - b.sortRank);
    return branches;
  }

  // Canvas-built fallback: branches come from the container's non-'continue' outgoing
  // edges; each branch chain is followed via plain forward edges until it joins back or
  // is already claimed. Loop back-edges are stopped by the `building`/`claimed` guards.
  function buildBranchesEdges(container: Node): LayoutBranch[] {
    const out = (outBySource.get(container.id) ?? []).filter((e) => e.sourceHandle !== 'continue');
    if (out.length === 0) return [];
    const branches: LayoutBranch[] = [];
    for (const edge of out) {
      const scope = branchScopeFromBranchPath(branchPathOf(edge) ?? edge.sourceHandle ?? 'then');
      const chain: LayoutTreeNode[] = [];
      let cursor: string | undefined = edge.target;
      const localSeen = new Set<string>([container.id]);
      while (cursor && !claimed.has(cursor) && !localSeen.has(cursor) && !building.has(cursor)) {
        const node = byId.get(cursor);
        if (!node) break;
        localSeen.add(cursor);
        claimed.add(cursor);
        chain.push(toTreeNode(node));
        // Continue down a single plain forward edge; stop at branch/continue forks or joins.
        const next = (outBySource.get(cursor) ?? []).filter(
          (e) => e.sourceHandle !== 'continue' && e.sourceHandle !== 'false' && branchPathOf(e) === undefined,
        );
        cursor = next.length === 1 ? next[0].target : undefined;
      }
      if (chain.length > 0) {
        branches.push({ scope, sortRank: branchSortRank(scope), children: chain });
      }
    }
    branches.sort((a, b) => a.sortRank - b.sortRank);
    return branches;
  }

  const treeNodes = new Map<string, LayoutTreeNode>();
  for (const n of layoutable) treeNodes.set(n.id, toTreeNode(n));

  // Edge-ordered spine: walk from the start node's successor following non-branch,
  // non-back forward edges; append any unclaimed/unvisited nodes (orphans) afterward.
  const spine: LayoutTreeNode[] = [];
  const onSpine = new Set<string>();
  function pushSpine(id: string) {
    if (onSpine.has(id) || claimed.has(id)) return;
    const tn = treeNodes.get(id);
    if (!tn) return;
    onSpine.add(id);
    spine.push(tn);
  }

  const startOut = outBySource.get(START_ID) ?? [];
  let cursor: string | undefined = startOut[0]?.target;
  const walkSeen = new Set<string>();
  while (cursor && !walkSeen.has(cursor)) {
    walkSeen.add(cursor);
    pushSpine(cursor);
    const node = byId.get(cursor);
    const isContainer = node ? isContainerNode(node) : false;
    const out = outBySource.get(cursor) ?? [];
    // The continuation out of a container is its 'continue' handle; otherwise the plain
    // next edge whose target isn't a claimed branch child.
    const cont = isContainer
      ? out.find((e) => e.sourceHandle === 'continue')
      : out.find((e) => !claimed.has(e.target) && branchPathOf(e) === undefined && e.sourceHandle !== 'false');
    cursor = cont?.target;
    if (cursor && (onSpine.has(cursor) || claimed.has(cursor))) cursor = undefined;
  }

  // Any remaining top-level nodes (disconnected/orphans) keep their place at the end.
  for (const n of layoutable) if (!claimed.has(n.id) && !onSpine.has(n.id)) pushSpine(n.id);

  return { spine };
}
```

- [ ] **Step 4: Run to verify all pass**

Run: `npx vitest run src/utils/layout/__tests__/treeBuilder.test.ts`
Expected: PASS (Task 3 + Task 4 cases — 7 total).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/treeBuilder.ts FlowCanvas/src/utils/layout/__tests__/treeBuilder.test.ts
git commit -m 'feat(flow-canvas): tree builder edge fallback, spine order, cycle + orphan handling'
```

---

### Task 5: Placement — constants, spine, single-branch indent

**Files:**
- Create: `FlowCanvas/src/utils/layout/hierarchicalLayout.ts`
- Test: `FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts`

- [ ] **Step 1: Write the failing test**

`FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import type { LayoutTree, LayoutTreeNode } from '../types';
import { placeTree, LAYOUT } from '../hierarchicalLayout';

const leaf = (id: string): LayoutTreeNode => ({ id, node: { id } as never, isContainer: false, branches: [] });

describe('placeTree — spine', () => {
  it('stacks spine nodes vertically at the spine X, spaced by NodeSpacingY', () => {
    const tree: LayoutTree = { spine: [leaf('a'), leaf('b'), leaf('c')] };
    const pos = placeTree(tree);
    expect(pos.get('a')!.x).toBe(LAYOUT.NODE_START_X);
    expect(pos.get('b')!.x).toBe(LAYOUT.NODE_START_X);
    expect(pos.get('b')!.y - pos.get('a')!.y).toBe(LAYOUT.NODE_SPACING_Y);
    expect(pos.get('c')!.y - pos.get('b')!.y).toBe(LAYOUT.NODE_SPACING_Y);
  });
});

describe('placeTree — single-branch container (loop indents right)', () => {
  it('indents the loop body to the right of the container center', () => {
    const loop: LayoutTreeNode = {
      id: 'loop', node: { id: 'loop' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [leaf('body1'), leaf('body2')] }],
    };
    const pos = placeTree({ spine: [loop] });
    expect(pos.get('body1')!.x).toBe(LAYOUT.NODE_START_X + LAYOUT.SINGLE_BRANCH_CHILD_OFFSET);
    expect(pos.get('body2')!.y).toBeGreaterThan(pos.get('body1')!.y);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx vitest run src/utils/layout/__tests__/hierarchicalLayout.test.ts`
Expected: FAIL — "Cannot find module '../hierarchicalLayout'".

- [ ] **Step 3: Implement constants + placeTree (spine + single-branch)**

`FlowCanvas/src/utils/layout/hierarchicalLayout.ts`:

```typescript
import type { Edge, Node } from '@xyflow/react';
import { buildLayoutTree } from './treeBuilder';
import type { LayoutBranch, LayoutTree, LayoutTreeNode, Point } from './types';

/** Single source of truth for layout spacing (ported from FlowCanvasBridge.cs). */
export const LAYOUT = {
  NODE_SPACING_Y: 106,
  SINGLE_BRANCH_CHILD_OFFSET: 70,
  NODE_START_X: 250,
  NODE_START_Y: 40,
  CHILD_NODE_MAX_WIDTH: 260,
  COLUMN_GAP: 30,
  get BASE_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 290
  COLUMN_WIDTH_DECAY: 0.92,
  get MIN_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 290
  MAX_SPREAD_WIDTH: 1400,
  MAX_NESTING_DEPTH: 5,
} as const;

interface SubtreeSize { columns: number; rows: number; }

function nonEmptyBranches(node: LayoutTreeNode): LayoutBranch[] {
  return node.branches.filter((b) => b.children.length > 0);
}

function getColumnWidth(depth: number): number {
  return Math.max(LAYOUT.MIN_COLUMN_WIDTH, LAYOUT.BASE_COLUMN_WIDTH * Math.pow(LAYOUT.COLUMN_WIDTH_DECAY, depth));
}

/** Mirrors C# MeasureSteps: column count + row count of a branch subtree. */
function measureSteps(children: LayoutTreeNode[]): SubtreeSize {
  let columns = 1;
  let rows = 0;
  for (const child of children) {
    rows += 1;
    if (!child.isContainer) continue;
    const branches = nonEmptyBranches(child);
    if (branches.length >= 2) {
      let totalCols = 0;
      let maxRows = 0;
      for (const b of branches) {
        const s = measureSteps(b.children);
        totalCols += s.columns;
        maxRows = Math.max(maxRows, s.rows);
      }
      columns = Math.max(columns, Math.max(2, totalCols));
      rows += maxRows;
    } else {
      for (const b of branches) {
        const s = measureSteps(b.children);
        columns = Math.max(columns, s.columns);
        rows += s.rows;
      }
    }
  }
  return { columns, rows };
}

function placeBranchSteps(
  children: LayoutTreeNode[],
  depth: number,
  childX: number,
  centerX: number,
  startY: number,
  pos: Map<string, Point>,
): number {
  let y = startY;
  for (const child of children) {
    pos.set(child.id, { x: childX, y });
    y += LAYOUT.NODE_SPACING_Y;
    if (child.isContainer && depth < LAYOUT.MAX_NESTING_DEPTH && nonEmptyBranches(child).length > 0) {
      y = placeContainer(child, depth + 1, centerX, y, pos);
    }
  }
  return y;
}

function placeSingleBranch(branch: LayoutBranch, depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const childX = centerX + LAYOUT.SINGLE_BRANCH_CHILD_OFFSET;
  return placeBranchSteps(branch.children, depth, childX, childX, startY, pos);
}

function placeMultiBranch(branches: LayoutBranch[], depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const sizes = branches.map((b) => measureSteps(b.children));
  const totalColumns = sizes.reduce((sum, s) => sum + s.columns, 0);
  let colWidth = getColumnWidth(depth);
  let totalPixelWidth = totalColumns * colWidth;
  if (totalPixelWidth > LAYOUT.MAX_SPREAD_WIDTH) {
    colWidth = LAYOUT.MAX_SPREAD_WIDTH / totalColumns;
    totalPixelWidth = LAYOUT.MAX_SPREAD_WIDTH;
  }
  let leftEdge = centerX - totalPixelWidth / 2;
  let maxEndY = startY;
  for (let i = 0; i < branches.length; i++) {
    const branchPixelWidth = sizes[i].columns * colWidth;
    const branchCenterX = leftEdge + branchPixelWidth / 2;
    const endY = placeBranchSteps(branches[i].children, depth, branchCenterX, branchCenterX, startY, pos);
    maxEndY = Math.max(maxEndY, endY);
    leftEdge += branchPixelWidth;
  }
  return maxEndY;
}

function placeContainer(node: LayoutTreeNode, depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const branches = nonEmptyBranches(node);
  if (branches.length === 0) return startY;
  if (branches.length >= 2) return placeMultiBranch(branches, depth, centerX, startY, pos);
  return placeSingleBranch(branches[0], depth, centerX, startY, pos);
}

export function placeTree(tree: LayoutTree): Map<string, Point> {
  const pos = new Map<string, Point>();
  let currentY = LAYOUT.NODE_START_Y + LAYOUT.NODE_SPACING_Y;
  for (const node of tree.spine) {
    pos.set(node.id, { x: LAYOUT.NODE_START_X, y: currentY });
    currentY += LAYOUT.NODE_SPACING_Y;
    if (node.isContainer && nonEmptyBranches(node).length > 0) {
      currentY = placeContainer(node, 1, LAYOUT.NODE_START_X, currentY, pos);
    }
  }
  return pos;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `npx vitest run src/utils/layout/__tests__/hierarchicalLayout.test.ts`
Expected: PASS (spine + single-branch cases).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/hierarchicalLayout.ts FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts
git commit -m 'feat(flow-canvas): placement engine — constants, spine, single-branch indent'
```

---

### Task 6: Placement — multi-branch columns + recursion (verify against the existing port)

The multi-branch + recursion code already exists from Task 5; this task adds the tests that prove the column geometry and nesting.

**Files:**
- Test: `FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts`

- [ ] **Step 1: Add failing tests for columns + recursion**

Append to `hierarchicalLayout.test.ts`:

```typescript
import { buildLayoutTree } from '../treeBuilder';
import { computeHierarchicalLayout } from '../hierarchicalLayout';

describe('placeTree — multi-branch (fans into side-by-side columns)', () => {
  it('places then/else at the same start Y in distinct X columns, centered on the container', () => {
    const ifNode: LayoutTreeNode = {
      id: 'if', node: { id: 'if' } as never, isContainer: true,
      branches: [
        { scope: 'then', sortRank: 0, children: [leaf('t1')] },
        { scope: 'else', sortRank: 2000, children: [leaf('e1')] },
      ],
    };
    const pos = placeTree({ spine: [ifNode] });
    // Both branch heads share the same Y (sibling columns start level).
    expect(pos.get('t1')!.y).toBe(pos.get('e1')!.y);
    // then is left of else.
    expect(pos.get('t1')!.x).toBeLessThan(pos.get('e1')!.x);
    // The two columns are centered on the container's X.
    const mid = (pos.get('t1')!.x + pos.get('e1')!.x) / 2;
    expect(mid).toBeCloseTo(LAYOUT.NODE_START_X, 5);
  });

  it('recurses into a nested container inside a branch', () => {
    const inner: LayoutTreeNode = {
      id: 'inner', node: { id: 'inner' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [leaf('deep')] }],
    };
    const outer: LayoutTreeNode = {
      id: 'outer', node: { id: 'outer' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [inner] }],
    };
    const pos = placeTree({ spine: [outer] });
    expect(pos.get('deep')).toBeDefined();
    expect(pos.get('deep')!.y).toBeGreaterThan(pos.get('inner')!.y);
  });
});
```

- [ ] **Step 2: Run to verify it passes (no overlap geometry)**

Run: `npx vitest run src/utils/layout/__tests__/hierarchicalLayout.test.ts`
Expected: PASS — the Task 5 implementation already supports this.

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts
git commit -m 'test(flow-canvas): multi-branch column + recursion geometry'
```

---

### Task 7: `computeHierarchicalLayout` entry + no-overlap invariant

**Files:**
- Modify: `FlowCanvas/src/utils/layout/hierarchicalLayout.ts`
- Test: `FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts`

- [ ] **Step 1: Add the failing integration test**

Append to `hierarchicalLayout.test.ts`:

```typescript
import type { Edge, Node } from '@xyflow/react';

function importedIfElse(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 999, y: 999 }, data: { blockType: '_start' } } as Node,
    { id: 'if-1', type: 'block', position: { x: 7, y: 7 }, data: { blockType: 'if', props: { _stepPath: 'steps/0' } } } as Node,
    { id: 'then-1', type: 'block', position: { x: 7, y: 7 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } } as Node,
    { id: 'else-1', type: 'block', position: { x: 7, y: 7 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } } as Node,
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' } as Edge,
    { id: 'e1', source: 'if-1', target: 'then-1' } as Edge,
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false' } as Edge,
  ];
  return { nodes, edges };
}

describe('computeHierarchicalLayout', () => {
  it('repositions layoutable nodes and leaves the start node untouched', () => {
    const { nodes, edges } = importedIfElse();
    const out = computeHierarchicalLayout(nodes, edges);
    const start = out.find((n) => n.id === '__start__')!;
    expect(start.position).toEqual({ x: 999, y: 999 }); // start node not in spine, untouched
    const ifNode = out.find((n) => n.id === 'if-1')!;
    expect(ifNode.position.x).toBe(LAYOUT.NODE_START_X);
  });

  it('produces no overlapping branch children', () => {
    const { nodes, edges } = importedIfElse();
    const out = computeHierarchicalLayout(nodes, edges);
    const t = out.find((n) => n.id === 'then-1')!.position;
    const e = out.find((n) => n.id === 'else-1')!.position;
    // Different columns, far enough apart to not overlap (>= one node width).
    expect(Math.abs(t.x - e.x)).toBeGreaterThanOrEqual(LAYOUT.CHILD_NODE_MAX_WIDTH);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `npx vitest run src/utils/layout/__tests__/hierarchicalLayout.test.ts`
Expected: FAIL — `computeHierarchicalLayout` is not exported yet.

- [ ] **Step 3: Implement the entry point**

Append to `FlowCanvas/src/utils/layout/hierarchicalLayout.ts`:

```typescript
/**
 * Structure-aware layout: rebuild the container/branch tree and position it with the
 * smart-hybrid rules. Returns new node objects with updated positions; nodes not in the
 * tree (the start node, comments, orphans the builder left unplaced) keep their position.
 */
export function computeHierarchicalLayout(nodes: Node[], edges: Edge[]): Node[] {
  const tree = buildLayoutTree(nodes, edges);
  const pos = placeTree(tree);
  return nodes.map((n) => {
    const p = pos.get(n.id);
    return p ? { ...n, position: p } : n;
  });
}
```

- [ ] **Step 4: Run to verify it passes, and run the full unit suite**

Run: `npx vitest run src/utils/layout/__tests__/hierarchicalLayout.test.ts`
Expected: PASS.
Run: `npm test`
Expected: PASS (whole suite green).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/hierarchicalLayout.ts FlowCanvas/src/utils/layout/__tests__/hierarchicalLayout.test.ts
git commit -m 'feat(flow-canvas): computeHierarchicalLayout entry + no-overlap invariant'
```

---

### Task 8: Wire the Auto-organize button; delete dagre

**Files:**
- Modify: `FlowCanvas/src/hooks/useAutoLayout.ts`
- Delete: `FlowCanvas/src/utils/autoLayout.ts`
- Modify: `FlowCanvas/package.json`

- [ ] **Step 1: Point the button at the new engine**

Replace the entire body of `FlowCanvas/src/hooks/useAutoLayout.ts`:

```typescript
import { useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeHierarchicalLayout } from '../utils/layout/hierarchicalLayout';

/**
 * Returns a stable callback that re-lays the whole graph with the structure-aware
 * hierarchical engine. Always overrides the current arrangement (explicit user action);
 * pushes an undo snapshot first so it is reversible.
 */
export function useAutoLayout(): () => void {
  return useCallback(() => {
    const store = useFlowStore.getState();
    store.pushSnapshot('Auto-layout');
    const layouted = computeHierarchicalLayout(store.nodes, store.edges);
    store.setNodes(layouted, { markDirty: true });
  }, []);
}
```

- [ ] **Step 2: Delete the dagre layout and its dependency**

```bash
git rm FlowCanvas/src/utils/autoLayout.ts
```

In `FlowCanvas/package.json`, remove the line `"@dagrejs/dagre": "^3.0.0",` from `dependencies` and `"@types/dagre": "^0.7.54",` from `devDependencies`. Then refresh the lockfile:

Run: `npm install` (from `FlowCanvas/`)
Expected: removes dagre from `node_modules` / `package-lock.json`, exits 0.

- [ ] **Step 3: Verify no remaining references to the deleted module**

Run: `npx tsc --noEmit` (from `FlowCanvas/`)
Expected: PASS — no import of `./utils/autoLayout` or `@dagrejs/dagre` remains. If tsc reports an unresolved import, grep for `autoLayout` and `dagre` and remove the stragglers.

- [ ] **Step 4: Build + unit tests**

Run: `npm test`
Expected: PASS.
Run: `npm run build` (from `FlowCanvas/`)
Expected: `tsc && vite build` succeeds.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/hooks/useAutoLayout.ts FlowCanvas/package.json FlowCanvas/package-lock.json
git commit -m 'feat(flow-canvas): drive Auto-organize button with hierarchical engine; remove dagre'
```

---

## Phase 2 — Import unification

### Task 9: C# — add `hasUserLayout` to the load-graph message

**Files:**
- Modify: `UI/FlowCanvasForm.cs` (`LoadGraph`)
- Modify: `Form1.cs` (`LoadCurrentScriptIntoCanvas`)

- [ ] **Step 1: Add the optional flag to `LoadGraph`**

In `UI/FlowCanvasForm.cs`, replace `LoadGraph`:

```csharp
        /// <summary>
        /// Sends a load-graph message to display nodes and edges.
        /// <paramref name="hasUserLayout"/> tells the canvas whether the positions are a
        /// saved user arrangement (true → keep) or algorithmic defaults (false → the canvas
        /// will run its hierarchical auto-layout).
        /// </summary>
        public void LoadGraph(object nodes, object edges, bool hasUserLayout = false)
        {
            SendMessage(new { type = "load-graph", nodes, edges, hasUserLayout });
        }
```

(The optional parameter keeps the existing empty-graph call site `LoadGraph(new JArray(), new JArray())` compiling — it sends `hasUserLayout = false`.)

- [ ] **Step 2: Compute and pass the flag on import**

In `Form1.cs` `LoadCurrentScriptIntoCanvas`, replace the merge block + send with:

```csharp
                var bridge = new FlowCanvasBridge();
                var (nodes, edges) = bridge.TextToGraph(scriptText);

                // Merge stored canvas layout if the script structure hasn't changed.
                bool hasUserLayout = false;
                if (!string.IsNullOrEmpty(_activePresetName))
                {
                    var preset = _presetManager.Get(_activePresetName);
                    var layout = preset?.CanvasLayout;
                    if (layout != null)
                    {
                        var currentHash = FlowCanvasBridge.ComputeStructureHash(nodes);
                        if (string.Equals(currentHash, layout.StructureHash, StringComparison.Ordinal))
                        {
                            FlowCanvasBridge.MergeLayout(nodes, layout);
                            hasUserLayout = true;
                        }
                    }
                }

                _flowCanvasForm.LoadGraph(nodes, edges, hasUserLayout);
```

- [ ] **Step 3: Build the host**

Run (repo root): `dotnet build SSH_Helper.csproj -c Debug --nologo -v quiet`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add UI/FlowCanvasForm.cs Form1.cs
git commit -m 'feat(flow-canvas): send hasUserLayout flag with load-graph'
```

---

### Task 10: React — run the engine on import when there is no saved layout

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts`
- Modify: `FlowCanvas/src/communication-message-types.ts` (doc comment only)

- [ ] **Step 1: Update the `load-graph` handler**

In `FlowCanvas/src/stores/messageBridge.ts`, add the import at the top with the other imports:

```typescript
import { computeHierarchicalLayout } from '../utils/layout/hierarchicalLayout';
```

Replace the `messageBus.on('load-graph', ...)` handler with:

```typescript
    messageBus.on('load-graph', (msg) => {
      if (msg.nodes && msg.edges) {
        store.getState().setNodes(msg.nodes as Node[]);
        store.getState().setEdges(msg.edges as Edge[]);
        ensureStartNodeExists(store);

        // No saved arrangement → lay it out with the structure-aware engine. Compute then
        // setNodes once (synchronous, before paint) so there is no flash of raw positions.
        const hasUserLayout = (msg as { hasUserLayout?: boolean }).hasUserLayout === true;
        if (!hasUserLayout) {
          const s = store.getState();
          store.getState().setNodes(computeHierarchicalLayout(s.nodes, s.edges));
        }

        resetGraphSessionState(store);

        // Restore disabled block state from loaded node data
        const state = store.getState();
        const disabledIds: string[] = [];
        for (const node of state.nodes) {
          const data = node.data as Record<string, unknown> | undefined;
          if (data?.disabled === true) {
            disabledIds.push(node.id);
          }
        }
        if (disabledIds.length > 0) {
          state.restoreDisabledBlocks(disabledIds);
        }
      }
    }),
```

- [ ] **Step 2: Document the flag**

In `FlowCanvas/src/communication-message-types.ts`, add a doc comment above `CANVAS_HOST_MESSAGES` (no behavioral change — `load-graph` is handled directly, not via this map):

```typescript
/**
 * Note: the host also sends a `load-graph` message (handled directly in messageBridge.ts):
 *   { type: 'load-graph', nodes, edges, hasUserLayout?: boolean }
 * `hasUserLayout` true = positions are a saved user arrangement (keep them);
 * false/absent = the canvas runs computeHierarchicalLayout() on import.
 */
```

- [ ] **Step 3: Type-check + unit tests**

Run (from `FlowCanvas/`): `npx tsc --noEmit`
Expected: PASS.
Run: `npm test`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts FlowCanvas/src/communication-message-types.ts
git commit -m 'feat(flow-canvas): auto-layout fresh imports via hierarchical engine'
```

---

### Task 11: E2E — import + button produce a clean hybrid

**Files:**
- Create: `FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts`

Uses the existing harness (`loadGraphFixture`, `getGraphSnapshot`, `installHostMessageCapture`, `waitForOutgoingMessage`) and reads positions from the store snapshot (zoom-independent flow coords — avoids the `boundingBox`/zoom pitfall).

- [ ] **Step 1: Write the e2e spec**

`FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts`:

```typescript
import { expect, test, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

// Imported if/else with deliberately scattered/overlapping positions.
const messyIfElse = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'if-1', type: 'block', position: { x: 500, y: 500 }, data: { blockType: 'if', label: 'If', props: { condition: '${x}', _stepPath: 'steps/0' } } },
    { id: 'then-1', type: 'block', position: { x: 505, y: 505 }, data: { blockType: 'print', label: 'Then', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then', message: 't' } } },
    { id: 'else-1', type: 'block', position: { x: 510, y: 510 }, data: { blockType: 'print', label: 'Else', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else', message: 'e' } } },
  ],
  edges: [
    { id: 'edge-start-if', source: '__start__', target: 'if-1', style: { stroke: '#666' } },
    { id: 'edge-if-then', source: 'if-1', target: 'then-1', label: 'then', style: { stroke: 'var(--fc-branch-then)' } },
    { id: 'edge-if-else', source: 'if-1', target: 'else-1', sourceHandle: 'false', label: 'else', style: { stroke: 'var(--fc-branch-else)' } },
  ],
};

async function posById(page: Page, id: string): Promise<{ x: number; y: number }> {
  const snap = await getGraphSnapshot(page);
  const n = (snap.nodes as Array<{ id: string; position: { x: number; y: number } }>).find((m) => m.id === id)!;
  return n.position;
}

test.describe('Flow Canvas Auto-Organize (hierarchical)', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('fresh import lays branches into clean, non-overlapping columns', async ({ page }) => {
    // hasUserLayout omitted/false → engine runs on import.
    await postHostMessage(page, { type: 'load-graph', ...messyIfElse });
    await expect(page.locator('.react-flow__node[data-id="if-1"]')).toBeVisible();

    const then = await posById(page, 'then-1');
    const els = await posById(page, 'else-1');
    const ifp = await posById(page, 'if-1');

    expect(then.y).toBe(els.y);                       // sibling columns start level
    expect(then.x).toBeLessThan(els.x);               // then left of else
    expect(Math.abs(then.x - els.x)).toBeGreaterThanOrEqual(260); // no overlap
    expect((then.x + els.x) / 2).toBeCloseTo(ifp.x, 0); // columns centered on the container
    expect(then.y).toBeGreaterThan(ifp.y);            // children below the container
  });

  test('Auto-organize button overrides a saved arrangement', async ({ page }) => {
    // hasUserLayout true → import keeps the messy positions...
    await postHostMessage(page, { type: 'load-graph', ...messyIfElse, hasUserLayout: true });
    await expect(page.locator('.react-flow__node[data-id="if-1"]')).toBeVisible();
    expect((await posById(page, 'if-1')).x).toBe(500); // kept as-is

    // ...until the user presses the button, which re-lays everything.
    await loadGraphFixture; // (no-op import guard; keep lint happy if unused)
    await page.getByRole('button', { name: /Auto-organize|Layout/ }).click();

    expect((await posById(page, 'if-1')).x).toBe(250); // NODE_START_X
    const then = await posById(page, 'then-1');
    const els = await posById(page, 'else-1');
    expect(then.x).toBeLessThan(els.x);
  });
});
```

> If the Auto-organize button's accessible name differs, find it in `panels/Toolbar.tsx` (the `title="Auto-organize layout"` button) and adjust the `getByRole` name. If `getGraphSnapshot`/`postHostMessage` signatures differ, mirror their usage in `e2e/flow-canvas-execution-path.spec.ts`.

- [ ] **Step 2: Build the app and run the spec**

Run (from `FlowCanvas/`): `npm run build`
Then: `npx playwright test e2e/flow-canvas-auto-layout.spec.ts`
Expected: 2 passed. (If the dev-server config is used instead of the built app, follow the pattern the other e2e specs use — they share `playwright.config.ts`.)

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-auto-layout.spec.ts
git commit -m 'test(flow-canvas): e2e for hierarchical import layout + button override'
```

---

## Phase 3 — Retire C# position math (optional cleanup)

After Phase 2, the canvas re-lays-out every fresh import, so the precise X/Y produced by the C# `Expand*`/`PlaceBranchSteps`/`MeasureSteps` code is never displayed. This phase removes that dead math while keeping node/edge/metadata creation and the structure hash. It is lower priority and can be a separate plan; do it only with the C# test suite green before and after.

### Task 12: Simplify C# child positioning

**Files:**
- Modify: `Services/FlowCanvasBridge.cs`
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` (should stay green unchanged)

- [ ] **Step 1: Confirm the baseline is green**

Run (repo root): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridge"`
Expected: PASS (record the count, e.g. 83 passed).

- [ ] **Step 2: Replace column measurement with a trivial vertical stack**

In `Services/FlowCanvasBridge.cs`, change `ExpandMultiBranch` so every branch is a simple right-indented vertical stack at incremental X (positions are placeholders the canvas overrides; structure/edges/metadata are unchanged). Replace the body of `ExpandMultiBranch` with:

```csharp
        private List<string> ExpandMultiBranch(
            List<BranchInfo> branches,
            string parentNodeId,
            string parentStepPath,
            ref double currentY,
            int depth,
            double centerX,
            JArray nodes,
            JArray edges)
        {
            // Positions are placeholders only — the canvas recomputes layout on import.
            // Keep branches in distinct columns so a host-only (no-canvas) render is still legible.
            var branchStartY = currentY;
            var maxBranchEndY = currentY;
            var branchEndNodes = new List<string>();
            double columnX = centerX;
            foreach (var branch in branches)
            {
                var branchY = branchStartY;
                var lastNodeId = PlaceBranchSteps(branch, parentNodeId, parentStepPath, ref branchY, depth, columnX, columnX, nodes, edges);
                branchEndNodes.Add(lastNodeId);
                maxBranchEndY = Math.Max(maxBranchEndY, branchY);
                columnX += MinColumnWidth;
            }
            currentY = maxBranchEndY;
            return branchEndNodes;
        }
```

- [ ] **Step 3: Delete the now-unused measurement helpers**

Remove `MeasureSteps`, `GetColumnWidth`, the `SubtreeSize` class, and the constants `ColumnWidthDecay`, `BaseColumnWidth`, `MaxSpreadWidth` (keep `MinColumnWidth`, still used above). After deleting, build to catch any remaining reference:

Run (repo root): `dotnet build SSH_Helper.csproj -c Debug --nologo -v quiet`
Expected: Build succeeded, 0 errors. If a deleted symbol is still referenced, the error names it — remove that reference too.

- [ ] **Step 4: Re-run the C# tests**

Run (repo root): `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasBridge"`
Expected: PASS (same count as Step 1 — no test asserts on computed Y positions).

- [ ] **Step 5: Commit**

```bash
git add Services/FlowCanvasBridge.cs
git commit -m 'refactor(flow-canvas): retire dead C# layout math (canvas owns positions)'
```

---

## Self-Review

- **Spec coverage:** root cause → Tasks 3–8 (engine replaces dagre); smart-hybrid rules → Tasks 5–6; unify import+button → Tasks 8–10; behavior matrix (button always overrides, import preserves saved) → Task 8 (button), Task 9–10 (`hasUserLayout`); delete dagre → Task 8; dual-origin reconstruction → Tasks 3–4; cycle/orphan/no-flash risks → Task 4 (guards), Task 10 (compute-then-set once); testing → Tasks 2–7 (vitest), 11 (e2e), 12 (C#); Phase 3 retire C# math → Task 12. All spec sections map to a task.
- **Type consistency:** `computeHierarchicalLayout(nodes, edges)`, `buildLayoutTree(nodes, edges)`, `placeTree(tree)`, `LAYOUT`, `LayoutTree`/`LayoutTreeNode`/`LayoutBranch`, `branchScopeFromStepPath`/`branchScopeFromBranchPath`/`branchSortRank` are used identically across tasks. `LayoutBranch.scope`/`sortRank`/`children` are consistent. `LoadGraph(object, object, bool=false)` matches the React `hasUserLayout` read.
- **Placeholders:** none — every code step shows full code; e2e notes point at concrete existing files to mirror if harness signatures differ.
