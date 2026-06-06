import { it, expect, vi } from 'vitest';
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: { getState: () => ({ disabledBlocks: new Set() }) },
}));
import { buildExecutableGraphPayload } from '../exportGraph';

it('preserves kind and anchor on exported comments', () => {
  const nodes = [{ id: 'c1', type: 'comment', position: { x: 1, y: 2 },
    data: { text: 'hi', kind: 'comment', anchor: { type: 'leading', stepPath: 'steps/0' } } }] as never[];
  const payload = buildExecutableGraphPayload(nodes, []);
  expect(payload.comments[0].kind).toBe('comment');
  expect(payload.comments[0].anchor).toEqual({ type: 'leading', stepPath: 'steps/0' });
});
