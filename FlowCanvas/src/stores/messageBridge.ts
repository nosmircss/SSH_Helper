/**
 * Connects the MessageBus (WebView2 ↔ React transport) to the Zustand store.
 * All message handling logic that was in App.tsx moves here.
 */
import { messageBus } from '../MessageBus';
import { useFlowStore } from './useFlowStore';
import type { Node, Edge } from '@xyflow/react';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';
import type { BlockExecState } from './slices/executionSlice';
import type { NodeDiagnostic } from './slices/uiSlice';
import { isConnectionAllowed } from '../utils/connectionRules';
import type { ConnectionVerdict } from '../utils/connectionRules';
import type { Connection } from '@xyflow/react';
import { computeHierarchicalLayout, placeNewBlocksNearNeighbors } from '../utils/layout/hierarchicalLayout';
import { placeAnchoredComments } from '../utils/layout/placeAnchoredComments';
import type { CanvasSettings } from './slices/settingsSlice';

interface FlowCanvasTestHooks {
  onOutgoingMessage?: (msg: unknown) => void;
  setGraphViaActions?: (graph: { nodes?: unknown[]; edges?: unknown[] }) => void;
  clearGraphViaActions?: () => void;
  getGraphSnapshot?: () => { nodes: unknown[]; edges: unknown[] };
  isConnectionAllowed?: (conn: Connection, nodes: Node[], edges: Edge[]) => ConnectionVerdict;
  connectViaActions?: (conn: Connection) => void;
  getConnectionNotice?: () => { message: string; nonce: number } | null;
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
  store.getState().clearIterations();
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

  hooks.isConnectionAllowed = (conn, nodes, edges) => isConnectionAllowed(conn, nodes, edges);

  // Drives the real store onConnect path (same code real drags hit) so tests can assert
  // the guard lets valid connections through and produces identical edge metadata.
  hooks.connectViaActions = (conn) => store.getState().onConnect(conn);

  // Exposes the live connectionNotice slice so gesture tests can assert a VALID drag never
  // flashes a notice (regression guard: v12 adds the edge before onConnectEnd runs).
  hooks.getConnectionNotice = () => store.getState().connectionNotice;

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

        // Mode-aware layout: the host tells us the preset's mode and what to do on this load.
        const layoutMode = (msg as { layoutMode?: string }).layoutMode === 'manual' ? 'manual' : 'auto';
        const layoutAction = (msg as { layoutAction?: string }).layoutAction === 'keep' ? 'keep' : 'reflow';
        const rawNewIds = (msg as { newNodeIds?: unknown[] }).newNodeIds;
        const newNodeIds: string[] = Array.isArray(rawNewIds) ? rawNewIds.map(String) : [];
        store.getState().restoreLayoutMode(layoutMode); // host-driven, no echo

        const s = store.getState();
        const sizing = { blockWidth: s.blockWidth, density: s.density, textScale: s.textScale, compactComments: s.compactCommentsEnabled };
        if (layoutAction === 'reflow') {
          store.getState().setNodes(computeHierarchicalLayout(s.nodes, s.edges, sizing));
        } else {
          // Manual keep: positions already merged by the host. Place only the new blocks near their
          // neighbor, then (re-)anchor comments above their block/band.
          const placed = placeNewBlocksNearNeighbors(s.nodes, s.edges, new Set(newNodeIds), sizing);
          store.getState().setNodes(placeAnchoredComments(placed, store.getState().compactCommentsEnabled));
        }

        resetGraphSessionState(store);

        // Restore disabled block state from loaded node data
        const state = store.getState();
        const disabledIds: string[] = [];
        for (const node of state.nodes) {
          const data = node.data as Record<string, unknown> | undefined;
          if (data?.disabled === true) {
            disabledIds.push(node.id);
          }
        }
        if (disabledIds.length > 0) {
          state.restoreDisabledBlocks(disabledIds);
        }
        const expandedIds: string[] = [];
        for (const node of state.nodes) {
          const data = node.data as Record<string, unknown> | undefined;
          if (data?.expanded === true) expandedIds.push(node.id);
        }
        if (expandedIds.length > 0) state.restoreExpandedNodes(expandedIds);
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

      const rawDiag = Array.isArray(msg.diagnostics) ? msg.diagnostics : [];
      const parsed: NodeDiagnostic[] = rawDiag.map((d: any) => ({
        nodeId: d.nodeId != null ? String(d.nodeId) : undefined,
        severity: d.severity === 'error' ? 'error' : 'warning',
        message: String(d.message ?? ''),
      }));
      store.getState().setDiagnostics(parsed);

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
      store.getState().clearIterations();
      store.getState().setRunning(true);
      store.getState().clearExportStatus();
      if (!store.getState().runOutputPoppedOut) store.getState().setOutputTab('run');
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
        state.setBlockTiming(stepId, Date.now());
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
        const now = Date.now();
        const dur = msg.duration != null ? Number(msg.duration) : undefined;
        state.setBlockTiming(stepId, dur != null ? now - dur : now, now);
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

      // Loop & branch instrumentation (final/summary; arrives with the completion message).
      // Stored in transient executionSlice Maps — never written onto node.data, so export is unaffected.
      if (msg.iterationCount != null) {
        const n = Number(msg.iterationCount);
        if (Number.isFinite(n) && n >= 0) {
          state.setLoopIteration(stepId, n);
        }
      }
      if (typeof msg.branchTaken === 'string' && msg.branchTaken.trim().length > 0) {
        state.setBranchTaken(stepId, msg.branchTaken.trim());
      }

      // Iteration attribution: every tagged event lands in the iteration log (transient,
      // never written onto node.data, so export is unaffected).
      if (Array.isArray(msg.iterationStack) && msg.iterationStack.length > 0) {
        const isCompletion = execState === 'success' || execState === 'error' || execState === 'skipped';
        state.recordIterationEvent(
          stepId,
          msg.iterationStack,
          {
            state: execState,
            duration: msg.duration != null ? Number(msg.duration) : undefined,
            branchTaken:
              typeof msg.branchTaken === 'string' && msg.branchTaken.trim().length > 0
                ? msg.branchTaken.trim()
                : undefined,
            suppressed: msg.suppressedError === true ? true : undefined,
          },
          isCompletion && msg.variables && typeof msg.variables === 'object'
            ? (msg.variables as Record<string, unknown>)
            : undefined,
        );
      }
    }),

    // Per-step output
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.stepOutput, (msg) => {
      if (msg.stepId && msg.output) {
        const stepId = String(msg.stepId);
        store.getState().appendBlockOutput(
          stepId,
          String(msg.output),
          msg.stepType ? String(msg.stepType) : undefined
        );
        // Tie this output entry to its iteration so the stepper can recall it.
        if (Array.isArray(msg.iterationStack) && msg.iterationStack.length > 0) {
          const outputIdx = (store.getState().blockOutputs.get(stepId)?.length ?? 1) - 1;
          store.getState().recordIterationEvent(stepId, msg.iterationStack, { outputIdx });
        }
      }
    }),

    // Full run output — live mirror of the main form's output box
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutput, (msg) => {
      if (typeof msg.chunk === 'string' && msg.chunk.length > 0) {
        const state = store.getState();
        state.appendRunOutput(msg.chunk);
        if (state.outputTab !== 'run' && !state.runOutputPoppedOut) {
          state.setRunOutputUnread(true);
        }
      }
    }),

    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutputClear, () => {
      const state = store.getState();
      state.clearRunOutput();
      state.setRunOutputUnread(false); // clearing the buffer also clears any stale unread dot
    }),

    // The detached Run Output window was closed (by its own X) — dock the console back.
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutputWindowClosed, () => {
      const state = store.getState();
      state.setRunOutputPoppedOut(false);
      state.setOutputTab('run');
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

    // Theme sync from WinForms — Flow Canvas always uses dark mode
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.themeSync, () => {
      // Intentionally ignored: canvas is always dark regardless of main app theme
    }),

    // Restore panel sizes and canvas settings from WinForms persisted settings
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.layoutRestore, (msg) => {
      if (msg.panelSizes && typeof msg.panelSizes === 'object') {
        const sizes: Record<string, number> = {};
        for (const [k, v] of Object.entries(msg.panelSizes as Record<string, unknown>)) {
          if (typeof v === 'number' && v > 0) sizes[k] = v;
        }
        store.getState().restorePanelSizes(sizes);
      }
      if (typeof msg.heatmapEnabled === 'boolean') store.getState().restoreHeatmapEnabled(msg.heatmapEnabled);
      // Restore the global DEFAULT mode (settings popover). The active preset's mode arrives via
      // load-graph (restoreLayoutMode), so this only seeds the default shown in settings.
      if (msg.defaultLayoutMode === 'auto' || msg.defaultLayoutMode === 'manual') {
        store.getState().restoreDefaultLayoutMode(msg.defaultLayoutMode);
      }

      const cs: Partial<CanvasSettings> = {};
      if (typeof msg.blockWidth === 'number' && msg.blockWidth > 0) cs.blockWidth = msg.blockWidth;
      if (typeof msg.textScale === 'number' && msg.textScale > 0) cs.textScale = msg.textScale;
      if (typeof msg.density === 'number' && msg.density > 0) cs.density = msg.density;
      if (typeof msg.defaultBlockExpanded === 'boolean') cs.defaultBlockExpanded = msg.defaultBlockExpanded;
      if (Object.keys(cs).length > 0) store.getState().restoreCanvasSettings(cs);

      if (typeof msg.snapToGrid === 'boolean') store.getState().restoreSnapToGrid(msg.snapToGrid);
      if (typeof msg.branchBandsEnabled === 'boolean') store.getState().restoreBranchBands(msg.branchBandsEnabled);
      if (typeof msg.compactCommentsEnabled === 'boolean') store.getState().restoreCompactComments(msg.compactCommentsEnabled);
      store.getState().restoreRunOutputPrefs({
        runOutputColor: typeof msg.runOutputColor === 'boolean' ? msg.runOutputColor : undefined,
        runOutputWrap: typeof msg.runOutputWrap === 'boolean' ? msg.runOutputWrap : undefined,
        runOutputFollow: typeof msg.runOutputFollow === 'boolean' ? msg.runOutputFollow : undefined,
      });
    }),

    // Restore UI prefs from WinForms persisted settings (no echo back to host)
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.prefRestore, (msg) => {
      if (typeof msg.reducedMotion === 'boolean') {
        store.getState().restoreReducedMotion(msg.reducedMotion);
      }
      if (typeof msg.iterationHistoryCap === 'number' && msg.iterationHistoryCap > 0) {
        store.getState().restoreIterationHistoryCap(msg.iterationHistoryCap);
      }
    }),
  ];

  // Seed reduced-motion from the OS preference before announcing readiness so the
  // body class is correct on first paint. A host `pref-restore` arrives after `ready`
  // and overrides this seed (explicit toggle stays load-bearing).
  const prefersReduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  if (prefersReduced) store.getState().restoreReducedMotion(true);

  // Signal ready
  messageBus.sendReady();

  return () => unsubs.forEach((u) => u());
}
