import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';

let changeHighlightTimer: ReturnType<typeof setTimeout> | null = null;

export interface VariableEntry {
  name: string;
  value: unknown;
  changed?: boolean;
  setBy?: string;
}

export interface VariableSlice {
  variables: VariableEntry[];
  previousValues: Map<string, unknown>;

  setVariables: (vars: VariableEntry[]) => void;
  setVariablesWithChanges: (vars: Record<string, unknown>, changedKeys?: string[]) => void;
  clearVariables: () => void;
}

export const createVariableSlice: StateCreator<FlowStore, [], [], VariableSlice> = (set, get) => ({
  variables: [],
  previousValues: new Map(),

  setVariables: (vars) => set({ variables: vars }),

  setVariablesWithChanges: (vars, changedKeys) => {
    const changedSet = new Set(changedKeys || []);
    const prev = get().previousValues;
    const nextPrev = new Map<string, unknown>();
    const entries: VariableEntry[] = [];

    for (const [name, value] of Object.entries(vars)) {
      nextPrev.set(name, value);
      entries.push({
        name,
        value,
        changed: changedSet.has(name) || (prev.has(name) && prev.get(name) !== value),
      });
    }

    set({ variables: entries, previousValues: nextPrev });

    // Clear the 'changed' flag after animation duration (800ms)
    if (changeHighlightTimer) clearTimeout(changeHighlightTimer);
    changeHighlightTimer = setTimeout(() => {
      changeHighlightTimer = null;
      set((s) => ({
        variables: s.variables.map((v) => ({ ...v, changed: false })),
      }));
    }, 800);
  },

  clearVariables: () => {
    if (changeHighlightTimer) {
      clearTimeout(changeHighlightTimer);
      changeHighlightTimer = null;
    }
    set({ variables: [], previousValues: new Map() });
  },
});
