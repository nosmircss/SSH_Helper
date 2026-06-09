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
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
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

  it('renders plain text (no per-line kinds) when color is off', () => {
    mock.state.runOutput = '############### CONNECTED ###############\nplain';
    mock.state.runOutputColor = false;
    render(<RunOutputView />);
    expect(screen.queryAllByTestId('run-output-line')).toHaveLength(0);
    expect(screen.getByTestId('run-output-plain').textContent).toContain('############### CONNECTED ###############');
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
});
