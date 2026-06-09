import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

const { bridgeCleanup, initBridge } = vi.hoisted(() => {
  const bridgeCleanup = vi.fn();
  const initBridge = vi.fn(() => bridgeCleanup);
  return { bridgeCleanup, initBridge };
});
vi.mock('../stores/runOutputWindowBridge', () => ({ initRunOutputWindowBridge: initBridge }));

const mock = vi.hoisted(() => ({
  state: {
    runOutput: '', isRunning: false, runOutputColor: true, runOutputWrap: false, runOutputFollow: true,
    runOutputPoppedOut: false,
    setRunOutputPoppedOut: vi.fn((v: boolean) => { mock.state.runOutputPoppedOut = v; }),
    toggleRunOutputColor: vi.fn(), toggleRunOutputWrap: vi.fn(), toggleRunOutputFollow: vi.fn(),
    openRunOutputWindow: vi.fn(),
  } as any,
}));
vi.mock('../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputWindowApp from '../RunOutputWindowApp';

describe('RunOutputWindowApp', () => {
  beforeEach(() => { mock.state.runOutputPoppedOut = false; vi.clearAllMocks(); });

  it('renders the console and inits the window bridge', () => {
    render(<RunOutputWindowApp />);
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
    expect(initBridge).toHaveBeenCalledTimes(1);
  });

  it('marks itself popped-out so the console hides its own Pop out button', () => {
    render(<RunOutputWindowApp />);
    expect(mock.state.setRunOutputPoppedOut).toHaveBeenCalledWith(true);
  });
});
