/**
 * Connects the MessageBus (WebView2 ↔ React transport) to the Zustand store.
 * All message handling logic that was in App.tsx moves here.
 */
import { messageBus } from '../MessageBus';
import { useFlowStore } from './useFlowStore';
import type { Node, Edge } from '@xyflow/react';

export function initMessageBridge(): () => void {
  const store = useFlowStore;

  const unsubs = [
    // Load graph from C# (YAML → graph conversion result)
    messageBus.on('load-graph', (msg) => {
      if (msg.nodes && msg.edges) {
        store.getState().setNodes(msg.nodes as Node[]);
        store.getState().setEdges(msg.edges as Edge[]);
        store.getState().clearHistory();
        store.getState().clearExecution();
        store.getState().clearTimeline();
      }
    }),

    // Execution started
    messageBus.on('execution-started', () => {
      store.getState().clearExecution();
      store.getState().clearTimeline();
      store.getState().setRunning(true);
    }),

    // Execution finished
    messageBus.on('execution-finished', (msg) => {
      store.getState().setRunning(false);
      store.getState().setPaused(false);
    }),

    // Per-step execution state update
    messageBus.on('execution-update', (msg) => {
      const state = store.getState();
      const stepId = String(msg.stepId);
      const execState = String(msg.state) as any;

      state.setBlockState(stepId, execState);

      // Timeline entry
      if (execState === 'running') {
        const node = state.nodes.find((n) => n.id === stepId);
        state.addTimelineEntry({
          nodeId: stepId,
          nodeLabel: String((node?.data as any)?.label || stepId),
          blockType: String((node?.data as any)?.blockType || ''),
          state: 'running',
          startTime: Date.now(),
          variables: Object.fromEntries(
            state.variables.map((v) => [v.name, v.value])
          ),
        });
      } else if (execState === 'success' || execState === 'error' || execState === 'skipped') {
        state.updateTimelineEntry(stepId, {
          state: execState,
          endTime: Date.now(),
          duration: msg.duration ? Number(msg.duration) : undefined,
        });
      }

      // Update variables if included
      if (msg.variables && typeof msg.variables === 'object') {
        const changedKeys = Array.isArray(msg.changedKeys) ? msg.changedKeys as string[] : undefined;
        state.setVariablesWithChanges(msg.variables as Record<string, unknown>, changedKeys);
      }
    }),

    // Per-step output
    messageBus.on('step-output', (msg) => {
      if (msg.stepId && msg.output) {
        store.getState().appendBlockOutput(
          String(msg.stepId),
          String(msg.output),
          msg.stepType ? String(msg.stepType) : undefined
        );
      }
    }),

    // Test step result (single-step execution)
    messageBus.on('test-step-result', (msg) => {
      const state = store.getState();
      const stepId = String(msg.stepId ?? '');

      if (msg.output) {
        state.appendBlockOutput(stepId, String(msg.output));
      }
      if (msg.variables && typeof msg.variables === 'object') {
        state.setVariablesWithChanges(msg.variables as Record<string, unknown>);
      }
      if (stepId) {
        state.setBlockState(stepId, msg.success ? 'success' : 'error');
      }
    }),

    // Variables snapshot (with change tracking)
    messageBus.on('variables-snapshot', (msg) => {
      if (msg.variables && typeof msg.variables === 'object') {
        const changedKeys = Array.isArray(msg.changedKeys) ? msg.changedKeys as string[] : undefined;
        store.getState().setVariablesWithChanges(
          msg.variables as Record<string, unknown>,
          changedKeys
        );
      }
    }),

    // Debug paused
    messageBus.on('debug-paused', (msg) => {
      const state = store.getState();
      state.setPaused(true, String(msg.stepId ?? ''), (msg.callStack as string[]) ?? []);
      state.setBlockState(String(msg.stepId), 'running');

      if (msg.variables && typeof msg.variables === 'object') {
        state.setVariablesWithChanges(msg.variables as Record<string, unknown>);
      }
    }),

    // Theme sync from WinForms
    messageBus.on('theme-sync', (msg) => {
      if (msg.theme === 'dark' || msg.theme === 'light') {
        store.getState().setTheme(msg.theme);
      }
    }),
  ];

  // Signal ready
  messageBus.sendReady();

  return () => unsubs.forEach((u) => u());
}
