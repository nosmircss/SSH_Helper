import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save', setLayoutMode: 'set-layout-mode', openRunOutputWindow: 'open-run-output-window', closeRunOutputWindow: 'close-run-output-window' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('uiSlice run-output view prefs', () => {
  beforeEach(() => {
    useFlowStore.setState({ outputTab: 'block', runOutputColor: true, runOutputWrap: false, runOutputFollow: true, runOutputUnread: false, runOutputPoppedOut: false });
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

  it('openRunOutputWindow sends open + sets popped-out and the Block tab', () => {
    useFlowStore.getState().openRunOutputWindow();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'open-run-output-window' }));
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(true);
    expect(useFlowStore.getState().outputTab).toBe('block');
  });

  it('closeRunOutputWindow sends close + clears popped-out and shows the Run tab', () => {
    useFlowStore.setState({ runOutputPoppedOut: true, outputTab: 'block' });
    useFlowStore.getState().closeRunOutputWindow();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'close-run-output-window' }));
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(false);
    expect(useFlowStore.getState().outputTab).toBe('run');
  });

  it('open/closeRunOutputWindow clear the unread dot (no spurious indicator after docking)', () => {
    useFlowStore.setState({ runOutputUnread: true });
    useFlowStore.getState().openRunOutputWindow();
    expect(useFlowStore.getState().runOutputUnread).toBe(false);
    useFlowStore.setState({ runOutputUnread: true });
    useFlowStore.getState().closeRunOutputWindow();
    expect(useFlowStore.getState().runOutputUnread).toBe(false);
  });

  it('restore setters apply without echo', () => {
    useFlowStore.getState().restoreRunOutputPrefs({ runOutputColor: false, runOutputWrap: true, runOutputFollow: false });
    const s = useFlowStore.getState();
    expect([s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual([false, true, false]);
    expect(messageBus.send).not.toHaveBeenCalled();
  });

  it('restoreRunOutputPrefs preserves current values for undefined/partial input (?? not ||)', () => {
    // Fresh-config path: nothing persisted -> defaults must survive (false is NOT clobbered to default).
    useFlowStore.getState().restoreRunOutputPrefs({});
    let s = useFlowStore.getState();
    expect([s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual([true, false, true]);
    // Partial: only color changes to false; wrap/follow untouched (regression guard for ?? vs ||).
    useFlowStore.getState().restoreRunOutputPrefs({ runOutputColor: false });
    s = useFlowStore.getState();
    expect([s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual([false, false, true]);
  });

  it('setRunOutputPoppedOut sets the flag directly', () => {
    useFlowStore.getState().setRunOutputPoppedOut(true);
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(true);
    useFlowStore.getState().setRunOutputPoppedOut(false);
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(false);
  });
});
