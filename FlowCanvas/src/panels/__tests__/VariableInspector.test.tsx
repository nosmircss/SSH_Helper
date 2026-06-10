import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { useFlowStore } from '../../stores/useFlowStore';
import type { IterationFrameMsg } from '../../communication-message-types';
import VariableInspector from '../VariableInspector';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

beforeEach(() => {
  cleanup();
  useFlowStore.getState().clearIterations();
  useFlowStore.setState({ variables: [] });
});

describe('VariableInspector — time travel', () => {
  it('shows live variables with no active iteration context', () => {
    useFlowStore.setState({ variables: [{ name: 'host', value: 'final' }] });
    render(<VariableInspector />);

    expect(screen.queryByTestId('iter-vars-banner')).toBeNull();
    expect(screen.getByText('host')).not.toBeNull();
    expect(screen.getByText('"final"')).not.toBeNull();
  });

  it('time-travels to the selected iteration\'s snapshot', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [F('L', 0, 'host0')], { state: 'success' }, { host: 'web-00' });
    rec('A', [F('L', 1, 'host1')], { state: 'success' }, { host: 'web-01' });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    useFlowStore.getState().setIterationSelection('L', records[0].seq);
    // Live values are something else entirely.
    useFlowStore.setState({ variables: [{ name: 'host', value: 'LIVE' }] });

    render(<VariableInspector />);

    const banner = screen.getByTestId('iter-vars-banner');
    expect(banner.textContent).toContain('1/2');
    expect(banner.textContent).toContain('host0');

    // Snapshot value, not the live one.
    expect(screen.getByText('"web-00"')).not.toBeNull();
    expect(screen.queryByText('"LIVE"')).toBeNull();
  });

  it('Live button returns to live values', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [F('L', 0, 'host0')], { state: 'success' }, { host: 'web-00' });
    rec('A', [F('L', 1, 'host1')], { state: 'success' }, { host: 'web-01' });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    useFlowStore.getState().setIterationSelection('L', records[0].seq);
    useFlowStore.setState({ variables: [{ name: 'host', value: 'LIVE' }] });

    render(<VariableInspector />);

    fireEvent.click(screen.getByTestId('iter-vars-live'));

    expect(screen.queryByTestId('iter-vars-banner')).toBeNull();
    expect(screen.getByText('"LIVE"')).not.toBeNull();
    expect(useFlowStore.getState().iterationSelections.get('L')).toBeNull();
  });

  it('shows the empty note for iterations without a snapshot', () => {
    // A running-only event records the iteration but passes no variables snapshot.
    useFlowStore.getState().recordIterationEvent('A', [F('L', 0, 'host0')], { state: 'running' });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    useFlowStore.getState().setIterationSelection('L', records[0].seq);
    useFlowStore.setState({ variables: [{ name: 'host', value: 'LIVE' }] });

    render(<VariableInspector />);

    expect(screen.getByTestId('iter-vars-banner')).not.toBeNull();
    expect(screen.getByTestId('iter-vars-empty')).not.toBeNull();
    expect(screen.queryByText('"LIVE"')).toBeNull();
  });

  it('failed iterations are flagged in the banner', () => {
    useFlowStore.getState().recordIterationEvent('A', [F('L', 0, 'host0')], { state: 'error' }, { host: 'web-00' });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    useFlowStore.getState().setIterationSelection('L', records[0].seq);

    render(<VariableInspector />);

    const banner = screen.getByTestId('iter-vars-banner');
    expect(banner.textContent).toContain('⚠');
  });
});
