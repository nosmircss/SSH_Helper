import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
import { useFlowStore } from '../../useFlowStore';

describe('addComment kinds', () => {
  beforeEach(() => useFlowStore.setState({ nodes: [] }));
  it('creates a comment-kind note', () => {
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'b1', 'comment');
    const nodes = useFlowStore.getState().nodes;
    const n = nodes[nodes.length - 1]!;
    expect((n.data as Record<string, unknown>).kind).toBe('comment');
  });
  it('defaults to sticky', () => {
    useFlowStore.getState().addComment({ x: 0, y: 0 });
    const nodes = useFlowStore.getState().nodes;
    const n = nodes[nodes.length - 1]!;
    expect((n.data as Record<string, unknown>).kind).toBe('sticky');
  });
});
