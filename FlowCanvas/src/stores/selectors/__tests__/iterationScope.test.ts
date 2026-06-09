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

/** Foreach LO wrapping foreach LI wrapping a single leaf. */
function seedNestedGraph() {
  useFlowStore.setState({
    nodes: [
      node('LO', 'foreach', { _stepPath: 'steps/0' }),
      node('LI', 'foreach', { _stepPath: 'steps/0/do/0', _isChildOf: 'LO', _branchLabel: 'do' }),
      node('leaf', 'ssh', { _stepPath: 'steps/0/do/0/do/0', _isChildOf: 'LI', _branchLabel: 'do' }),
    ],
    edges: [edge('eLOLI', 'LO', 'LI'), edge('eLIleaf', 'LI', 'leaf')],
  });
}

function runNestedIterations() {
  const st = useFlowStore.getState();
  // Outer iteration 0: inner iterates twice; then the inner loop's OWN completion
  // lands on the OUTER record (the executor pops the inner frame BEFORE the loop's
  // completion event — C# pop-before-completion semantics).
  st.recordIterationEvent('leaf', [F('LO', 0), F('LI', 0)], { state: 'success' });
  st.recordIterationEvent('leaf', [F('LO', 0), F('LI', 1)], { state: 'success' });
  st.recordIterationEvent('LI', [F('LO', 0)], { state: 'success' });
  // Outer iteration 1: inner iterates once.
  st.recordIterationEvent('leaf', [F('LO', 1), F('LI', 0)], { state: 'success' });
  st.recordIterationEvent('LI', [F('LO', 1)], { state: 'success' });
}

beforeEach(() => {
  useFlowStore.getState().clearIterations();
  useFlowStore.getState().clearExecution();
  useFlowStore.setState({ nodes: [], edges: [], pathVisible: true, iterationHistoryCap: 500 });
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

describe('nested loops', () => {
  it('resolves the INNERMOST selected ancestor; outer-only selection governs via the outer record', () => {
    seedNestedGraph();
    runNestedIterations();
    const st = useFlowStore.getState();
    const inner = st.iterationLog.get('LI')!; // [outer0/i0, outer0/i1, outer1/i0]
    const outer = st.iterationLog.get('LO')!; // [i0, i1]
    expect(inner).toHaveLength(3);
    expect(outer).toHaveLength(2);

    // Inner selection wins for the leaf (even though inner-pulls-outer also selects LO).
    st.setIterationSelection('LI', inner[1].seq);
    const withInner = useFlowStore.getState();
    expect(withInner.iterationSelections.get('LO')).toBe(outer[0].seq); // pulled to the containing iteration
    expect(selectIterationScope(withInner, 'leaf')?.seq).toBe(inner[1].seq);

    // Selecting the outer loop resets the inner to ALL → the OUTER record governs the leaf.
    withInner.setIterationSelection('LO', outer[0].seq);
    const outerOnly = useFlowStore.getState();
    expect(outerOnly.iterationSelections.get('LI')).toBeNull();
    expect(selectIterationScope(outerOnly, 'leaf')?.seq).toBe(outer[0].seq);

    // The edge INTO the inner loop reflects the outer scope: LI completed in outer iter 0.
    expect(selectEdgePathStatus(outerOnly, 'eLOLI')).toBe('on-path');
  });

  it('constrains selectVisibleIterations to records under the selected outer iteration', () => {
    seedNestedGraph();
    runNestedIterations();
    const st = useFlowStore.getState();
    const outer = st.iterationLog.get('LO')!;
    st.setIterationSelection('LO', outer[0].seq);

    const visible = selectVisibleIterations(useFlowStore.getState(), 'LI');
    expect(visible).toHaveLength(2); // NOT the third record from outer iteration 1
    expect(visible.map((r) => r.i)).toEqual([0, 1]);
    expect(visible.every((r) => r.parent?.seq === outer[0].seq)).toBe(true);
  });

  it('tolerates dangling parent refs after cap eviction (no throw, no infinite walk)', () => {
    useFlowStore.setState({ iterationHistoryCap: 1 });
    seedNestedGraph();
    const st = useFlowStore.getState();
    // Outer iteration 0 runs the inner loop once...
    st.recordIterationEvent('leaf', [F('LO', 0), F('LI', 0)], { state: 'success' });
    // ...outer iteration 1 skips it (event at outer depth only). cap=1 evicts the
    // outer iter-0 record, leaving the inner record's parent.seq dangling.
    st.recordIterationEvent('LI', [F('LO', 1)], { state: 'skipped' });

    const after = useFlowStore.getState();
    expect(after.iterationLog.get('LO')).toHaveLength(1); // iter-0 record evicted
    const innerRecs = after.iterationLog.get('LI')!;
    expect(innerRecs).toHaveLength(1);
    expect(after.iterationLog.get('LO')![0].seq).not.toBe(innerRecs[0].parent!.seq); // parent dangles

    // No selection: unconstrained — the dangling record is still listed.
    expect(selectVisibleIterations(after, 'LI')).toHaveLength(1);

    // Governing selection active: the dangling record's parent walk dead-ends → filtered out.
    after.setIterationSelection('LO', after.iterationLog.get('LO')![0].seq);
    expect(selectVisibleIterations(useFlowStore.getState(), 'LI')).toEqual([]);
  });
});

describe('branch arms under iteration scope', () => {
  it('marks the untaken arm untaken (not idle) for the selected iteration', () => {
    useFlowStore.setState({
      nodes: [
        node('L', 'foreach', { _stepPath: 'steps/0' }),
        node('IFN', 'if', { _stepPath: 'steps/0/do/0', _isChildOf: 'L', _branchLabel: 'do' }),
        node('THEN_C', 'ssh', { _stepPath: 'steps/0/do/0/then/0', _isChildOf: 'IFN', _branchLabel: 'then' }),
        node('ELSE_C', 'ssh', { _stepPath: 'steps/0/do/0/else/0', _isChildOf: 'IFN', _branchLabel: 'else' }),
      ],
      edges: [
        edge('eLIF', 'L', 'IFN'),
        edge('eThen', 'IFN', 'THEN_C'), // imported-style arm: identity via _isChildOf
        { ...edge('eElse', 'IFN', 'ELSE_C'), data: { branchPath: 'else' } }, // canvas-style arm
      ],
    });

    const st = useFlowStore.getState();
    // Iteration 0: the if runs and takes 'then'; ELSE_C never runs.
    st.recordIterationEvent('IFN', [F('L', 0)], { state: 'success', branchTaken: 'then' });
    st.recordIterationEvent('THEN_C', [F('L', 0)], { state: 'success' });
    st.setIterationSelection('L', useFlowStore.getState().iterationLog.get('L')![0].seq);

    const after = useFlowStore.getState();
    expect(selectEdgePathStatus(after, 'eThen')).toBe('on-path');
    expect(selectEdgePathStatus(after, 'eElse')).toBe('untaken'); // branch arm fades, never 'idle'
  });
});
