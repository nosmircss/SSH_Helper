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
import VariableInspector, { type VariableEntry } from './panels/VariableInspector';
import OutputPreview from './panels/OutputPreview';
import DebugPanel, { type DebugState } from './panels/DebugPanel';
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
  const [variables, setVariables] = useState<VariableEntry[]>([]);
  const [variablesVisible, setVariablesVisible] = useState(false);
  const [outputText, setOutputText] = useState<string | null>(null);
  const [outputLabel, setOutputLabel] = useState<string>('');
  const [debugState, setDebugState] = useState<DebugState>({ paused: false, callStack: [] });
  const [debugVisible, setDebugVisible] = useState(false);
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

      const position = reactFlowInstance.current.screenToFlowPosition({
        x: event.clientX,
        y: event.clientY,
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

  // Keyboard shortcuts
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ctrl+Enter: Test Step on selected node
      if (e.ctrlKey && e.key === 'Enter' && selectedNodeId) {
        e.preventDefault();
        messageBus.send({ type: 'test-step', stepId: selectedNodeId });
      }
      // Escape: deselect / close panels
      if (e.key === 'Escape') {
        setSelectedNodeId(null);
        setOutputText(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedNodeId]);

  // Signal ready to WinForms host
  useEffect(() => {
    messageBus.sendReady();

    const unsubs = [
      messageBus.on('load-graph', (msg) => {
        if (msg.nodes && msg.edges) {
          setNodes(msg.nodes as Node[]);
          setEdges(msg.edges as Edge[]);
        }
      }),
      messageBus.on('test-step-result', (msg) => {
        // Show output from a test-step execution
        if (msg.output) {
          setOutputText(String(msg.output));
          setOutputLabel(String(msg.stepId ?? ''));
        }
        // Update variables
        if (msg.variables && typeof msg.variables === 'object') {
          const vars = Object.entries(msg.variables as Record<string, unknown>).map(
            ([name, value]) => ({ name, value }),
          );
          setVariables(vars);
          setVariablesVisible(true);
        }
        // Update block execution state
        if (msg.stepId) {
          setNodes((nds) =>
            nds.map((n) =>
              n.id === msg.stepId
                ? { ...n, data: { ...n.data, execState: msg.success ? 'success' : 'error' } }
                : n,
            ),
          );
        }
      }),
      messageBus.on('execution-update', (msg) => {
        if (msg.stepId && msg.state) {
          setNodes((nds) =>
            nds.map((n) =>
              n.id === msg.stepId
                ? { ...n, data: { ...n.data, execState: String(msg.state) } }
                : n,
            ),
          );
        }
        if (msg.variables && typeof msg.variables === 'object') {
          const vars = Object.entries(msg.variables as Record<string, unknown>).map(
            ([name, value]) => ({ name, value }),
          );
          setVariables(vars);
        }
      }),
      messageBus.on('debug-paused', (msg) => {
        setDebugState({
          paused: true,
          pausedAtStepId: String(msg.stepId ?? ''),
          callStack: (msg.callStack as string[]) ?? [],
        });
        setDebugVisible(true);
        // Highlight the paused block
        if (msg.stepId) {
          setNodes((nds) =>
            nds.map((n) =>
              n.id === msg.stepId
                ? { ...n, data: { ...n.data, execState: 'running', breakpoint: true } }
                : n,
            ),
          );
        }
        // Update variables
        if (msg.variables && typeof msg.variables === 'object') {
          const vars = Object.entries(msg.variables as Record<string, unknown>).map(
            ([name, value]) => ({ name, value }),
          );
          setVariables(vars);
          setVariablesVisible(true);
        }
      }),
    ];

    return () => unsubs.forEach((u) => u());
  }, [setNodes, setEdges]);

  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Toolbar
        selectedNodeId={selectedNodeId}
        variablesVisible={variablesVisible}
        onToggleVariables={() => setVariablesVisible((v) => !v)}
      />
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
      <VariableInspector
        variables={variables}
        visible={variablesVisible}
        onToggle={() => setVariablesVisible(false)}
      />
      <DebugPanel debugState={debugState} visible={debugVisible} />
      {outputText !== null && (
        <OutputPreview
          output={outputText}
          blockLabel={outputLabel}
          onClose={() => setOutputText(null)}
        />
      )}
      </div>
    </div>
  );
}
