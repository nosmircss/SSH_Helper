import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save', setLayoutMode: 'set-layout-mode' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('uiSlice run-output view prefs', () => {
  beforeEach(() => {
    useFlowStore.setState({ outputTab: 'block', runOutputColor: true, runOutputWrap: false, runOutputFollow: true, runOutputUnread: false });
    vi.clearAllMocks();
  });

  it('defaults: block tab, color on, wrap off, follow on', () => {
    const s = useFlowStore.getState();
    expect([s.outputTab, s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual(['block', true, false, true]);
  });

  it('setOutputTab switches and clears unread when showing run', () => {
    useFlowStore.setState({ runOutputUnread: true });
    useFlowStore.getState().setOutputTab('run');
    expect(useFlowStore.getState().outputTab).toBe('run');
    expect(useFlowStore.getState().runOutputUnread).toBe(false);
  });

  it('toggleRunOutputColor flips state and persists via layout-save', () => {
    useFlowStore.getState().toggleRunOutputColor();
    expect(useFlowStore.getState().runOutputColor).toBe(false);
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', runOutputColor: false }));
  });

  it('toggleRunOutputWrap and toggleRunOutputFollow persist via layout-save', () => {
    useFlowStore.getState().toggleRunOutputWrap();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', runOutputWrap: true }));
    useFlowStore.getState().toggleRunOutputFollow();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', runOutputFollow: false }));
  });

  it('restore setters apply without echo', () => {
    useFlowStore.getState().restoreRunOutputPrefs({ runOutputColor: false, runOutputWrap: true, runOutputFollow: false });
    const s = useFlowStore.getState();
    expect([s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual([false, true, false]);
    expect(messageBus.send).not.toHaveBeenCalled();
  });
});
