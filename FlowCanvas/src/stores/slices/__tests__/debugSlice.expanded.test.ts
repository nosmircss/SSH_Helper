import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { sendLayoutAutosave } from '../../../utils/layoutAutosave';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../../../utils/layout/hierarchicalLayout';

describe('expandedNodes', () => {
  beforeEach(() => { useFlowStore.setState({ expandedNodes: new Set() }); vi.clearAllMocks(); });

  it('toggleExpanded adds/removes and reports via isExpanded', () => {
    const s = useFlowStore.getState();
    s.toggleExpanded('n1');
    expect(useFlowStore.getState().isExpanded('n1')).toBe(true);
    s.toggleExpanded('n1');
    expect(useFlowStore.getState().isExpanded('n1')).toBe(false);
  });

  it('toggleExpanded persists via sendLayoutAutosave', () => {
    useFlowStore.getState().toggleExpanded('n1');
    expect(sendLayoutAutosave).toHaveBeenCalled();
  });

  it('restoreExpandedNodes replaces the set', () => {
    useFlowStore.getState().restoreExpandedNodes(['a', 'b']);
    expect(useFlowStore.getState().isExpanded('a')).toBe(true);
    expect(useFlowStore.getState().expandedNodes.size).toBe(2);
  });
});

describe('toggleExpanded reflow (Option A)', () => {
  it('reflows so an expanded block pushes its successor further down', () => {
    const nodes = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', props: { command: 'a', capture: 'b' } } },
      { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'x' } } },
    ];
    const edges = [
      { id: 'e0', source: '__start__', target: 'A' },
      { id: 'e1', source: 'A', target: 'B' },
    ];
    useFlowStore.setState({ expandedNodes: new Set() });
    useFlowStore.getState().setNodes(nodes as never);
    useFlowStore.getState().setEdges(edges as never);
    // baseline layout with A collapsed
    const s0 = useFlowStore.getState();
    s0.setNodes(computeHierarchicalLayout(s0.nodes, s0.edges, DEFAULT_BLOCK_SIZING));
    const beforeB = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    // expanding A must reflow and push B further down
    useFlowStore.getState().toggleExpanded('A');
    const afterB = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    expect(afterB).toBeGreaterThan(beforeB);
  });
});

describe('setAllExpanded', () => {
  const buildGraph = () => {
    const nodes = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', props: { command: 'a', capture: 'b' } } },
      { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'x' } } },
      { id: 'C', type: 'comment', position: { x: 0, y: 0 }, data: { text: 'note' } },
    ];
    const edges = [
      { id: 'e0', source: '__start__', target: 'A' },
      { id: 'e1', source: 'A', target: 'B' },
    ];
    useFlowStore.setState({ expandedNodes: new Set() });
    useFlowStore.getState().setNodes(nodes as never);
    useFlowStore.getState().setEdges(edges as never);
  };

  it('expands every block node and leaves start/comment untouched', () => {
    buildGraph();
    useFlowStore.getState().setAllExpanded(true);
    const s = useFlowStore.getState();
    expect(s.isExpanded('A')).toBe(true);
    expect(s.isExpanded('B')).toBe(true);
    expect(s.isExpanded('__start__')).toBe(false);
    expect(s.isExpanded('C')).toBe(false);
    expect(s.expandedNodes.size).toBe(2);
    const byId = new Map(s.nodes.map((n) => [n.id, n]));
    expect((byId.get('A')!.data as Record<string, unknown>).expanded).toBe(true);
    expect((byId.get('B')!.data as Record<string, unknown>).expanded).toBe(true);
    expect((byId.get('__start__')!.data as Record<string, unknown>).expanded).toBeUndefined();
    expect((byId.get('C')!.data as Record<string, unknown>).expanded).toBeUndefined();
  });

  it('collapses every block node', () => {
    buildGraph();
    useFlowStore.getState().setAllExpanded(true);
    useFlowStore.getState().setAllExpanded(false);
    const s = useFlowStore.getState();
    expect(s.expandedNodes.size).toBe(0);
    const byId = new Map(s.nodes.map((n) => [n.id, n]));
    expect((byId.get('A')!.data as Record<string, unknown>).expanded).toBe(false);
    expect((byId.get('B')!.data as Record<string, unknown>).expanded).toBe(false);
  });

  it('persists via sendLayoutAutosave', () => {
    buildGraph();
    vi.clearAllMocks();
    useFlowStore.getState().setAllExpanded(true);
    expect(sendLayoutAutosave).toHaveBeenCalled();
  });

  it('reflows so expanding all pushes a downstream block lower', () => {
    buildGraph();
    const s0 = useFlowStore.getState();
    s0.setNodes(computeHierarchicalLayout(s0.nodes, s0.edges, DEFAULT_BLOCK_SIZING));
    const beforeB = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    useFlowStore.getState().setAllExpanded(true);
    const afterB = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    expect(afterB).toBeGreaterThan(beforeB);
  });
});
