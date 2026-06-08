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

  it('animates the packet on an edge once control reaches its target', () => {
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, isRunning: true, blockStates: new Map([['node-2', 'running']]) });

    const { container } = renderEdge({ ...baseProps });
    expect(hasPacket(container)).toBe(true);
  });

  it('keeps the trail continuous across a skipped/disabled target', () => {
    // Disabled and when:-guard-skipped steps emit no 'running' update — only a completion
    // with state skipped/disabled. The edge into them must still animate or the trail breaks.
    const edges: Edge[] = [{ id: 'e1', source: 'node-1', target: 'node-2' } as Edge];
    setStore({ edges, isRunning: true, blockStates: new Map([['node-2', 'disabled']]) });

    const { container } = renderEdge({ ...baseProps });
    expect(hasPacket(container)).toBe(true);
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
});
