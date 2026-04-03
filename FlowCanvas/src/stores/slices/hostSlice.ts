import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';

export interface TargetHost {
  ip: string;
  port: number;
  username: string;
  variables: Record<string, string>;
}

export interface HostSlice {
  targetHost: TargetHost | null;
  setTargetHost: (host: TargetHost | null) => void;
}

export const createHostSlice: StateCreator<FlowStore, [], [], HostSlice> = (set) => ({
  targetHost: null,
  setTargetHost: (host) => set({ targetHost: host }),
});
