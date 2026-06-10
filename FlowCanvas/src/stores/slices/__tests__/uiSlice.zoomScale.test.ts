import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() } }));
import { useFlowStore } from '../../useFlowStore';
import { UI_SCALE_MAX } from '../uiSlice';

/**
 * uiZoomScale drives the inverse-zoom sizing of flow-space chrome (connection handle
 * pseudo-elements, band pills, iteration steppers, connectionRadius). Contract:
 * scale = clamp(1, 1/zoom, UI_SCALE_MAX), quantized to 0.05 so pan/zoom frames don't
 * churn subscribers.
 */
describe('uiSlice.syncUiZoomScale', () => {
  beforeEach(() => {
    useFlowStore.setState({ uiZoomScale: 1 });
  });

  it('stays 1 at zoom 1 and when zoomed in', () => {
    useFlowStore.getState().syncUiZoomScale(1);
    expect(useFlowStore.getState().uiZoomScale).toBe(1);
    useFlowStore.getState().syncUiZoomScale(2);
    expect(useFlowStore.getState().uiZoomScale).toBe(1);
  });

  it('compensates inversely when zoomed out', () => {
    useFlowStore.getState().syncUiZoomScale(0.5);
    expect(useFlowStore.getState().uiZoomScale).toBe(2);
  });

  it('caps at UI_SCALE_MAX at extreme zoom-out', () => {
    useFlowStore.getState().syncUiZoomScale(0.2); // 1/0.2 = 5 → capped
    expect(useFlowStore.getState().uiZoomScale).toBe(UI_SCALE_MAX);
  });

  it('quantizes to 0.05 steps', () => {
    useFlowStore.getState().syncUiZoomScale(0.87); // 1/0.87 ≈ 1.1494 → 1.15
    expect(useFlowStore.getState().uiZoomScale).toBe(1.15);
  });

  it('does not notify subscribers when the quantized value is unchanged', () => {
    const listener = vi.fn();
    const unsub = useFlowStore.subscribe(listener);
    useFlowStore.getState().syncUiZoomScale(0.5);
    useFlowStore.getState().syncUiZoomScale(0.501); // same 2.0 bucket
    unsub();
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it('ignores invalid zoom values', () => {
    useFlowStore.setState({ uiZoomScale: 1.5 });
    useFlowStore.getState().syncUiZoomScale(0);
    useFlowStore.getState().syncUiZoomScale(-1);
    useFlowStore.getState().syncUiZoomScale(Number.NaN);
    expect(useFlowStore.getState().uiZoomScale).toBe(1.5);
  });
});
