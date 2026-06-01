import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';

export type BlockExecState = 'idle' | 'running' | 'success' | 'error' | 'skipped' | 'disabled';

export interface BlockOutput {
  text: string;
  timestamp: number;
  stepType?: string;
}

export interface DataBlockTestResult {
  success: boolean;
  output: string;
  error?: string;
  changedKeys?: string[];
  timestamp: number;
}

export interface ExecutionSlice {
  isRunning: boolean;
  blockStates: Map<string, BlockExecState>;
  blockOutputs: Map<string, BlockOutput[]>;
  blockTimings: Map<string, { start: number; end?: number; duration?: number }>;
  loopIterations: Map<string, number>;
  branchTaken: Map<string, string>;
  dataBlockTestResults: Map<string, DataBlockTestResult>;
  /** When false, the "Clear Path" control hides the edge highlight without touching node badges. */
  pathVisible: boolean;

  setRunning: (running: boolean) => void;
  setBlockState: (id: string, state: BlockExecState) => void;
  appendBlockOutput: (id: string, output: string, stepType?: string) => void;
  setBlockTiming: (id: string, start: number, end?: number) => void;
  setLoopIteration: (id: string, iteration: number) => void;
  setBranchTaken: (id: string, key: string) => void;
  setPathVisible: (visible: boolean) => void;
  clearPath: () => void;
  clearExecution: () => void;
  getBlockOutput: (id: string) => BlockOutput[];
  setDataBlockTestResult: (id: string, result: DataBlockTestResult) => void;
  clearDataBlockTestResult: (id: string) => void;
}

export const createExecutionSlice: StateCreator<FlowStore, [], [], ExecutionSlice> = (set, get) => ({
  isRunning: false,
  pathVisible: true,
  blockStates: new Map(),
  blockOutputs: new Map(),
  blockTimings: new Map(),
  loopIterations: new Map(),
  branchTaken: new Map(),
  dataBlockTestResults: new Map(),

  setRunning: (running) => set({ isRunning: running }),

  setBlockState: (id, state) => {
    const hasNode = get().nodes.some((node) => node.id === id);
    if (!hasNode) {
      console.warn(`[FlowCanvas] execution update targeted unknown node id '${id}' (state='${state}').`);
    }

    set((s) => {
      const next = new Map(s.blockStates);
      next.set(id, state);
      return { blockStates: next };
    });
    // Also update the node's visual execState
    get().updateNodeData(id, { execState: state });
  },

  appendBlockOutput: (id, text, stepType) => {
    set((s) => {
      const next = new Map(s.blockOutputs);
      const existing = next.get(id) || [];
      next.set(id, [...existing, { text, timestamp: Date.now(), stepType }]);
      return { blockOutputs: next };
    });
  },

  setBlockTiming: (id, start, end) => {
    set((s) => {
      const next = new Map(s.blockTimings);
      next.set(id, { start, end, duration: end ? end - start : undefined });
      return { blockTimings: next };
    });
  },

  setLoopIteration: (id, iteration) => {
    set((s) => {
      const next = new Map(s.loopIterations);
      next.set(id, iteration);
      return { loopIterations: next };
    });
  },

  setBranchTaken: (id, key) => {
    set((s) => {
      const next = new Map(s.branchTaken);
      next.set(id, key);
      return { branchTaken: next };
    });
  },

  setPathVisible: (visible) => set({ pathVisible: visible }),

  // Clear Path: hide the edge highlight only. Node blockStates/badges are untouched.
  clearPath: () => set({ pathVisible: false }),

  clearExecution: () => {
    set({
      blockStates: new Map(),
      blockOutputs: new Map(),
      blockTimings: new Map(),
      loopIterations: new Map(),
      branchTaken: new Map(),
      dataBlockTestResults: new Map(),
      pathVisible: true,
    });
    // Reset all node exec states to idle
    set((s) => ({
      nodes: s.nodes.map((n) => ({
        ...n,
        data: { ...n.data, execState: 'idle' },
      })),
    }));
  },

  getBlockOutput: (id) => {
    return get().blockOutputs.get(id) || [];
  },

  setDataBlockTestResult: (id, result) => {
    set((s) => {
      const next = new Map(s.dataBlockTestResults);
      next.set(id, result);
      return { dataBlockTestResults: next };
    });
  },

  clearDataBlockTestResult: (id) => {
    set((s) => {
      const next = new Map(s.dataBlockTestResults);
      next.delete(id);
      return { dataBlockTestResults: next };
    });
  },
});
