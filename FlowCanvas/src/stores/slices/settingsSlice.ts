import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';
import { reflowLayout } from '../reflow';
import { selectCanvasSizing } from './canvasSizing';

/** Width presets (px). Normal=330 is today's default. */
export const WIDTH_PRESETS = [
  { label: 'Compact', px: 300 },
  { label: 'Normal', px: 330 },
  { label: 'Wide', px: 440 },
  { label: 'Extra', px: 560 },
  { label: 'Max', px: 700 },
] as const;
export const TEXT_SCALES = [
  { label: 'S', v: 0.9 }, { label: 'M', v: 1 }, { label: 'L', v: 1.15 },
  { label: 'XL', v: 1.35 }, { label: 'XXL', v: 1.6 },
] as const;
export const DENSITIES = [
  { label: 'Tight', v: 0.85 }, { label: 'Normal', v: 1 }, { label: 'Roomy', v: 1.2 },
] as const;

export const SETTINGS_DEFAULTS = {
  blockWidth: 330,
  textScale: 1,
  density: 1,
  defaultBlockExpanded: false,
} as const;

export type CanvasSettings = Pick<SettingsSlice, 'blockWidth' | 'textScale' | 'density' | 'defaultBlockExpanded'>;

/** Live canvas sizing read from the store — the single shape every reflow caller MUST thread into
 *  computeHierarchicalLayout so auto-layout, expand/collapse and import all honor the user's
 *  Display Settings instead of silently reverting to the factory 330/1/1 geometry. */
// Re-exported from the runtime-leaf module so existing importers keep working; the canonical
// definition lives in ./canvasSizing to avoid an init-order cycle (see that file).
export { selectCanvasSizing };

export interface SettingsSlice {
  blockWidth: number;
  textScale: number;
  density: number;
  defaultBlockExpanded: boolean;

  setBlockWidth: (px: number) => void;
  setTextScale: (v: number) => void;
  setDensity: (v: number) => void;
  setDefaultBlockExpanded: (v: boolean) => void;
  resetCanvasSettings: () => void;
  restoreCanvasSettings: (s: Partial<CanvasSettings>) => void;
}

export const createSettingsSlice: StateCreator<FlowStore, [], [], SettingsSlice> = (set, get) => {
  // Reflow (gated by the Auto-layout setting; keepOrphans inside reflowLayout), persist the changed
  // setting (layout-save) AND the new node positions (layout-autosave).
  const reflowAndPersist = (changed: Record<string, unknown>) => {
    reflowLayout(get);
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, ...changed });
    sendLayoutAutosave();
  };

  return {
    ...SETTINGS_DEFAULTS,

    setBlockWidth: (px) => { set({ blockWidth: px }); reflowAndPersist({ blockWidth: px }); },
    setTextScale: (v) => { set({ textScale: v }); reflowAndPersist({ textScale: v }); },
    setDensity: (v) => { set({ density: v }); reflowAndPersist({ density: v }); },
    setDefaultBlockExpanded: (v) => {
      set({ defaultBlockExpanded: v });
      messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, defaultBlockExpanded: v });
      // The control reads as the blocks' default presentation, so apply it to the open graph
      // immediately (setAllExpanded stamps carrier flags, reflows, and autosaves) — load-graph
      // and restoreCanvasSettings enforce it for future loads.
      if (get().nodes.length > 0) get().setAllExpanded(v);
    },
    resetCanvasSettings: () => {
      set({ ...SETTINGS_DEFAULTS });
      reflowAndPersist({ ...SETTINGS_DEFAULTS });
    },
    // Host-driven restore. Apply values, then reflow if a graph is already loaded so a
    // restore that arrives AFTER load-graph still re-lays-out at the saved sizing. No echo.
    restoreCanvasSettings: (s) => {
      set({ ...s });
      if (get().nodes.length > 0) {
        // Fresh-open ordering: load-graph lands before this restore, so a restored
        // "Default block state: Expanded" must be applied to the already-loaded graph here
        // (setAllExpanded stamps flags + reflows). Restoring OFF must NOT collapse anything —
        // the preset's own saved expansion governs — so it just reflows at the new sizing.
        if (s.defaultBlockExpanded === true) get().setAllExpanded(true, { autosave: false });
        else reflowLayout(get);
      }
    },
  };
};
