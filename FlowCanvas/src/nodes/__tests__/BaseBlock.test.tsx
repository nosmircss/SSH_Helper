import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

// ── Minimal stubs for @xyflow/react (data-position exposes the handle's side) ──
vi.mock('@xyflow/react', () => ({
  Handle: ({ type, position, id, style, className }: any) =>
    React.createElement('div', {
      'data-testid': `handle-${type}-${id ?? position}`,
      'data-position': position,
      className,
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
    updateNodeData: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

// ── Import the component under test ─────────────────────────────────────────
import BaseBlock, { JUST_PLACED_ANIMATION, shouldClearJustPlaced } from '../BaseBlock';

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
    // The footer keeps the defaults hint only — the "Edit in Properties" link was removed
    // (clicking the block already selects it and opens Properties).
    expect(screen.queryByText('Edit in Properties')).toBeNull();
    expect(screen.getByText(/fields at default/)).toBeInTheDocument();
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

  // The fc-handle class hangs the zoom-compensating pseudo-elements (baseblock.css) off every
  // handle, while the element's own box MUST stay at its fixed inline size — React Flow measures
  // it for edge anchors and the spine-straightness test keys on its exact center.
  it('all handles carry fc-handle and keep their fixed measured box', () => {
    renderNode({ data: { blockType: 'if', label: 'If', props: {} } as any });
    const handles = [
      screen.getByTestId('handle-target-top'),
      screen.getByTestId('handle-source-bottom'),
      screen.getByTestId('handle-source-false'),
      screen.getByTestId('handle-source-continue'),
    ];
    for (const h of handles) {
      expect(h.className).toContain('fc-handle');
      expect(parseInt(h.style.width, 10)).toBeGreaterThanOrEqual(8);
      expect(h.style.transform).toBe(''); // no scale on the element itself
    }
  });
});

describe('BaseBlock — just-placed entrance highlight', () => {
  it('applies the just-placed highlight class for a newly placed block', () => {
    mock.state.reducedMotion = false;
    renderNode({ data: { blockType: 'send', label: 'Send', props: {}, _justPlaced: true } as any });
    expect(screen.getByTestId('block-node').className).toContain('fc-just-placed');
    mock.state.reducedMotion = false; // restore
  });

  it('omits the just-placed highlight under reduced motion', () => {
    mock.state.reducedMotion = true;
    renderNode({ data: { blockType: 'send', label: 'Send', props: {}, _justPlaced: true } as any });
    expect(screen.getByTestId('block-node').className).not.toContain('fc-just-placed');
    mock.state.reducedMotion = false; // restore
  });

  // The component's onAnimationEnd handler delegates to shouldClearJustPlaced, then calls the
  // non-dirty updateNodeData(id, { _justPlaced: false }). React 19's delegated animation events
  // don't fire under jsdom (no AnimationEvent constructor), so we assert the decision predicate
  // directly — it's the load-bearing logic that decides whether to clear the flag.
  it('shouldClearJustPlaced fires only for the entrance pulse on a still-flagged block', () => {
    expect(shouldClearJustPlaced(JUST_PLACED_ANIMATION, true)).toBe(true);
  });

  it('shouldClearJustPlaced ignores an unrelated animation name', () => {
    expect(shouldClearJustPlaced('fc-exec-running', true)).toBe(false);
  });

  it('shouldClearJustPlaced ignores the entrance pulse once the flag is already cleared', () => {
    expect(shouldClearJustPlaced(JUST_PLACED_ANIMATION, false)).toBe(false);
    expect(shouldClearJustPlaced(JUST_PLACED_ANIMATION, undefined)).toBe(false);
  });
});

describe('BaseBlock — breakpoint gutter', () => {
  it('renders the breakpoint gutter on a top-level block', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByTitle('Toggle breakpoint')).toBeInTheDocument();
  });

  it('renders the breakpoint gutter on a child (nested) block so breakpoints work inside loops/containers', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: { _isChildOf: 'loop-1', _stepPath: 'steps/0/do/0' } } as any });
    expect(screen.getByTitle('Toggle breakpoint')).toBeInTheDocument();
  });

  it('toggles the breakpoint when the gutter on a nested block is clicked', () => {
    const toggle = vi.fn();
    mock.state.toggleBreakpoint = toggle;
    renderNode({ id: 'child-1', data: { blockType: 'send', label: 'Send', props: { _isChildOf: 'loop-1', _stepPath: 'steps/0/do/0' } } as any });
    fireEvent.click(screen.getByTitle('Toggle breakpoint'));
    expect(toggle).toHaveBeenCalledWith('child-1');
    mock.state.toggleBreakpoint = () => {}; // restore for other tests
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
