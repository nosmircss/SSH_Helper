import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import type { IterationFrameMsg } from '../../../communication-message-types';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

function reset() {
  useFlowStore.getState().clearIterations();
  useFlowStore.setState({ iterationHistoryCap: 500 });
}

describe('iterationSlice — recordIterationEvent', () => {
  beforeEach(reset);

  it('creates one record per iteration with per-node entries', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [F('L', 0, 'host0')], { state: 'running' });
    rec('A', [F('L', 0, 'host0')], { state: 'success', duration: 12 });
    rec('A', [F('L', 1, 'host1')], { state: 'success', duration: 15 });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(records).toHaveLength(2);
    expect(records[0].i).toBe(0);
    expect(records[0].label).toBe('host0');
    expect(records[0].nodes.get('A')).toMatchObject({ state: 'success', duration: 12 });
    expect(records[1].i).toBe(1);
    expect(useFlowStore.getState().totalIterations.get('L')).toBe(2);
  });

  it('writes to every frame on the stack — ancestors aggregate with sticky error', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    // Outer iteration 0, inner iterations 0..1; node X errors in inner 0, succeeds in inner 1.
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'error' });
    rec('X', [F('OUT', 0), F('IN', 1)], { state: 'success', duration: 9 });

    const outer = useFlowStore.getState().iterationLog.get('OUT')![0];
    const inner = useFlowStore.getState().iterationLog.get('IN')!;

    // Inner records: exact per-iteration values.
    expect(inner[0].nodes.get('X')!.state).toBe('error');
    expect(inner[1].nodes.get('X')!.state).toBe('success');
    // Outer record aggregates: error is sticky even after a later success.
    expect(outer.nodes.get('X')!.state).toBe('error');
    expect(outer.failed).toBe(true);
    expect(inner[0].failed).toBe(true);
    expect(inner[1].failed).toBe(false);
    // Parent links point at the outer record's seq.
    expect(inner[0].parent).toEqual({ loopId: 'OUT', seq: outer.seq });
  });

  it('restarted inner loops start NEW records, not merged ones', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'success' });
    rec('X', [F('OUT', 1), F('IN', 0)], { state: 'success' }); // inner i restarts at 0

    const inner = useFlowStore.getState().iterationLog.get('IN')!;
    expect(inner).toHaveLength(2);
    expect(inner[0].i).toBe(0);
    expect(inner[1].i).toBe(0);
    expect(inner[0].parent!.seq).not.toBe(inner[1].parent!.seq);
  });

  it('evicts oldest records past the cap and keeps the true total', () => {
    useFlowStore.setState({ iterationHistoryCap: 3 });
    const rec = useFlowStore.getState().recordIterationEvent;
    for (let i = 0; i < 5; i++) rec('A', [F('L', i)], { state: 'success' });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(records.map((r) => r.i)).toEqual([2, 3, 4]);
    expect(useFlowStore.getState().totalIterations.get('L')).toBe(5);
  });

  it('ignores malformed frames and empty stacks', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [], { state: 'success' });
    rec('A', [{ loopId: 'L', i: -1 } as IterationFrameMsg], { state: 'success' });
    expect(useFlowStore.getState().iterationLog.size).toBe(0);
  });
});

describe('iterationSlice — selections', () => {
  beforeEach(reset);

  it('selecting an outer iteration resets descendant selections', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'success' });
    rec('X', [F('OUT', 1), F('IN', 0)], { state: 'success' });

    const st = useFlowStore.getState();
    const innerSeq = st.iterationLog.get('IN')![0].seq;
    st.setIterationSelection('IN', innerSeq);
    expect(useFlowStore.getState().iterationSelections.get('IN')).toBe(innerSeq);

    const outerSeq1 = st.iterationLog.get('OUT')![1].seq;
    useFlowStore.getState().setIterationSelection('OUT', outerSeq1);
    expect(useFlowStore.getState().iterationSelections.get('IN')).toBeNull();
  });

  it('selecting an inner iteration pulls every ancestor to the containing iteration', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'success' });
    rec('X', [F('OUT', 1), F('IN', 0)], { state: 'success' });

    const st = useFlowStore.getState();
    const secondInner = st.iterationLog.get('IN')![1];
    st.setIterationSelection('IN', secondInner.seq);

    const outerRecords = useFlowStore.getState().iterationLog.get('OUT')!;
    expect(useFlowStore.getState().iterationSelections.get('OUT')).toBe(outerRecords[1].seq);
  });

  it('clearIterations wipes log, selections, and totals', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [F('L', 0)], { state: 'success' });
    useFlowStore.getState().setIterationSelection('L', useFlowStore.getState().iterationLog.get('L')![0].seq);

    useFlowStore.getState().clearIterations();

    expect(useFlowStore.getState().iterationLog.size).toBe(0);
    expect(useFlowStore.getState().iterationSelections.size).toBe(0);
    expect(useFlowStore.getState().totalIterations.size).toBe(0);
  });
});
