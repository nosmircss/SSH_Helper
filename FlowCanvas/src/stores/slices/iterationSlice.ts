import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import type { BlockExecState } from './executionSlice';
import type { IterationFrameMsg } from '../../communication-message-types';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';

export const DEFAULT_ITERATION_HISTORY_CAP = 500;

export interface IterationNodeEntry {
  state: BlockExecState;
  branchTaken?: string;
  duration?: number;
  /** Index into executionSlice.blockOutputs[nodeId] for this iteration's output. */
  outputIdx?: number;
}

export interface IterationRecord {
  /** Unique, monotonic per run. Selections and parent links use seq — array positions
   *  shift under eviction, and iteration index i repeats when an inner loop re-runs. */
  seq: number;
  /** 0-based iteration index within the loop (the executor's index — may have gaps
   *  for when-skipped foreach items). */
  i: number;
  /** Foreach item value (truncated host-side); undefined for while/repeat. */
  label?: string;
  /** True if any step in this iteration (at any depth) errored. */
  failed: boolean;
  /** The containing iteration of the next loop up, or null for top-level loops. */
  parent: { loopId: string; seq: number } | null;
  /** Per-node entries: innermost-loop records hold exact values; ancestor records
   *  aggregate (error sticky, otherwise last write wins). */
  nodes: Map<string, IterationNodeEntry>;
}

export interface IterationSlice {
  iterationLog: Map<string, IterationRecord[]>;
  /** Selected record seq per loop node id; null/absent = ALL (aggregate view). */
  iterationSelections: Map<string, number | null>;
  /** True iteration count per loop (survives eviction). */
  totalIterations: Map<string, number>;
  iterationSeq: number;
  iterationHistoryCap: number;

  recordIterationEvent: (
    nodeId: string,
    stack: IterationFrameMsg[],
    patch: Partial<IterationNodeEntry>,
  ) => void;
  setIterationSelection: (loopId: string, seq: number | null) => void;
  clearIterations: () => void;
  /** User-initiated: persists via pref-save. */
  setIterationHistoryCap: (v: number) => void;
  /** Host-driven restore: no pref-save echo. */
  restoreIterationHistoryCap: (v: number) => void;
}

export const createIterationSlice: StateCreator<FlowStore, [], [], IterationSlice> = (set, get) => ({
  iterationLog: new Map(),
  iterationSelections: new Map(),
  totalIterations: new Map(),
  iterationSeq: 0,
  iterationHistoryCap: DEFAULT_ITERATION_HISTORY_CAP,

  recordIterationEvent: (nodeId, stack, patch) => set((s) => {
    if (!Array.isArray(stack) || stack.length === 0) return {};

    const log = new Map(s.iterationLog);
    const totals = new Map(s.totalIterations);
    let nextSeq = s.iterationSeq;
    const eventFailed = patch.state === 'error';
    let parentRef: { loopId: string; seq: number } | null = null;

    for (let d = 0; d < stack.length; d++) {
      const frame = stack[d];
      if (!frame || typeof frame.loopId !== 'string' || frame.loopId.length === 0) continue;
      if (!Number.isFinite(frame.i) || frame.i < 0) continue;

      const records = [...(log.get(frame.loopId) ?? [])];
      const last = records[records.length - 1];
      // The event belongs to the loop's latest record only if BOTH the iteration index and
      // the containing parent iteration match — a restarted inner loop (i back to 0 under a
      // new outer iteration) must start a fresh record, never merge into the old one.
      const sameParent = (last?.parent?.seq ?? null) === (parentRef?.seq ?? null);
      let rec: IterationRecord;
      if (last && last.i === frame.i && sameParent) {
        rec = { ...last, nodes: new Map(last.nodes) };
        records[records.length - 1] = rec;
      } else {
        rec = {
          seq: ++nextSeq,
          i: frame.i,
          label: typeof frame.label === 'string' && frame.label.length > 0 ? frame.label : undefined,
          failed: false,
          parent: parentRef,
          nodes: new Map(),
        };
        records.push(rec);
        if (records.length > s.iterationHistoryCap) {
          records.splice(0, records.length - s.iterationHistoryCap);
        }
      }

      const prev = rec.nodes.get(nodeId);
      rec.nodes.set(nodeId, {
        state: prev?.state === 'error' ? 'error' : (patch.state ?? prev?.state ?? 'running'),
        branchTaken: patch.branchTaken ?? prev?.branchTaken,
        duration: patch.duration ?? prev?.duration,
        outputIdx: patch.outputIdx ?? prev?.outputIdx,
      });
      if (eventFailed) rec.failed = true;

      log.set(frame.loopId, records);
      totals.set(frame.loopId, Math.max(totals.get(frame.loopId) ?? 0, frame.i + 1));
      parentRef = { loopId: frame.loopId, seq: rec.seq };
    }

    return { iterationLog: log, totalIterations: totals, iterationSeq: nextSeq };
  }),

  setIterationSelection: (loopId, seq) => set((s) => {
    const sels = new Map(s.iterationSelections);

    // Walk a loop's parent chain (via any of its records) to test ancestry.
    const isDescendantOf = (childLoop: string, ancestorLoop: string): boolean => {
      let curLoop: string | undefined = childLoop;
      const seen = new Set<string>();
      while (curLoop && !seen.has(curLoop)) {
        seen.add(curLoop);
        const recs: IterationRecord[] = s.iterationLog.get(curLoop) ?? [];
        const parent = recs.find((r) => r.parent)?.parent;
        if (!parent) return false;
        if (parent.loopId === ancestorLoop) return true;
        curLoop = parent.loopId;
      }
      return false;
    };

    // Changing this loop's selection re-ranges every nested loop's iteration list.
    for (const otherLoop of sels.keys()) {
      if (otherLoop !== loopId && isDescendantOf(otherLoop, loopId)) sels.set(otherLoop, null);
    }
    sels.set(loopId, seq);

    // Inner-pulls-outer: a concrete selection forces each ancestor to the containing
    // iteration, so clusters can never contradict each other.
    if (seq != null) {
      let rec = (s.iterationLog.get(loopId) ?? []).find((r) => r.seq === seq);
      const seen = new Set<string>([loopId]);
      while (rec?.parent && !seen.has(rec.parent.loopId)) {
        const pLoop: string = rec.parent.loopId;
        const pSeq: number = rec.parent.seq;
        seen.add(pLoop);
        sels.set(pLoop, pSeq);
        rec = (s.iterationLog.get(pLoop) ?? []).find((r) => r.seq === pSeq);
      }
    }

    return { iterationSelections: sels };
  }),

  clearIterations: () => set({
    iterationLog: new Map(),
    iterationSelections: new Map(),
    totalIterations: new Map(),
    iterationSeq: 0,
  }),

  setIterationHistoryCap: (v) => {
    if (!Number.isFinite(v) || v < 1) return;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.prefSave, iterationHistoryCap: v });
    set({ iterationHistoryCap: v });
  },

  restoreIterationHistoryCap: (v) => {
    if (!Number.isFinite(v) || v < 1) return;
    set({ iterationHistoryCap: v }); // host-driven, no echo
  },
});
