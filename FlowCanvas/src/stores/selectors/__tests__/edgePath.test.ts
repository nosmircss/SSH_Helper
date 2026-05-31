import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { selectEdgePathStatus } from '../edgePath';
import { START_NODE_ID } from '../../slices/graphSlice';

// Minimal stand-in for the FlowStore: the selector only reads these fields.
function makeState(overrides: {
  pathVisible?: boolean;
  edges?: Edge[];
  nodes?: Node[];
  blockStates?: Map<string, string>;
  branchTaken?: Map<string, string>;
  loopIterations?: Map<string, number>;
}): any {
  return {
    pathVisible: overrides.pathVisible ?? true,
    edges: overrides.edges ?? [],
    nodes: overrides.nodes ?? [],
    blockStates: overrides.blockStates ?? new Map(),
    branchTaken: overrides.branchTaken ?? new Map(),
    loopIterations: overrides.loopIterations ?? new Map(),
  };
}

const ifNode: Node = { id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node;
const loopNode: Node = { id: 'loop-1', position: { x: 0, y: 0 }, data: { blockType: 'foreach' } } as Node;
const parallelNode: Node = { id: 'par-1', position: { x: 0, y: 0 }, data: { blockType: 'parallel' } } as Node;

describe('selectEdgePathStatus', () => {
  it('returns idle for every edge when pathVisible is false', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ pathVisible: false, edges, blockStates: new Map([['a', 'success']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('idle');
  });

  it('returns idle when the edge id is unknown', () => {
    expect(selectEdgePathStatus(makeState({}), 'nope')).toBe('idle');
  });

  it('returns idle when the source never ran', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    expect(selectEdgePathStatus(makeState({ edges }), 'e1')).toBe('idle');
  });

  it('marks a plain successor on-path when the source completed', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'success']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('on-path');
  });

  it('does NOT mark a successor on-path while the source is still running', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'running']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('idle');
  });

  it('does NOT mark a successor on-path when the source errored (trail halts)', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'error']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('idle');
  });

  it('treats a skipped (disabled) source as pass-through', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'a', target: 'b' } as Edge];
    const state = makeState({ edges, blockStates: new Map([['a', 'skipped']]) });
    expect(selectEdgePathStatus(state, 'e1')).toBe('on-path');
  });

  it('lights the taken if-branch and fades the untaken sibling', () => {
    const edges: Edge[] = [
      { id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
      { id: 'else', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
    ];
    const state = makeState({
      edges, nodes: [ifNode],
      blockStates: new Map([['if-1', 'success']]),
      branchTaken: new Map([['if-1', 'then']]),
    });
    expect(selectEdgePathStatus(state, 'then')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'else')).toBe('untaken');
  });

  it('matches the else branch by its branchPath value', () => {
    const edges: Edge[] = [
      { id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
      { id: 'else', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
    ];
    const state = makeState({
      edges, nodes: [ifNode],
      blockStates: new Map([['if-1', 'success']]),
      branchTaken: new Map([['if-1', 'else']]),
    });
    expect(selectEdgePathStatus(state, 'else')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'then')).toBe('untaken');
  });

  it('matches an indexed switch case', () => {
    const edges: Edge[] = [
      { id: 'c0', source: 'if-1', target: 'a', data: { branchPath: 'cases/0/do' } } as Edge,
      { id: 'c2', source: 'if-1', target: 'b', data: { branchPath: 'cases/2/do' } } as Edge,
    ];
    const state = makeState({
      edges, nodes: [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'switch' } } as Node],
      blockStates: new Map([['if-1', 'success']]),
      branchTaken: new Map([['if-1', 'cases/2/do']]),
    });
    expect(selectEdgePathStatus(state, 'c2')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'c0')).toBe('untaken');
  });

  it('returns idle for a branch edge when no branchTaken was recorded (does not guess)', () => {
    const edges: Edge[] = [{ id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge];
    const state = makeState({ edges, nodes: [ifNode], blockStates: new Map([['if-1', 'success']]) });
    expect(selectEdgePathStatus(state, 'then')).toBe('idle');
  });

  it('returns idle for branch edges when the conditional itself errored', () => {
    const edges: Edge[] = [{ id: 'then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge];
    const state = makeState({
      edges, nodes: [ifNode],
      blockStates: new Map([['if-1', 'error']]),
      branchTaken: new Map([['if-1', 'then']]),
    });
    expect(selectEdgePathStatus(state, 'then')).toBe('idle');
  });

  it('lights the loop body once the loop iterated, fades it otherwise', () => {
    const edges: Edge[] = [{ id: 'body', source: 'loop-1', target: 'x', data: { branchPath: 'do' } } as Edge];
    const ran = makeState({
      edges, nodes: [loopNode],
      blockStates: new Map([['loop-1', 'success']]),
      loopIterations: new Map([['loop-1', 3]]),
    });
    const zero = makeState({
      edges, nodes: [loopNode],
      blockStates: new Map([['loop-1', 'success']]),
      loopIterations: new Map([['loop-1', 0]]),
    });
    expect(selectEdgePathStatus(ran, 'body')).toBe('on-path');
    expect(selectEdgePathStatus(zero, 'body')).toBe('untaken');
  });

  it('lights every parallel branch (no untaken among them)', () => {
    const edges: Edge[] = [
      { id: 'p0', source: 'par-1', target: 'a', data: { branchPath: 'parallel/0' } } as Edge,
      { id: 'p1', source: 'par-1', target: 'b', data: { branchPath: 'parallel/1' } } as Edge,
    ];
    const state = makeState({ edges, nodes: [parallelNode], blockStates: new Map([['par-1', 'success']]) });
    expect(selectEdgePathStatus(state, 'p0')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'p1')).toBe('on-path');
  });

  it('treats a container continuation edge as a plain successor', () => {
    const edges: Edge[] = [{ id: 'cont', source: 'if-1', target: 'after', sourceHandle: 'continue' } as Edge];
    const state = makeState({ edges, nodes: [ifNode], blockStates: new Map([['if-1', 'success']]) });
    expect(selectEdgePathStatus(state, 'cont')).toBe('on-path');
  });

  it('lights the start edge once its target block has run', () => {
    const edges: Edge[] = [{ id: 's', source: START_NODE_ID, target: 'first' } as Edge];
    const ran = makeState({ edges, blockStates: new Map([['first', 'running']]) });
    const notRun = makeState({ edges });
    expect(selectEdgePathStatus(ran, 's')).toBe('on-path');
    expect(selectEdgePathStatus(notRun, 's')).toBe('idle');
  });

  // ── Imported presets: branch edges carry NO data.branchPath. Branch identity is on the
  //    target child's props._stepPath, with props._isChildOf === the container's id. ──
  const importedIf: Node = { id: 'iif', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _stepPath: 'steps/0' } } } as Node;
  function importedChild(id: string, stepPath: string, parent: string, blockType = 'print'): Node {
    return { id, position: { x: 0, y: 0 }, data: { blockType, props: { _stepPath: stepPath, _isChildOf: parent } } } as Node;
  }

  it('imported if: lights the taken then-branch and fades the else-branch via _stepPath', () => {
    const nodes = [importedIf, importedChild('t', 'steps/0/then/0', 'iif'), importedChild('e', 'steps/0/else/0', 'iif')];
    const edges: Edge[] = [
      { id: 'e-then', source: 'iif', target: 't', style: { stroke: 'green' } } as Edge,
      { id: 'e-else', source: 'iif', target: 'e', sourceHandle: 'false', style: { stroke: 'red' } } as Edge,
    ];
    const state = makeState({ nodes, edges, blockStates: new Map([['iif', 'success']]), branchTaken: new Map([['iif', 'then']]) });
    expect(selectEdgePathStatus(state, 'e-then')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'e-else')).toBe('untaken');
  });

  it('imported then-vs-elif: taken "then" does NOT match the elif child (_stepPath disambiguates)', () => {
    const nodes = [importedIf, importedChild('t', 'steps/0/then/0', 'iif'), importedChild('el', 'steps/0/elif/0/then/0', 'iif')];
    const edges: Edge[] = [
      { id: 'e-then', source: 'iif', target: 't' } as Edge,
      { id: 'e-elif', source: 'iif', target: 'el' } as Edge,
    ];
    const state = makeState({ nodes, edges, blockStates: new Map([['iif', 'success']]), branchTaken: new Map([['iif', 'then']]) });
    expect(selectEdgePathStatus(state, 'e-then')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'e-elif')).toBe('untaken');
  });

  it('imported switch: matches the taken case index and fades the others', () => {
    const sw: Node = { id: 'sw', position: { x: 0, y: 0 }, data: { blockType: 'switch', props: { _stepPath: 'steps/1' } } } as Node;
    const nodes = [sw, importedChild('c0', 'steps/1/cases/0/do/0', 'sw'), importedChild('c2', 'steps/1/cases/2/do/0', 'sw')];
    const edges: Edge[] = [
      { id: 'e-c0', source: 'sw', target: 'c0' } as Edge,
      { id: 'e-c2', source: 'sw', target: 'c2' } as Edge,
    ];
    const state = makeState({ nodes, edges, blockStates: new Map([['sw', 'success']]), branchTaken: new Map([['sw', 'cases/2/do']]) });
    expect(selectEdgePathStatus(state, 'e-c2')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'e-c0')).toBe('untaken');
  });

  it('imported loop: body edge (target is a child) lights when iterated, fades at zero', () => {
    const fe: Node = { id: 'fe', position: { x: 0, y: 0 }, data: { blockType: 'foreach', props: { _stepPath: 'steps/2' } } } as Node;
    const nodes = [fe, importedChild('b', 'steps/2/do/0', 'fe')];
    const edges: Edge[] = [{ id: 'e-body', source: 'fe', target: 'b' } as Edge];
    const ran = makeState({ nodes, edges, blockStates: new Map([['fe', 'success']]), loopIterations: new Map([['fe', 2]]) });
    const zero = makeState({ nodes, edges, blockStates: new Map([['fe', 'success']]), loopIterations: new Map([['fe', 0]]) });
    expect(selectEdgePathStatus(ran, 'e-body')).toBe('on-path');
    expect(selectEdgePathStatus(zero, 'e-body')).toBe('untaken');
  });

  it('imported within-branch child→child edge is a plain successor, not a branch', () => {
    const nodes = [importedIf, importedChild('t1', 'steps/0/then/0', 'iif'), importedChild('t2', 'steps/0/then/1', 'iif')];
    const edges: Edge[] = [{ id: 'e-t1-t2', source: 't1', target: 't2' } as Edge];
    const state = makeState({ nodes, edges, blockStates: new Map([['t1', 'success']]) });
    expect(selectEdgePathStatus(state, 'e-t1-t2')).toBe('on-path');
  });

  it('imported container continuation (target is not a child) is a plain successor', () => {
    const after: Node = { id: 'after', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/1' } } } as Node;
    const nodes = [importedIf, after];
    const edges: Edge[] = [{ id: 'e-cont', source: 'iif', target: 'after' } as Edge];
    const state = makeState({ nodes, edges, blockStates: new Map([['iif', 'success']]) });
    expect(selectEdgePathStatus(state, 'e-cont')).toBe('on-path');
  });

  it('imported if without _stepPath falls back to sourceHandle === "false" for the else edge', () => {
    const containerNode: Node = { id: 'iif-bare', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node;
    const childNode: Node = { id: 'e-bare', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'iif-bare' } } } as Node;
    const edges: Edge[] = [
      { id: 'e-else-bare', source: 'iif-bare', target: 'e-bare', sourceHandle: 'false' } as Edge,
    ];
    const state = makeState({ nodes: [containerNode, childNode], edges, blockStates: new Map([['iif-bare', 'success']]), branchTaken: new Map([['iif-bare', 'else']]) });
    expect(selectEdgePathStatus(state, 'e-else-bare')).toBe('on-path');
  });

  it('imported parallel: every branch edge lights (no untaken among them)', () => {
    const par: Node = { id: 'par', position: { x: 0, y: 0 }, data: { blockType: 'parallel', props: { _stepPath: 'steps/3' } } } as Node;
    const nodes = [par, importedChild('p0', 'steps/3/parallel/0/0', 'par'), importedChild('p1', 'steps/3/parallel/1/0', 'par')];
    const edges: Edge[] = [
      { id: 'e-p0', source: 'par', target: 'p0' } as Edge,
      { id: 'e-p1', source: 'par', target: 'p1' } as Edge,
    ];
    // No branchTaken — parallel runs all branches; the blockType short-circuit covers it.
    const state = makeState({ nodes, edges, blockStates: new Map([['par', 'success']]) });
    expect(selectEdgePathStatus(state, 'e-p0')).toBe('on-path');
    expect(selectEdgePathStatus(state, 'e-p1')).toBe('on-path');
  });
});
