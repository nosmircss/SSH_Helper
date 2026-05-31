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
});
