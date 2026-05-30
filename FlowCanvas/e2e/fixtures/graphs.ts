export interface GraphFixture {
  nodes: Array<Record<string, unknown>>;
  edges: Array<Record<string, unknown>>;
}

export function createRunParityFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-1',
        type: 'block',
        position: { x: 120, y: 80 },
        data: {
          blockType: 'print',
          label: 'Alpha',
          props: { message: 'alpha' },
        },
      },
      {
        id: 'node-2',
        type: 'block',
        position: { x: 120, y: 220 },
        data: {
          blockType: 'print',
          label: 'Beta',
          props: { message: 'beta' },
        },
      },
      {
        id: 'comment-1',
        type: 'comment',
        position: { x: 360, y: 120 },
        data: {
          blockType: 'comment',
          commentId: 'comment-1',
          text: 'fixture-comment',
        },
      },
    ],
    edges: [
      {
        id: 'edge-1-2',
        source: 'node-1',
        target: 'node-2',
      },
    ],
  };
}

export function createInteractionFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-1',
        type: 'block',
        position: { x: 100, y: 80 },
        data: {
          blockType: 'print',
          label: 'One',
          props: { message: 'one' },
        },
      },
      {
        id: 'node-2',
        type: 'block',
        position: { x: 100, y: 240 },
        data: {
          blockType: 'print',
          label: 'Two',
          props: { message: 'two' },
        },
      },
      {
        id: 'node-3',
        type: 'block',
        position: { x: 420, y: 140 },
        data: {
          blockType: 'print',
          label: 'Three',
          props: { message: 'three' },
        },
      },
    ],
    edges: [
      {
        id: 'edge-1-2',
        source: 'node-1',
        target: 'node-2',
      },
      {
        id: 'edge-2-3',
        source: 'node-2',
        target: 'node-3',
      },
    ],
  };
}

export function createPropertiesTypingFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-send',
        type: 'block',
        position: { x: 120, y: 100 },
        data: {
          blockType: 'send',
          label: 'Send Block',
          props: {
            _preview: 'old command from import',
            command: 'old command from import',
            expect: '',
          },
        },
      },
      {
        id: 'node-http',
        type: 'block',
        position: { x: 420, y: 100 },
        data: {
          blockType: 'http',
          label: 'Http Block',
          props: {
            url: 'https://example.local/api',
            body: '',
          },
        },
      },
    ],
    edges: [
      {
        id: 'edge-send-http',
        source: 'node-send',
        target: 'node-http',
      },
    ],
  };
}

export function createPathPropertyFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-playsound',
        type: 'block',
        position: { x: 160, y: 120 },
        data: {
          blockType: 'playsound',
          label: 'Play Sound',
          props: {
            path: '',
          },
        },
      },
    ],
    edges: [],
  };
}

export function createChoiceOptionsUxFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-choose-ux',
        type: 'block',
        position: { x: 140, y: 120 },
        data: {
          blockType: 'choose',
          label: 'Choose UX',
          props: {
            into: 'selected_choice',
            options: 'alpha,beta',
            default: 'missing-default',
          },
        },
      },
      {
        id: 'node-multiselect-ux',
        type: 'block',
        position: { x: 420, y: 120 },
        data: {
          blockType: 'multiselect',
          label: 'Multi UX',
          props: {
            into: 'selected_choices',
            options: ['one', { label: 'Two Label', value: 'two_value' }],
          },
        },
      },
    ],
    edges: [],
  };
}

export function createRequiredMarkersFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-extract',
        type: 'block',
        position: { x: 80, y: 80 },
        data: {
          blockType: 'extract',
          label: 'Extract',
          props: {
            pattern: 'Version (.+)',
            into: 'version',
          },
        },
      },
      {
        id: 'node-browser-callback',
        type: 'block',
        position: { x: 280, y: 80 },
        data: {
          blockType: 'browser_callback',
          label: 'Browser Callback',
          props: {
            start_url: 'https://idp.example.com/start',
          },
        },
      },
      {
        id: 'node-input',
        type: 'block',
        position: { x: 480, y: 80 },
        data: {
          blockType: 'input',
          label: 'Input',
          props: {
            into: 'answer',
          },
        },
      },
      {
        id: 'node-choose',
        type: 'block',
        position: { x: 680, y: 80 },
        data: {
          blockType: 'choose',
          label: 'Choose',
          props: {
            into: 'choice',
            options: ['a', 'b'],
          },
        },
      },
      {
        id: 'node-multiselect',
        type: 'block',
        position: { x: 880, y: 80 },
        data: {
          blockType: 'multiselect',
          label: 'Multiselect',
          props: {
            into: 'choices',
            options: ['a', 'b'],
          },
        },
      },
      {
        id: 'node-confirm',
        type: 'block',
        position: { x: 80, y: 300 },
        data: {
          blockType: 'confirm',
          label: 'Confirm',
          props: {
            into: 'confirmed',
          },
        },
      },
      {
        id: 'node-portcheck',
        type: 'block',
        position: { x: 280, y: 300 },
        data: {
          blockType: 'portcheck',
          label: 'Portcheck',
          props: {
            host: '127.0.0.1',
          },
        },
      },
      {
        id: 'node-writefile',
        type: 'block',
        position: { x: 480, y: 300 },
        data: {
          blockType: 'writefile',
          label: 'Writefile',
          props: {
            path: 'C:\\temp\\out.txt',
          },
        },
      },
      {
        id: 'node-readfile',
        type: 'block',
        position: { x: 680, y: 300 },
        data: {
          blockType: 'readfile',
          label: 'Readfile',
          props: {
            into: 'lines',
            select_file: false,
          },
        },
      },
      {
        id: 'node-http-required',
        type: 'block',
        position: { x: 880, y: 300 },
        data: {
          blockType: 'http',
          label: 'HTTP',
          props: {
            url: 'https://example.com',
            auth: 'none',
          },
        },
      },
      {
        id: 'node-interactive-required',
        type: 'block',
        position: { x: 80, y: 520 },
        data: {
          blockType: 'interactive',
          label: 'Interactive',
          props: {
            show_window: true,
          },
        },
      },
    ],
    edges: [],
  };
}

// Linear chain (__start__ → g-a → g-b → g-c) plus a free node (g-free). Supports the
// connection-guard gesture cases: self-loop (g-a→g-a), duplicate (g-a→g-b again),
// second-plain-successor (g-a→g-free, g-a already has g-b), fan-in (g-free→g-b),
// and cycle (g-c→g-a). The chain is wide enough that handles don't overlap during drags.
export function createConnectionGuardFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 20 },
        data: { blockType: '_start', label: 'Start', props: {} },
      },
      {
        id: 'g-a',
        type: 'block',
        position: { x: 80, y: 160 },
        data: { blockType: 'print', label: 'A', props: { message: 'a' } },
      },
      {
        id: 'g-b',
        type: 'block',
        position: { x: 80, y: 280 },
        data: { blockType: 'print', label: 'B', props: { message: 'b' } },
      },
      {
        id: 'g-c',
        type: 'block',
        position: { x: 80, y: 400 },
        data: { blockType: 'print', label: 'C', props: { message: 'c' } },
      },
      {
        id: 'g-free',
        type: 'block',
        position: { x: 380, y: 160 },
        data: { blockType: 'print', label: 'Free', props: { message: 'free' } },
      },
    ],
    edges: [
      { id: 'g-start-a', source: '__start__', target: 'g-a' },
      { id: 'g-a-b', source: 'g-a', target: 'g-b' },
      { id: 'g-b-c', source: 'g-b', target: 'g-c' },
    ],
  };
}

// Pre-existing fan-in (two edges into g-sink). Used to prove the guard does NOT gate the
// load path — an imported graph with fan-in must still load intact (guard runs on new drags only).
export function createFanInLoadFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 20 },
        data: { blockType: '_start', label: 'Start', props: {} },
      },
      {
        id: 'g-src1',
        type: 'block',
        position: { x: 40, y: 160 },
        data: { blockType: 'print', label: 'Src1', props: { message: 's1' } },
      },
      {
        id: 'g-src2',
        type: 'block',
        position: { x: 320, y: 160 },
        data: { blockType: 'print', label: 'Src2', props: { message: 's2' } },
      },
      {
        id: 'g-sink',
        type: 'block',
        position: { x: 180, y: 320 },
        data: { blockType: 'print', label: 'Sink', props: { message: 'sink' } },
      },
    ],
    edges: [
      { id: 'g-start-src1', source: '__start__', target: 'g-src1' },
      { id: 'g-src1-sink', source: 'g-src1', target: 'g-sink' },
      { id: 'g-src2-sink', source: 'g-src2', target: 'g-sink' },
    ],
  };
}

export function createImportedChildEditingFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 40 },
        data: {
          blockType: '_start',
          label: 'Untitled Script',
          props: {},
        },
      },
      {
        id: 'if-1',
        type: 'block',
        position: { x: 80, y: 140 },
        data: {
          blockType: 'if',
          label: 'If',
          props: {
            condition: '${enabled}',
            _yamlSnippet: '- if:\n    condition: "${enabled}"\n    then:\n      - print:\n          message: stale-from-snippet\n',
          },
        },
      },
      {
        id: 'then-1',
        type: 'block',
        position: { x: 80, y: 280 },
        data: {
          blockType: 'print',
          label: 'Print',
          props: {
            _isChildOf: 'if-1',
            _branchLabel: 'then',
            _branchColor: '#2ecc71',
            message: 'imported-child-value',
          },
        },
      },
    ],
    edges: [
      {
        id: 'edge-start-if',
        source: '__start__',
        target: 'if-1',
      },
      {
        id: 'edge-if-then',
        source: 'if-1',
        target: 'then-1',
        data: {
          branchPath: 'then',
        },
      },
    ],
  };
}
