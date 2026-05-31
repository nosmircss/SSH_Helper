import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

// ── Minimal stubs for @xyflow/react ──────────────────────────────────────────
vi.mock('@xyflow/react', () => ({
  Handle: ({ type, position, id, style }: any) =>
    React.createElement('div', { 'data-testid': `handle-${type}-${id ?? position}`, style }),
  Position: { Top: 'top', Bottom: 'bottom', Right: 'right', Left: 'left' },
}));

// ── Stub the store (returns defaults so hooks never throw) ───────────────────
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) =>
    selector({
      toggleBreakpoint: vi.fn(),
      blockTimings: new Map(),
      heatmapEnabled: false,
      reducedMotion: false,
      loopIterations: new Map(),
      branchTaken: new Map(),
    }),
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
});
