import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../../../utils/layout/hierarchicalLayout';

const yPos = (id: string) => useFlowStore.getState().nodes.find((n) => n.id === id)!.position.y;

function seed() {
  const nodes = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', props: { command: 'a', capture: 'b' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'x' } } },
  ];
  const edges = [{ id: 'e0', source: '__start__', target: 'A' }, { id: 'e1', source: 'A', target: 'B' }];
  useFlowStore.getState().setNodes(nodes as never);
  useFlowStore.getState().setEdges(edges as never);
  const s0 = useFlowStore.getState();
  s0.setNodes(computeHierarchicalLayout(s0.nodes, s0.edges, DEFAULT_BLOCK_SIZING));
}

describe('layout mode', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], expandedNodes: new Set(), layoutMode: 'auto' });
    vi.clearAllMocks();
  });

  it('setLayoutMode flips the active mode', () => {
    useFlowStore.getState().setLayoutMode('manual');
    expect(useFlowStore.getState().layoutMode).toBe('manual');
    useFlowStore.getState().setLayoutMode('auto');
    expect(useFlowStore.getState().layoutMode).toBe('auto');
  });

  it('in Auto-flow, expanding a block pushes its successor down', () => {
    seed();
    const before = yPos('B');
    useFlowStore.getState().toggleExpanded('A');
    expect(yPos('B')).toBeGreaterThan(before);
  });

  it('in Manual, expanding a block does NOT move its successor (layout frozen)', () => {
    seed();
    useFlowStore.setState({ layoutMode: 'manual' });
    const before = yPos('B');
    useFlowStore.getState().toggleExpanded('A');
    expect(yPos('B')).toBe(before);
  });

  it('in Manual, adding an anchored comment does not move blocks', () => {
    seed();
    useFlowStore.setState({ layoutMode: 'manual' });
    const before = yPos('B');
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'A', 'comment');
    expect(yPos('B')).toBe(before);
    const c = useFlowStore.getState().nodes.find((n) => n.type === 'comment')!;
    expect(c.position.x).toBe(useFlowStore.getState().nodes.find((n) => n.id === 'A')!.position.x);
  });
});
