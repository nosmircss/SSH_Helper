import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

// ── Minimal stubs for @xyflow/react (data-position exposes the handle's side) ──
vi.mock('@xyflow/react', () => ({
  Handle: ({ type, position, id, style }: any) =>
    React.createElement('div', {
      'data-testid': `handle-${type}-${id ?? position}`,
      'data-position': position,
      style,
    }),
  Position: { Top: 'top', Bottom: 'bottom', Right: 'right', Left: 'left' },
}));

// ── Stub the store (mutable so tests can vary `nodes` for branch-count selectors) ──
const mock = vi.hoisted(() => ({
  state: {
    toggleBreakpoint: () => {},
    blockTimings: new Map(),
    heatmapEnabled: false,
    reducedMotion: false,
    loopIterations: new Map(),
    branchTaken: new Map(),
    isExpanded: () => true,
    toggleExpanded: () => {},
    selectNode: () => {},
    nodes: [] as any[],
    blockWidth: 330,
    textScale: 1,
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

// ── Import the component under test ─────────────────────────────────────────
import BaseBlock from '../BaseBlock';

// Helper: minimal NodeProps shape required by BaseBlock
function renderNode(overrides: { data: any; selected?: boolean; id?: string }) {
  const props = {
    id: overrides.id ?? 'n1',
    selected: overrides.selected ?? false,
    data: overrides.data,
    // Unused NodeProps fields – pass empty/sensible defaults
    type: 'baseBlock',
    zIndex: 0,
    isConnectable: true,
    positionAbsoluteX: 0,
    positionAbsoluteY: 0,
    dragging: false,
  } as any;
  return render(React.createElement(BaseBlock, props));
}

describe('BaseBlock', () => {
  it('renders an idle node without crashing and keeps the block-node container', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByTestId('block-node')).toBeInTheDocument();
  });

  it('no longer renders the legacy accent rail (the category border carries identity now)', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.queryByTestId('node-rail')).toBeNull();
  });

  it('renders the read-only summary when expanded and hides the preview', () => {
    // store stub returns isExpanded=true
    renderNode({ data: { blockType: 'send', label: 'Send', props: { command: 'show ver', capture: 'out' } } as any });
    expect(screen.getByTestId('block-summary')).toBeInTheDocument();
    expect(screen.getByText('Edit in Properties')).toBeInTheDocument();
  });

  it('block width follows the store blockWidth setting', () => {
    mock.state.blockWidth = 700;
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByTestId('block-node').style.minWidth).toBe('700px');
    // child block: 700 - 30 = 670
    renderNode({ data: { blockType: 'send', label: 'Send', props: { _isChildOf: 'p' } } as any });
    const blocks = screen.getAllByTestId('block-node');
    expect(blocks[blocks.length - 1].style.minWidth).toBe('670px');
    mock.state.blockWidth = 330; // restore for other tests
  });

  it('renders a spine block at 330px and a child block at 300px', () => {
    const { rerender } = renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByTestId('block-node').style.minWidth).toBe('330px');
    rerender(
      React.createElement(BaseBlock, {
        id: 'n1', selected: false, type: 'baseBlock', zIndex: 0, isConnectable: true,
        positionAbsoluteX: 0, positionAbsoluteY: 0, dragging: false,
        data: { blockType: 'send', label: 'Send', props: { _isChildOf: 'p' } },
      } as any),
    );
    expect(screen.getByTestId('block-node').style.minWidth).toBe('300px');
  });

  it('block label font scales with textScale', () => {
    mock.state.textScale = 1.15;
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByText('Send').style.fontSize).toBe(`${13 * 1.15}px`); // '14.95px'
    mock.state.textScale = 1; // restore for other tests
  });
});

describe('BaseBlock — container continuation handle', () => {
  it('puts the continue handle at bottom-center for a single-branch (then-only) IF', () => {
    mock.state.nodes = [
      { id: 'if-1', data: { blockType: 'if', props: { _stepPath: 'steps/0' } } },
      { id: 't', data: { props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0' } } },
    ];
    renderNode({ id: 'if-1', data: { blockType: 'if', label: 'If', props: { _stepPath: 'steps/0' } } as any });
    expect(screen.getByTestId('handle-source-continue').getAttribute('data-position')).toBe('bottom');
  });

  it('separates the THEN (body) handle from the continue handle on a single-branch IF, and keeps continue un-rotated (straight)', () => {
    mock.state.nodes = [
      { id: 'if-1', data: { blockType: 'if', props: { _stepPath: 'steps/0' } } },
      { id: 't', data: { props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0' } } },
    ];
    renderNode({ id: 'if-1', data: { blockType: 'if', label: 'If', props: { _stepPath: 'steps/0' } } as any });
    // The THEN/body handle is shifted right toward its indented body, clear of the continue handle.
    expect(screen.getByTestId('handle-source-bottom').style.left).toBe('75%');
    // The continue handle is NOT rotated: a rotate(45) diamond offsets React Flow's connection
    // point ~2px, which bends the continuation. Un-rotated → RF centers it → renders straight.
    expect(screen.getByTestId('handle-source-continue').style.transform).toBe('');
  });

  it('also puts the continue handle at bottom-center for a multi-branch (then+else) IF (unified straight routing)', () => {
    // Option B: every container indents its branches right of the spine, so even a multi-branch IF
    // routes its continuation straight down the gutter from bottom-center — no more bottom-left
    // corridor that escaped the band.
    mock.state.nodes = [
      { id: 'if-2', data: { blockType: 'if', props: { _stepPath: 'steps/0' } } },
      { id: 't', data: { props: { _isChildOf: 'if-2', _stepPath: 'steps/0/then/0' } } },
      { id: 'e', data: { props: { _isChildOf: 'if-2', _stepPath: 'steps/0/else/0' } } },
    ];
    renderNode({ id: 'if-2', data: { blockType: 'if', label: 'If', props: { _stepPath: 'steps/0' } } as any });
    expect(screen.getByTestId('handle-source-continue').getAttribute('data-position')).toBe('bottom');
  });
});
