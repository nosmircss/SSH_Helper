import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Connection, Edge, Node, NodeChange } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';
import { isConnectionAllowed } from '../../../utils/connectionRules';

function node(id: string, blockType: string, props: Record<string, unknown>): Node {
  return { id, type: blockType === '_start' ? 'start' : 'block', position: { x: 0, y: 0 }, data: { blockType, props } } as Node;
}
function edge(id: string, source: string, target: string, sourceHandle?: string): Edge {
  return { id, source, target, ...(sourceHandle ? { sourceHandle } : {}) } as Edge;
}
const propsOf = (n: Node | undefined) => ((n?.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};

// The user's real preset: foreach with a small body, plus a fresh unconnected 'NEW' send on canvas.
function setup() {
  const nodes: Node[] = [
    node('__start__', '_start', {}),
    node('M', 'multiselect', { _stepPath: 'steps/0' }),
    node('F', 'foreach', { _stepPath: 'steps/1' }),
    node('p', 'print', { _isChildOf: 'F', _stepPath: 'steps/1/do/0', _branchLabel: 'loop', _depth: 0 }),
    node('s1', 'send', { _isChildOf: 'F', _stepPath: 'steps/1/do/1', _branchLabel: 'loop', _depth: 0 }),
    node('s2', 'send', { _isChildOf: 'F', _stepPath: 'steps/1/do/2', _branchLabel: 'loop', _depth: 0 }),
    node('NEW', 'send', {}), // dropped fresh, no _stepPath
  ];
  const edges: Edge[] = [
    edge('e0', '__start__', 'M'),
    edge('e1', 'M', 'F'),
    edge('e2', 'F', 'p'),
    edge('e3', 'p', 's1'),
    edge('e4', 's1', 's2'),
  ];
  useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
  useFlowStore.getState().setNodes(nodes);
  useFlowStore.getState().setEdges(edges);
}

function bodyIndices(): number[] {
  return useFlowStore.getState().nodes
    .filter((n) => propsOf(n)._isChildOf === 'F' && n.id !== 'NEW')
    .map((n) => Number((propsOf(n)._stepPath as string).split('/').pop()))
    .sort((a, b) => a - b);
}

const bottomConn = (source: string, target: string): Connection =>
  ({ source, target, sourceHandle: null, targetHandle: null });

describe('add → connect → delete keeps loop-body _stepPath contiguous (real store)', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
    vi.clearAllMocks();
  });

  // The real trigger: drag from the foreach's body (empty handle) to a fresh block. The container
  // accepts multiple empty-handle edges, so this is ALLOWED and inserts NEW as do/0 (gesture 3),
  // bumping the existing body children — exactly "messing up at the start of the loop".
  it('connecting to the foreach body inserts NEW as do/0 and bumps the existing body', () => {
    setup();
    const v = useFlowStore.getState();
    expect(isConnectionAllowed(bottomConn('F', 'NEW'), v.nodes, v.edges).ok).toBe(true);
    useFlowStore.getState().onConnect(bottomConn('F', 'NEW'));
    const st = useFlowStore.getState();
    expect(propsOf(st.nodes.find((n) => n.id === 'NEW'))._stepPath).toBe('steps/1/do/0');
    expect(propsOf(st.nodes.find((n) => n.id === 'p'))._stepPath).toBe('steps/1/do/1'); // bumped
  });

  it('removeNodes(NEW) after the foreach-body insert restores contiguous indices', () => {
    setup();
    useFlowStore.getState().onConnect(bottomConn('F', 'NEW'));
    useFlowStore.getState().removeNodes(['NEW']);
    expect(bodyIndices()).toEqual([0, 1, 2]);
    expect(propsOf(useFlowStore.getState().nodes.find((n) => n.id === 'p'))._stepPath).toBe('steps/1/do/0');
  });

  it('delete via onNodesChange remove also restores contiguous indices', () => {
    setup();
    useFlowStore.getState().onConnect(bottomConn('F', 'NEW'));
    useFlowStore.getState().onNodesChange([{ type: 'remove', id: 'NEW' } as NodeChange]);
    expect(bodyIndices()).toEqual([0, 1, 2]);
  });
});
