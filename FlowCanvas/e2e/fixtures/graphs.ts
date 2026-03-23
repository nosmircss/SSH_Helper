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
