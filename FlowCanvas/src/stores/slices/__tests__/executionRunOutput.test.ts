import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save', setLayoutMode: 'set-layout-mode' } },
}));
import { useFlowStore } from '../../useFlowStore';

const MAX = 5000;

describe('executionSlice runOutput', () => {
  beforeEach(() => {
    useFlowStore.getState().clearRunOutput();
  });

  it('starts empty', () => {
    expect(useFlowStore.getState().runOutput).toBe('');
  });

  it('appendRunOutput concatenates raw chunks', () => {
    useFlowStore.getState().appendRunOutput('### CONNECTED ###\n');
    useFlowStore.getState().appendRunOutput('line one\n');
    expect(useFlowStore.getState().runOutput).toBe('### CONNECTED ###\nline one\n');
  });

  it('clearRunOutput empties the buffer', () => {
    useFlowStore.getState().appendRunOutput('something');
    useFlowStore.getState().clearRunOutput();
    expect(useFlowStore.getState().runOutput).toBe('');
  });

  it('caps the buffer at the last 5000 lines', () => {
    const big = Array.from({ length: MAX + 200 }, (_, i) => `line ${i}`).join('\n');
    useFlowStore.getState().appendRunOutput(big);
    const lines = useFlowStore.getState().runOutput.split('\n');
    expect(lines.length).toBeLessThanOrEqual(MAX);
    expect(lines[lines.length - 1]).toBe(`line ${MAX + 200 - 1}`);
  });

  it('clearExecution also resets runOutput', () => {
    useFlowStore.getState().appendRunOutput('residue');
    useFlowStore.getState().clearExecution();
    expect(useFlowStore.getState().runOutput).toBe('');
  });
});
