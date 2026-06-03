import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { sendLayoutAutosave } from '../../../utils/layoutAutosave';
import { computeHierarchicalLayout } from '../../../utils/layout/hierarchicalLayout';

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
    s0.setNodes(computeHierarchicalLayout(s0.nodes, s0.edges));
    const beforeB = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    // expanding A must reflow and push B further down
    useFlowStore.getState().toggleExpanded('A');
    const afterB = useFlowStore.getState().nodes.find((n) => n.id === 'B')!.position.y;
    expect(afterB).toBeGreaterThan(beforeB);
  });
});
