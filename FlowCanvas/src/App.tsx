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
  type Connection,
  type FinalConnectionState,
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
import { EdgeMarkers } from './nodes/EdgeMarkers';
import BranchBandsLayer from './nodes/BranchBandsLayer';
import { contentSizeComment, orderCommentsBehind } from './utils/displayNodes';
import Palette from './panels/Palette';
import Properties from './panels/Properties';
import RightPanel from './panels/RightPanel';
import Toolbar from './panels/Toolbar';
import HostBar from './panels/HostBar';
import VariableInspector from './panels/VariableInspector';
import OutputPreview from './panels/OutputPreview';
import DebugPanel from './panels/DebugPanel';
import ProblemsPanel from './panels/ProblemsPanel';
import ConnectionNotice from './panels/ConnectionNotice';
import SearchOverlay from './panels/SearchOverlay';
import { sendLayoutAutosave } from './utils/layoutAutosave';
import TimelinePanel from './panels/TimelinePanel';
import BlockContextMenu from './panels/BlockContextMenu';
import EdgeContextMenu from './panels/EdgeContextMenu';
import { blockDefMap, categoryColors } from './blockDefs/registry';
import { resolveCssVar } from './utils/tokens';
import { isConnectionAllowed } from './utils/connectionRules';

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
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  const snapToGrid = useFlowStore((s) => s.snapToGrid);
  const gridSize = useFlowStore((s) => s.gridSize);
  const searchResults = useFlowStore((s) => s.searchResults);
  const searchIndex = useFlowStore((s) => s.searchIndex);
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

  // Toggle the global reduced-motion kill switch class on <body>.
  useEffect(() => {
    document.body.classList.toggle('fc-reduced-motion', reducedMotion);
  }, [reducedMotion]);

  // Reject illegal drop targets while dragging a connection. Reads getState() so it always
  // sees current nodes/edges without re-subscribing (returning false aborts the drop in v12).
  const isValidConnection = useCallback(
    (conn: Connection | Edge) =>
      isConnectionAllowed(conn as Connection, useFlowStore.getState().nodes, useFlowStore.getState().edges).ok,
    [],
  );

  // When isValidConnection blocks a drop, ReactFlow never calls onConnect — so surface the
  // specific rejection reason here. v12 fires onConnect (which ADDS the edge) BEFORE
  // onConnectEnd, so we MUST NOT recompute the verdict for accepted drops: the just-added edge
  // would trip the duplicate/fan-in checks and flash a false notice. Gate on state.isValid:
  // it carries isValidConnection's last result, so `=== false` means the drop was rejected and
  // onConnect never ran (the edge is absent, the recomputed reason is the real one).
  const onConnectEnd = useCallback((_event: MouseEvent | TouchEvent, state: FinalConnectionState) => {
    if (state.isValid !== false) return; // accepted, or dropped on empty canvas — nothing to explain
    const { fromHandle, toHandle } = state;
    if (!fromHandle || !toHandle) return; // no concrete handle pair to describe

    const source = fromHandle.type === 'source' ? fromHandle : toHandle;
    const target = fromHandle.type === 'source' ? toHandle : fromHandle;
    const connection: Connection = {
      source: source.nodeId,
      target: target.nodeId,
      sourceHandle: source.id ?? null,
      targetHandle: target.id ?? null,
    };

    const store = useFlowStore.getState();
    const verdict = isConnectionAllowed(connection, store.nodes, store.edges);
    if (!verdict.ok) {
      store.showConnectionNotice(verdict.reason ?? 'Connection not allowed.');
    }
  }, []);

  // Capture one undo snapshot at drag start (pre-move state).
  const onNodeDragStart = useCallback(() => {
    if (dragSnapshotTaken.current) return;
    dragSnapshotTaken.current = true;
    pushSnapshot('Move blocks');
  }, [pushSnapshot]);

  const onNodeDragStop = useCallback(() => {
    dragSnapshotTaken.current = false;
    sendLayoutAutosave();
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
      if (useFlowStore.getState().defaultBlockExpanded) {
        useFlowStore.getState().toggleExpanded(newNode.id);
      }
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

  // The output pane mirrors the SELECTED block's own per-step output. The executor
  // computes that per block (a send shows its own output; a container/non-send shows the
  // output of the send preceding its start), so the pane shows exactly that — blank when
  // the block carried nothing, never walking to a neighbour's output.
  const outputSourceId = firstSelectedId;

  const selectedOutput = outputSourceId
    ? blockOutputs.get(outputSourceId)
    : null;
  const latestOutput = selectedOutput && selectedOutput.length > 0
    ? selectedOutput[selectedOutput.length - 1]
    : null;

  // Add visual selection and search highlight to nodes
  // contentSizeComment strips comments' oversized fixed hit box so RF auto-measures the card;
  // orderCommentsBehind renders comments behind blocks so a block always wins a pointer overlap.
  // Together these stop a comment from hijacking drags meant for its block or the branch-band handle.
  const displayNodes = orderCommentsBehind(nodes.map((n) => ({
    ...contentSizeComment(n),
    selected: selectedNodeIds.has(n.id),
    className: [
      searchHighlightSet.has(n.id) ? 'search-match' : '',
      n.id === highlightedNodeId ? 'search-current' : '',
    ].filter(Boolean).join(' ') || undefined,
  })));

  // Canvas ships dark-only; values come from the token layer (styles/tokens.css).
  const canvasBg = 'var(--fc-canvas-bg)';
  const controlsBg = 'var(--fc-surface-1)';
  const controlsBorder = 'var(--fc-border)';
  const minimapBg = 'var(--fc-surface-0)';
  const minimapMask = 'var(--fc-overlay-scrim)';
  const dotColor = 'var(--fc-grid-dot)';
  const selectedStroke = 'var(--fc-accent)';

  // SVG presentation attributes in WebView2 may not accept var(), so resolve the minimap's
  // mask/background/node colors to concrete strings once. CSS contexts (Background/Controls)
  // keep the var() strings above.
  const minimapColors = useMemo(() => ({
    mask: resolveCssVar('var(--fc-overlay-scrim)', 'rgba(0,0,0,0.6)'),
    bg: resolveCssVar('var(--fc-surface-0)', '#12122a'),
    fallback: resolveCssVar('var(--fc-accent)', '#4a9eff'),
    byCategory: Object.fromEntries(
      (Object.keys(categoryColors) as Array<keyof typeof categoryColors>)
        .map((k) => [k, resolveCssVar(categoryColors[k].border, '#4a9eff')]),
    ),
  }), []);

  // Build enhanced edges — all edges use AnimatedEdge (rest + running) + selection highlight
  const displayEdges = edges.map((e) => ({
    ...e,
    type: 'animated',
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
      <EdgeMarkers />
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
              isValidConnection={isValidConnection}
              onConnectEnd={onConnectEnd}
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
              minZoom={0.2}
              fitView
              fitViewOptions={{ maxZoom: 0.85, padding: 0.15 }}
              proOptions={{ hideAttribution: true }}
              style={{ background: canvasBg }}
              defaultEdgeOptions={{ type: 'animated', style: { stroke: 'var(--fc-edge-idle)' } }}
            >
              <BranchBandsLayer />
              <Controls
                style={{ background: controlsBg, borderColor: controlsBorder, borderRadius: '6px' }}
              />
              <MiniMap
                // pointer-events:none so the overview never swallows clicks meant for a node
                // beneath it (it isn't pannable/zoomable, so this removes no interaction).
                style={{ background: minimapBg, borderColor: controlsBorder, borderRadius: '6px', pointerEvents: 'none' }}
                nodeColor={(node) => {
                  const bt = (node.data as any)?.blockType;
                  const def = bt ? blockDefMap.get(bt) : null;
                  return def ? minimapColors.byCategory[def.category] ?? minimapColors.fallback : minimapColors.fallback;
                }}
                maskColor={minimapColors.mask}
              />
              <Background variant={BackgroundVariant.Dots} gap={20} size={1} color={dotColor} />
            </ReactFlow>
            <SearchOverlay />
            <ConnectionNotice />
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
        <ProblemsPanel />
      </div>
    </div>
  );
}
