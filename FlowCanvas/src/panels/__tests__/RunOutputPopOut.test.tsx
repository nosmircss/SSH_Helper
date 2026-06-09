import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
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

  it('removes drag listeners when docked mid-drag (no leak)', () => {
    const removeSpy = vi.spyOn(window, 'removeEventListener');
    mock.state.runOutputPoppedOut = true;
    const { rerender } = render(<RunOutputPopOut />);
    // Start a drag — registers window mousemove/mouseup listeners.
    fireEvent.mouseDown(screen.getByTestId('run-output-popout-drag'), { clientX: 300, clientY: 120 });
    // Dock mid-drag: poppedOut -> false. The effect cleanup must tear the listeners down
    // even though the mouse was never released.
    mock.state.runOutputPoppedOut = false;
    rerender(<RunOutputPopOut />);
    expect(removeSpy).toHaveBeenCalledWith('mousemove', expect.any(Function));
    expect(removeSpy).toHaveBeenCalledWith('mouseup', expect.any(Function));
    removeSpy.mockRestore();
  });

  it('resizes the overlay when the corner handle is dragged', () => {
    render(<RunOutputPopOut />);
    const panel = screen.getByTestId('run-output-popout');
    expect(panel.style.width).toBe('520px');
    expect(panel.style.height).toBe('300px');
    fireEvent.mouseDown(screen.getByTestId('run-output-popout-resize'), { clientX: 700, clientY: 360 });
    act(() => { window.dispatchEvent(new MouseEvent('mousemove', { clientX: 760, clientY: 420 })); });
    expect(screen.getByTestId('run-output-popout').style.width).toBe('580px'); // 520 + 60
    expect(screen.getByTestId('run-output-popout').style.height).toBe('360px'); // 300 + 60
    act(() => { window.dispatchEvent(new MouseEvent('mouseup')); });
  });

  it('does not shrink below the minimum size', () => {
    render(<RunOutputPopOut />);
    fireEvent.mouseDown(screen.getByTestId('run-output-popout-resize'), { clientX: 700, clientY: 360 });
    act(() => { window.dispatchEvent(new MouseEvent('mousemove', { clientX: 0, clientY: 0 })); });
    expect(screen.getByTestId('run-output-popout').style.width).toBe('260px'); // clamped to MIN_W
    expect(screen.getByTestId('run-output-popout').style.height).toBe('140px'); // clamped to MIN_H
    act(() => { window.dispatchEvent(new MouseEvent('mouseup')); });
  });
});
