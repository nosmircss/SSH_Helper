import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('compactComments setting', () => {
  beforeEach(() => { useFlowStore.setState({ compactCommentsEnabled: true }); vi.clearAllMocks(); });
  it('defaults ON', () => { expect(useFlowStore.getState().compactCommentsEnabled).toBe(true); });
  it('toggle flips and persists via layout-save', () => {
    useFlowStore.getState().toggleCompactComments();
    expect(useFlowStore.getState().compactCommentsEnabled).toBe(false);
    expect(messageBus.send).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'layout-save', compactCommentsEnabled: false }));
  });
  it('restore applies host value', () => {
    useFlowStore.getState().restoreCompactComments(false);
    expect(useFlowStore.getState().compactCommentsEnabled).toBe(false);
  });
});
