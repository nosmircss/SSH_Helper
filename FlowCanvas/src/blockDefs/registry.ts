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

export interface PropertyDef {
  key: string;
  label: string;
  type: 'text' | 'number' | 'boolean' | 'select' | 'code' | 'textarea';
  required?: boolean;
  placeholder?: string;
  options?: string[];
  defaultValue?: unknown;
}

export interface BlockDef {
  type: string;
  label: string;
  category: BlockCategory;
  icon: string;
  description: string;
  /** Key property shown in the block preview (e.g., the command text for SEND) */
  previewKey?: string;
  /** Number of output handles (1 = normal, 2 = if true/false, N = switch cases) */
  outputs?: number;
  /** Whether this block is a container that holds child steps */
  isContainer?: boolean;
  /** Property schema for the properties panel */
  properties: PropertyDef[];
}

export const categoryColors: Record<BlockCategory, { border: string; bg: string; badge: string; badgeText: string; text: string }> = {
  'ssh':          { border: '#4a9eff', bg: '#1a2744', badge: '#4a9eff', badgeText: '#000', text: '#8aafdb' },
  'control-flow': { border: '#f0c040', bg: '#2a2a1a', badge: '#f0c040', badgeText: '#000', text: '#e0d080' },
  'data':         { border: '#9b59b6', bg: '#1a1a2a', badge: '#9b59b6', badgeText: '#fff', text: '#c9a0dc' },
  'network':      { border: '#1abc9c', bg: '#1a2a2a', badge: '#1abc9c', badgeText: '#000', text: '#80d4c0' },
  'io':           { border: '#e67e22', bg: '#201a2a', badge: '#e67e22', badgeText: '#fff', text: '#e8a860' },
  'grid':         { border: '#3498db', bg: '#1a2a3a', badge: '#3498db', badgeText: '#fff', text: '#7ab8e0' },
  'timing':       { border: '#95a5a6', bg: '#1a1a1a', badge: '#95a5a6', badgeText: '#000', text: '#bdc3c7' },
};

const onErrorProp: PropertyDef = {
  key: 'on_error', label: 'On Error', type: 'select',
  options: ['stop', 'continue'], defaultValue: 'stop',
};

const timeoutProp: PropertyDef = {
  key: 'timeout', label: 'Timeout (s)', type: 'number', placeholder: 'default',
};

export const blockDefs: BlockDef[] = [
  // ── SSH ──
  {
    type: 'send', label: 'Send', category: 'ssh', icon: '📡',
    description: 'Send an SSH command and capture output',
    previewKey: 'command',
    properties: [
      { key: 'command', label: 'Command', type: 'code', required: true },
      timeoutProp,
      { key: 'expect', label: 'Expect Pattern', type: 'text', placeholder: 'regex' },
      { key: 'delay', label: 'Delay (ms)', type: 'number' },
      onErrorProp,
    ],
  },
  {
    type: 'interactive', label: 'Interactive', category: 'ssh', icon: '🔗',
    description: 'Open an interactive terminal session',
    properties: [
      { key: 'title', label: 'Window Title', type: 'text' },
      timeoutProp,
    ],
  },
  {
    type: 'sftp', label: 'SFTP', category: 'ssh', icon: '📁',
    description: 'Transfer files via SFTP',
    previewKey: 'action',
    properties: [
      { key: 'action', label: 'Action', type: 'select', options: ['upload', 'download'], required: true },
      { key: 'local', label: 'Local Path', type: 'text', required: true },
      { key: 'remote', label: 'Remote Path', type: 'text', required: true },
      onErrorProp,
    ],
  },

  // ── Control Flow ──
  {
    type: 'if', label: 'If', category: 'control-flow', icon: '🔀',
    description: 'Conditional branch',
    previewKey: 'condition',
    outputs: 2, isContainer: true,
    properties: [
      { key: 'condition', label: 'Condition', type: 'code', required: true },
    ],
  },
  {
    type: 'foreach', label: 'Foreach', category: 'control-flow', icon: '🔁',
    description: 'Iterate over a collection',
    previewKey: 'expression',
    isContainer: true,
    properties: [
      { key: 'variable', label: 'Variable', type: 'text', required: true, placeholder: 'item' },
      { key: 'expression', label: 'In Expression', type: 'code', required: true },
    ],
  },
  {
    type: 'while', label: 'While', category: 'control-flow', icon: '🔄',
    description: 'Loop while condition is true',
    previewKey: 'condition',
    isContainer: true,
    properties: [
      { key: 'condition', label: 'Condition', type: 'code', required: true },
      { key: 'max_iterations', label: 'Max Iterations', type: 'number', defaultValue: 100 },
    ],
  },
  {
    type: 'switch', label: 'Switch', category: 'control-flow', icon: '🔃',
    description: 'Multi-branch based on expression',
    previewKey: 'expression',
    isContainer: true,
    properties: [
      { key: 'expression', label: 'Expression', type: 'code', required: true },
    ],
  },
  {
    type: 'parallel', label: 'Parallel', category: 'control-flow', icon: '⚡',
    description: 'Execute branches concurrently',
    isContainer: true,
    properties: [],
  },
  {
    type: 'try', label: 'Try', category: 'control-flow', icon: '🛡',
    description: 'Error handling block',
    isContainer: true,
    properties: [],
  },
  {
    type: 'break', label: 'Break', category: 'control-flow', icon: '⏹',
    description: 'Exit the current loop',
    properties: [],
  },
  {
    type: 'continue', label: 'Continue', category: 'control-flow', icon: '⏭',
    description: 'Skip to next loop iteration',
    properties: [],
  },
  {
    type: 'call', label: 'Call', category: 'control-flow', icon: '📞',
    description: 'Call a subroutine',
    previewKey: 'subroutine',
    properties: [
      { key: 'subroutine', label: 'Subroutine', type: 'text', required: true },
    ],
  },
  {
    type: 'return', label: 'Return', category: 'control-flow', icon: '↩',
    description: 'Return from subroutine',
    properties: [
      { key: 'value', label: 'Return Value', type: 'text' },
    ],
  },
  {
    type: 'exit', label: 'Exit', category: 'control-flow', icon: '🚪',
    description: 'Terminate script execution',
    previewKey: 'status',
    properties: [
      { key: 'status', label: 'Status', type: 'select', options: ['success', 'error'] },
      { key: 'message', label: 'Message', type: 'text' },
    ],
  },

  // ── Data ──
  {
    type: 'extract', label: 'Extract', category: 'data', icon: '🔍',
    description: 'Extract data from output using regex',
    previewKey: 'pattern',
    properties: [
      { key: 'pattern', label: 'Pattern (regex)', type: 'code', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'source', label: 'Source', type: 'select', options: ['output', 'variable'], defaultValue: 'output' },
      { key: 'match', label: 'Match', type: 'select', options: ['first', 'all', 'last'], defaultValue: 'first' },
    ],
  },
  {
    type: 'set', label: 'Set', category: 'data', icon: '📝',
    description: 'Set a variable value (format: varname = expression)',
    previewKey: 'expression',
    properties: [
      { key: 'expression', label: 'Expression', type: 'code', required: true, placeholder: 'varname = value' },
    ],
  },
  {
    type: 'parse', label: 'Parse', category: 'data', icon: '📊',
    description: 'Parse structured output (FortiGate config)',
    previewKey: 'format',
    properties: [
      { key: 'format', label: 'Format', type: 'select', options: ['fortigate'], required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
    ],
  },
  {
    type: 'table', label: 'Table', category: 'data', icon: '📋',
    description: 'Format data as a table',
    properties: [
      { key: 'source', label: 'Source Variable', type: 'text', required: true },
      { key: 'columns', label: 'Columns', type: 'text' },
    ],
  },
  {
    type: 'assert', label: 'Assert', category: 'data', icon: '✅',
    description: 'Assert a condition or fail',
    previewKey: 'condition',
    properties: [
      { key: 'condition', label: 'Condition', type: 'code', required: true },
      { key: 'message', label: 'Failure Message', type: 'text' },
    ],
  },

  // ── Network ──
  {
    type: 'ping', label: 'Ping', category: 'network', icon: '🏓',
    description: 'Ping a host',
    previewKey: 'target',
    properties: [
      { key: 'target', label: 'Target', type: 'text', required: true },
      { key: 'count', label: 'Count', type: 'number', defaultValue: 4 },
      onErrorProp,
    ],
  },
  {
    type: 'dns', label: 'DNS', category: 'network', icon: '🌐',
    description: 'DNS lookup',
    previewKey: 'hostname',
    properties: [
      { key: 'hostname', label: 'Hostname', type: 'text', required: true },
      { key: 'type', label: 'Record Type', type: 'select', options: ['A', 'AAAA', 'MX', 'TXT', 'CNAME'] },
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'portcheck', label: 'Port Check', category: 'network', icon: '🔌',
    description: 'Check if a TCP port is open',
    previewKey: 'target',
    properties: [
      { key: 'target', label: 'Host:Port', type: 'text', required: true },
      timeoutProp,
      onErrorProp,
    ],
  },
  {
    type: 'http', label: 'HTTP', category: 'network', icon: '🌍',
    description: 'Make an HTTP request',
    previewKey: 'url',
    properties: [
      { key: 'url', label: 'URL', type: 'text', required: true },
      { key: 'method', label: 'Method', type: 'select', options: ['GET', 'POST', 'PUT', 'DELETE'], defaultValue: 'GET' },
      { key: 'body', label: 'Body', type: 'textarea' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      onErrorProp,
    ],
  },
  {
    type: 'webhook', label: 'Webhook', category: 'network', icon: '🪝',
    description: 'Send a webhook POST',
    previewKey: 'url',
    properties: [
      { key: 'url', label: 'URL', type: 'text', required: true },
      { key: 'body', label: 'Body', type: 'textarea' },
      onErrorProp,
    ],
  },
  {
    type: 'browser_callback', label: 'Browser Callback', category: 'network', icon: '🌐',
    description: 'Capture OAuth/SSO browser callback',
    previewKey: 'url',
    properties: [
      { key: 'url', label: 'Start URL', type: 'text', required: true },
      { key: 'callback_path', label: 'Callback Path', type: 'text', defaultValue: '/callback' },
      { key: 'into', label: 'Into Variable', type: 'text' },
      timeoutProp,
    ],
  },

  // ── I/O & UI ──
  {
    type: 'print', label: 'Print', category: 'io', icon: '💬',
    description: 'Output a message',
    previewKey: 'message',
    properties: [
      { key: 'message', label: 'Message', type: 'code', required: true },
    ],
  },
  {
    type: 'input', label: 'Input', category: 'io', icon: '⌨️',
    description: 'Prompt user for text input',
    previewKey: 'prompt',
    properties: [
      { key: 'prompt', label: 'Prompt', type: 'text', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      { key: 'default', label: 'Default Value', type: 'text' },
    ],
  },
  {
    type: 'choose', label: 'Choose', category: 'io', icon: '☑️',
    description: 'Show a selection dialog',
    previewKey: 'prompt',
    properties: [
      { key: 'prompt', label: 'Prompt', type: 'text', required: true },
      { key: 'options', label: 'Options (comma-separated)', type: 'text', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
    ],
  },
  {
    type: 'multiselect', label: 'Multi-Select', category: 'io', icon: '☑️',
    description: 'Show a multi-selection dialog',
    previewKey: 'prompt',
    properties: [
      { key: 'prompt', label: 'Prompt', type: 'text', required: true },
      { key: 'options', label: 'Options (comma-separated)', type: 'text', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
    ],
  },
  {
    type: 'confirm', label: 'Confirm', category: 'io', icon: '❓',
    description: 'Show a yes/no confirmation',
    previewKey: 'prompt',
    properties: [
      { key: 'prompt', label: 'Prompt', type: 'text', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
    ],
  },
  {
    type: 'readfile', label: 'Read File', category: 'io', icon: '📂',
    description: 'Read a local file',
    previewKey: 'path',
    properties: [
      { key: 'path', label: 'File Path', type: 'text', required: true },
      { key: 'into', label: 'Into Variable', type: 'text', required: true },
      onErrorProp,
    ],
  },
  {
    type: 'writefile', label: 'Write File', category: 'io', icon: '💾',
    description: 'Write to a local file',
    previewKey: 'path',
    properties: [
      { key: 'path', label: 'File Path', type: 'text', required: true },
      { key: 'content', label: 'Content', type: 'textarea', required: true },
      { key: 'append', label: 'Append', type: 'boolean', defaultValue: false },
      onErrorProp,
    ],
  },
  {
    type: 'log', label: 'Log', category: 'io', icon: '📋',
    description: 'Structured log message',
    previewKey: 'message',
    properties: [
      { key: 'message', label: 'Message', type: 'code', required: true },
      { key: 'level', label: 'Level', type: 'select', options: ['info', 'warning', 'error', 'debug'], defaultValue: 'info' },
    ],
  },

  // ── Grid Updates ──
  {
    type: 'updatecolumn', label: 'Update Column', category: 'grid', icon: '📊',
    description: 'Write a value back to the host grid',
    previewKey: 'expression',
    properties: [
      { key: 'column', label: 'Column Name', type: 'text', required: true },
      { key: 'expression', label: 'Value', type: 'code', required: true },
    ],
  },
  {
    type: 'updateenvironment', label: 'Update Environment', category: 'grid', icon: '🌍',
    description: 'Set an environment variable',
    previewKey: 'expression',
    properties: [
      { key: 'variable', label: 'Variable Name', type: 'text', required: true },
      { key: 'expression', label: 'Value', type: 'code', required: true },
    ],
  },

  // ── Timing ──
  {
    type: 'wait', label: 'Wait', category: 'timing', icon: '⏱',
    description: 'Pause execution',
    previewKey: 'duration',
    properties: [
      { key: 'duration', label: 'Duration (ms)', type: 'number', required: true, defaultValue: 1000 },
    ],
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
  'ssh': 'SSH',
  'control-flow': 'Control Flow',
  'data': 'Data',
  'network': 'Network',
  'io': 'I/O & UI',
  'grid': 'Grid Updates',
  'timing': 'Timing',
};
