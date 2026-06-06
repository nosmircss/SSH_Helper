import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';

const pos = (id: string) => useFlowStore.getState().nodes.find((n) => n.id === id)!.position;

describe('onNodesChange comment drag-follow', () => {
  beforeEach(() => {
    useFlowStore.setState({
      nodes: [
        { id: 'A', type: 'block', position: { x: 100, y: 100 }, data: { blockType: 'print', props: {} } },
        { id: 'c1', type: 'comment', position: { x: 100, y: 72 }, data: { commentId: 'c1', attachedToNodeId: 'A', anchor: { type: 'leading' }, text: 'note' } },
        { id: 'B', type: 'block', position: { x: 100, y: 300 }, data: { blockType: 'print', props: {} } },
      ] as never,
      edges: [],
      selectedNodeIds: new Set(),
    });
    vi.clearAllMocks();
  });

  it('moves an anchored comment by the same delta when its block is dragged', () => {
    useFlowStore.getState().onNodesChange([{ type: 'position', id: 'A', position: { x: 140, y: 160 }, dragging: true }]);
    expect(pos('A')).toEqual({ x: 140, y: 160 });
    expect(pos('c1')).toEqual({ x: 140, y: 132 }); // followed by +40,+60
  });

  it('leaves a comment attached to a DIFFERENT block alone', () => {
    useFlowStore.getState().onNodesChange([{ type: 'position', id: 'B', position: { x: 150, y: 350 }, dragging: true }]);
    expect(pos('c1')).toEqual({ x: 100, y: 72 }); // c1 is attached to A, not B
  });

  it('does not double-move a comment that has its own position change (multi-select)', () => {
    useFlowStore.getState().onNodesChange([
      { type: 'position', id: 'A', position: { x: 140, y: 160 }, dragging: true },
      { type: 'position', id: 'c1', position: { x: 140, y: 132 }, dragging: true },
    ]);
    expect(pos('c1')).toEqual({ x: 140, y: 132 }); // applied once via its own change, not delta-added again
  });

  it('ignores non-position changes (no spurious comment move on select)', () => {
    useFlowStore.getState().onNodesChange([{ type: 'select', id: 'A', selected: true }]);
    expect(pos('c1')).toEqual({ x: 100, y: 72 });
  });
});
