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
