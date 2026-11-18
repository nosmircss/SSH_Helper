import { beforeEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import HostBar from '../HostBar';
import { useFlowStore } from '../../stores/useFlowStore';

describe('HostBar connection policy', () => {
  beforeEach(() => {
    cleanup();
    useFlowStore.setState({ targetHost: null });
  });

  it('displays the raw candidate policy rather than compatibility port22', () => {
    useFlowStore.setState({ targetHost: {
      ip: 'router', port: 22, username: '', variables: { port: '22,222,2222' },
    } });
    render(<HostBar />);
    expect(screen.getByText('22,222,2222')).toBeTruthy();
    expect(screen.queryByText('22')).toBeNull();
  });

  it('retains a legacy scalar payload', () => {
    useFlowStore.setState({ targetHost: {
      ip: 'router', port: 2222, username: '', variables: {},
    } });
    render(<HostBar />);
    expect(screen.getByText('2222')).toBeTruthy();
  });

  it('shows no-target guidance', () => {
    render(<HostBar />);
    expect(screen.getByText(/No target host/)).toBeTruthy();
  });

  it('handles case-insensitive grid port columns without a duplicate variable chip', () => {
    useFlowStore.setState({ targetHost: {
      ip: 'router', port: 22, username: '', variables: { PoRt: '222,2222' },
    } });
    render(<HostBar />);
    expect(screen.getAllByText('222,2222')).toHaveLength(1);
    expect(screen.queryByText('PoRt:')).toBeNull();
  });
});
