import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
import React from 'react';

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (sel: (s: unknown) => unknown) =>
    sel({
      updateComment: vi.fn(),
      removeComment: vi.fn(),
      compactCommentsEnabled: true,
    }),
}));

import CommentNode from '../CommentNode';

describe('CommentNode', () => {
  it('renders a compact pill for an anchored comment-kind note', () => {
    const props = {
      id: 'c1',
      data: {
        commentId: 'c1',
        text: 'Get hostname',
        kind: 'comment',
        anchor: { type: 'leading', stepPath: 'steps/0' },
      },
      type: 'comment',
      zIndex: 0,
      isConnectable: true,
      positionAbsoluteX: 0,
      positionAbsoluteY: 0,
      dragging: false,
      selected: false,
    } as never;

    const { container } = render(React.createElement(CommentNode, props));
    const pill = container.querySelector('[data-testid="comment-pill"]');
    expect(pill).not.toBeNull();
    expect(pill!.textContent).toContain('Get hostname');
  });

  it('renders the full box for a sticky note', () => {
    const props = {
      id: 'c2',
      data: {
        commentId: 'c2',
        text: 'My sticky',
        kind: 'sticky',
      },
      type: 'comment',
      zIndex: 0,
      isConnectable: true,
      positionAbsoluteX: 0,
      positionAbsoluteY: 0,
      dragging: false,
      selected: false,
    } as never;

    const { container } = render(React.createElement(CommentNode, props));
    const pill = container.querySelector('[data-testid="comment-pill"]');
    const full = container.querySelector('[data-testid="comment-full"]');
    expect(pill).toBeNull();
    expect(full).not.toBeNull();
  });
});
