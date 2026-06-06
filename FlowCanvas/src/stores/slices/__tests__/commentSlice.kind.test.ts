import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
import { useFlowStore } from '../../useFlowStore';

describe('commentSlice kind/anchor', () => {
  beforeEach(() => { useFlowStore.setState({ nodes: [], edges: [] }); vi.clearAllMocks(); });

  it('updateComment preserves kind and anchor', () => {
    useFlowStore.setState({ nodes: [{
      id: 'c1', type: 'comment', position: { x: 0, y: 0 },
      data: { commentId: 'c1', text: 'x', kind: 'comment', anchor: { type: 'leading', stepPath: 'steps/0' } },
    }] as never });
    useFlowStore.getState().updateComment('c1', { text: 'y' });
    const n = useFlowStore.getState().nodes.find((m) => m.id === 'c1')!;
    expect((n.data as Record<string, unknown>).kind).toBe('comment');
    expect((n.data as Record<string, unknown>).anchor).toEqual({ type: 'leading', stepPath: 'steps/0' });
    expect((n.data as Record<string, unknown>).text).toBe('y');
  });
});
