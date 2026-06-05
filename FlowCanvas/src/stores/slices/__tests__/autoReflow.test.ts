import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
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

describe('autoReflow setting', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], expandedNodes: new Set(), autoReflowEnabled: true });
    vi.clearAllMocks();
  });

  it('toggleAutoReflow flips the flag', () => {
    useFlowStore.getState().toggleAutoReflow();
    expect(useFlowStore.getState().autoReflowEnabled).toBe(false);
    useFlowStore.getState().toggleAutoReflow();
    expect(useFlowStore.getState().autoReflowEnabled).toBe(true);
  });

  it('with auto-reflow ON, expanding a block pushes its successor down', () => {
    seed();
    const before = yPos('B');
    useFlowStore.getState().toggleExpanded('A');
    expect(yPos('B')).toBeGreaterThan(before);
  });

  it('with auto-reflow OFF, expanding a block does NOT move its successor (layout frozen)', () => {
    seed();
    useFlowStore.setState({ autoReflowEnabled: false });
    const before = yPos('B');
    useFlowStore.getState().toggleExpanded('A');
    expect(yPos('B')).toBe(before);
  });

  it('with auto-reflow OFF, adding an anchored comment does not move blocks', () => {
    seed();
    useFlowStore.setState({ autoReflowEnabled: false });
    const before = yPos('B');
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'A', 'comment');
    expect(yPos('B')).toBe(before);
    // the comment still got anchored above its block (placeAnchoredComments ran)
    const c = useFlowStore.getState().nodes.find((n) => n.type === 'comment')!;
    expect(c.position.x).toBe(useFlowStore.getState().nodes.find((n) => n.id === 'A')!.position.x);
  });
});
