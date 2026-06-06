import { describe, it, expect, vi, beforeEach } from 'vitest';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: { setLayoutMode: 'set-layout-mode', layoutSave: 'layout-save' } } }));
import { messageBus } from '../../MessageBus';
import { useFlowStore } from '../../stores/useFlowStore';

/**
 * Store-level test: verifies setLayoutMode toggles state and echoes set-layout-mode
 * over the message bus — the same side effects the toolbar button produces.
 *
 * Render approach was skipped: Toolbar transitively pulls in @xyflow/react, blockDefs/registry,
 * mix/tokens, buildExecutableGraphPayload, SettingsPopover, and more. Mocking all of these
 * for a single-button interaction test would create a larger maintenance burden than the
 * store-level assertion itself. The toolbar button is tested via the store contract.
 */
describe('Toolbar layout-mode toggle (store level)', () => {
  beforeEach(() => {
    useFlowStore.setState({ layoutMode: 'auto', nodes: [] });
    vi.clearAllMocks();
  });

  it('toggles to manual and echoes set-layout-mode', () => {
    useFlowStore.getState().setLayoutMode('manual');
    expect(useFlowStore.getState().layoutMode).toBe('manual');
    expect(messageBus.send).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'set-layout-mode', mode: 'manual' }),
    );
  });

  it('toggles back to auto and echoes set-layout-mode', () => {
    useFlowStore.setState({ layoutMode: 'manual' });
    vi.clearAllMocks();
    useFlowStore.getState().setLayoutMode('auto');
    expect(useFlowStore.getState().layoutMode).toBe('auto');
    expect(messageBus.send).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'set-layout-mode', mode: 'auto' }),
    );
  });

  it('is a no-op when mode is already set (no duplicate echo)', () => {
    useFlowStore.getState().setLayoutMode('auto'); // already auto → no change
    expect(messageBus.send).not.toHaveBeenCalled();
  });
});
