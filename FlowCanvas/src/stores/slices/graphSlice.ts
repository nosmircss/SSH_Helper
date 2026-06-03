import type { StateCreator } from 'zustand';
import type { Node, Edge, OnNodesChange, OnEdgesChange, Connection } from '@xyflow/react';
import { applyNodeChanges, applyEdgeChanges, addEdge } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import { blockDefMap } from '../../blockDefs/registry';
import { isConnectionAllowed } from '../../utils/connectionRules';
import { branchColorVar } from '../../utils/branchBands';
import { deriveChildMembership, applyChildMembership, clearConnectAuthoredMembership } from '../../utils/childMembership';

export const START_NODE_ID = '__start__';

function clearedExportStatusState(): Pick<FlowStore, 'exportStatus' | 'diagnostics'> {
  return {
    exportStatus: {
      hasErrors: false,
      errors: [],
      warnings: [],
    },
    diagnostics: [],
  };
}

interface SetGraphOptions {
  markDirty?: boolean;
}

interface BranchMetadata {
  branchPath?: string;
  condition?: string;
  caseValue?: string;
}

function getEdgeBranchPath(edge: Edge): string | undefined {
  const data = (edge.data ?? {}) as Record<string, unknown>;
  const value = data.branchPath;
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function getEdgeBranchCondition(edge: Edge): string | undefined {
  const data = (edge.data ?? {}) as Record<string, unknown>;
  const value = data.condition;
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function getEdgeCaseValue(edge: Edge): string | undefined {
  const data = (edge.data ?? {}) as Record<string, unknown>;
  const value = data.caseValue;
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function parseIndexedBranch(branchPath: string | undefined, prefix: string): number | null {
  if (!branchPath) return null;
  const parts = branchPath.split('/');
  if (parts.length < 2 || parts[0] !== prefix) return null;
  const parsed = Number.parseInt(parts[1] ?? '', 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function inferDefaultBranchMetadata(
  blockType: string,
  sourceHandle: string | null | undefined,
  existingOutgoing: Edge[],
): BranchMetadata {
  if (blockType === 'if') {
    if (sourceHandle === 'false') {
      return { branchPath: 'else' };
    }

    const existingDefaultBranches = existingOutgoing.filter((edge) => {
      if (edge.sourceHandle === 'false') return false;
      const branchPath = getEdgeBranchPath(edge);
      return branchPath !== 'else';
    });

    const hasThen = existingDefaultBranches.some((edge) => getEdgeBranchPath(edge) === 'then');
    if (!hasThen) {
      return { branchPath: 'then' };
    }

    const existingElifIndices = existingDefaultBranches
      .map((edge) => parseIndexedBranch(getEdgeBranchPath(edge), 'elif'))
      .filter((value): value is number => value !== null);
    const nextElifIndex = existingElifIndices.length === 0
      ? 0
      : Math.max(...existingElifIndices) + 1;

    return {
      branchPath: `elif/${nextElifIndex}/then`,
      condition: '',
    };
  }

  if (blockType === 'foreach' || blockType === 'while') {
    return { branchPath: 'do' };
  }

  if (blockType === 'try') {
    const existingPaths = new Set(existingOutgoing.map((edge) => getEdgeBranchPath(edge)));
    if (!existingPaths.has('try') && !existingPaths.has('do')) return { branchPath: 'try' };
    if (!existingPaths.has('catch')) return { branchPath: 'catch' };
    if (!existingPaths.has('finally')) return { branchPath: 'finally' };
    return { branchPath: 'catch' };
  }

  if (blockType === 'switch') {
    const caseIndices = existingOutgoing
      .map((edge) => parseIndexedBranch(getEdgeBranchPath(edge), 'cases'))
      .filter((value): value is number => value !== null);
    const nextCase = caseIndices.length === 0 ? 0 : Math.max(...caseIndices) + 1;
    return {
      branchPath: `cases/${nextCase}/do`,
      caseValue: `case_${nextCase + 1}`,
    };
  }

  if (blockType === 'parallel') {
    const branchIndices = existingOutgoing
      .map((edge) => parseIndexedBranch(getEdgeBranchPath(edge), 'parallel'))
      .filter((value): value is number => value !== null);
    const nextBranch = branchIndices.length === 0 ? 0 : Math.max(...branchIndices) + 1;
    return { branchPath: `parallel/${nextBranch}` };
  }

  return {};
}

function getBranchVisual(
  blockType: string | undefined,
  metadata: BranchMetadata,
): {
  label?: string;
  style: Record<string, unknown>;
  labelStyle?: Record<string, unknown>;
} {
  const defaultVisual = { style: { stroke: 'var(--fc-edge-idle)' } };
  if (!blockType) return defaultVisual;

  const branchPath = metadata.branchPath;
  if (!branchPath) return defaultVisual;

  // getBranchVisual stays the blockType-aware KEY resolver; branchColorVar is the single
  // branch→token map (shared with the Wave 2a bands + Properties chip). No more dashes —
  // color now carries branch meaning (Wave 2b Live Wires).
  const visual = (key: string, label: string) => ({
    label,
    style: { stroke: branchColorVar(key) },
    labelStyle: { fill: branchColorVar(key), fontSize: 11, fontWeight: 600 },
  });

  if (blockType === 'if') {
    if (branchPath === 'else') return visual('else', 'else');
    if (branchPath.startsWith('elif/')) {
      const condition = (metadata.condition ?? '').trim();
      return visual('elif', condition ? `elif: ${condition}` : 'elif');
    }
    return visual('then', 'then');
  }

  if (blockType === 'foreach' || blockType === 'while') {
    return visual('do', 'do');
  }

  if (blockType === 'try') {
    if (branchPath === 'catch') return visual('catch', 'catch');
    if (branchPath === 'finally') return visual('finally', 'finally');
    return visual('try', 'do');
  }

  if (blockType === 'switch') {
    if (branchPath === 'default' || branchPath === 'else') return visual('default', 'default');
    const caseValue = (metadata.caseValue ?? '').trim();
    return visual('case', caseValue ? `case: ${caseValue}` : 'case');
  }

  if (blockType === 'parallel') {
    const index = parseIndexedBranch(branchPath, 'parallel');
    return visual('parallel', index === null ? 'branch' : `branch ${index + 1}`);
  }

  return defaultVisual;
}

export interface GraphSlice {
  nodes: Node[];
  edges: Edge[];
  selectedNodeIds: Set<string>;
  selectedEdgeIds: Set<string>;
  /** True when the user has made structural/property changes since the graph was loaded. */
  isDirty: boolean;

  setNodes: (nodes: Node[], options?: SetGraphOptions) => void;
  setEdges: (edges: Edge[], options?: SetGraphOptions) => void;
  onNodesChange: OnNodesChange;
  onEdgesChange: OnEdgesChange;
  onConnect: (connection: Connection) => void;
  addNode: (node: Node) => void;
  removeNodes: (ids: string[]) => void;
  removeEdges: (ids: string[]) => void;
  selectEdge: (id: string | null) => void;
  updateNodeData: (id: string, data: Record<string, unknown>) => void;
  updateNodeLabel: (id: string, label: string) => void;
  updateNodeProp: (id: string, key: string, value: unknown) => void;
  updateEdgeBranchMetadata: (id: string, metadata: BranchMetadata) => void;
  updateNodePosition: (id: string, position: { x: number; y: number }) => void;
  /** Shift several nodes by the same delta in one batched update (drag a band by its label). */
  translateNodesBy: (ids: string[], dx: number, dy: number) => void;
  selectNode: (id: string | null) => void;
  toggleNodeSelection: (id: string) => void;
  selectNodes: (ids: string[]) => void;
  clearSelection: () => void;
  clearDirty: () => void;
}

export const createGraphSlice: StateCreator<FlowStore, [], [], GraphSlice> = (set, get) => ({
  nodes: [],
  edges: [],
  selectedNodeIds: new Set<string>(),
  selectedEdgeIds: new Set<string>(),
  isDirty: false,

  setNodes: (nodes, options) => set({
    nodes,
    ...(options?.markDirty ? { isDirty: true } : {}),
    ...clearedExportStatusState(),
  }),
  setEdges: (edges, options) => set({
    edges,
    ...(options?.markDirty ? { isDirty: true } : {}),
    ...clearedExportStatusState(),
  }),

  onNodesChange: (changes) => {
    const filtered = changes.filter(
      (c) => c.type !== 'remove' || c.id !== START_NODE_ID,
    );
    set((state) => {
      const nextNodes = applyNodeChanges(filtered, state.nodes);
      const hasSelectionChange = filtered.some((c) => c.type === 'select');
      const hasGraphMutation = filtered.some((c) => c.type !== 'select');
      return {
        nodes: nextNodes,
        selectedNodeIds: hasSelectionChange
          ? new Set(nextNodes.filter((n) => !!n.selected).map((n) => n.id))
          : state.selectedNodeIds,
        ...(hasGraphMutation ? clearedExportStatusState() : {}),
      };
    });
  },

  onEdgesChange: (changes) => {
    const hasStructuralChange = changes.some((c) => c.type === 'remove' || c.type === 'add');
    set((state) => {
      // Deleting a wire that conferred band membership releases the block back to the spine.
      const removedIds = new Set(changes.filter((c) => c.type === 'remove').map((c) => c.id));
      const removed = removedIds.size > 0 ? state.edges.filter((e) => removedIds.has(e.id)) : [];
      const nextNodes = removed.length > 0 ? clearConnectAuthoredMembership(state.nodes, removed) : state.nodes;
      return {
        ...(nextNodes !== state.nodes ? { nodes: nextNodes } : {}),
        edges: applyEdgeChanges(changes, state.edges),
        ...(hasStructuralChange ? { isDirty: true } : {}),
        ...(changes.length > 0 ? clearedExportStatusState() : {}),
      };
    });
  },

  onConnect: (connection) => {
    // Reject shapes the YAML exporter cannot faithfully serialize (self-loop, fan-in,
    // duplicate, extra plain successor, cycle, edge-into-start). Valid drags fall through
    // unchanged so exported output is identical to before the guard.
    const verdict = isConnectionAllowed(connection, get().nodes, get().edges);
    if (!verdict.ok) {
      get().showConnectionNotice(verdict.reason ?? 'Connection not allowed.');
      return;
    }
    // Push undo snapshot before connecting
    get().pushSnapshot('Connect edge');
    set((state) => {
      // Determine if this connection originates from a container block's branch handle
      const sourceNode = state.nodes.find((n) => n.id === connection.source);
      const blockType = (sourceNode?.data as Record<string, unknown>)?.blockType as string | undefined;
      const def = blockType ? blockDefMap.get(blockType) : undefined;

      const isContinuation = connection.sourceHandle === 'continue';
      const isContainer = !!def?.isContainer;
      const branchMetadata = (isContainer && !isContinuation)
        ? inferDefaultBranchMetadata(
            blockType ?? '',
            connection.sourceHandle,
            state.edges.filter((edge) => edge.source === connection.source),
          )
        : {};

      const edgeProps: Record<string, unknown> = {
        ...connection,
      };

      // Wiring a fresh block into a container (a continue handle, a leaf's bottom handle, or a
      // branch handle) confers band membership: write the _isChildOf/_stepPath metadata import would
      // have produced so layout, bands and the YAML exporter treat it as a real member instead of
      // orphaning it at the spine. Returns null (and leaves nodes untouched) for gestures that don't
      // nest — top-level successors, canvas-authored containers, already-nested targets.
      let nextNodes = state.nodes;
      const membership = deriveChildMembership(state.nodes, connection, { sourceIsContainer: isContainer, branchMetadata });
      if (membership) nextNodes = applyChildMembership(state.nodes, membership);

      if (isContinuation) {
        // Continuation edges get explicit styling — bypass getBranchVisual
        edgeProps.style = { stroke: 'var(--fc-accent)' };
        edgeProps.label = 'next';
        edgeProps.labelStyle = { fill: 'var(--fc-accent)', fontSize: 9, fontWeight: 600 };
        // No data assignment — continuation edges carry no branch metadata
      } else {
        const branchVisual = isContainer
          ? getBranchVisual(blockType, branchMetadata)
          : { style: { stroke: 'var(--fc-edge-idle)' } };
        edgeProps.style = branchVisual.style;
        if (branchVisual.label) edgeProps.label = branchVisual.label;
        if (branchVisual.labelStyle) edgeProps.labelStyle = branchVisual.labelStyle;
        if (isContainer) edgeProps.data = branchMetadata;
      }

      return {
        ...(nextNodes !== state.nodes ? { nodes: nextNodes } : {}),
        edges: addEdge(edgeProps as Edge, state.edges),
        isDirty: true,
        ...clearedExportStatusState(),
      };
    });
  },

  addNode: (node) => {
    get().pushSnapshot('Add block');
    set((state) => ({ nodes: [...state.nodes, node], isDirty: true, ...clearedExportStatusState() }));
  },

  removeNodes: (ids) => {
    const filtered = ids.filter((id) => id !== START_NODE_ID);
    if (filtered.length === 0) return;
    get().pushSnapshot('Delete blocks');
    const idSet = new Set(filtered);
    set((state) => ({
      nodes: state.nodes.filter((n) => !idSet.has(n.id)),
      edges: state.edges.filter((e) => !idSet.has(e.source) && !idSet.has(e.target)),
      selectedNodeIds: new Set([...state.selectedNodeIds].filter((id) => !idSet.has(id))),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
  },

  removeEdges: (ids) => {
    get().pushSnapshot('Delete connections');
    const idSet = new Set(ids);
    set((state) => {
      const removed = state.edges.filter((e) => idSet.has(e.id));
      const nextNodes = clearConnectAuthoredMembership(state.nodes, removed);
      return {
        ...(nextNodes !== state.nodes ? { nodes: nextNodes } : {}),
        edges: state.edges.filter((e) => !idSet.has(e.id)),
        selectedEdgeIds: new Set<string>(),
        isDirty: true,
        ...clearedExportStatusState(),
      };
    });
  },

  selectEdge: (id) => {
    set({
      selectedEdgeIds: id ? new Set([id]) : new Set(),
      selectedNodeIds: new Set(),
    });
  },

  updateNodeData: (id, data) => {
    set((state) => ({
      nodes: state.nodes.map((n) =>
        n.id === id ? { ...n, data: { ...n.data, ...data } } : n
      ),
    }));
  },

  updateNodeLabel: (id, label) => {
    set((state) => ({
      nodes: state.nodes.map((n) => {
        if (n.id !== id) return n;
        return {
          ...n,
          data: {
            ...(n.data as Record<string, unknown>),
            label,
          },
        };
      }),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
  },

  updateNodeProp: (id, key, value) => {
    set((state) => ({
      nodes: (() => {
        const nodeMap = new Map<string, Node>(state.nodes.map((node) => [node.id, node]));
        const containersToForceExport = new Set<string>();
        const visitedNodeIds = new Set<string>();
        let currentNodeId: string | undefined = id;

        while (currentNodeId && !visitedNodeIds.has(currentNodeId)) {
          visitedNodeIds.add(currentNodeId);
          const currentNode = nodeMap.get(currentNodeId);
          if (!currentNode) break;

          const currentData = (currentNode.data as Record<string, unknown>) ?? {};
          const currentProps = (currentData.props as Record<string, unknown> | undefined) ?? {};
          const currentBlockType = typeof currentData.blockType === 'string' ? currentData.blockType : '';
          if (blockDefMap.get(currentBlockType)?.isContainer) {
            containersToForceExport.add(currentNodeId);
          }

          const parentNodeId = currentProps['_isChildOf'];
          currentNodeId = typeof parentNodeId === 'string' && parentNodeId.length > 0
            ? parentNodeId
            : undefined;
        }

        return state.nodes.map((n) => {
          if (n.id !== id && !containersToForceExport.has(n.id)) return n;

          const currentData = (n.data as Record<string, unknown>) ?? {};
          const currentProps = (currentData.props as Record<string, unknown> | undefined) ?? {};
          const nextProps: Record<string, unknown> = { ...currentProps };

          if (n.id === id) {
            nextProps[key] = value;
          }
          if (containersToForceExport.has(n.id)) {
            nextProps['_forceGraphExport'] = true;
          }

          return {
            ...n,
            data: {
              ...currentData,
              props: nextProps,
            },
          };
        });
      })(),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
  },

  updateEdgeBranchMetadata: (id, metadata) => {
    get().pushSnapshot('Edit branch');
    set((state) => ({
      edges: state.edges.map((edge) => {
        if (edge.id !== id) return edge;

        const sourceNode = state.nodes.find((node) => node.id === edge.source);
        const sourceBlockType = typeof (sourceNode?.data as Record<string, unknown> | undefined)?.blockType === 'string'
          ? String((sourceNode?.data as Record<string, unknown>).blockType)
          : undefined;

        const existingData = (edge.data ?? {}) as Record<string, unknown>;
        const nextMetadata: BranchMetadata = {
          branchPath: metadata.branchPath ?? (typeof existingData.branchPath === 'string' ? existingData.branchPath : undefined),
          condition: metadata.condition ?? (typeof existingData.condition === 'string' ? existingData.condition : undefined),
          caseValue: metadata.caseValue ?? (typeof existingData.caseValue === 'string' ? existingData.caseValue : undefined),
        };
        const visual = getBranchVisual(sourceBlockType, nextMetadata);

        return {
          ...edge,
          data: {
            ...existingData,
            ...nextMetadata,
          },
          style: visual.style,
          label: visual.label,
          labelStyle: visual.labelStyle,
        };
      }),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
  },

  updateNodePosition: (id, position) => {
    set((state) => ({
      nodes: state.nodes.map((n) => (n.id === id ? { ...n, position } : n)),
      ...clearedExportStatusState(),
    }));
  },

  translateNodesBy: (ids, dx, dy) => {
    const idSet = new Set(ids);
    set((state) => ({
      nodes: state.nodes.map((n) =>
        idSet.has(n.id) ? { ...n, position: { x: n.position.x + dx, y: n.position.y + dy } } : n,
      ),
      ...clearedExportStatusState(),
    }));
  },

  selectNode: (id) => {
    set({ selectedNodeIds: id ? new Set([id]) : new Set(), selectedEdgeIds: new Set() });
  },

  toggleNodeSelection: (id) => {
    set((state) => {
      const next = new Set(state.selectedNodeIds);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return { selectedNodeIds: next };
    });
  },

  selectNodes: (ids) => {
    set({ selectedNodeIds: new Set(ids) });
  },

  clearSelection: () => {
    set({ selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
  },

  clearDirty: () => {
    set({ isDirty: false });
  },
});
