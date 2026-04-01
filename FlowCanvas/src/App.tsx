import { useCallback, useEffect, useMemo, useRef, type DragEvent } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  MiniMap,
  Controls,
  Background,
  BackgroundVariant,
  type Node,
  type Edge,
  type ReactFlowInstance,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { useFlowStore } from './stores/useFlowStore';
import { initMessageBridge } from './stores/messageBridge';
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts';
import { applyTheme } from './utils/theme';
import BaseBlock from './nodes/BaseBlock';
import CommentNode from './nodes/CommentNode';
import StartNode from './nodes/StartNode';
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
import EdgeContextMenu from './panels/EdgeContextMenu';
import { blockDefMap, categoryColors } from './blockDefs/registry';

// Register custom node types
const nodeTypes = {
  block: BaseBlock,
  comment: CommentNode,
  start: StartNode,
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
  const selectedEdgeIds = useFlowStore((s) => s.selectedEdgeIds);
  const selectEdge = useFlowStore((s) => s.selectEdge);
  const showContextMenu = useFlowStore((s) => s.showContextMenu);
  const hideContextMenu = useFlowStore((s) => s.hideContextMenu);
  const showEdgeContextMenu = useFlowStore((s) => s.showEdgeContextMenu);
  const hideEdgeContextMenu = useFlowStore((s) => s.hideEdgeContextMenu);
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

      // Populate default property values from the block definition
      const defaultProps: Record<string, unknown> = {};
      for (const propDef of def.properties) {
        if (propDef.defaultValue !== undefined) {
          defaultProps[propDef.key] = propDef.defaultValue;
        }
      }

      const newNode: Node = {
        id: nextId(),
        type: 'block',
        position,
        data: {
          blockType,
          label: def.label,
          props: defaultProps,
        },
      };

      addNode(newNode);
      selectNode(newNode.id);
    },
    [addNode, selectNode],
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

  const onEdgeClick = useCallback(
    (_: React.MouseEvent, edge: Edge) => {
      selectEdge(edge.id);
      hideContextMenu();
      hideEdgeContextMenu();
    },
    [selectEdge, hideContextMenu, hideEdgeContextMenu],
  );

  const onEdgeContextMenu = useCallback(
    (event: React.MouseEvent, edge: Edge) => {
      event.preventDefault();
      selectEdge(edge.id);
      showEdgeContextMenu(event.clientX, event.clientY, edge.id);
    },
    [selectEdge, showEdgeContextMenu],
  );

  const onPaneClick = useCallback(() => {
    clearSelection();
    hideContextMenu();
    hideEdgeContextMenu();
    // Clear any text selection (e.g. from the output panel)
    window.getSelection()?.removeAllRanges();
  }, [clearSelection, hideContextMenu, hideEdgeContextMenu]);

  // Highlight search results on nodes
  const searchHighlightSet = new Set(searchResults);
  const highlightedNodeId = searchResults.length > 0 ? searchResults[searchIndex] : null;

  // Get first selected node for output preview
  const firstSelectedId = selectedNodeIds.size > 0
    ? [...selectedNodeIds][0]
    : null;

  // Walk backward through the flow to find the nearest send/interactive block
  // that produced SSH output. Extract/Print/etc. operate on the output of the
  // preceding send command, so clicking them should show that send's output.
  const OUTPUT_BLOCK_TYPES = new Set(['send', 'interactive']);
  const outputSourceId = useMemo(() => {
    if (!firstSelectedId) return null;
    const nodeMap = new Map(nodes.map(n => [n.id, n]));
    // Build a reverse adjacency map (target -> sources)
    const parentMap = new Map<string, string[]>();
    for (const e of edges) {
      const list = parentMap.get(e.target) || [];
      list.push(e.source);
      parentMap.set(e.target, list);
    }
    // If the selected node itself is a send/interactive with output, use it
    const selectedNode = nodeMap.get(firstSelectedId);
    if (selectedNode && OUTPUT_BLOCK_TYPES.has((selectedNode.data as any)?.blockType)) {
      if (blockOutputs.get(firstSelectedId)?.length) return firstSelectedId;
    }
    // BFS backward to find the nearest send/interactive ancestor with output
    const visited = new Set<string>();
    const queue = [firstSelectedId];
    while (queue.length > 0) {
      const current = queue.shift()!;
      if (visited.has(current)) continue;
      visited.add(current);
      const parents = parentMap.get(current) || [];
      for (const pid of parents) {
        const pnode = nodeMap.get(pid);
        if (pnode && OUTPUT_BLOCK_TYPES.has((pnode.data as any)?.blockType)) {
          if (blockOutputs.get(pid)?.length) return pid;
        }
        queue.push(pid);
      }
    }
    return null;
  }, [firstSelectedId, nodes, edges, blockOutputs]);

  const selectedOutput = outputSourceId
    ? blockOutputs.get(outputSourceId)
    : null;
  const latestOutput = selectedOutput && selectedOutput.length > 0
    ? selectedOutput[selectedOutput.length - 1]
    : null;

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

  // Build enhanced edges with animated type when running + selection highlight
  const selectedStroke = isDark ? '#4a9eff' : '#2563eb';
  const displayEdges = edges.map((e) => ({
    ...e,
    ...(isRunning ? { type: 'animated' } : {}),
    selected: selectedEdgeIds.has(e.id),
    style: {
      ...e.style,
      ...(selectedEdgeIds.has(e.id)
        ? { stroke: selectedStroke, strokeWidth: 3 }
        : {}),
    },
  }));

  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
      <Toolbar />
      <HostBar />
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden', position: 'relative' }}>
        <Palette />
        <div ref={wrapperRef} style={{ flex: 1, height: '100%', display: 'flex', flexDirection: 'column' }}>
          <div style={{ flex: 1, position: 'relative', minHeight: 0 }}>
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
              onEdgeClick={onEdgeClick}
              onEdgeContextMenu={onEdgeContextMenu}
              onNodeDragStart={onNodeDragStart}
              onNodeDragStop={onNodeDragStop}
              nodeTypes={nodeTypes}
              edgeTypes={edgeTypes}
              snapToGrid={snapToGrid}
              snapGrid={[gridSize, gridSize]}
              selectionOnDrag
              panOnDrag={[1, 2]}
              fitView
              fitViewOptions={{ maxZoom: 0.85, padding: 0.15 }}
              proOptions={{ hideAttribution: true }}
              style={{ background: canvasBg }}
              defaultEdgeOptions={{ type: 'smoothstep', style: { stroke: isDark ? '#555' : '#aaa' } }}
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
            <EdgeContextMenu />
          </div>
          {panelsVisible.output && (
            <OutputPreview
              output={latestOutput?.text || ''}
              blockLabel={outputSourceId ? (nodes.find(n => n.id === outputSourceId)?.data as any)?.label || outputSourceId : undefined}
              nodeId={outputSourceId || undefined}
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
