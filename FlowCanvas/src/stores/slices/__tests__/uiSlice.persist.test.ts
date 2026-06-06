import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('uiSlice toggle persistence', () => {
  beforeEach(() => { useFlowStore.setState({ snapToGrid: false, branchBandsEnabled: true }); vi.clearAllMocks(); });

  it('toggleSnapToGrid persists via layout-save', () => {
    useFlowStore.getState().toggleSnapToGrid();
    expect(messageBus.send).toHaveBeenCalledWith({ type: 'layout-save', snapToGrid: true });
  });
  it('toggleBranchBands persists via layout-save', () => {
    useFlowStore.getState().toggleBranchBands();
    expect(messageBus.send).toHaveBeenCalledWith({ type: 'layout-save', branchBandsEnabled: false });
  });
  it('restoreSnapToGrid / restoreBranchBands set state without echo', () => {
    useFlowStore.getState().restoreSnapToGrid(true);
    useFlowStore.getState().restoreBranchBands(false);
    expect(useFlowStore.getState().snapToGrid).toBe(true);
    expect(useFlowStore.getState().branchBandsEnabled).toBe(false);
  });
});
