import { useCallback, useEffect, useRef, type DragEvent } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  MiniMap,
  Controls,
  Background,
  BackgroundVariant,
  type Node,
  type ReactFlowInstance,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useFlowStore } from './stores/useFlowStore';
import { initMessageBridge } from './stores/messageBridge';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import { applyTheme } from './utils/theme';
import BaseBlock from './nodes/BaseBlock';
import CommentNode from './nodes/CommentNode';
import AnimatedEdge from './nodes/AnimatedEdge';
import Palette from './panels/Palette';
import Properties from './panels/Properties';
import RightPanel from './panels/RightPanel';
import Toolbar from './panels/Toolbar';
import HostBar from './panels/HostBar';
import VariableInspector from './panels/VariableInspector';
import OutputPreview from './panels/OutputPreview';
import DebugPanel from './panels/DebugPanel';
import SearchOverlay from './panels/SearchOverlay';
import TimelinePanel from './panels/TimelinePanel';
import BlockContextMenu from './panels/BlockContextMenu';
import { blockDefMap, categoryColors } from './blockDefs/registry';

// Register custom node types
const nodeTypes = {
  block: BaseBlock,
  comment: CommentNode,
};

// Register custom edge types
const edgeTypes = {
  animated: AnimatedEdge,
};

let idCounter = 0;
function nextId() {
  return `block-${Date.now()}-${idCounter++}`;
}

export default function App() {
  return (
    <ReactFlowProvider>
      <FlowCanvasInner />
    </ReactFlowProvider>
  );
}

function FlowCanvasInner() {
  // Store selectors
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const onNodesChange = useFlowStore((s) => s.onNodesChange);
  const onEdgesChange = useFlowStore((s) => s.onEdgesChange);
  const onConnect = useFlowStore((s) => s.onConnect);
  const addNode = useFlowStore((s) => s.addNode);
  const selectNode = useFlowStore((s) => s.selectNode);
  const clearSelection = useFlowStore((s) => s.clearSelection);
  const selectedNodeIds = useFlowStore((s) => s.selectedNodeIds);
  const showContextMenu = useFlowStore((s) => s.showContextMenu);
  const hideContextMenu = useFlowStore((s) => s.hideContextMenu);
  const pushSnapshot = useFlowStore((s) => s.pushSnapshot);
  const theme = useFlowStore((s) => s.theme);
  const snapToGrid = useFlowStore((s) => s.snapToGrid);
  const gridSize = useFlowStore((s) => s.gridSize);
  const searchResults = useFlowStore((s) => s.searchResults);
  const searchIndex = useFlowStore((s) => s.searchIndex);
  const isRunning = useFlowStore((s) => s.isRunning);
  const panelsVisible = useFlowStore((s) => s.panelsVisible);
  const variables = useFlowStore((s) => s.variables);
  const paused = useFlowStore((s) => s.paused);
  const pausedAtNodeId = useFlowStore((s) => s.pausedAtNodeId);
  const callStack = useFlowStore((s) => s.callStack);
  const blockOutputs = useFlowStore((s) => s.blockOutputs);

  const reactFlowInstance = useRef<ReactFlowInstance<any, any> | null>(null);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const dragSnapshotTaken = useRef(false);

  // Initialize message bridge and keyboard shortcuts
  useEffect(() => {
    const cleanup = initMessageBridge();
    return cleanup;
  }, []);

  useKeyboardShortcuts();

  // Apply theme CSS variables
  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  // Capture one undo snapshot at drag start (pre-move state).
  const onNodeDragStart = useCallback(() => {
    if (dragSnapshotTaken.current) return;
    dragSnapshotTaken.current = true;
    pushSnapshot('Move blocks');
  }, [pushSnapshot]);

  const onNodeDragStop = useCallback(() => {
    dragSnapshotTaken.current = false;
  }, []);

  // Drag and drop from palette
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

      addNode(newNode);
    },
    [addNode],
  );

  // Right-click context menu
  const onNodeContextMenu = useCallback(
    (event: React.MouseEvent, node: Node) => {
      event.preventDefault();
      showContextMenu(event.clientX, event.clientY, node.id);
    },
    [showContextMenu],
  );

  // Click handlers
  const onNodeClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      // If shift is held, toggle multi-select
      if (_.shiftKey) {
        useFlowStore.getState().toggleNodeSelection(node.id);
      } else {
        selectNode(node.id);
      }
      hideContextMenu();
    },
    [selectNode, hideContextMenu],
  );

  const onPaneClick = useCallback(() => {
    clearSelection();
    hideContextMenu();
  }, [clearSelection, hideContextMenu]);

  // Highlight search results on nodes
  const searchHighlightSet = new Set(searchResults);
  const highlightedNodeId = searchResults.length > 0 ? searchResults[searchIndex] : null;

  // Get first selected node for output preview
  const firstSelectedId = selectedNodeIds.size > 0
    ? [...selectedNodeIds][0]
    : null;
  const selectedOutput = firstSelectedId
    ? blockOutputs.get(firstSelectedId)
    : null;
  const latestOutput = selectedOutput && selectedOutput.length > 0
    ? selectedOutput[selectedOutput.length - 1]
    : null;

  // Build enhanced edges with animated type when running
  const displayEdges = isRunning
    ? edges.map((e) => ({ ...e, type: 'animated' }))
    : edges;

  // Add visual selection and search highlight to nodes
  const displayNodes = nodes.map((n) => ({
    ...n,
    selected: selectedNodeIds.has(n.id),
    className: [
      searchHighlightSet.has(n.id) ? 'search-match' : '',
      n.id === highlightedNodeId ? 'search-current' : '',
    ].filter(Boolean).join(' ') || undefined,
  }));

  // Theme-dependent colors
  const isDark = theme === 'dark';
  const canvasBg = isDark ? '#1a1a2e' : '#f5f5f8';
  const controlsBg = isDark ? '#222244' : '#e8e8f0';
  const controlsBorder = isDark ? '#2a2a4a' : '#d0d0d8';
  const minimapBg = isDark ? '#12122a' : '#e0e0e8';
  const minimapMask = isDark ? 'rgba(0,0,0,0.6)' : 'rgba(255,255,255,0.6)';
  const dotColor = isDark ? '#2a2a4a' : '#c0c0c8';

  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Toolbar />
      <HostBar />
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden', position: 'relative' }}>
        <Palette />
        <div ref={wrapperRef} style={{ flex: 1, height: '100%', position: 'relative' }}>
          <ReactFlow
            nodes={displayNodes}
            edges={displayEdges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            onDragOver={onDragOver}
            onDrop={onDrop}
            onInit={(instance) => { reactFlowInstance.current = instance; }}
            onNodeClick={onNodeClick}
            onPaneClick={onPaneClick}
            onNodeContextMenu={onNodeContextMenu}
            onNodeDragStart={onNodeDragStart}
            onNodeDragStop={onNodeDragStop}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            snapToGrid={snapToGrid}
            snapGrid={[gridSize, gridSize]}
            selectionOnDrag
            panOnDrag={[1, 2]}
            fitView
            proOptions={{ hideAttribution: true }}
            style={{ background: canvasBg }}
            defaultEdgeOptions={{ style: { stroke: isDark ? '#555' : '#aaa' } }}
          >
            <Controls
              style={{ background: controlsBg, borderColor: controlsBorder, borderRadius: '6px' }}
            />
            <MiniMap
              style={{ background: minimapBg, borderColor: controlsBorder, borderRadius: '6px' }}
              nodeColor={(node) => {
                const bt = (node.data as any)?.blockType;
                const def = bt ? blockDefMap.get(bt) : null;
                return def ? categoryColors[def.category].border : '#4a9eff';
              }}
              maskColor={minimapMask}
            />
            <Background variant={BackgroundVariant.Dots} gap={20} size={1} color={dotColor} />
          </ReactFlow>
          <SearchOverlay />
          <BlockContextMenu />
          {latestOutput && firstSelectedId && (
            <OutputPreview
              output={latestOutput.text}
              blockLabel={firstSelectedId}
              nodeId={firstSelectedId}
            />
          )}
        </div>
        <RightPanel>
          <Properties />
          {panelsVisible.variables && <VariableInspector />}
          {panelsVisible.timeline && <TimelinePanel />}
        </RightPanel>
        <DebugPanel />
      </div>
    </div>
  );
}
