import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';

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
  debugAction: (action: 'continue' | 'step' | 'stop') => void;
  restoreDisabledBlocks: (nodeIds: string[]) => void;
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
    const nowHasBreakpoint = get().breakpoints.has(nodeId);
    get().updateNodeData(nodeId, { breakpoint: nowHasBreakpoint });
    // Notify C#
    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.breakpointToggle,
      stepId: nodeId,
    });
  },

  toggleDisabled: (nodeId) => {
    let nowDisabled = false;
    set((s) => {
      const next = new Set(s.disabledBlocks);
      nowDisabled = !next.has(nodeId);
      if (nowDisabled) next.add(nodeId);
      else next.delete(nodeId);
      return { disabledBlocks: next };
    });
    // Update node visual
    get().updateNodeData(nodeId, { execState: nowDisabled ? 'disabled' : 'idle' });
    // Notify C#
    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.disableBlock,
      stepId: nodeId,
      disabled: nowDisabled,
    });
    sendLayoutAutosave();
  },

  setPaused: (paused, nodeId, callStack) => {
    set({
      paused,
      pausedAtNodeId: nodeId ?? null,
      callStack: callStack ?? [],
    });
  },

  debugAction: (action) => {
    if (!Object.values(CANVAS_HOST_MESSAGES.debugAction).includes(action as 'continue' | 'step' | 'stop')) {
      console.warn('[FlowCanvas] Unknown debug action ignored:', action);
      return;
    }

    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.debugAction,
      action,
    });
    if (action === CANVAS_HOST_MESSAGES.debugAction.stop || action === CANVAS_HOST_MESSAGES.debugAction.continue) {
      set({ paused: false, pausedAtNodeId: null, callStack: [] });
    }
  },

  restoreDisabledBlocks: (nodeIds) => {
    set({ disabledBlocks: new Set(nodeIds) });
    for (const id of nodeIds) {
      get().updateNodeData(id, { execState: 'disabled' });
    }
  },

  isDisabled: (nodeId) => get().disabledBlocks.has(nodeId),
  hasBreakpoint: (nodeId) => get().breakpoints.has(nodeId),
});
