import type { StateCreator } from 'zustand';
import type { Node, Edge } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';

const MAX_HISTORY = 100;

export interface UndoSnapshot {
  nodes: Node[];
  edges: Edge[];
  label: string;
  timestamp: number;
}

export interface UndoSlice {
  past: UndoSnapshot[];
  future: UndoSnapshot[];

  pushSnapshot: (label: string) => void;
  undo: () => void;
  redo: () => void;
  canUndo: () => boolean;
  canRedo: () => boolean;
  clearHistory: () => void;
}

export const createUndoSlice: StateCreator<FlowStore, [], [], UndoSlice> = (set, get) => ({
  past: [],
  future: [],

  pushSnapshot: (label) => {
    const { nodes, edges } = get();
    set((s) => ({
      past: [
        ...s.past.slice(-(MAX_HISTORY - 1)),
        {
          nodes: JSON.parse(JSON.stringify(nodes)),
          edges: JSON.parse(JSON.stringify(edges)),
          label,
          timestamp: Date.now(),
        },
      ],
      future: [], // Clear redo stack on new action
    }));
  },

  undo: () => {
    const { past, nodes, edges } = get();
    if (past.length === 0) return;

    const prev = past[past.length - 1];
    set((s) => ({
      past: s.past.slice(0, -1),
      future: [
        ...s.future,
        {
          nodes: JSON.parse(JSON.stringify(nodes)),
          edges: JSON.parse(JSON.stringify(edges)),
          label: 'undo',
          timestamp: Date.now(),
        },
      ],
      nodes: prev.nodes,
      edges: prev.edges,
      exportStatus: {
        hasErrors: false,
        errors: [],
        warnings: [],
      },
      diagnostics: [],
    }));
  },

  redo: () => {
    const { future, nodes, edges } = get();
    if (future.length === 0) return;

    const next = future[future.length - 1];
    set((s) => ({
      future: s.future.slice(0, -1),
      past: [
        ...s.past,
        {
          nodes: JSON.parse(JSON.stringify(nodes)),
          edges: JSON.parse(JSON.stringify(edges)),
          label: 'redo',
          timestamp: Date.now(),
        },
      ],
      nodes: next.nodes,
      edges: next.edges,
      exportStatus: {
        hasErrors: false,
        errors: [],
        warnings: [],
      },
      diagnostics: [],
    }));
  },

  canUndo: () => get().past.length > 0,
  canRedo: () => get().future.length > 0,
  clearHistory: () => set({ past: [], future: [] }),
});
