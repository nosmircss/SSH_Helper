import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Node } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';

function node(id: string, x: number, y: number): Node {
  return { id, type: 'block', position: { x, y }, data: { props: {} } } as Node;
}

describe('translateNodesBy', () => {
  beforeEach(() => {
    useFlowStore.setState({
      nodes: [node('a', 100, 100), node('b', 200, 300), node('c', 500, 500)],
      edges: [],
    });
  });

  it('shifts only the targeted ids by the delta and leaves others untouched', () => {
    useFlowStore.getState().translateNodesBy(['a', 'b'], 25, -10);
    const byId = Object.fromEntries(useFlowStore.getState().nodes.map((n) => [n.id, n.position]));
    expect(byId['a']).toEqual({ x: 125, y: 90 });
    expect(byId['b']).toEqual({ x: 225, y: 290 });
    expect(byId['c']).toEqual({ x: 500, y: 500 });
  });

  it('is a no-op for ids not present in the graph', () => {
    useFlowStore.getState().translateNodesBy(['missing'], 50, 50);
    const byId = Object.fromEntries(useFlowStore.getState().nodes.map((n) => [n.id, n.position]));
    expect(byId['a']).toEqual({ x: 100, y: 100 });
  });
});
