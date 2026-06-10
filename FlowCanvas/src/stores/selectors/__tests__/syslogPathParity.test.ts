import { describe, it, expect } from 'vitest';
import { selectEdgePathStatus } from '../edgePath';
import graph from './__syslog_graph.json';

// Reproduces the user's "syslog restart" preset graph (imported foreach with a nested if).
// Simulates a FULLY SUCCESSFUL run — every node success, loop iterated, both ifs took 'then' —
// then asserts the post-run path overlay lights the WHOLE traversed chain. If the selector is
// correct, every spine/branch edge that was actually traversed must be 'on-path'.

type N = { id: string; blockType: string; _stepPath?: string | null; _isChildOf?: string | null };
type E = { id: string; source: string; target: string; sourceHandle?: string | null; branchPath?: string | null };

function buildState() {
  const nodes = (graph.nodes as N[]).map((n) => ({
    id: n.id,
    data: {
      blockType: n.blockType,
      props: {
        ...(n._stepPath ? { _stepPath: n._stepPath } : {}),
        ...(n._isChildOf ? { _isChildOf: n._isChildOf } : {}),
      },
    },
  }));
  const edges = (graph.edges as E[]).map((e) => ({
    id: e.id,
    source: e.source,
    target: e.target,
    sourceHandle: e.sourceHandle || undefined,
    data: e.branchPath ? { branchPath: e.branchPath } : {},
  }));

  const blockStates = new Map<string, string>();
  for (const n of nodes) {
    if (n.id === '__start__') continue;
    blockStates.set(n.id, 'success'); // full successful run
  }
  const loopIterations = new Map<string, number>();
  const branchTaken = new Map<string, string>();
  for (const n of graph.nodes as N[]) {
    if (n.blockType === 'foreach') loopIterations.set(n.id, 1);
    if (n.blockType === 'if') branchTaken.set(n.id, 'then'); // all ifs were True per the run log
  }
  // The top-level if (do/8) took 'then', so its 'else' arm (node-22) did NOT run.
  // Leave node-22 success=false? It never executed → idle.
  blockStates.delete((graph.nodes as N[]).find((n) => n._stepPath === 'steps/1/do/8/else/0')!.id);

  return { pathVisible: true, nodes, edges, blockStates, loopIterations, branchTaken } as any;
}

describe('syslog preset post-run path parity', () => {
  const state = buildState();
  const byId = new Map((graph.nodes as N[]).map((n) => [n.id, n]));
  const stepPathOf = (id: string) => byId.get(id)?._stepPath ?? id;

  // Every edge along the traversed chain (everything except the untaken else arm).
  const traversedEdges = (graph.edges as E[]).filter(
    (e) => !(e.sourceHandle === 'false'), // node-10 -> node-22 (else) was not taken
  );

  for (const e of traversedEdges) {
    it(`edge ${stepPathOf(e.source)} -> ${stepPathOf(e.target)} is on-path`, () => {
      expect(selectEdgePathStatus(state, e.id)).toBe('on-path');
    });
  }
});
