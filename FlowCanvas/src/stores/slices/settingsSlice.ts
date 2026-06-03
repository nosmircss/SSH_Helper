import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';
import { computeHierarchicalLayout, type BlockSizing } from '../../utils/layout/hierarchicalLayout';

/** Width presets (px). M=330 is today's default. */
export const WIDTH_PRESETS = [
  { label: 'Compact', px: 300 },
  { label: 'Normal', px: 330 },
  { label: 'Wide', px: 440 },
  { label: 'Extra', px: 560 },
  { label: 'Max', px: 700 },
] as const;
export const TEXT_SCALES = [
  { label: 'S', v: 0.9 }, { label: 'M', v: 1 }, { label: 'L', v: 1.15 },
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
  const sizing = (): BlockSizing => {
    const s = get();
    return { blockWidth: s.blockWidth, density: s.density, textScale: s.textScale };
  };
  // Reflow with the current sizing, persist the changed setting (layout-save) AND the new
  // node positions (layout-autosave). Mirrors debugSlice's setAllExpanded side effects.
  const reflowAndPersist = (changed: Record<string, unknown>) => {
    const st = get();
    st.setNodes(computeHierarchicalLayout(st.nodes, st.edges, sizing()));
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
    },
    resetCanvasSettings: () => {
      set({ ...SETTINGS_DEFAULTS });
      reflowAndPersist({ ...SETTINGS_DEFAULTS });
    },
    // Host-driven restore. Apply values, then reflow if a graph is already loaded so a
    // restore that arrives AFTER load-graph still re-lays-out at the saved sizing. No echo.
    restoreCanvasSettings: (s) => {
      set({ ...s });
      const st = get();
      if (st.nodes.length > 0) st.setNodes(computeHierarchicalLayout(st.nodes, st.edges, sizing()));
    },
  };
};
