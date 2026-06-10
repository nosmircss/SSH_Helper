import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Connection, Edge, Node } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';
import { isConnectionAllowed } from '../../../utils/connectionRules';
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
});
