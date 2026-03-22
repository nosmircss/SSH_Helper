import { useCallback, useEffect, useRef, useState, type DragEvent } from 'react';
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
  type ReactFlowInstance,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { messageBus } from './MessageBus';
import BaseBlock from './nodes/BaseBlock';
import Palette from './panels/Palette';
import Properties from './panels/Properties';
import Toolbar from './panels/Toolbar';
import { blockDefMap, categoryColors } from './blockDefs/registry';

// Register custom node types
const nodeTypes = {
  block: BaseBlock,
};

// Demo nodes showing the custom block component
const initialNodes: Node[] = [
  {
    id: 'demo-send',
    type: 'block',
    position: { x: 250, y: 40 },
    data: {
      blockType: 'send',
      label: 'Get Version',
      props: { command: 'show version | include Software' },
    },
  },
  {
    id: 'demo-extract',
    type: 'block',
    position: { x: 250, y: 160 },
    data: {
      blockType: 'extract',
      label: 'Parse Version',
      props: { pattern: 'Version (\\S+)', into: 'firmware_ver' },
    },
  },
  {
    id: 'demo-if',
    type: 'block',
    position: { x: 250, y: 280 },
    data: {
      blockType: 'if',
      label: 'Version Check',
      props: { condition: 'firmware_ver != "17.9.5"' },
    },
  },
  {
    id: 'demo-print',
    type: 'block',
    position: { x: 150, y: 400 },
    data: {
      blockType: 'print',
      props: { message: '⚠ Outdated firmware!' },
      execState: 'idle',
    },
  },
  {
    id: 'demo-updatecol',
    type: 'block',
    position: { x: 380, y: 400 },
    data: {
      blockType: 'updatecolumn',
      props: { column: 'Status', expression: '"OUTDATED"' },
    },
  },
];

const initialEdges: Edge[] = [
  { id: 'e1', source: 'demo-send', target: 'demo-extract', style: { stroke: '#4a9eff' } },
  { id: 'e2', source: 'demo-extract', target: 'demo-if', style: { stroke: '#9b59b6' } },
  { id: 'e3', source: 'demo-if', target: 'demo-print', style: { stroke: '#f0c040' } },
  { id: 'e4', source: 'demo-if', sourceHandle: 'false', target: 'demo-updatecol', style: { stroke: '#e74c3c' } },
];

let idCounter = 0;
function nextId() {
  return `block-${Date.now()}-${idCounter++}`;
}

export default function App() {
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const reactFlowInstance = useRef<ReactFlowInstance | null>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);

  const onConnect = useCallback(
    (connection: Connection) =>
      setEdges((eds) => addEdge({ ...connection, style: { stroke: '#666' } }, eds)),
    [setEdges],
  );

  const onDragOver = useCallback((event: DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: DragEvent) => {
      event.preventDefault();
      const blockType = event.dataTransfer.getData('application/flowcanvas-block');
      if (!blockType || !reactFlowInstance.current) return;

      const def = blockDefMap.get(blockType);
      if (!def) return;

      const bounds = wrapperRef.current?.getBoundingClientRect();
      const position = reactFlowInstance.current.screenToFlowPosition({
        x: event.clientX - (bounds?.left ?? 0),
        y: event.clientY - (bounds?.top ?? 0),
      });

      const newNode: Node = {
        id: nextId(),
        type: 'block',
        position,
        data: {
          blockType,
          label: def.label,
          props: {},
        },
      };

      setNodes((nds) => [...nds, newNode]);
    },
    [setNodes],
  );

  // Signal ready to WinForms host
  useEffect(() => {
    messageBus.sendReady();

    const unsub = messageBus.on('load-graph', (msg) => {
      if (msg.nodes && msg.edges) {
        setNodes(msg.nodes as Node[]);
        setEdges(msg.edges as Edge[]);
      }
    });

    return unsub;
  }, [setNodes, setEdges]);

  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Toolbar />
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
      <Palette />
      <div ref={wrapperRef} style={{ flex: 1, height: '100%' }}>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onConnect={onConnect}
          onDragOver={onDragOver}
          onDrop={onDrop}
          onInit={(instance) => { reactFlowInstance.current = instance; }}
          onNodeClick={(_, node) => setSelectedNodeId(node.id)}
          onPaneClick={() => setSelectedNodeId(null)}
          nodeTypes={nodeTypes}
          fitView
          proOptions={{ hideAttribution: true }}
          style={{ background: '#1a1a2e' }}
          defaultEdgeOptions={{ style: { stroke: '#555' } }}
        >
          <Controls
            style={{ background: '#222244', borderColor: '#2a2a4a', borderRadius: '6px' }}
          />
          <MiniMap
            style={{ background: '#12122a', borderColor: '#2a2a4a', borderRadius: '6px' }}
            nodeColor={(node) => {
              const bt = (node.data as any)?.blockType;
              const def = bt ? blockDefMap.get(bt) : null;
              return def ? categoryColors[def.category].border : '#4a9eff';
            }}
            maskColor="rgba(0,0,0,0.6)"
          />
          <Background variant={BackgroundVariant.Dots} gap={20} size={1} color="#2a2a4a" />
        </ReactFlow>
      </div>
      <Properties selectedNodeId={selectedNodeId} />
      </div>
    </div>
  );
}
