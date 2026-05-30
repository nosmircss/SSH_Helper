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
