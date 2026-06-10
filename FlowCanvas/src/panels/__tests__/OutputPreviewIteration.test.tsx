import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { IterationRecord } from '../../stores/slices/iterationSlice';

/**
 * Iteration-scoped Block Output honesty: when a governing iteration record exists, the panel
 * must (1) distinguish "(not reached in this iteration)" — no entry for the node — from
 * "(no output in this iteration)" — entry present but it produced nothing, never falling back
 * to the latest output, (2) show an iteration-context chip, and (3) show a loop-container hint
 * when the selected node is the loop itself.
 *
 * selectIterationScope / selectVisibleIterations / LOOP_TYPES run against the real store state,
 * so the mock provides a real iterationLog/iterationSelections plus a getState() (the chip memo
 * reads the live state) — not just selector stubs.
 */

const F = 'F';
const A = 'A';

function record(seq: number, i: number, label: string, nodes: Map<string, { outputIdx?: number }>): IterationRecord {
  return { seq, i, label, failed: false, parent: null, nodes: nodes as IterationRecord['nodes'] };
}

const mock = vi.hoisted(() => ({ state: {} as any }));

// Build a fresh store state for one scenario.
//  - aOutputIdx set       → A ran and produced output at that index
//  - reachedNoOutput true → A has an iteration entry but no output (reached-but-silent)
//  - neither              → A has no entry at all (never reached this iteration)
function buildState(opts: { selectedNode: string; aOutputIdx?: number; reachedNoOutput?: boolean; selectedSeq: number }) {
  const nodes: Map<string, { outputIdx?: number }> =
    opts.aOutputIdx != null ? new Map([[A, { outputIdx: opts.aOutputIdx }]])
    : opts.reachedNoOutput ? new Map([[A, {}]])
    : new Map();
  const rec = record(opts.selectedSeq, opts.selectedSeq - 1, `host${opts.selectedSeq - 1}`, nodes);
  const iterationLog = new Map<string, IterationRecord[]>([[F, [rec]]]);
  const iterationSelections = new Map<string, number | null>([[F, opts.selectedSeq]]);
  return {
    blockOutputs: new Map([[A, [{ text: 'LATEST disk output', stepType: 'send' }]]]),
    togglePanel: vi.fn(),
    panelSizes: { outputHeight: 200 },
    setPanelSize: vi.fn(),
    outputTab: 'block' as const,
    setOutputTab: vi.fn(),
    runOutputUnread: false,
    runOutputPoppedOut: false,
    closeRunOutputWindow: vi.fn(),
    nodes: [
      { id: F, data: { blockType: 'foreach', label: 'For each host' } },
      { id: A, data: { blockType: 'send', label: 'Check disk', props: { _isChildOf: F, _stepPath: 'steps/0/do/0' } } },
    ],
    iterationLog,
    iterationSelections,
  };
}

vi.mock('../../stores/useFlowStore', () => {
  const useFlowStore = (selector: (s: any) => any) => selector(mock.state);
  (useFlowStore as any).getState = () => mock.state;
  return { useFlowStore };
});

import OutputPreview from '../OutputPreview';

describe('OutputPreview iteration honesty', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows "(not reached in this iteration)" instead of the latest when the node never ran', () => {
    mock.state = buildState({ selectedNode: A, aOutputIdx: undefined, selectedSeq: 2 });
    render(<OutputPreview output="LATEST disk output" nodeId={A} />);
    expect(screen.getByTestId('iter-output-empty')).toHaveTextContent('(not reached in this iteration)');
    expect(screen.queryByText('LATEST disk output')).toBeNull();
    // The panel is still iteration-scoped, so the context chip shows alongside the empty note.
    expect(screen.getByTestId('iter-output-chip')).toBeInTheDocument();
    expect(screen.getByTestId('iter-output-chip')).toHaveTextContent('1/1');
    expect(screen.getByTestId('iter-output-chip')).toHaveTextContent('host1');
  });

  it('shows "(no output in this iteration)" when the node ran but produced nothing', () => {
    mock.state = buildState({ selectedNode: A, reachedNoOutput: true, selectedSeq: 2 });
    render(<OutputPreview output="LATEST disk output" nodeId={A} />);
    expect(screen.getByTestId('iter-output-empty')).toHaveTextContent('(no output in this iteration)');
    expect(screen.queryByText('LATEST disk output')).toBeNull();
    expect(screen.getByTestId('iter-output-chip')).toBeInTheDocument();
  });

  it('shows the per-iteration output (not the empty note) when the node ran that iteration', () => {
    mock.state = buildState({ selectedNode: A, aOutputIdx: 0, selectedSeq: 1 });
    render(<OutputPreview output="LATEST disk output" nodeId={A} />);
    expect(screen.queryByTestId('iter-output-empty')).toBeNull();
    expect(screen.getByText('LATEST disk output')).toBeInTheDocument();
    expect(screen.getByTestId('iter-output-chip')).toBeInTheDocument();
  });

  it('shows the loop-container hint when the selected node is the loop itself', () => {
    mock.state = buildState({ selectedNode: F, selectedSeq: 1 });
    // The loop node carries no own output; selecting it should surface the guidance hint.
    render(<OutputPreview output="" nodeId={F} />);
    expect(screen.getByTestId('iter-output-loophint')).toBeInTheDocument();
    expect(screen.queryByTestId('iter-output-empty')).toBeNull();
  });
});
