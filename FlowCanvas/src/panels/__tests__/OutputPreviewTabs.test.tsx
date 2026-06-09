import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    blockOutputs: new Map(),
    togglePanel: vi.fn(),
    panelSizes: { outputHeight: 200 },
    setPanelSize: vi.fn(),
    outputTab: 'block' as 'block' | 'run',
    setOutputTab: vi.fn((t: 'block' | 'run') => { mock.state.outputTab = t; }),
    runOutputUnread: false,
    runOutputPoppedOut: false,
    closeRunOutputWindow: vi.fn(),
    // RunOutputView selectors (used when the run tab is active):
    runOutput: '',
    isRunning: false,
    runOutputColor: true,
    runOutputWrap: false,
    runOutputFollow: true,
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
    openRunOutputWindow: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import OutputPreview from '../OutputPreview';

function renderPanel() {
  return render(<OutputPreview output="" />);
}

describe('OutputPreview tabs', () => {
  beforeEach(() => {
    mock.state.outputTab = 'block';
    mock.state.runOutputUnread = false;
    mock.state.runOutputPoppedOut = false;
    vi.clearAllMocks();
  });

  it('renders both tab buttons', () => {
    renderPanel();
    expect(screen.getByTestId('output-tab-block')).toBeInTheDocument();
    expect(screen.getByTestId('output-tab-run')).toBeInTheDocument();
  });

  it('shows the block view by default, not the run console', () => {
    renderPanel();
    expect(screen.queryByTestId('run-output-view')).toBeNull();
  });

  it('clicking the Run tab calls setOutputTab', () => {
    renderPanel();
    screen.getByTestId('output-tab-run').click();
    expect(mock.state.setOutputTab).toHaveBeenCalledWith('run');
  });

  it('shows the run console when outputTab is run', () => {
    mock.state.outputTab = 'run';
    renderPanel();
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
  });

  it('shows an unread dot on the Run tab when runOutputUnread and not on it', () => {
    mock.state.runOutputUnread = true;
    renderPanel();
    expect(screen.getByTestId('output-tab-run-unread')).toBeInTheDocument();
  });

  it('hides the unread dot when already on the run tab', () => {
    mock.state.runOutputUnread = true;
    mock.state.outputTab = 'run';
    renderPanel();
    expect(screen.queryByTestId('output-tab-run-unread')).toBeNull();
  });

  it('clicking the Run tab while popped out docks back (closeRunOutputWindow)', () => {
    mock.state.runOutputPoppedOut = true;
    mock.state.outputTab = 'block';
    renderPanel();
    screen.getByTestId('output-tab-run').click();
    expect(mock.state.closeRunOutputWindow).toHaveBeenCalledTimes(1);
  });
});
