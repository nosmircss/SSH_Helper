import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    runOutput: '',
    isRunning: false,
    runOutputColor: true,
    runOutputWrap: false,
    runOutputFollow: true,
    runOutputPoppedOut: false,
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
    toggleRunOutputPoppedOut: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputView from '../RunOutputView';

describe('RunOutputView', () => {
  beforeEach(() => {
    mock.state.runOutput = '';
    mock.state.isRunning = false;
    mock.state.runOutputColor = true;
    mock.state.runOutputPoppedOut = false;
    vi.clearAllMocks();
  });

  it('shows an empty-state hint when there is no output', () => {
    render(<RunOutputView />);
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
    expect(screen.getByText(/no run output yet/i)).toBeInTheDocument();
  });

  it('renders one element per line with a data-kind when color is on', () => {
    mock.state.runOutput = '############### CONNECTED TO h ###############\nVersion 1.0\nCommand fail. Return code -1';
    render(<RunOutputView />);
    const lines = screen.getAllByTestId('run-output-line');
    expect(lines).toHaveLength(3);
    expect(lines[0].getAttribute('data-kind')).toBe('banner');
    expect(lines[1].getAttribute('data-kind')).toBe('normal');
    expect(lines[2].getAttribute('data-kind')).toBe('error');
  });

  it('renders per-line divs with no kind classification when color is off', () => {
    mock.state.runOutput = '############### CONNECTED ###############\nplain';
    mock.state.runOutputColor = false;
    render(<RunOutputView />);
    const lines = screen.getAllByTestId('run-output-line');
    expect(lines).toHaveLength(2);
    expect(lines.every((l) => l.getAttribute('data-kind') === 'plain')).toBe(true);
    expect(lines[0].textContent).toContain('############### CONNECTED ###############');
  });

  it('preserves blank lines (does not collapse them) in color mode', () => {
    mock.state.runOutput = 'a\n\nb';
    render(<RunOutputView />);
    expect(screen.getAllByTestId('run-output-line')).toHaveLength(3);
  });

  it('strips the trailing CR of CRLF lines so blank lines survive', () => {
    mock.state.runOutput = 'a\r\n\r\nb'; // CRLF with a blank line in the middle
    render(<RunOutputView />);
    const lines = screen.getAllByTestId('run-output-line');
    expect(lines).toHaveLength(3);
    expect(lines[0].textContent).toBe('a'); // no dangling \r
    expect(lines[2].textContent).toBe('b');
  });

  it('shows the LIVE indicator only while running', () => {
    mock.state.isRunning = true;
    render(<RunOutputView />);
    expect(screen.getByTestId('run-output-live')).toBeInTheDocument();
  });

  it('Color button toggles the color pref', () => {
    render(<RunOutputView />);
    screen.getByTestId('run-output-btn-color').click();
    expect(mock.state.toggleRunOutputColor).toHaveBeenCalledTimes(1);
  });

  it('shows the Pop out button when docked, hides it when popped out', () => {
    const { rerender } = render(<RunOutputView />);
    expect(screen.getByTestId('run-output-btn-popout')).toBeInTheDocument();
    mock.state.runOutputPoppedOut = true;
    rerender(<RunOutputView />);
    expect(screen.queryByTestId('run-output-btn-popout')).toBeNull();
  });
});
