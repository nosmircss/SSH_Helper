/**
 * Note: the host also sends a `load-graph` message (handled directly in messageBridge.ts):
 *   { type: 'load-graph', nodes, edges, layoutMode?: 'auto'|'manual',
 *     layoutAction?: 'reflow'|'keep', newNodeIds?: string[] }
 * layoutMode  = the active preset's mode (drives the toolbar toggle + reflow gating).
 * layoutAction= 'reflow' runs computeHierarchicalLayout; 'keep' preserves merged positions and
 *               near-neighbor-places the ids in newNodeIds.
 */
export const CANVAS_HOST_MESSAGES = {
  incoming: {
    applyResult: 'apply-result',
    executionFinished: 'execution-finished',
    executionStarted: 'execution-started',
    executionUpdate: 'execution-update',
    stepOutput: 'step-output',
    testStepResult: 'test-step-result',
    testDataBlockResult: 'test-data-block-result',
    variablesSnapshot: 'variables-snapshot',
    debugPaused: 'debug-paused',
    debugResumed: 'debug-resumed',
    themeSync: 'theme-sync',
    setTargetHost: 'set-target-host',
    layoutRestore: 'layout-restore',
    browsePathResult: 'browse-path-result',
    prefRestore: 'pref-restore',
  },
  outgoing: {
    ready: 'ready',
    applyYaml: 'apply-yaml',
    executeCanvas: 'execute-canvas',
    run: 'run',
    testStep: 'test-step',
    debugAction: 'debug-action',
    disableBlock: 'disable-block',
    breakpointToggle: 'breakpoint-toggle',
    testDataBlock: 'test-data-block',
    testStepResult: 'test-step-result',
    layoutSave: 'layout-save',
    layoutAutosave: 'layout-autosave',
    browsePath: 'browse-path',
    prefSave: 'pref-save',
    setLayoutMode: 'set-layout-mode',
  },
  deprecatedOutgoingAliases: {
    runRequest: 'run-request',
  },
  debugAction: {
    continue: 'continue',
    step: 'step',
    stop: 'stop',
  },
} as const;

export type OutgoingCanvasMessage =
  (typeof CANVAS_HOST_MESSAGES)['outgoing'][keyof typeof CANVAS_HOST_MESSAGES['outgoing']];
export type IncomingCanvasMessage =
  (typeof CANVAS_HOST_MESSAGES)['incoming'][keyof typeof CANVAS_HOST_MESSAGES['incoming']];

/** Shape of an 'execution-update' host message (fields are validated loosely at parse time). */
export interface ExecutionUpdateMessage {
  type: 'execution-update';
  stepId: string | number;
  state: string;
  duration?: number | null;
  variables?: Record<string, unknown>;
  changedKeys?: string[];
  /** Loop body-execution count (foreach/while/repeat); number or null. */
  iterationCount?: number | null;
  /** Taken branch scope-key (if/switch), e.g. 'else', 'cases/2/do', 'elif/0/then'. */
  branchTaken?: string | null;
}
