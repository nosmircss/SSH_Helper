import { describe, it, expect, beforeEach } from 'vitest';
import { useFlowStore } from '../../useFlowStore';

describe('execution path visibility', () => {
  beforeEach(() => {
    useFlowStore.setState({ pathVisible: true });
    useFlowStore.getState().clearExecution();
  });

  it('defaults to visible', () => {
    expect(useFlowStore.getState().pathVisible).toBe(true);
  });

  it('clearPath hides the path', () => {
    useFlowStore.getState().clearPath();
    expect(useFlowStore.getState().pathVisible).toBe(false);
  });

  it('setPathVisible toggles the flag', () => {
    useFlowStore.getState().setPathVisible(false);
    expect(useFlowStore.getState().pathVisible).toBe(false);
    useFlowStore.getState().setPathVisible(true);
    expect(useFlowStore.getState().pathVisible).toBe(true);
  });

  it('clearExecution (a fresh run) re-shows the path', () => {
    useFlowStore.getState().clearPath();
    expect(useFlowStore.getState().pathVisible).toBe(false);
    useFlowStore.getState().clearExecution();
    expect(useFlowStore.getState().pathVisible).toBe(true);
  });
});
