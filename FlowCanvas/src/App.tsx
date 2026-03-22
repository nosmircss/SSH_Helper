import { useCallback, useEffect } from 'react';
import {
  ReactFlow,
  MiniMap,
  Controls,
  Background,
  useNodesState,
  useEdgesState,
  addEdge,
  BackgroundVariant,
  type Connection,
  type Node,
  type Edge,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { messageBus } from './MessageBus';

// Demo nodes to verify the canvas is working
const initialNodes: Node[] = [
  {
    id: 'start',
    type: 'default',
    position: { x: 250, y: 0 },
    data: { label: 'START' },
    style: {
      background: '#1e3a2e',
      border: '2px solid #2ecc71',
      borderRadius: '24px',
      color: '#2ecc71',
      fontWeight: 600,
      padding: '8px 20px',
    },
  },
  {
    id: 'send-1',
    type: 'default',
    position: { x: 200, y: 100 },
    data: { label: 'SEND: show version' },
    style: {
      background: '#1a2744',
      border: '2px solid #4a9eff',
      borderRadius: '8px',
      color: '#8aafdb',
      fontFamily: 'monospace',
      fontSize: '13px',
    },
  },
  {
    id: 'extract-1',
    type: 'default',
    position: { x: 200, y: 220 },
    data: { label: 'EXTRACT: Version (\\S+)' },
    style: {
      background: '#1a1a2a',
      border: '2px solid #9b59b6',
      borderRadius: '8px',
      color: '#c9a0dc',
      fontFamily: 'monospace',
      fontSize: '13px',
    },
  },
  {
    id: 'print-1',
    type: 'default',
    position: { x: 200, y: 340 },
    data: { label: 'PRINT: "Check complete"' },
    style: {
      background: '#201a2a',
      border: '2px solid #e67e22',
      borderRadius: '8px',
      color: '#e8a860',
      fontFamily: 'monospace',
      fontSize: '13px',
    },
  },
];

const initialEdges: Edge[] = [
  { id: 'e-start-send', source: 'start', target: 'send-1', animated: true, style: { stroke: '#2ecc71' } },
  { id: 'e-send-extract', source: 'send-1', target: 'extract-1', style: { stroke: '#4a9eff' } },
  { id: 'e-extract-print', source: 'extract-1', target: 'print-1', style: { stroke: '#9b59b6' } },
];

export default function App() {
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);

  const onConnect = useCallback(
    (connection: Connection) => setEdges((eds) => addEdge({ ...connection, style: { stroke: '#666' } }, eds)),
    [setEdges],
  );

  // Signal ready to WinForms host
  useEffect(() => {
    messageBus.sendReady();

    // Listen for graph load messages from WinForms
    const unsub = messageBus.on('load-graph', (msg) => {
      if (msg.nodes && msg.edges) {
        setNodes(msg.nodes as Node[]);
        setEdges(msg.edges as Edge[]);
      }
    });

    return unsub;
  }, [setNodes, setEdges]);

  return (
    <div style={{ width: '100%', height: '100%' }}>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        fitView
        proOptions={{ hideAttribution: true }}
        style={{ background: '#1a1a2e' }}
      >
        <Controls
          style={{ background: '#222244', borderColor: '#2a2a4a', borderRadius: '6px' }}
        />
        <MiniMap
          style={{ background: '#12122a', borderColor: '#2a2a4a', borderRadius: '6px' }}
          nodeColor="#4a9eff"
          maskColor="rgba(0,0,0,0.6)"
        />
        <Background variant={BackgroundVariant.Dots} gap={20} size={1} color="#2a2a4a" />
      </ReactFlow>
    </div>
  );
}
