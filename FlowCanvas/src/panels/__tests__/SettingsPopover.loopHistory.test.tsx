import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => ({ messageBus: { send: vi.fn() } }));
import { render, screen, fireEvent } from '@testing-library/react';
import { messageBus } from '../../MessageBus';
import { useFlowStore } from '../../stores/useFlowStore';
import SettingsPopover from '../SettingsPopover';

/** Loop history is a free-form numeric field: any value commits on blur/Enter,
 *  clamped to [1, 100000]; junk reverts without touching the store. */
function openPopoverInput(): HTMLInputElement {
  render(<SettingsPopover />);
  fireEvent.click(screen.getByTitle('Display settings'));
  return screen.getByTestId('number-field-input') as HTMLInputElement;
}

describe('SettingsPopover loop history field', () => {
  beforeEach(() => {
    useFlowStore.setState({ iterationHistoryCap: 500 });
    vi.clearAllMocks();
  });

  it('shows the current cap and commits an arbitrary value on blur', () => {
    const input = openPopoverInput();
    expect(input.value).toBe('500');

    fireEvent.change(input, { target: { value: '1234' } });
    fireEvent.blur(input);

    expect(useFlowStore.getState().iterationHistoryCap).toBe(1234);
    expect(messageBus.send).toHaveBeenCalledWith(
      expect.objectContaining({ iterationHistoryCap: 1234 }),
    );
  });

  it('commits on Enter', () => {
    const input = openPopoverInput();
    fireEvent.change(input, { target: { value: '42' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(useFlowStore.getState().iterationHistoryCap).toBe(42);
  });

  it('clamps below the minimum to 1', () => {
    const input = openPopoverInput();
    fireEvent.change(input, { target: { value: '0' } });
    fireEvent.blur(input);
    expect(useFlowStore.getState().iterationHistoryCap).toBe(1);
    expect(input.value).toBe('1');
  });

  it('clamps above the maximum to 100000', () => {
    const input = openPopoverInput();
    fireEvent.change(input, { target: { value: '99999999' } });
    fireEvent.blur(input);
    expect(useFlowStore.getState().iterationHistoryCap).toBe(100000);
  });

  it('reverts non-numeric input without committing', () => {
    const input = openPopoverInput();
    fireEvent.change(input, { target: { value: 'abc' } });
    fireEvent.blur(input);
    expect(useFlowStore.getState().iterationHistoryCap).toBe(500);
    expect(input.value).toBe('500');
    expect(messageBus.send).not.toHaveBeenCalled();
  });

  it('reverts an emptied field without committing', () => {
    const input = openPopoverInput();
    fireEvent.change(input, { target: { value: '' } });
    fireEvent.blur(input);
    expect(useFlowStore.getState().iterationHistoryCap).toBe(500);
    expect(input.value).toBe('500');
  });
});
