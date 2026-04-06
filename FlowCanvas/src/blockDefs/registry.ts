/**
 * Block type definitions for all 35 SSH Helper script commands.
 * Each definition includes category, color, icon, default properties, and property schema.
 */

export type BlockCategory =
  | 'ssh'
  | 'control-flow'
  | 'data'
  | 'network'
  | 'io'
  | 'grid'
  | 'timing';

export type PropertyGroup = 'core' | 'advanced' | 'on_error';
export type PropertyEditor = 'default' | 'choice-options';

export interface PropertyDef {
  key: string;
  label: string;
  type: 'text' | 'number' | 'boolean' | 'select' | 'code' | 'textarea' | 'keyvalue';
  browse?: 'file';
  required?: boolean;
  placeholder?: string;
  options?: string[];
  defaultValue?: unknown;
  helpText?: string;
  group?: PropertyGroup;
  editor?: PropertyEditor;
}

export interface BlockDef {
  type: string;
  label: string;
  category: BlockCategory;
  icon: string;
  description: string;
  previewKey?: string;
  outputs?: number;
  isContainer?: boolean;
  properties: PropertyDef[];
}

export const categoryColors: Record<BlockCategory, { border: string; bg: string; badge: string; badgeText: string; text: string }> = {
  ssh: { border: '#4a9eff', bg: '#1a2744', badge: '#4a9eff', badgeText: '#000', text: '#8aafdb' },
  'control-flow': { border: '#f0c040', bg: '#2a2a1a', badge: '#f0c040', badgeText: '#000', text: '#e0d080' },
  data: { border: '#9b59b6', bg: '#1a1a2a', badge: '#9b59b6', badgeText: '#fff', text: '#c9a0dc' },
  network: { border: '#1abc9c', bg: '#1a2a2a', badge: '#1abc9c', badgeText: '#000', text: '#80d4c0' },
  io: { border: '#e67e22', bg: '#201a2a', badge: '#e67e22', badgeText: '#fff', text: '#e8a860' },
  grid: { border: '#3498db', bg: '#1a2a3a', badge: '#3498db', badgeText: '#fff', text: '#7ab8e0' },
  timing: { border: '#95a5a6', bg: '#1a1a1a', badge: '#95a5a6', badgeText: '#000', text: '#bdc3c7' },
};

const onErrorProp: PropertyDef = {
  key: 'on_error',
  label: 'On Error',
  type: 'select',
  options: ['stop', 'continue'],
  defaultValue: 'stop',
  group: 'on_error',
};

const timeoutProp: PropertyDef = {
  key: 'timeout',
  label: 'Timeout (s)',
  type: 'number',
  placeholder: 'default',
};

export const blockDefs: BlockDef[] = [
  // SSH
  {
    type: 'send',
    label: 'Send',
    category: 'ssh',
    icon: 'ssh',
    description: 'Send an SSH command and capture output',
    previewKey: 'command',
    properties: [
      { key: 'command', label: 'Command', type: 'code', required: true },
      { key: 'capture', label: 'Capture Variable', type: 'text' },
      { key: 'suppress', label: 'Suppress Output', type: 'boolean', defaultValue: false },
      { key: 'expect', label: 'Expect Pattern', type: 'text', placeholder: 'regex' },
      timeoutProp,
      { key: 'retry', label: 'Retry Count', type: 'number', defaultValue: 0 },
      { key: 'retry_delay', label: 'Retry Delay (s)', type: 'number', defaultValue: 1 },
      { key: 'fail_on_nonzero', label: 'Fail On Non-Zero', type: 'boolean', defaultValue: false },
      onErrorProp,
    ],
  },
  {
    type: 'interactive',
    label: 'Interactive',
    category: 'ssh',
    icon: 'term',
    description: 'Open an interactive terminal session',
    previewKey: 'command',
    properties: [
      { key: 'session', label: 'Session', type: 'select', options: ['separate', 'shared'], defaultValue: 'separate' },
      { key: 'title', label: 'Window Title', type: 'text' },
      { key: 'command', label: 'Command', type: 'code' },
      { key: 'capture', label: 'Capture Variable', type: 'text' },
      { key: 'max_seconds', label: 'Max Seconds', type: 'number' },
      { key: 'max_lines', label: 'Max Lines', type: 'number' },
      { key: 'width', label: 'Width (px)', type: 'number' },
      { key: 'height', label: 'Height (px)', type: 'number' },
      { key: 'mirror_output', label: 'Mirror Output', type: 'boolean', defaultValue: false },
      { key: 'show_window', label: 'Show Window', type: 'boolean', defaultValue: true },
      onErrorProp,
    ],
  },
  {
    type: 'sftp',
    label: 'SFTP',
    category: 'ssh',
    icon: 'sftp',
    description: 'Transfer files via SFTP',
    previewKey: 'action',
    properties: [
      { key: 'action', label: 'Action', type: 'select', options: ['upload', 'download'], required: true },
      { key: 'local_path', label: 'Local Path', type: 'text', required: true, browse: 'file' },
      { key: 'remote_path', label: 'Remote Path', type: 'text', required: true },
      { key: 'host', label: 'Host Override', type: 'text' },
      { key: 'port', label: 'Port Override', type: 'number' },
      { key: 'username', label: 'Username Override', type: 'text' },
      { key: 'password', label: 'Password Override', type: 'text' },
      { key: 'overwrite', label: 'Overwrite Existing', type: 'boolean', defaultValue: true },
      timeoutProp,
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },

  // Control Flow
  {
    type: 'if',
    label: 'If',
    category: 'control-flow',
    icon: 'if',
    description: 'Conditional branch',
    previewKey: 'condition',
    outputs: 2,
    isContainer: true,
    properties: [{ key: 'condition', label: 'Condition', type: 'code', required: true }],
  },
  {
    type: 'foreach',
    label: 'Foreach',
    category: 'control-flow',
    icon: 'for',
    description: 'Iterate over a collection',
    previewKey: 'iterator',
    isContainer: true,
    properties: [
      { key: 'iterator', label: 'Iterator', type: 'code', required: true, placeholder: 'item in ${items}' },
      { key: 'when', label: 'When Condition', type: 'code' },
    ],
  },
  {
    type: 'while',
    label: 'While',
    category: 'control-flow',
    icon: 'while',
    description: 'Loop while condition is true',
    previewKey: 'condition',
    isContainer: true,
    properties: [
      { key: 'condition', label: 'Condition', type: 'code', required: true },
      { key: 'max_iterations', label: 'Max Iterations', type: 'number', defaultValue: 100 },
    ],
  },
  {
    type: 'switch',
    label: 'Switch',
    category: 'control-flow',
    icon: 'switch',
    description: 'Multi-branch based on expression',
    previewKey: 'value',
    isContainer: true,
    properties: [{ key: 'value', label: 'Value', type: 'code', required: true }],
  },
  { type: 'parallel', label: 'Parallel', category: 'control-flow', icon: 'parallel', description: 'Execute branches concurrently', isContainer: true, properties: [] },
  { type: 'try', label: 'Try', category: 'control-flow', icon: 'try', description: 'Error handling block', isContainer: true, properties: [] },
  { type: 'break', label: 'Break', category: 'control-flow', icon: 'break', description: 'Exit the current loop', properties: [] },
  { type: 'continue', label: 'Continue', category: 'control-flow', icon: 'continue', description: 'Skip to next loop iteration', properties: [] },
  {
    type: 'call',
    label: 'Call',
    category: 'control-flow',
    icon: 'call',
    description: 'Call a subroutine',
    previewKey: 'subroutine',
    properties: [{ key: 'subroutine', label: 'Subroutine', type: 'text', required: true }],
  },
  { type: 'return', label: 'Return', category: 'control-flow', icon: 'return', description: 'Return from subroutine', properties: [] },
  {
    type: 'exit',
    label: 'Exit',
    category: 'control-flow',
    icon: 'exit',
    description: 'Terminate script execution',
    previewKey: 'status',
    properties: [
      { key: 'status', label: 'Status', type: 'select', options: ['success', 'failure', 'error'], defaultValue: 'success' },
      { key: 'message', label: 'Message', type: 'text' },
    ],
  },

  // Data
  {
    type: 'extract',
    label: 'Extract',
    category: 'data',
    icon: 'extract',
    description: 'Extract data from output using regex',
    previewKey: 'pattern',
    properties: [
      { key: 'pattern', label: 'Pattern (regex)', type: 'code', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'from', label: 'From', type: 'text', required: true },
      { key: 'match', label: 'Match', type: 'select', options: ['first', 'all', 'last'], defaultValue: 'first' },
      { key: 'required', label: 'Required (fail on no match)', type: 'boolean', defaultValue: true },
    ],
  },
  {
    type: 'set',
    label: 'Set',
    category: 'data',
    icon: 'set',
    description: 'Set a variable value (format: varname = expression)',
    previewKey: 'expression',
    properties: [{ key: 'expression', label: 'Expression', type: 'code', required: true, placeholder: 'varname = value' }],
  },
  {
    type: 'parse',
    label: 'Parse',
    category: 'data',
    icon: 'parse',
    description: 'Parse structured output (FortiGate config)',
    previewKey: 'format',
    properties: [
      { key: 'format', label: 'Format', type: 'select', options: ['fortigate'], required: true },
      { key: 'from', label: 'From Variable', type: 'text', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'sections', label: 'Sections', type: 'text', placeholder: 'comma-separated' },
    ],
  },
  {
    type: 'table',
    label: 'Table',
    category: 'data',
    icon: 'table',
    description: 'Format data as a table',
    previewKey: 'data',
    properties: [
      { key: 'data', label: 'Data Variable', type: 'text', required: true },
      { key: 'columns', label: 'Columns', type: 'text' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      { key: 'align', label: 'Align', type: 'select', options: ['left', 'right', 'center'], defaultValue: 'left' },
      { key: 'show_header', label: 'Show Header', type: 'boolean', defaultValue: true },
    ],
  },
  {
    type: 'assert',
    label: 'Assert',
    category: 'data',
    icon: 'assert',
    description: 'Assert a condition or fail',
    previewKey: 'condition',
    properties: [
      { key: 'condition', label: 'Condition', type: 'code', required: true },
      { key: 'message', label: 'Failure Message', type: 'text' },
      { key: 'severity', label: 'Severity', type: 'select', options: ['error', 'warning'], defaultValue: 'error' },
    ],
  },

  // Network
  {
    type: 'ping',
    label: 'Ping',
    category: 'network',
    icon: 'ping',
    description: 'Ping a host',
    previewKey: 'host',
    properties: [
      { key: 'host', label: 'Host', type: 'text', required: true },
      { key: 'count', label: 'Count', type: 'number', defaultValue: 4 },
      timeoutProp,
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'dns',
    label: 'DNS',
    category: 'network',
    icon: 'dns',
    description: 'DNS lookup',
    previewKey: 'host',
    properties: [
      { key: 'host', label: 'Host', type: 'text', required: true },
      { key: 'type', label: 'Record Type', type: 'select', options: ['A', 'AAAA', 'PTR'], defaultValue: 'A' },
      timeoutProp,
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'portcheck',
    label: 'Port Check',
    category: 'network',
    icon: 'port',
    description: 'Check if a TCP port is open',
    previewKey: 'host',
    properties: [
      { key: 'host', label: 'Host', type: 'text', required: true },
      { key: 'port', label: 'Port', type: 'number', defaultValue: 22 },
      timeoutProp,
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'http',
    label: 'HTTP',
    category: 'network',
    icon: 'http',
    description: 'Make an HTTP request',
    previewKey: 'url',
    properties: [
      { key: 'url', label: 'URL', type: 'text', required: true },
      { key: 'method', label: 'Method', type: 'select', options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS'], defaultValue: 'GET' },
      { key: 'body', label: 'Body', type: 'textarea' },
      { key: 'headers', label: 'Headers (JSON)', type: 'textarea' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      timeoutProp,
      { key: 'follow_redirects', label: 'Follow Redirects', type: 'boolean', defaultValue: true },
      { key: 'allow_failure', label: 'Allow Failure', type: 'boolean', defaultValue: false },
      { key: 'verify_tls', label: 'Verify TLS', type: 'boolean', defaultValue: true },
      { key: 'auth', label: 'Auth', type: 'select', options: ['none', 'basic', 'bearer'], defaultValue: 'none' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'password', label: 'Password', type: 'text' },
      { key: 'token', label: 'Bearer Token', type: 'text' },
      { key: 'content_type', label: 'Content Type', type: 'select', options: ['json', 'form', 'text', 'xml'] },
      onErrorProp,
    ],
  },
  {
    type: 'webhook',
    label: 'Webhook',
    category: 'network',
    icon: 'webhook',
    description: 'Send a webhook request',
    previewKey: 'url',
    properties: [
      { key: 'url', label: 'URL', type: 'text', required: true },
      { key: 'method', label: 'Method', type: 'select', options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'], defaultValue: 'POST' },
      { key: 'body', label: 'Body', type: 'textarea' },
      { key: 'headers', label: 'Headers (JSON)', type: 'textarea' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      timeoutProp,
      onErrorProp,
    ],
  },
  {
    type: 'browser_callback',
    label: 'Browser Callback',
    category: 'network',
    icon: 'oauth',
    description: 'Capture OAuth/SSO browser callback',
    previewKey: 'start_url',
    properties: [
      { key: 'start_url', label: 'Start URL', type: 'text', required: true },
      { key: 'callback_path', label: 'Callback Path', type: 'text', defaultValue: '/callback' },
      { key: 'local_port', label: 'Local Port', type: 'number', defaultValue: 8086 },
      { key: 'capture_mode', label: 'Capture Mode', type: 'select', options: ['auto', 'fragment', 'query', 'post_body'], defaultValue: 'auto' },
      { key: 'browser_mode', label: 'Browser Mode', type: 'select', options: ['external', 'webview2'], defaultValue: 'external' },
      { key: 'show_after_seconds', label: 'Show After (s)', type: 'number', defaultValue: 0 },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'required_fields', label: 'Required Fields', type: 'text', placeholder: 'comma-separated' },
      timeoutProp,
      { key: 'open_browser', label: 'Open Browser', type: 'boolean', defaultValue: true },
      { key: 'auto_close_browser', label: 'Auto Close Browser', type: 'boolean', defaultValue: true },
      { key: 'completion_message', label: 'Completion Message', type: 'text' },
      { key: 'failure_message', label: 'Failure Message', type: 'text' },
      { key: 'quiet', label: 'Quiet Output', type: 'boolean', defaultValue: true },
      onErrorProp,
    ],
  },

  {
    type: 'vault',
    label: 'Vault',
    category: 'network',
    icon: 'vault',
    description: 'Read, write, or patch secrets from HashiCorp Vault',
    previewKey: 'path',
    properties: [
      { key: 'profile', label: 'Profile', type: 'text' },
      { key: 'path', label: 'Path', type: 'text', required: true },
      { key: 'key', label: 'Key', type: 'text' },
      { key: 'keys', label: 'Keys Map', type: 'keyvalue' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      { key: 'version', label: 'Version', type: 'number' },
      { key: 'write', label: 'Write Data', type: 'keyvalue' },
      { key: 'patch', label: 'Patch Data', type: 'keyvalue' },
      onErrorProp,
    ],
  },

  // I/O & UI
  {
    type: 'print',
    label: 'Print',
    category: 'io',
    icon: 'print',
    description: 'Output a message',
    previewKey: 'message',
    properties: [{ key: 'message', label: 'Message', type: 'code', required: true }],
  },
  {
    type: 'input',
    label: 'Input',
    category: 'io',
    icon: 'input',
    description: 'Prompt user for text input',
    previewKey: 'prompt',
    properties: [
      { key: 'title', label: 'Title', type: 'text' },
      { key: 'prompt', label: 'Prompt', type: 'text' },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'default', label: 'Default Value', type: 'text' },
      { key: 'password', label: 'Password Mode', type: 'boolean', defaultValue: false },
      { key: 'validate', label: 'Validate Regex', type: 'text' },
      { key: 'validation_error', label: 'Validation Error', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'choose',
    label: 'Choose',
    category: 'io',
    icon: 'choose',
    description: 'Show a selection dialog',
    previewKey: 'prompt',
    properties: [
      { key: 'title', label: 'Title', type: 'text' },
      { key: 'prompt', label: 'Prompt', type: 'text' },
      {
        key: 'options',
        label: 'Options',
        type: 'text',
        required: true,
        editor: 'choice-options',
        helpText: 'Select source mode: use a runtime variable (${var} or var_name), or define static option rows.',
      },
      { key: 'default', label: 'Default Value', type: 'text' },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      onErrorProp,
    ],
  },
  {
    type: 'multiselect',
    label: 'Multi-Select',
    category: 'io',
    icon: 'multi',
    description: 'Show a multi-selection dialog',
    previewKey: 'prompt',
    properties: [
      { key: 'title', label: 'Title', type: 'text' },
      { key: 'prompt', label: 'Prompt', type: 'text' },
      {
        key: 'options',
        label: 'Options',
        type: 'text',
        required: true,
        editor: 'choice-options',
        helpText: 'Select source mode: use a runtime variable (${var} or var_name), or define static option rows.',
      },
      { key: 'min', label: 'Min', type: 'number' },
      { key: 'max', label: 'Max', type: 'number' },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      onErrorProp,
    ],
  },
  {
    type: 'confirm',
    label: 'Confirm',
    category: 'io',
    icon: 'confirm',
    description: 'Show a yes/no confirmation',
    previewKey: 'prompt',
    properties: [
      { key: 'title', label: 'Title', type: 'text' },
      { key: 'prompt', label: 'Prompt', type: 'text' },
      { key: 'default', label: 'Default Yes', type: 'boolean', defaultValue: false },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      onErrorProp,
    ],
  },
  {
    type: 'readfile',
    label: 'Read File',
    category: 'io',
    icon: 'read',
    description: 'Read a local file',
    previewKey: 'path',
    properties: [
      { key: 'path', label: 'File Path', type: 'text', required: true, browse: 'file' },
      { key: 'select_file', label: 'Pick File At Runtime', type: 'boolean', defaultValue: false },
      { key: 'message', label: 'Picker Message', type: 'text' },
      { key: 'fileext', label: 'Allowed Extensions', type: 'text' },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'skip_empty_lines', label: 'Skip Empty Lines', type: 'boolean', defaultValue: true },
      { key: 'trim_lines', label: 'Trim Lines', type: 'boolean', defaultValue: true },
      { key: 'max_lines', label: 'Max Lines', type: 'number', defaultValue: 10000 },
      { key: 'encoding', label: 'Encoding', type: 'select', options: ['utf-8', 'ascii', 'utf-16', 'utf-32'], defaultValue: 'utf-8' },
      onErrorProp,
    ],
  },
  {
    type: 'writefile',
    label: 'Write File',
    category: 'io',
    icon: 'write',
    description: 'Write to a local file',
    previewKey: 'path',
    properties: [
      { key: 'path', label: 'File Path', type: 'text', required: true, browse: 'file' },
      { key: 'content', label: 'Content', type: 'textarea' },
      { key: 'mode', label: 'Mode', type: 'select', options: ['overwrite', 'append'], defaultValue: 'overwrite' },
      { key: 'format', label: 'Format', type: 'select', options: ['text', 'json', 'jsonl', 'csv'], defaultValue: 'text' },
      { key: 'pretty', label: 'Pretty JSON', type: 'boolean', defaultValue: true },
      { key: 'headers', label: 'CSV Headers (comma-separated)', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'exists',
    label: 'Exists',
    category: 'io',
    icon: 'exists',
    description: 'Check whether a local path exists',
    previewKey: 'path',
    properties: [
      { key: 'path', label: 'Path', type: 'text', required: true, browse: 'file' },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'type', label: 'Type', type: 'select', options: ['any', 'file', 'directory'], defaultValue: 'any' },
      onErrorProp,
    ],
  },
  {
    type: 'playsound',
    label: 'Play Sound',
    category: 'io',
    icon: 'audio',
    description: 'Play a local WAV or MP3 file',
    previewKey: 'path',
    properties: [
      { key: 'path', label: 'File Path', type: 'text', required: true, browse: 'file' },
      { key: 'wait', label: 'Wait For Completion', type: 'boolean', defaultValue: true },
      { key: 'volume', label: 'Volume (0-100)', type: 'number', defaultValue: 100 },
      { key: 'max_seconds', label: 'Max Seconds', type: 'number' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'log',
    label: 'Log',
    category: 'io',
    icon: 'log',
    description: 'Structured log message',
    previewKey: 'message',
    properties: [
      { key: 'message', label: 'Message', type: 'code', required: true },
      { key: 'level', label: 'Level', type: 'select', options: ['info', 'debug', 'warning', 'error', 'success'], defaultValue: 'info' },
    ],
  },

  // Local Command
  {
    type: 'localcmd',
    label: 'Local Command',
    category: 'io',
    icon: 'terminal',
    description: 'Run a command on the local machine',
    previewKey: 'command',
    properties: [
      { key: 'command', label: 'Command', type: 'textarea', required: true,
        placeholder: 'Get-Process | Select-Object -First 5',
        helpText: 'The command to execute locally', group: 'core' },
      { key: 'shell', label: 'Shell', type: 'select',
        options: ['powershell', 'custom'], defaultValue: 'powershell',
        helpText: 'Shell to execute the command in. "custom" enables Shell Path.', group: 'core' },
      { key: 'shell_path', label: 'Shell Path', type: 'text',
        placeholder: 'python',
        helpText: 'Path to custom shell executable (Shell=custom)', group: 'core' },
      { key: 'args', label: 'Shell Arguments', type: 'textarea',
        placeholder: '["-NoProfile"]',
        helpText: 'Prefer JSON array syntax. Scalar string still supported.', group: 'core' },
      { key: 'env', label: 'Environment (JSON)', type: 'textarea',
        placeholder: '{"CONFIGURATION":"Release"}',
        helpText: 'Optional process environment variables', group: 'core' },
      { key: 'working_dir', label: 'Working Directory', type: 'text',
        placeholder: 'C:\\Scripts',
        helpText: 'Directory to run the command in', group: 'core' },
      { key: 'interactive', label: 'Interactive', type: 'boolean', defaultValue: false,
        helpText: 'Open in an external terminal window (foreground only)', group: 'core' },
      { key: 'keep_open', label: 'Keep Open', type: 'boolean', defaultValue: false,
        helpText: 'Keep the terminal open after command completion (interactive only)', group: 'core' },
      { key: 'run_mode', label: 'Run Mode', type: 'select',
        options: ['foreground', 'background'], defaultValue: 'foreground',
        helpText: 'Foreground waits for completion; background returns after spawn', group: 'core' },
      { key: 'lifetime', label: 'Background Lifetime', type: 'select',
        options: ['detached', 'script', 'app'], defaultValue: 'detached',
        helpText: 'Applies only when run_mode=background', group: 'advanced' },
      { key: 'kill_on_cancel', label: 'Kill On Cancel', type: 'boolean', defaultValue: false,
        helpText: 'Applies to non-detached background mode', group: 'advanced' },
      { key: 'fail_on_nonzero', label: 'Fail On Non-Zero', type: 'boolean', defaultValue: true,
        helpText: 'Fail when exit code is not in success_codes', group: 'advanced' },
      { key: 'success_codes', label: 'Success Codes', type: 'text',
        placeholder: '0,3010',
        helpText: 'Comma-separated allowed exit codes (foreground and interactive close)', group: 'advanced' },
      { key: 'max_output_bytes', label: 'Max Capture Bytes', type: 'number', defaultValue: 1048576,
        helpText: 'Per-stream capture limit', group: 'advanced' },
      { key: 'confirm', label: 'Confirm Policy', type: 'select',
        options: ['always', 'once', 'never'], defaultValue: 'always',
        helpText: 'Prompt policy before execution', group: 'advanced' },
      { key: 'quiet', label: 'Quiet Command Echo', type: 'boolean', defaultValue: false,
        helpText: 'Hide the [localcmd] command banner lines', group: 'advanced' },
      { key: 'suppress', label: 'Suppress Output', type: 'boolean', defaultValue: false,
        helpText: 'Hide command banner and live stdout/stderr output (capture still works)', group: 'advanced' },
      { key: 'into', label: 'Into Prefix', type: 'text',
        placeholder: 'result',
        helpText: 'Prefix for output variables. Interactive mode sets only <into>_exit_code.', group: 'core' },
      timeoutProp,
      onErrorProp,
    ],
  },

  // Grid Updates
  {
    type: 'updatecolumn',
    label: 'Update Column',
    category: 'grid',
    icon: 'column',
    description: 'Write a value back to the host grid',
    previewKey: 'value',
    properties: [
      { key: 'column', label: 'Column Name', type: 'text', required: true },
      { key: 'value', label: 'Value', type: 'code', required: true },
    ],
  },
  {
    type: 'updateenvironment',
    label: 'Update Environment',
    category: 'grid',
    icon: 'env',
    description: 'Set an environment variable',
    previewKey: 'value',
    properties: [
      { key: 'variable', label: 'Variable Name', type: 'text', required: true },
      { key: 'value', label: 'Value', type: 'code', required: true },
    ],
  },

  // Timing
  {
    type: 'wait',
    label: 'Wait',
    category: 'timing',
    icon: 'wait',
    description: 'Pause execution',
    previewKey: 'seconds',
    properties: [{ key: 'seconds', label: 'Seconds', type: 'number', required: true, defaultValue: 1 }],
  },
];

/** Lookup block definition by type. */
export const blockDefMap = new Map(blockDefs.map((d) => [d.type, d]));

/** Get block definitions grouped by category. */
export function getBlocksByCategory(): Map<BlockCategory, BlockDef[]> {
  const map = new Map<BlockCategory, BlockDef[]>();
  for (const def of blockDefs) {
    if (!map.has(def.category)) map.set(def.category, []);
    map.get(def.category)!.push(def);
  }
  return map;
}

export const categoryLabels: Record<BlockCategory, string> = {
  ssh: 'SSH',
  'control-flow': 'Control Flow',
  data: 'Data',
  network: 'Network',
  io: 'I/O & UI',
  grid: 'Grid Updates',
  timing: 'Timing',
};
