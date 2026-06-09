import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import type { IterationFrameMsg } from '../../../communication-message-types';
import { selectIterationScope, selectVisibleIterations, LOOP_TYPES } from '../iterationScope';
import { selectEdgePathStatus } from '../edgePath';
import type { Node, Edge } from '@xyflow/react';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

const node = (id: string, blockType: string, props: Record<string, unknown> = {}): Node => ({
  id,
  type: 'flowBlock',
  position: { x: 0, y: 0 },
  data: { blockType, props },
});

const edge = (id: string, source: string, target: string): Edge => ({ id, source, target });

/** Foreach L wrapping A -> B, with a plain successor T after the loop. */
function seedGraph() {
  useFlowStore.setState({
    nodes: [
      node('L', 'foreach', { _stepPath: 'steps/0' }),
      node('A', 'ssh', { _stepPath: 'steps/0/do/0', _isChildOf: 'L', _branchLabel: 'do' }),
      node('B', 'print', { _stepPath: 'steps/0/do/1', _isChildOf: 'L', _branchLabel: 'do' }),
      node('T', 'ssh', { _stepPath: 'steps/1' }),
    ],
    edges: [edge('eLA', 'L', 'A'), edge('eAB', 'A', 'B'), edge('eLT', 'L', 'T')],
  });
}

function runTwoIterations() {
  const st = useFlowStore.getState();
  // Iteration 0: A and B both run and succeed.
  st.recordIterationEvent('A', [F('L', 0, 'h0')], { state: 'success', duration: 5 });
  st.recordIterationEvent('B', [F('L', 0, 'h0')], { state: 'success', duration: 7 });
  // Iteration 1: only A runs (B never reached).
  st.recordIterationEvent('A', [F('L', 1, 'h1')], { state: 'success', duration: 6 });
  // Aggregate state as the run would leave it.
  st.setBlockState('L', 'success');
  st.setBlockState('A', 'success');
  st.setBlockState('B', 'success');
  st.setBlockState('T', 'success');
  st.setLoopIteration('L', 2);
}

beforeEach(() => {
  useFlowStore.getState().clearIterations();
  useFlowStore.getState().clearExecution();
  useFlowStore.setState({ nodes: [], edges: [], pathVisible: true });
});

describe('selectIterationScope', () => {
  it('returns null with no selections (aggregate view)', () => {
    seedGraph();
    runTwoIterations();
    expect(selectIterationScope(useFlowStore.getState(), 'A')).toBeNull();
  });

  it('returns the selected record for nodes inside the loop, null outside', () => {
    seedGraph();
    runTwoIterations();
    const st = useFlowStore.getState();
    const rec0 = st.iterationLog.get('L')![0];
    st.setIterationSelection('L', rec0.seq);

    const after = useFlowStore.getState();
    expect(selectIterationScope(after, 'A')?.seq).toBe(rec0.seq);
    expect(selectIterationScope(after, 'B')?.seq).toBe(rec0.seq);
    expect(selectIterationScope(after, 'T')).toBeNull();   // outside the loop
    expect(selectIterationScope(after, 'L')).toBeNull();   // the loop node itself is governed by ITS ancestors
  });
});

describe('selectVisibleIterations', () => {
  it('is unconstrained without ancestor selections and exposes LOOP_TYPES', () => {
    seedGraph();
    runTwoIterations();
    expect(LOOP_TYPES.has('foreach')).toBe(true);
    expect(selectVisibleIterations(useFlowStore.getState(), 'L')).toHaveLength(2);
  });
});

describe('selectEdgePathStatus under iteration scope', () => {
  it('scopes loop-body edges to the selected iteration', () => {
    seedGraph();
    runTwoIterations();
    const st = useFlowStore.getState();

    // Aggregate: everything reached at least once → on-path.
    expect(selectEdgePathStatus(st, 'eAB')).toBe('on-path');

    // Iteration 0: B was reached → eAB on-path.
    st.setIterationSelection('L', st.iterationLog.get('L')![0].seq);
    expect(selectEdgePathStatus(useFlowStore.getState(), 'eAB')).toBe('on-path');

    // Iteration 1: B never ran → eAB drops out of the path.
    useFlowStore.getState().setIterationSelection('L', useFlowStore.getState().iterationLog.get('L')![1].seq);
    expect(selectEdgePathStatus(useFlowStore.getState(), 'eAB')).not.toBe('on-path');

    // The edge to T (outside the loop) keeps its aggregate status either way.
    expect(selectEdgePathStatus(useFlowStore.getState(), 'eLT')).toBe('on-path');
  });
});
