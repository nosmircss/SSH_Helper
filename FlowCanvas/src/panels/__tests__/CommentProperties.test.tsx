import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@testing-library/react';
import React from 'react';

const updateComment = vi.fn();
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (sel: (s: unknown) => unknown) => sel({ updateComment }),
}));

import { CommentProperties } from '../CommentProperties';

describe('CommentProperties', () => {
  it('edits comment text and kind', () => {
    const { getByTestId } = render(
      React.createElement(CommentProperties, {
        nodeId: 'c1',
        data: { commentId: 'c1', text: 'a', kind: 'comment' },
      }),
    );
    fireEvent.change(getByTestId('comment-text-input'), { target: { value: 'b' } });
    expect(updateComment).toHaveBeenCalledWith('c1', { text: 'b' });
    fireEvent.change(getByTestId('comment-kind-input'), { target: { value: 'sticky' } });
    expect(updateComment).toHaveBeenCalledWith('c1', { kind: 'sticky' });
  });
});
