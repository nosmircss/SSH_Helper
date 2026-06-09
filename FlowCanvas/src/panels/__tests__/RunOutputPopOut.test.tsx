import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    runOutputPoppedOut: true,
    toggleRunOutputPoppedOut: vi.fn(),
    runOutput: 'hello world',
    isRunning: false,
    runOutputColor: true, runOutputWrap: false, runOutputFollow: false,
    toggleRunOutputColor: vi.fn(), toggleRunOutputWrap: vi.fn(), toggleRunOutputFollow: vi.fn(),
  } as any,
}));
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputPopOut from '../RunOutputPopOut';

describe('RunOutputPopOut', () => {
  beforeEach(() => { mock.state.runOutputPoppedOut = true; vi.clearAllMocks(); });

  it('renders a floating overlay containing the console', () => {
    render(<RunOutputPopOut />);
    expect(screen.getByTestId('run-output-popout')).toBeInTheDocument();
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
  });

  it('renders nothing when not popped out', () => {
    mock.state.runOutputPoppedOut = false;
    const { container } = render(<RunOutputPopOut />);
    expect(container.firstChild).toBeNull();
  });

  it('the dock button calls toggleRunOutputPoppedOut', () => {
    render(<RunOutputPopOut />);
    fireEvent.click(screen.getByTestId('run-output-popout-dock'));
    expect(mock.state.toggleRunOutputPoppedOut).toHaveBeenCalled();
  });
});
