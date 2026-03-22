import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';

export interface DebugSlice {
  paused: boolean;
  pausedAtNodeId: string | null;
  callStack: string[];
  breakpoints: Set<string>;
  disabledBlocks: Set<string>;
  stepMode: boolean;

  toggleBreakpoint: (nodeId: string) => void;
  toggleDisabled: (nodeId: string) => void;
  setPaused: (paused: boolean, nodeId?: string, callStack?: string[]) => void;
  debugAction: (action: 'continue' | 'step' | 'step-into' | 'stop') => void;
  isDisabled: (nodeId: string) => boolean;
  hasBreakpoint: (nodeId: string) => boolean;
}

export const createDebugSlice: StateCreator<FlowStore, [], [], DebugSlice> = (set, get) => ({
  paused: false,
  pausedAtNodeId: null,
  callStack: [],
  breakpoints: new Set<string>(),
  disabledBlocks: new Set<string>(),
  stepMode: false,

  toggleBreakpoint: (nodeId) => {
    set((s) => {
      const next = new Set(s.breakpoints);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return { breakpoints: next };
    });
    // Update node visual
    const has = get().breakpoints.has(nodeId);
    get().updateNodeData(nodeId, { breakpoint: !has });
    // Notify C#
    messageBus.send({ type: 'breakpoint-toggle', stepId: nodeId });
  },

  toggleDisabled: (nodeId) => {
    set((s) => {
      const next = new Set(s.disabledBlocks);
      const nowDisabled = !next.has(nodeId);
      if (nowDisabled) next.add(nodeId);
      else next.delete(nodeId);
      // Update node visual
      get().updateNodeData(nodeId, { execState: nowDisabled ? 'disabled' : 'idle' });
      // Notify C#
      messageBus.send({ type: 'disable-block', stepId: nodeId, disabled: nowDisabled });
      return { disabledBlocks: next };
    });
  },

  setPaused: (paused, nodeId, callStack) => {
    set({
      paused,
      pausedAtNodeId: nodeId ?? null,
      callStack: callStack ?? [],
    });
  },

  debugAction: (action) => {
    messageBus.send({ type: 'debug-action', action });
    if (action === 'stop' || action === 'continue') {
      set({ paused: false, pausedAtNodeId: null, callStack: [] });
    }
  },

  isDisabled: (nodeId) => get().disabledBlocks.has(nodeId),
  hasBreakpoint: (nodeId) => get().breakpoints.has(nodeId),
});
