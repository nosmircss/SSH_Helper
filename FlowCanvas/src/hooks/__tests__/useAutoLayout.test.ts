import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { Edge, Node } from '@xyflow/react';
import { useFlowStore } from '../../stores/useFlowStore';
import { useAutoLayout } from '../useAutoLayout';
import { computeBranchBands } from '../../utils/branchBands';
import { BLOCK_WIDTH_INSET } from '../../utils/nodeSize';

vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => ({
  messageBus: { send: vi.fn(), on: vi.fn(() => () => {}) },
  CANVAS_HOST_MESSAGES: { outgoing: {}, incoming: {} },
}));

function ifElseGraph(): { nodes: Node[]; edges: Edge[] } {
  const nodes = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _stepPath: 'steps/0', condition: 'x' } } },
    { id: 'then-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then', message: 't' } } },
    { id: 'else-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else', message: 'e' } } },
  ] as never;
  const edges = [
    { id: 'e0', source: '__start__', target: 'if-1' },
    { id: 'e1', source: 'if-1', target: 'then-1' },
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false' },
  ] as never;
  return { nodes, edges };
}

describe('useAutoLayout honors current Display Settings (sizing)', () => {
  beforeEach(() => {
    useFlowStore.setState({ blockWidth: 330, density: 1, textScale: 1 });
  });

  it('lays out at the current Max block width so then/else branch bands do not overlap', () => {
    const { nodes, edges } = ifElseGraph();
    useFlowStore.setState({ blockWidth: 700, density: 1.2, textScale: 1.15 });
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    const { result } = renderHook(() => useAutoLayout());
    act(() => result.current());

    const placed = useFlowStore.getState().nodes;
    const bands = computeBranchBands(placed, 700 - BLOCK_WIDTH_INSET);
    const thenB = bands.find((b) => b.parentId === 'if-1' && b.branchKey === 'then');
    const elseB = bands.find((b) => b.parentId === 'if-1' && b.branchKey === 'else');

    expect(thenB).toBeDefined();
    expect(elseB).toBeDefined();
    // ELSE band must start at or right of the THEN band's right edge — no overlap.
    expect(elseB!.x).toBeGreaterThanOrEqual(thenB!.x + thenB!.width);
  });

  it('preserves Roomy vertical spacing (does not collapse to default density)', () => {
    const { nodes, edges } = ifElseGraph();
    useFlowStore.setState({ blockWidth: 330, density: 1, textScale: 1 });
    useFlowStore.getState().setNodes(nodes);
    useFlowStore.getState().setEdges(edges);

    const { result } = renderHook(() => useAutoLayout());
    act(() => result.current());
    const normalIfY = useFlowStore.getState().nodes.find((n) => n.id === 'if-1')!.position.y;

    useFlowStore.setState({ blockWidth: 330, density: 1.2, textScale: 1 });
    act(() => result.current());
    const roomyIfY = useFlowStore.getState().nodes.find((n) => n.id === 'if-1')!.position.y;

    // Roomy density pushes the spine node further down than Normal density.
    expect(roomyIfY).toBeGreaterThan(normalIfY);
  });
});
