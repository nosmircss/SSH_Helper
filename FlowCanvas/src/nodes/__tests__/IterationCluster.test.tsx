import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { render, screen, fireEvent } from '@testing-library/react';
import { useFlowStore } from '../../stores/useFlowStore';
import type { IterationFrameMsg } from '../../communication-message-types';
import type { BranchBand } from '../../utils/branchBands';
import IterationCluster from '../IterationCluster';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

const band: BranchBand = {
  id: 'L::do', parentId: 'L', branchKey: 'do',
  x: 0, y: 0, width: 320, height: 120,
  colorVar: 'var(--fc-branch-warning)', depth: 0, memberIds: [],
};

function seed(iterations: number, failedAt: number[] = []) {
  useFlowStore.getState().clearIterations();
  useFlowStore.setState({ isRunning: false });
  const rec = useFlowStore.getState().recordIterationEvent;
  for (let i = 0; i < iterations; i++) {
    rec('A', [F('L', i, `host${i}`)], { state: failedAt.includes(i) ? 'error' : 'success' });
  }
}

describe('IterationCluster', () => {
  beforeEach(() => seed(3));

  it('renders nothing while a run is in progress', () => {
    useFlowStore.setState({ isRunning: true });
    render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iteration-cluster')).toBeNull();
  });

  it('renders nothing when the loop has no recorded iterations', () => {
    useFlowStore.getState().clearIterations();
    render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iteration-cluster')).toBeNull();
  });

  it('shows the count in ALL mode and steps into iteration 1 on ▶', () => {
    render(<IterationCluster band={band} />);
    expect(screen.getByTestId('iter-counter').textContent).toBe('3');

    fireEvent.click(screen.getByTestId('iter-next'));
    expect(screen.getByTestId('iter-counter').textContent).toBe('1/3');
    expect(screen.getByTestId('iter-label').textContent).toBe('host0');

    const sel = useFlowStore.getState().iterationSelections.get('L');
    expect(sel).toBe(useFlowStore.getState().iterationLog.get('L')![0].seq);
  });

  it('ALL chip returns to the aggregate view', () => {
    render(<IterationCluster band={band} />);
    fireEvent.click(screen.getByTestId('iter-next'));
    fireEvent.click(screen.getByTestId('iter-all'));
    expect(useFlowStore.getState().iterationSelections.get('L')).toBeNull();
    expect(screen.getByTestId('iter-counter').textContent).toBe('3');
  });

  it('⚠ chip appears only with failures and jumps to the failed iteration', () => {
    const first = render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iter-fail')).toBeNull();
    first.unmount();

    seed(3, [1]);
    render(<IterationCluster band={band} />);
    const fail = screen.getByTestId('iter-fail');
    expect(fail.textContent).toContain('1');

    fireEvent.click(fail);
    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(useFlowStore.getState().iterationSelections.get('L')).toBe(records[1].seq);
  });

  it('shows the eviction hint when records were dropped under the history cap', () => {
    const prevCap = useFlowStore.getState().iterationHistoryCap;
    try {
      useFlowStore.setState({ iterationHistoryCap: 2 });
      seed(5);
      render(<IterationCluster band={band} />);
      expect(screen.getByTestId('iter-counter').textContent).toBe('2');
      expect(screen.getByTestId('iter-evicted').textContent).toBe('of 5');
    } finally {
      useFlowStore.setState({ iterationHistoryCap: prevCap });
    }
  });

  it('falls back to #N when an iteration has no label (while/repeat)', () => {
    useFlowStore.getState().clearIterations();
    useFlowStore.getState().recordIterationEvent('A', [{ loopId: 'L', i: 0 }], { state: 'success' });
    render(<IterationCluster band={band} />);
    fireEvent.click(screen.getByTestId('iter-next'));
    expect(screen.getByTestId('iter-label').textContent).toBe('#1');
  });

  it('◀ from ALL jumps to the last iteration and ▶ clamps at the end', () => {
    render(<IterationCluster band={band} />);
    fireEvent.click(screen.getByTestId('iter-prev'));
    expect(screen.getByTestId('iter-counter').textContent).toBe('3/3');
    fireEvent.click(screen.getByTestId('iter-next'));
    expect(screen.getByTestId('iter-counter').textContent).toBe('3/3');
  });
});

describe('IterationCluster — scrubber', () => {
  it('appears above 20 iterations, not at 20', () => {
    seed(20);
    const first = render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iter-scrubber')).toBeNull();
    first.unmount();

    seed(21);
    render(<IterationCluster band={band} />);
    expect(screen.getByTestId('iter-scrubber')).not.toBeNull();
  });

  it('buckets ticks at 60 max and clicking a tick selects its first iteration', () => {
    seed(200, [150]);
    render(<IterationCluster band={band} />);
    const ticks = screen.getAllByTestId('iter-tick');
    expect(ticks.length).toBeLessThanOrEqual(60);

    fireEvent.click(ticks[0]);
    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(useFlowStore.getState().iterationSelections.get('L')).toBe(records[0].seq);
  });

  it('pressing the scrubber begins a scrub and selects a bucket', () => {
    seed(100);
    render(<IterationCluster band={band} />);
    const scrubber = screen.getByTestId('iter-scrubber');
    // jsdom getBoundingClientRect returns zeros → frac 0 → bucket 0 → records[0].
    fireEvent.pointerDown(scrubber, { clientX: 0, pointerId: 1 });
    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(useFlowStore.getState().iterationSelections.get('L')).toBe(records[0].seq);
  });
});
