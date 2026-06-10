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
  beforeEach(() => { mock.state.runOutputColor = true; vi.clearAllMocks(); });

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

  it('clears the query (and highlights) when the find box is closed', () => {
    render(<RunOutputView />);
    act(() => { fireEvent.click(screen.getByTestId('run-output-btn-find')); });
    fireEvent.change(screen.getByTestId('run-output-find-input'), { target: { value: 'bravo' } });
    expect(document.querySelectorAll('mark').length).toBeGreaterThan(0);
    act(() => { fireEvent.click(screen.getByTestId('run-output-btn-find')); }); // close
    expect(screen.queryByTestId('run-output-find-input')).toBeNull();
    expect(document.querySelectorAll('mark').length).toBe(0);
  });

  it('highlights matches in plain (color-off) mode too', () => {
    mock.state.runOutputColor = false;
    render(<RunOutputView />);
    act(() => { fireEvent.click(screen.getByTestId('run-output-btn-find')); });
    fireEvent.change(screen.getByTestId('run-output-find-input'), { target: { value: 'bravo' } });
    expect(document.querySelectorAll('mark').length).toBe(2);
  });
});
