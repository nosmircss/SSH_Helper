/**
 * Minimal bridge for the standalone Run Output window (?panel=runoutput). Unlike the full
 * messageBridge.ts (which drives the whole canvas), this only feeds the RunOutputView console:
 * the run-output stream, run-state for the LIVE dot, and the view-pref seed. Dark-only.
 */
import { messageBus } from '../MessageBus';
import { useFlowStore } from './useFlowStore';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';

// Module-level cleanup so re-initing (e.g. StrictMode double-effect) removes old handlers first.
let _cleanup: (() => void) | null = null;

export function initRunOutputWindowBridge(): () => void {
  _cleanup?.();
  const store = useFlowStore;
  const unsubs = [
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutput, (msg) => {
      if (typeof msg.chunk === 'string' && msg.chunk.length > 0) {
        store.getState().appendRunOutput(msg.chunk);
      }
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutputClear, () => {
      store.getState().clearRunOutput();
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionStarted, () => {
      store.getState().setRunning(true);
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionFinished, () => {
      store.getState().setRunning(false);
    }),
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.layoutRestore, (msg) => {
      store.getState().restoreRunOutputPrefs({
        runOutputColor: typeof msg.runOutputColor === 'boolean' ? msg.runOutputColor : undefined,
        runOutputWrap: typeof msg.runOutputWrap === 'boolean' ? msg.runOutputWrap : undefined,
        runOutputFollow: typeof msg.runOutputFollow === 'boolean' ? msg.runOutputFollow : undefined,
      });
    }),
  ];
  messageBus.sendReady();
  _cleanup = () => unsubs.forEach((u) => u());
  return _cleanup;
}
