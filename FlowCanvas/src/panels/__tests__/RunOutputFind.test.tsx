import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act, fireEvent } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    runOutput: 'alpha\nbravo error\ncharlie\nbravo again',
    isRunning: false,
    runOutputColor: true,
    runOutputWrap: false,
    runOutputFollow: false,
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputView from '../RunOutputView';

describe('RunOutputView find', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('opens the find box when Find is clicked', () => {
    render(<RunOutputView />);
    act(() => { fireEvent.click(screen.getByTestId('run-output-btn-find')); });
    expect(screen.getByTestId('run-output-find-input')).toBeInTheDocument();
  });

  it('reports the match count for the query', () => {
    render(<RunOutputView />);
    act(() => { fireEvent.click(screen.getByTestId('run-output-btn-find')); });
    fireEvent.change(screen.getByTestId('run-output-find-input'), { target: { value: 'bravo' } });
    expect(screen.getByTestId('run-output-find-count').textContent).toContain('2');
  });
});
