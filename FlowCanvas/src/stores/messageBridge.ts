/**
 * Connects the MessageBus (WebView2 ↔ React transport) to the Zustand store.
 * All message handling logic that was in App.tsx moves here.
 */
import { messageBus } from '../MessageBus';
import { useFlowStore } from './useFlowStore';
import type { Node, Edge } from '@xyflow/react';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';
import type { BlockExecState } from './slices/executionSlice';

interface FlowCanvasTestHooks {
  onOutgoingMessage?: (msg: unknown) => void;
  setGraphViaActions?: (graph: { nodes?: unknown[]; edges?: unknown[] }) => void;
  clearGraphViaActions?: () => void;
  getGraphSnapshot?: () => { nodes: unknown[]; edges: unknown[] };
}

function cloneForTest<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function ensureStartNodeExists(store: typeof useFlowStore): void {
  const loadedNodes = store.getState().nodes;
  const loadedEdges = store.getState().edges;
  const hasStart = loadedNodes.some((n) => n.id === '__start__');
  if (hasStart) return;

  const preambleNode = loadedNodes.find(
    (n) => (n.data as any)?.blockType === '_preamble',
  );
  const startNode: Node = {
    id: '__start__',
    type: 'start',
    position: { x: 250, y: 0 },
    data: {
      blockType: '_start',
      label: 'Untitled Script',
      props: preambleNode
        ? (preambleNode.data as any)?.props ?? {}
        : {},
    },
  };

  const filtered = loadedNodes.filter(
    (n) => (n.data as any)?.blockType !== '_preamble',
  );
  store.getState().setNodes([startNode, ...filtered]);

  const incomingTargets = new Set(loadedEdges.map((e) => e.target));
  const firstRoot = filtered.find((n) => !incomingTargets.has(n.id));
  if (firstRoot) {
    store.getState().setEdges([
      ...loadedEdges,
      {
        id: `edge-start-${firstRoot.id}`,
        source: '__start__',
        target: firstRoot.id,
        style: { stroke: '#666' },
      } as Edge,
    ]);
  }
}

function resetGraphSessionState(store: typeof useFlowStore): void {
  store.getState().clearDirty();
  store.getState().clearHistory();
  store.getState().clearExecution();
  store.getState().clearTimeline();
  store.getState().clearExportStatus();
}

function installFlowCanvasTestHooks(store: typeof useFlowStore): void {
  const globalWindow = window as Window & { __FLOWCANVAS_TEST_HOOKS__?: FlowCanvasTestHooks };
  const hooks = globalWindow.__FLOWCANVAS_TEST_HOOKS__ ?? {};

  hooks.setGraphViaActions = (graph) => {
    const nodes = Array.isArray(graph?.nodes) ? cloneForTest(graph.nodes) as Node[] : [];
    const edges = Array.isArray(graph?.edges) ? cloneForTest(graph.edges) as Edge[] : [];

    store.getState().setNodes(nodes);
    store.getState().setEdges(edges);
    ensureStartNodeExists(store);
    resetGraphSessionState(store);
  };

  hooks.clearGraphViaActions = () => {
    store.getState().setNodes([]);
    store.getState().setEdges([]);
    resetGraphSessionState(store);
  };

  hooks.getGraphSnapshot = () => {
    const state = store.getState();
    return cloneForTest({
      nodes: state.nodes,
      edges: state.edges,
    });
  };

  globalWindow.__FLOWCANVAS_TEST_HOOKS__ = hooks;
}

export function initMessageBridge(): () => void {
  const store = useFlowStore;
  installFlowCanvasTestHooks(store);

  const unsubs = [
    // Load graph from C# (YAML → graph conversion result)
    messageBus.on('load-graph', (msg) => {
      if (msg.nodes && msg.edges) {
        store.getState().setNodes(msg.nodes as Node[]);
        store.getState().setEdges(msg.edges as Edge[]);
        ensureStartNodeExists(store);
        resetGraphSessionState(store);
      }
    }),

    // Apply/export result from host
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.applyResult, (msg) => {
      const errors = Array.isArray(msg.errors) ? msg.errors.map(String) : [];
      const warnings = Array.isArray(msg.warnings) ? msg.warnings.map(String) : [];
      const success = !!msg.success;

      store.getState().setExportStatus({
        hasErrors: !success && errors.length > 0,
        errors,
        warnings,
      });

      if (!success && errors.length > 0) {
        messageBus.send({ type: 'show-error', message: `Flow Canvas export failed:\n\n${errors.join('\n')}` });
      } else if (success && warnings.length > 0) {
        console.warn('[FlowCanvas] Export warnings:', warnings);
      }
    }),

    // Execution started
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionStarted, () => {
      store.getState().clearExecution();
      store.getState().clearTimeline();
      store.getState().setRunning(true);
      store.getState().clearExportStatus();
    }),

    // Execution finished
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionFinished, (msg) => {
      store.getState().setRunning(false);
      store.getState().setPaused(false);
    }),

    // Per-step execution state update
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionUpdate, (msg) => {
      const state = store.getState();
      if (msg.stepId === undefined || msg.stepId === null) {
        console.warn('[FlowCanvas] execution-update missing stepId; message ignored.', msg);
        return;
      }

      const stepId = String(msg.stepId);
      const rawState = String(msg.state ?? '');
      const allowedStates: BlockExecState[] = ['idle', 'running', 'success', 'error', 'skipped', 'disabled'];
      if (!allowedStates.includes(rawState as BlockExecState)) {
        console.warn(`[FlowCanvas] Unknown execution state '${rawState}' for step '${stepId}'; message ignored.`);
        return;
      }
      const execState = rawState as BlockExecState;

      state.setBlockState(stepId, execState);

      // Timeline entry
      if (execState === 'running') {
        const node = state.nodes.find((n) => n.id === stepId);
        state.addTimelineEntry({
          nodeId: stepId,
          nodeLabel: String((node?.data as any)?.blockType || stepId),
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
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.stepOutput, (msg) => {
      if (msg.stepId && msg.output) {
        store.getState().appendBlockOutput(
          String(msg.stepId),
          String(msg.output),
          msg.stepType ? String(msg.stepType) : undefined
        );
      }
    }),

    // Test step result (single-step execution)
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.testStepResult, (msg) => {
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

    // Test data block result (lightweight, no SSH)
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.testDataBlockResult, (msg) => {
      const state = store.getState();
      const stepId = String(msg.stepId ?? '');

      if (msg.output) {
        state.appendBlockOutput(stepId, String(msg.output), 'test-data-block');
      }
      if (msg.variables && typeof msg.variables === 'object') {
        const changedKeys = Array.isArray(msg.changedKeys) ? msg.changedKeys as string[] : undefined;
        state.setVariablesWithChanges(msg.variables as Record<string, unknown>, changedKeys);
      }
      if (stepId) {
        state.setBlockState(stepId, msg.success ? 'success' : 'error');
      }
      state.setDataBlockTestResult(stepId, {
        success: !!msg.success,
        output: String(msg.output ?? ''),
        error: msg.error ? String(msg.error) : undefined,
        changedKeys: Array.isArray(msg.changedKeys) ? msg.changedKeys.map(String) : undefined,
        timestamp: Date.now(),
      });
    }),

    // Variables snapshot (with change tracking)
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.variablesSnapshot, (msg) => {
      if (msg.variables && typeof msg.variables === 'object') {
        const changedKeys = Array.isArray(msg.changedKeys) ? msg.changedKeys as string[] : undefined;
        store.getState().setVariablesWithChanges(
          msg.variables as Record<string, unknown>,
          changedKeys
        );
      }
    }),

    // Debug paused
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.debugPaused, (msg) => {
      const state = store.getState();
      const stepId = typeof msg.stepId === 'string' ? msg.stepId : '';
      const callStack = Array.isArray(msg.callStack) ? (msg.callStack as string[]) : [];
      state.setPaused(true, stepId || undefined, callStack);
      if (stepId) {
        state.setBlockState(stepId, 'running');
      }

      if (msg.variables && typeof msg.variables === 'object') {
        state.setVariablesWithChanges(msg.variables as Record<string, unknown>);
      }
    }),

    // Debug resumed
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.debugResumed, (msg) => {
      const stepId = typeof msg.stepId === 'string' ? msg.stepId : '';
      const state = store.getState();
      const callStack = Array.isArray(msg.callStack) ? (msg.callStack as string[]) : [];
      state.setPaused(false, stepId || undefined, callStack);
      if (stepId) {
        state.setBlockState(stepId, 'running');
      }
    }),

    // Target host sync from WinForms
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.setTargetHost, (msg) => {
      const hostData = msg.host as {
        ip: string;
        port: number;
        username: string;
        variables: Record<string, string>;
      } | null;
      store.getState().setTargetHost(
        hostData
          ? {
              ip: hostData.ip ?? '',
              port: hostData.port ?? 22,
              username: hostData.username ?? '',
              variables: hostData.variables ?? {},
            }
          : null,
      );
    }),

    // Theme sync from WinForms
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.themeSync, (msg) => {
      if (msg.theme === 'dark' || msg.theme === 'light') {
        store.getState().setTheme(msg.theme);
      }
    }),

    // Restore panel sizes from WinForms persisted settings
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.layoutRestore, (msg) => {
      if (msg.panelSizes && typeof msg.panelSizes === 'object') {
        const sizes: Record<string, number> = {};
        for (const [k, v] of Object.entries(msg.panelSizes as Record<string, unknown>)) {
          if (typeof v === 'number' && v > 0) sizes[k] = v;
        }
        store.getState().restorePanelSizes(sizes);
      }
    }),
  ];

  // Signal ready
  messageBus.sendReady();

  return () => unsubs.forEach((u) => u());
}
