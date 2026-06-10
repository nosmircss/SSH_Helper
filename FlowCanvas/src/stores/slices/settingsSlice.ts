import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';
import { reflowLayout } from '../reflow';
import { selectCanvasSizing } from './canvasSizing';

/** Block width slider bounds (px). 330 is the factory default; the old preset row spanned
 *  300 (Compact) – 700 (Max) and the slider extends well past that on user request. */
export const BLOCK_WIDTH_MIN = 300;
export const BLOCK_WIDTH_MAX = 2000;
/** Text size slider bounds (scale factor; 1 = factory default). The old preset row spanned
 *  S 0.9 – XXL 1.6; the slider extends past that on user request. Height estimates
 *  (nodeSize.ts) are linear in textScale, so the whole range lays out correctly. */
export const TEXT_SCALE_MIN = 0.9;
export const TEXT_SCALE_MAX = 2.5;
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

  /** opts.persist=false is the slider's live-drag path: apply + reflow for preview, but
   *  defer the layout-save/autosave to the commit call on release. */
  setBlockWidth: (px: number, opts?: { persist?: boolean }) => void;
  /** opts.persist=false is the slider's live-drag path (see setBlockWidth). */
  setTextScale: (v: number, opts?: { persist?: boolean }) => void;
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

    setBlockWidth: (px, opts) => {
      set({ blockWidth: px });
      if (opts?.persist === false) { reflowLayout(get); return; }
      reflowAndPersist({ blockWidth: px });
    },
    setTextScale: (v, opts) => {
      set({ textScale: v });
      if (opts?.persist === false) { reflowLayout(get); return; }
      reflowAndPersist({ textScale: v });
    },
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
      const wasExpanded = get().defaultBlockExpanded;
      set({ ...SETTINGS_DEFAULTS });
      // Mirror setDefaultBlockExpanded(false): the reset flips "Default block state" to
      // Collapsed, so collapse the open graph too — otherwise the control reads Collapsed
      // while every block stays expanded and the autosave persists that expansion.
      if (wasExpanded && get().nodes.length > 0) get().setAllExpanded(false);
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
