import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Connection, Edge, Node } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';
import { isConnectionAllowed } from '../../../utils/connectionRules';
import { MEMBERSHIP_MARKER } from '../../../utils/childMembership';
import { computeBranchBands } from '../../../utils/branchBands';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../../../utils/layout/hierarchicalLayout';

// Faithful reproduction of the user's bug report (branch 0.51.23), modelled on the REAL syslog preset
// (SSH_Helper.Tests/Services/FlowCanvasForceRegenParityTests.cs): the IF (`port != "514"`) is DEEPLY
// NESTED — inside an outer IF, inside a foreach — and its `then` branch holds a single SEND ("y").
//
//   foreach (steps/1)
//     do
//       print                           steps/1/do/0
//       if syslog_status=="enable"       steps/1/do/1                 [outer IF, "OIF"]
//         then
//           send "set status disable"     steps/1/do/1/then/0          ["d0"]
//           IF  port != "514"             steps/1/do/1/then/1          ← the IF the user edits
//             then
//               send "y"                  steps/1/do/1/then/1/then/0   ["y"]
//
// The IF is the LAST then-child of the outer IF, so its `continue` diamond is FREE and sits at the
// bottom-CENTER, directly adjacent to the THEN/body handle (bottom, left:75%) — see BaseBlock.tsx.
//
// ROOT CAUSE (verified by unit + real-browser drag): dragging from the IF's bottom resolves to the
// `continue` handle, which makes the new block a SIBLING of the IF in the OUTER branch
// (deriveChildMembership gesture 1 → _isChildOf = OIF). The user then wires end→y to pull y under
// end, but `y` keeps its imported `_isChildOf: IF` because deriveChildMembership refused to re-home a
// block that "already has membership" — even though y's branch-entry edge (IF→y) was deleted, making
// it an orphan. So the IF::then band still wraps only y, and end dangles outside it.
//
// FIX: re-home an ORPHANED member (no incoming edge → its entry edge was deleted) when it is rewired.

function node(id: string, blockType: string, props: Record<string, unknown>): Node {
  return { id, type: blockType === '_start' ? 'start' : 'block', position: { x: 0, y: 0 }, data: { blockType, props } } as Node;
}
function importedEdge(id: string, source: string, target: string, opts: { sourceHandle?: string; label?: string } = {}): Edge {
  // Imported edges carry branch identity as a LABEL only — never data.branchPath.
  return {
    id, source, target,
    ...(opts.sourceHandle ? { sourceHandle: opts.sourceHandle } : {}),
    ...(opts.label ? { label: opts.label } : {}),
  } as Edge;
}
const propsOf = (n: Node | undefined) => ((n?.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};
const thenConn = (s: string, t: string): Connection => ({ source: s, target: t, sourceHandle: null, targetHandle: null });
const continueConn = (s: string, t: string): Connection => ({ source: s, target: t, sourceHandle: 'continue', targetHandle: null });

function setupImported(): void {
  const nodes: Node[] = [
    node('__start__', '_start', {}),
    node('M', 'multiselect', { _stepPath: 'steps/0' }),
    node('F', 'foreach', { _stepPath: 'steps/1' }),
    node('p', 'print', { _isChildOf: 'F', _stepPath: 'steps/1/do/0', _branchLabel: 'loop', _depth: 1 }),
    node('OIF', 'if', { _isChildOf: 'F', _stepPath: 'steps/1/do/1', _branchLabel: 'loop', _depth: 1 }),
    node('d0', 'send', { _isChildOf: 'OIF', _stepPath: 'steps/1/do/1/then/0', _branchLabel: 'then', _depth: 2 }),
    node('IF', 'if', { _isChildOf: 'OIF', _stepPath: 'steps/1/do/1/then/1', _branchLabel: 'then', _depth: 2, _preview: 'port != "514"' }),
    node('y', 'send', { _isChildOf: 'IF', _stepPath: 'steps/1/do/1/then/1/then/0', _branchLabel: 'then', _depth: 3 }),
  ];
  const edges: Edge[] = [
    importedEdge('e-start', '__start__', 'M'),
    importedEdge('e-mf', 'M', 'F'),
    importedEdge('e-loop', 'F', 'p'),
    importedEdge('e-p-oif', 'p', 'OIF'),
    importedEdge('e-oif-then', 'OIF', 'd0', { label: 'then' }),
    importedEdge('e-d0-if', 'd0', 'IF'),
    importedEdge('e-then', 'IF', 'y', { label: 'then' }), // THEN entry: sourceHandle null, label "then"
  ];
  useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
  useFlowStore.getState().setNodes(nodes);
  useFlowStore.getState().setEdges(edges);
}

// Band-to-band fixture (the user's clarified sequence): TWO top-level imported IFs. `end` is an
// imported member of IF-A's then band (with a sibling s2 AFTER it); `y` is the imported only child
// of IF-B's then band. Faithful imported edges: then entries are sourceHandle:null + label "then"
// (no data.branchPath); IF-A's continuation to IF-B leaves the `continue` handle labelled "next".
function setupTwoBands(): void {
  const nodes: Node[] = [
    node('__start__', '_start', {}),
    node('IF-A', 'if', { _stepPath: 'steps/0' }),
    node('end', 'send', { _isChildOf: 'IF-A', _stepPath: 'steps/0/then/0', _branchLabel: 'then', _branchColor: '#abc', _depth: 1 }),
    node('s2', 'send', { _isChildOf: 'IF-A', _stepPath: 'steps/0/then/1', _branchLabel: 'then', _branchColor: '#abc', _depth: 1 }),
    node('IF-B', 'if', { _stepPath: 'steps/1' }),
    node('y', 'send', { _isChildOf: 'IF-B', _stepPath: 'steps/1/then/0', _branchLabel: 'then', _branchColor: '#abc', _depth: 1 }),
  ];
  const edges: Edge[] = [
    importedEdge('e-start', '__start__', 'IF-A'),
    importedEdge('e-a-end', 'IF-A', 'end', { label: 'then' }),
    importedEdge('e-end-s2', 'end', 's2'),
    importedEdge('e-a-cont', 'IF-A', 'IF-B', { sourceHandle: 'continue', label: 'next' }),
    importedEdge('e-b-then', 'IF-B', 'y', { label: 'then' }),
  ];
  useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
  useFlowStore.getState().setNodes(nodes);
  useFlowStore.getState().setEdges(edges);
}

function bandMembersOf(id: string): string[] {
  const st = useFlowStore.getState();
  const laid = computeHierarchicalLayout(st.nodes, st.edges, DEFAULT_BLOCK_SIZING);
  return computeBranchBands(laid).find((b) => b.id === id)?.memberIds ?? [];
}

describe('rewiring a fresh block into a nested imported branch confers band membership', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
    vi.clearAllMocks();
  });

  // The user's actual gesture: grabbed the IF's continue diamond (overlaps the then handle) so `end`
  // landed as the IF's outer sibling. Then wired end→y to pull y down under end. Before the fix, y
  // stayed an orphan in the (now edge-less) IF::then band — the band tightly wrapped only y.
  it('continue-handle gesture then end→y re-homes the orphaned y under end (the reported bug)', () => {
    setupImported();
    // 1. delete the IF→y wire
    useFlowStore.getState().removeEdges(['e-then']);
    // 2. drop fresh 'end'
    useFlowStore.getState().addNode(node('end', 'send', {}));
    // 3. wire IF (continue handle) → end  → end becomes the IF's OUTER sibling (OIF::then/2)
    const v1 = useFlowStore.getState();
    expect(isConnectionAllowed(continueConn('IF', 'end'), v1.nodes, v1.edges).ok).toBe(true);
    useFlowStore.getState().onConnect(continueConn('IF', 'end'));
    expect(propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'end'))._isChildOf).toBe('OIF');

    // 4. wire end (bottom) → y. y was orphaned by step 1, so it re-homes as end's successor.
    const v2 = useFlowStore.getState();
    expect(isConnectionAllowed(thenConn('end', 'y'), v2.nodes, v2.edges).ok).toBe(true);
    useFlowStore.getState().onConnect(thenConn('end', 'y'));

    const st = useFlowStore.getState();
    const y = st.nodes.find((n) => n.id === 'y');
    const end = st.nodes.find((n) => n.id === 'end');
    // y now follows end in the OUTER branch (no longer stranded in the IF's empty then band).
    expect(propsOf(y)._isChildOf).toBe('OIF');
    expect(propsOf(y)._stepPath).toBe('steps/1/do/1/then/3'); // end is then/2, y its successor then/3
    // end + y share the same band; neither is orphaned.
    expect(bandMembersOf('OIF::then')).toEqual(expect.arrayContaining(['end', 'y']));
    // the now-empty IF::then band must not exist (or at least not wrap y).
    expect(bandMembersOf('IF::then')).not.toContain('y');
    // ancestor flagged for graph re-export.
    expect(propsOf(end)._isChildOf).toBe('OIF');
    expect(propsOf(st.nodes.find((n) => n.id === 'OIF'))._forceGraphExport).toBe(true);
  });

  // Control: the INTENDED gesture (then/body handle) already worked — end joins the IF's then band as
  // its first child and y shifts down. Asserting it stays correct after the orphan-rehome change.
  it('then-handle gesture: end joins the IF then band, y bumped (unchanged, both orders)', () => {
    for (const order of ['3-4', '4-3'] as const) {
      setupImported();
      useFlowStore.getState().removeEdges(['e-then']);
      useFlowStore.getState().addNode(node('end', 'send', {}));
      if (order === '3-4') {
        useFlowStore.getState().onConnect(thenConn('IF', 'end'));
        useFlowStore.getState().onConnect(thenConn('end', 'y'));
      } else {
        useFlowStore.getState().onConnect(thenConn('end', 'y'));
        useFlowStore.getState().onConnect(thenConn('IF', 'end'));
      }
      const st = useFlowStore.getState();
      expect(propsOf(st.nodes.find((n) => n.id === 'end'))._isChildOf, order).toBe('IF');
      expect(propsOf(st.nodes.find((n) => n.id === 'end'))._stepPath, order).toBe('steps/1/do/1/then/1/then/0');
      expect(propsOf(st.nodes.find((n) => n.id === 'y'))._stepPath, order).toBe('steps/1/do/1/then/1/then/1');
      expect(bandMembersOf('IF::then')).toEqual(expect.arrayContaining(['y', 'end']));
    }
  });

  // The user's clarified real-world sequence: `end` was an IMPORTED member of then-band-A, they
  // unplugged all its wires (imported membership survives by design), then wired it into a DIFFERENT
  // then-band-B (gesture 3) above B's existing imported child y. The move must be complete on both
  // sides: end fully re-homed into B (y bumped), AND the vacated branch A renumbered (s2 compacts to
  // then/0 — a gap at 0 would desync the runtime step↔node map) with IF-A flagged for graph
  // re-export (its stale snippet still nests `end` inside then-A otherwise).
  it('band-to-band move: end re-homes into then-B, vacated then-A renumbers and re-exports', () => {
    setupTwoBands();
    // The user unplugged end completely (entry + successor) and cleared IF-B's then entry — fan-in
    // would reject end→y while IF-B→y still exists.
    useFlowStore.getState().removeEdges(['e-a-end', 'e-end-s2', 'e-b-then']);
    // imported membership survives wire deletion by design — end still claims band A here
    expect(propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'end'))._isChildOf).toBe('IF-A');

    useFlowStore.getState().onConnect(thenConn('IF-B', 'end')); // gesture 3 into band B
    useFlowStore.getState().onConnect(thenConn('end', 'y'));

    const st = useFlowStore.getState();
    const end = propsOf(st.nodes.find((n) => n.id === 'end'));
    // fully re-homed into band B
    expect(end._isChildOf).toBe('IF-B');
    expect(end._stepPath).toBe('steps/1/then/0');
    // no stale band-A cosmetics left behind
    expect(end._branchLabel).toBeUndefined();
    expect(end._branchColor).toBeUndefined();
    expect(end._depth).toBeUndefined();
    // y shifted down to make room for end
    expect(propsOf(st.nodes.find((n) => n.id === 'y'))._stepPath).toBe('steps/1/then/1');
    // CRITICAL — vacated-branch contiguity: s2 compacts to then/0, no gap where end used to be
    expect(propsOf(st.nodes.find((n) => n.id === 's2'))._stepPath).toBe('steps/0/then/0');
    // BOTH containers must regenerate on export: B gained a child, A lost one
    expect(propsOf(st.nodes.find((n) => n.id === 'IF-A'))._forceGraphExport).toBe(true);
    expect(propsOf(st.nodes.find((n) => n.id === 'IF-B'))._forceGraphExport).toBe(true);
    // band geometry agrees with the metadata
    expect(bandMembersOf('IF-B::then')).toEqual(expect.arrayContaining(['end', 'y']));
    expect(bandMembersOf('IF-A::then')).toEqual(['s2']);
  });

  // Gesture-3 re-home sanity: stale membership is FULLY overwritten (not merged around), and the
  // result is wire-authored (MEMBERSHIP_MARKER) so deleting the conferring wire releases the block
  // to an orphan instead of leaving it stuck in band B.
  it('gesture-3 re-home overwrites stale membership and is revertible via wire-delete', () => {
    setupTwoBands();
    useFlowStore.getState().removeEdges(['e-a-end', 'e-end-s2', 'e-b-then']);
    useFlowStore.getState().onConnect(thenConn('IF-B', 'end'));

    let end = propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'end'));
    expect(end._isChildOf).toBe('IF-B');           // was IF-A
    expect(end._stepPath).toBe('steps/1/then/0');  // was steps/0/then/0
    expect(end[MEMBERSHIP_MARKER]).toBe(true);     // now wire-authored

    // deleting the conferring wire releases end to an orphan (not stuck in band B)
    const wireId = useFlowStore.getState().edges.find((e) => e.source === 'IF-B' && e.target === 'end')!.id;
    useFlowStore.getState().removeEdges([wireId]);
    end = propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'end'));
    expect(end._isChildOf).toBeUndefined();
    expect(end._stepPath).toBeUndefined();
    expect(end[MEMBERSHIP_MARKER]).toBeUndefined();
    // and y (bumped to then/1 by the insert) compacts back to then/0 on the release
    expect(propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'y'))._stepPath).toBe('steps/1/then/0');
  });

  // Moving a CONTAINER between bands must carry its whole subtree. renumberStepPaths alone can't fix
  // the descendants: it matches a container's children via the container's CURRENT path (already the
  // new one), so a child still on the band-A prefix fails the startsWith and is left stranded —
  // desyncing the runtime step↔node map for the moved subtree AND colliding with band-A survivors
  // that renumber onto the stale prefix (stealing their step events). The prefix rewrite must happen
  // inside applyChildMembership, before the renumber pass.
  it('band-to-band move of a CONTAINER carries its subtree to the new branch prefix', () => {
    // IF-A.then = [IF-C (whose then holds k), s2]; IF-B.then = [y]. Faithful imported edges: C's
    // continuation to its next sibling s2 leaves C's `continue` handle (nested-container shape).
    const nodes: Node[] = [
      node('__start__', '_start', {}),
      node('IF-A', 'if', { _stepPath: 'steps/0' }),
      node('C', 'if', { _isChildOf: 'IF-A', _stepPath: 'steps/0/then/0', _branchLabel: 'then', _depth: 1 }),
      node('k', 'send', { _isChildOf: 'C', _stepPath: 'steps/0/then/0/then/0', _branchLabel: 'then', _depth: 2 }),
      node('s2', 'send', { _isChildOf: 'IF-A', _stepPath: 'steps/0/then/1', _branchLabel: 'then', _depth: 1 }),
      node('IF-B', 'if', { _stepPath: 'steps/1' }),
      node('y', 'send', { _isChildOf: 'IF-B', _stepPath: 'steps/1/then/0', _branchLabel: 'then', _depth: 1 }),
    ];
    const edges: Edge[] = [
      importedEdge('e-start', '__start__', 'IF-A'),
      importedEdge('e-a-c', 'IF-A', 'C', { label: 'then' }),
      importedEdge('e-c-k', 'C', 'k', { label: 'then' }),
      importedEdge('e-c-s2', 'C', 's2', { sourceHandle: 'continue', label: 'next' }),
      importedEdge('e-a-cont', 'IF-A', 'IF-B', { sourceHandle: 'continue', label: 'next' }),
      importedEdge('e-b-then', 'IF-B', 'y', { label: 'then' }),
    ];
    useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    // Unwire C (entry + its continuation; C→k stays — the subtree moves together), then wire it
    // into band B above y.
    useFlowStore.getState().removeEdges(['e-a-c', 'e-c-s2']);
    const v = useFlowStore.getState();
    expect(isConnectionAllowed(thenConn('IF-B', 'C'), v.nodes, v.edges).ok).toBe(true);
    useFlowStore.getState().onConnect(thenConn('IF-B', 'C'));

    const st = useFlowStore.getState();
    // the container re-homed…
    expect(propsOf(st.nodes.find((n) => n.id === 'C'))._isChildOf).toBe('IF-B');
    expect(propsOf(st.nodes.find((n) => n.id === 'C'))._stepPath).toBe('steps/1/then/0');
    // …AND its child rode along onto the new prefix (the stranding bug)
    expect(propsOf(st.nodes.find((n) => n.id === 'k'))._isChildOf).toBe('C');
    expect(propsOf(st.nodes.find((n) => n.id === 'k'))._stepPath).toBe('steps/1/then/0/then/0');
    // band B's existing child shifted down; band A's survivor compacted
    expect(propsOf(st.nodes.find((n) => n.id === 'y'))._stepPath).toBe('steps/1/then/1');
    expect(propsOf(st.nodes.find((n) => n.id === 's2'))._stepPath).toBe('steps/0/then/0');
    // both containers regenerate on export
    expect(propsOf(st.nodes.find((n) => n.id === 'IF-A'))._forceGraphExport).toBe(true);
    expect(propsOf(st.nodes.find((n) => n.id === 'IF-B'))._forceGraphExport).toBe(true);
    // band geometry: the moved subtree lives in band B now, nothing of it remains in band A
    expect(bandMembersOf('IF-B::then')).toEqual(expect.arrayContaining(['C', 'k', 'y']));
    expect(bandMembersOf('IF-A::then')).toEqual(['s2']);
    expect(bandMembersOf('C::then')).toEqual(['k']);
  });
});
