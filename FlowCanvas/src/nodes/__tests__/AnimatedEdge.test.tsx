import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import React from 'react';
import type { Edge, Node } from '@xyflow/react';
import AnimatedEdge from '../AnimatedEdge';
import { useFlowStore } from '../../stores/useFlowStore';

// AnimatedEdge reads the real store and renders the real BaseEdge (a pure path — no
// ReactFlow context needed). We drive state via setState and read the rendered path.
// NOTE: jsdom does not reliably store `var(...)`/`color-mix(...)` on the SVG `stroke`
// property, so we assert the CLASS attribute (a plain DOM attribute — 100% reliable) here
// and verify the resolved stroke COLOR in the Playwright e2e (real Chromium).
const baseProps = {
  id: 'e1',
  sourceX: 0, sourceY: 0, targetX: 0, targetY: 100,
  sourcePosition: 'bottom', targetPosition: 'top',
  source: 'node-1', target: 'node-2',
  data: {},
  interactionWidth: 20,
} as any;

function setStore(partial: Record<string, unknown>) {
  useFlowStore.setState({
    pathVisible: true,
    isRunning: false,
    reducedMotion: false,
    blockStates: new Map(),
    branchTaken: new Map(),
    loopIterations: new Map(),
    nodes: [],
    edges: [],
    ...partial,
  } as any);
}

function renderEdge(props: any) {
  return render(React.createElement('svg', {}, React.createElement(AnimatedEdge, props)));
}

function pathClass(container: HTMLElement): string {
  return container.querySelector('.react-flow__edge-path')?.getAttribute('class') ?? '';
}

function hasPacket(container: HTMLElement): boolean {
  return container.querySelector('circle.fc-edge-packet') !== null;
}

describe('AnimatedEdge path overlay', () => {
  beforeEach(() => setStore({}));

  it('marks a traversed successor edge on-path', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, blockStates: new Map([['node-1', 'success']]) });

    const { container } = renderEdge({ ...baseProps, style: { stroke: 'var(--fc-edge-idle)' } });
    expect(pathClass(container)).toContain('fc-edge-onpath');
  });

  it('marks an on-path branch edge', () => {
    const edges: Edge[] = [
      { id: 'e1', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
    ];
    const nodes: Node[] = [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node];
    setStore({ edges, nodes, blockStates: new Map([['if-1', 'success']]), branchTaken: new Map([['if-1', 'then']]) });

    const { container } = renderEdge({ ...baseProps, source: 'if-1', target: 't', style: { stroke: 'var(--fc-branch-then)' } });
    expect(pathClass(container)).toContain('fc-edge-onpath');
  });

  it('fades an untaken branch edge', () => {
    const edges: Edge[] = [
      { id: 'e1', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
    ];
    const nodes: Node[] = [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node];
    setStore({ edges, nodes, blockStates: new Map([['if-1', 'success']]), branchTaken: new Map([['if-1', 'then']]) });

    const { container } = renderEdge({ ...baseProps, source: 'if-1', target: 'e', style: { stroke: 'var(--fc-branch-else)' } });
    expect(pathClass(container)).toContain('fc-edge-untaken');
  });

  it('leaves an idle edge unstyled when its source has not run', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, blockStates: new Map() });

    const { container } = renderEdge({ ...baseProps });
    expect(pathClass(container)).not.toContain('fc-edge-onpath');
    expect(pathClass(container)).not.toContain('fc-edge-untaken');
  });

  it('hides the path styling when pathVisible is false', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, blockStates: new Map([['node-1', 'success']]), pathVisible: false });

    const { container } = renderEdge({ ...baseProps, style: { stroke: 'var(--fc-edge-idle)' } });
    expect(pathClass(container)).not.toContain('fc-edge-onpath');
  });
});

describe('AnimatedEdge run packet', () => {
  beforeEach(() => setStore({}));

  const ifEdges: Edge[] = [
    { id: 'e1', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
    { id: 'e2', source: 'if-1', target: 'e', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
  ];
  const ifNodes: Node[] = [{ id: 'if-1', position: { x: 0, y: 0 }, data: { blockType: 'if' } } as Node];

  it('does not animate a packet on the arms of a running IF before a branch is taken', () => {
    // IF is mid-execution; neither branch child has started, so both stay idle.
    setStore({ edges: ifEdges, nodes: ifNodes, isRunning: true, blockStates: new Map([['if-1', 'running']]) });

    const thenArm = renderEdge({ ...baseProps, id: 'e1', source: 'if-1', target: 't' });
    const elseArm = renderEdge({ ...baseProps, id: 'e2', source: 'if-1', target: 'e' });
    expect(hasPacket(thenArm.container)).toBe(false);
    expect(hasPacket(elseArm.container)).toBe(false);
  });

  it('animates the packet only on the taken arm once its child block runs', () => {
    // THEN child has entered 'running'; the ELSE child is never visited and stays idle.
    setStore({ edges: ifEdges, nodes: ifNodes, isRunning: true, blockStates: new Map([['if-1', 'running'], ['t', 'running']]) });

    const thenArm = renderEdge({ ...baseProps, id: 'e1', source: 'if-1', target: 't' });
    const elseArm = renderEdge({ ...baseProps, id: 'e2', source: 'if-1', target: 'e' });
    expect(hasPacket(thenArm.container)).toBe(true);
    expect(hasPacket(elseArm.container)).toBe(false);
  });

  it('animates the packet on the single edge whose target is currently running (the live frontier)', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, isRunning: true, blockStates: new Map([['node-2', 'running']]) });

    const { container } = renderEdge({ ...baseProps });
    expect(hasPacket(container)).toBe(true);
  });

  it('does not leave a dot behind on a completed edge (the neon trail carries it, not the dot)', () => {
    // The frontier dot moves on; the edge it left is now a solid neon on-path wire, no dot.
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, isRunning: true, blockStates: new Map([['node-2', 'success']]) });

    const { container } = renderEdge({ ...baseProps });
    expect(hasPacket(container)).toBe(false);
  });

  it('does not put a dot on a skipped/disabled target (the neon overlay keeps that segment lit)', () => {
    // Disabled / when:-guard-skipped steps never enter 'running', so the frontier dot skips them;
    // the on-path neon (driven separately by selectEdgePathStatus) keeps the trail continuous.
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, isRunning: true, blockStates: new Map([['node-2', 'disabled']]) });

    const { container } = renderEdge({ ...baseProps });
    expect(hasPacket(container)).toBe(false);
  });

  it('does not animate a packet on an edge whose target has not been reached', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, isRunning: true, blockStates: new Map([['node-1', 'success']]) });

    const { container } = renderEdge({ ...baseProps });
    expect(hasPacket(container)).toBe(false);
  });

  it('never animates a packet when not running or under reduced motion', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];

    setStore({ edges, isRunning: false, blockStates: new Map([['node-2', 'running']]) });
    expect(hasPacket(renderEdge({ ...baseProps }).container)).toBe(false);

    setStore({ edges, isRunning: true, reducedMotion: true, blockStates: new Map([['node-2', 'running']]) });
    expect(hasPacket(renderEdge({ ...baseProps }).container)).toBe(false);
  });

  it('rides only the deepest running edge — a running container yields its dot to its running child', () => {
    const edges: Edge[] = [
      { id: 'into-if', source: 'p', target: 'if-1' } as Edge,
      { id: 'if-then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
    ];
    setStore({ edges, isRunning: true, blockStates: new Map([['if-1', 'running'], ['t', 'running']]) });

    const intoIf = renderEdge({ ...baseProps, id: 'into-if', source: 'p', target: 'if-1' });
    const ifThen = renderEdge({ ...baseProps, id: 'if-then', source: 'if-1', target: 't' });
    expect(hasPacket(intoIf.container)).toBe(false); // container yields its incoming dot
    expect(hasPacket(ifThen.container)).toBe(true);  // deepest running edge keeps the single dot
  });

  it('keeps the dot on a container incoming edge until a child actually starts running', () => {
    const edges: Edge[] = [
      { id: 'into-if', source: 'p', target: 'if-1' } as Edge,
      { id: 'if-then', source: 'if-1', target: 't', data: { branchPath: 'then' } } as Edge,
    ];
    // Container running, but no child has started yet — the dot belongs on the way in.
    setStore({ edges, isRunning: true, blockStates: new Map([['if-1', 'running']]) });

    const intoIf = renderEdge({ ...baseProps, id: 'into-if', source: 'p', target: 'if-1' });
    expect(hasPacket(intoIf.container)).toBe(true);
  });

  it('rides the deepest edge through multiple nesting levels', () => {
    // if-1 ▸ loop-1 ▸ send-1, all running. Only the innermost edge (loop-1 → send-1) carries the dot.
    const edges: Edge[] = [
      { id: 'into-if', source: 'p', target: 'if-1' } as Edge,
      { id: 'if-loop', source: 'if-1', target: 'loop-1', data: { branchPath: 'then' } } as Edge,
      { id: 'loop-send', source: 'loop-1', target: 'send-1', data: { branchPath: 'do' } } as Edge,
    ];
    setStore({
      edges, isRunning: true,
      blockStates: new Map([['if-1', 'running'], ['loop-1', 'running'], ['send-1', 'running']]),
    });
    expect(hasPacket(renderEdge({ ...baseProps, id: 'into-if', source: 'p', target: 'if-1' }).container)).toBe(false);
    expect(hasPacket(renderEdge({ ...baseProps, id: 'if-loop', source: 'if-1', target: 'loop-1' }).container)).toBe(false);
    expect(hasPacket(renderEdge({ ...baseProps, id: 'loop-send', source: 'loop-1', target: 'send-1' }).container)).toBe(true);
  });
});
