import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import type { BlockExecState } from './executionSlice';

export interface TimelineEntry {
  index: number;
  nodeId: string;
  nodeLabel: string;
  blockType: string;
  state: BlockExecState;
  startTime: number;
  endTime?: number;
  duration?: number;
  variables: Record<string, unknown>;
  output?: string;
}

export interface TimelineSlice {
  timelineEntries: TimelineEntry[];
  timelineIndex: number;
  timelineScrubbing: boolean;

  addTimelineEntry: (entry: Omit<TimelineEntry, 'index'>) => void;
  updateTimelineEntry: (nodeId: string, updates: Partial<TimelineEntry>) => void;
  scrubTo: (index: number) => void;
  stopScrubbing: () => void;
  clearTimeline: () => void;
}

export const createTimelineSlice: StateCreator<FlowStore, [], [], TimelineSlice> = (set, get) => ({
  timelineEntries: [],
  timelineIndex: -1,
  timelineScrubbing: false,

  addTimelineEntry: (entry) => {
    set((s) => ({
      timelineEntries: [
        ...s.timelineEntries,
        { ...entry, index: s.timelineEntries.length },
      ],
    }));
  },

  updateTimelineEntry: (nodeId, updates) => {
    set((s) => ({
      timelineEntries: s.timelineEntries.map((e) =>
        e.nodeId === nodeId && !e.endTime ? { ...e, ...updates } : e
      ),
    }));
  },

  scrubTo: (index) => {
    const entries = get().timelineEntries;
    if (index < 0 || index >= entries.length) return;
    const entry = entries[index];
    set({ timelineIndex: index, timelineScrubbing: true });

    // Highlight the block and show its variables
    get().selectNode(entry.nodeId);
    if (entry.variables) {
      get().setVariablesWithChanges(entry.variables);
    }
  },

  stopScrubbing: () => {
    set({ timelineScrubbing: false, timelineIndex: -1 });
  },

  clearTimeline: () => {
    set({ timelineEntries: [], timelineIndex: -1, timelineScrubbing: false });
  },
});
