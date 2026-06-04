import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Connection, Edge, Node } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';
import { buildLayoutTree } from '../../../utils/layout/treeBuilder';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../../../utils/layout/hierarchicalLayout';
import { computeBranchBands } from '../../../utils/branchBands';

function node(id: string, type: string, blockType: string, props: Record<string, unknown>): Node {
  return { id, type, position: { x: 0, y: 0 }, data: { blockType, props } } as Node;
}
function edge(id: string, source: string, target: string, sourceHandle?: string): Edge {
  return { id, source, target, ...(sourceHandle ? { sourceHandle } : {}) } as Edge;
}
function propsOf(n: Node | undefined): Record<string, unknown> {
  return ((n?.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};
}

// Faithful import shape: top-level outer IF; THEN = [a, b, innerIf]; innerIf is a nested container
// that is LAST in the branch (so its `continue` handle is free); plus an outer ELSE child. The
// fresh `print` block is on the canvas but not yet wired.
function importedNestedGraph(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    node('__start__', 'start', '_start', {}),
    node('outerIf', 'block', 'if', { _stepPath: 'steps/0' }),
    node('a', 'block', 'send', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/0', _branchLabel: 'then', _depth: 0 }),
    node('b', 'block', 'send', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/1', _branchLabel: 'then', _depth: 0 }),
    node('innerIf', 'block', 'if', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/2', _branchLabel: 'then', _depth: 0 }),
    node('innerThen', 'block', 'send', { _isChildOf: 'innerIf', _stepPath: 'steps/0/then/2/then/0', _branchLabel: 'then', _depth: 1 }),
    node('elseP', 'block', 'print', { _isChildOf: 'outerIf', _stepPath: 'steps/0/else/0', _branchLabel: 'else', _depth: 0 }),
    node('print', 'block', 'print', {}),
  ];
  const edges: Edge[] = [
    edge('e0', '__start__', 'outerIf'),
    edge('e1', 'outerIf', 'a'), // then entry (empty handle)
    edge('e2', 'a', 'b'),
    edge('e3', 'b', 'innerIf'),
    edge('e4', 'innerIf', 'innerThen'), // inner then entry
    edge('e5', 'outerIf', 'elseP', 'false'),
  ];
  return { nodes, edges };
}

const continueConn = (source: string, target: string): Connection =>
  ({ source, target, sourceHandle: 'continue', targetHandle: null });
const bottomConn = (source: string, target: string): Connection =>
  ({ source, target, sourceHandle: null, targetHandle: null });

describe('onConnect → continue handle confers band membership', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
    vi.clearAllMocks();
  });

  it('nests a continue-connected fresh block inside the inner container\'s parent branch after Layout', () => {
    const { nodes, edges } = importedNestedGraph();
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    useFlowStore.getState().onConnect(continueConn('innerIf', 'print'));
    const st = useFlowStore.getState();

    // 1. membership metadata is written (the source of truth)
    const print = st.nodes.find((n) => n.id === 'print');
    expect(propsOf(print)._isChildOf).toBe('outerIf');
    expect(propsOf(print)._stepPath).toBe('steps/0/then/3');
    // ancestor flagged so YAML export regenerates the container WITH the new child
    expect(propsOf(st.nodes.find((n) => n.id === 'outerIf'))._forceGraphExport).toBe(true);

    // 2. layout no longer orphans it — it's an ordered child of the THEN branch, not on the spine
    const tree = buildLayoutTree(st.nodes, st.edges);
    expect(tree.spine.map((n) => n.id)).not.toContain('print');
    const outer = tree.spine.find((n) => n.id === 'outerIf')!;
    const thenBranch = outer.branches.find((b) => b.scope === 'then')!;
    expect(thenBranch.children.map((c) => c.id)).toEqual(['a', 'b', 'innerIf', 'print']);

    // 3. after positioning, print is the bottom-most node of the THEN branch and the THEN band's box
    // grows down to wrap it — which only holds if print is a member of the band group (it was the
    // orphan that escaped the band before the fix).
    const laid = computeHierarchicalLayout(st.nodes, st.edges, DEFAULT_BLOCK_SIZING);
    const lp = laid.find((n) => n.id === 'print')!;
    const thenMaxY = Math.max(
      ...['a', 'b', 'innerIf', 'innerThen', 'print'].map((id) => laid.find((n) => n.id === id)!.position.y),
    );
    expect(lp.position.y).toBe(thenMaxY); // print sits at the foot of the branch

    const band = computeBranchBands(laid).find((b) => b.id === 'outerIf::then')!;
    expect(band).toBeTruthy();
    expect(band.x).toBeLessThanOrEqual(lp.position.x);
    expect(band.x + band.width).toBeGreaterThanOrEqual(lp.position.x + 300 /* CHILD_WIDTH */);
    expect(band.y + band.height).toBeGreaterThanOrEqual(lp.position.y + 52 /* COLLAPSED_HEIGHT */);
  });

  it('leaves a top-level container continuation to the spine walk (writes no membership)', () => {
    const nodes: Node[] = [
      node('__start__', 'start', '_start', {}),
      node('outerIf', 'block', 'if', { _stepPath: 'steps/0' }),
      node('tail', 'block', 'print', {}),
    ];
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges([edge('e0', '__start__', 'outerIf')]);

    useFlowStore.getState().onConnect(continueConn('outerIf', 'tail'));
    const st = useFlowStore.getState();

    expect(propsOf(st.nodes.find((n) => n.id === 'tail'))._isChildOf).toBeUndefined();
    expect(buildLayoutTree(st.nodes, st.edges).spine.map((n) => n.id)).toEqual(['outerIf', 'tail']);
  });

  it('reverts the membership write on undo', () => {
    const { nodes, edges } = importedNestedGraph();
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    useFlowStore.getState().onConnect(continueConn('innerIf', 'print'));
    expect(propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'print'))._isChildOf).toBe('outerIf');

    useFlowStore.getState().undo();
    const print = useFlowStore.getState().nodes.find((n) => n.id === 'print');
    expect(propsOf(print)._isChildOf).toBeUndefined();
    expect(propsOf(print)._stepPath).toBeUndefined();
    expect(useFlowStore.getState().edges.some((e) => e.target === 'print')).toBe(false);
  });

  it('nests a fresh block wired to a leaf\'s bottom handle as the next step in that branch', () => {
    const { nodes, edges } = importedNestedGraph();
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    // innerThen is the last leaf of the inner IF's then branch — its bottom handle is free.
    useFlowStore.getState().onConnect(bottomConn('innerThen', 'print'));
    const st = useFlowStore.getState();

    expect(propsOf(st.nodes.find((n) => n.id === 'print'))._isChildOf).toBe('innerIf');
    expect(propsOf(st.nodes.find((n) => n.id === 'print'))._stepPath).toBe('steps/0/then/2/then/1');

    const tree = buildLayoutTree(st.nodes, st.edges);
    expect(tree.spine.map((n) => n.id)).not.toContain('print');
    const inner = tree.spine
      .find((n) => n.id === 'outerIf')!
      .branches.find((b) => b.scope === 'then')!
      .children.find((c) => c.id === 'innerIf')!;
    expect(inner.branches.find((b) => b.scope === 'then')!.children.map((c) => c.id)).toEqual(['innerThen', 'print']);
  });

  it('releases the block back to the spine when its conferring wire is deleted', () => {
    const { nodes, edges } = importedNestedGraph();
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    useFlowStore.getState().onConnect(continueConn('innerIf', 'print'));
    const wired = useFlowStore.getState();
    expect(propsOf(wired.nodes.find((n) => n.id === 'print'))._isChildOf).toBe('outerIf');
    const edgeId = wired.edges.find((e) => e.target === 'print')!.id;

    useFlowStore.getState().removeEdges([edgeId]);
    const st = useFlowStore.getState();
    const print = st.nodes.find((n) => n.id === 'print');
    expect(propsOf(print)._isChildOf).toBeUndefined();
    expect(propsOf(print)._stepPath).toBeUndefined();
    // back to a spine orphan, no longer a then-branch child
    expect(buildLayoutTree(st.nodes, st.edges).spine.map((n) => n.id)).toContain('print');
  });

  it('does not strip membership from an imported child when an unrelated wire is deleted', () => {
    const { nodes, edges } = importedNestedGraph();
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    // delete the imported then-entry edge (outerIf → a); 'a' is imported, must keep its membership
    useFlowStore.getState().removeEdges(['e1']);
    const a = useFlowStore.getState().nodes.find((n) => n.id === 'a');
    expect(propsOf(a)._isChildOf).toBe('outerIf');
    expect(propsOf(a)._stepPath).toBe('steps/0/then/0');
  });
});
