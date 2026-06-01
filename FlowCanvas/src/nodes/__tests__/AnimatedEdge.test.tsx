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
