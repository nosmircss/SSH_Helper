import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => {
  const handlers = new Map<string, Set<(m: any) => void>>();
  return {
    messageBus: {
      on: (t: string, h: (m: any) => void) => {
        if (!handlers.has(t)) handlers.set(t, new Set());
        handlers.get(t)!.add(h);
        return () => handlers.get(t)?.delete(h);
      },
      send: vi.fn(),
      sendReady: vi.fn(),
      __emit: (m: any) => handlers.get(m.type)?.forEach((h) => h(m)),
    },
    CANVAS_HOST_MESSAGES: {
      incoming: { runOutput: 'run-output', runOutputClear: 'run-output-clear', executionStarted: 'execution-started', executionFinished: 'execution-finished', layoutRestore: 'layout-restore' },
      outgoing: { layoutSave: 'layout-save' },
    },
  };
});
import { useFlowStore } from '../useFlowStore';
import { messageBus } from '../../MessageBus';
import { initRunOutputWindowBridge } from '../runOutputWindowBridge';

const emit = (m: any) => (messageBus as any).__emit(m);

describe('runOutputWindowBridge', () => {
  let cleanup: () => void;
  beforeEach(() => {
    useFlowStore.getState().clearRunOutput();
    useFlowStore.getState().setRunning(false);
    cleanup = initRunOutputWindowBridge();
  });

  it('sends ready on init', () => {
    expect(messageBus.sendReady).toHaveBeenCalled();
  });
  it('appends run-output and clears on run-output-clear', () => {
    emit({ type: 'run-output', chunk: 'hello\n' });
    expect(useFlowStore.getState().runOutput).toBe('hello\n');
    emit({ type: 'run-output-clear' });
    expect(useFlowStore.getState().runOutput).toBe('');
  });
  it('drives isRunning from execution-started/finished', () => {
    emit({ type: 'execution-started' });
    expect(useFlowStore.getState().isRunning).toBe(true);
    emit({ type: 'execution-finished' });
    expect(useFlowStore.getState().isRunning).toBe(false);
  });
  it('restores prefs from layout-restore', () => {
    useFlowStore.setState({ runOutputColor: true, runOutputWrap: false });
    emit({ type: 'layout-restore', runOutputColor: false, runOutputWrap: true });
    expect(useFlowStore.getState().runOutputColor).toBe(false);
    expect(useFlowStore.getState().runOutputWrap).toBe(true);
    cleanup();
  });
});
