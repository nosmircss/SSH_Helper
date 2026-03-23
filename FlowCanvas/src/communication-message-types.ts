export const CANVAS_HOST_MESSAGES = {
  incoming: {
    applyResult: 'apply-result',
    executionFinished: 'execution-finished',
    executionStarted: 'execution-started',
    executionUpdate: 'execution-update',
    stepOutput: 'step-output',
    testStepResult: 'test-step-result',
    variablesSnapshot: 'variables-snapshot',
    debugPaused: 'debug-paused',
    debugResumed: 'debug-resumed',
    themeSync: 'theme-sync',
    setTargetHost: 'set-target-host',
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
    testStepResult: 'test-step-result',
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
