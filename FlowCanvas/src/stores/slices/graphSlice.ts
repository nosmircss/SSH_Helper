import type { StateCreator } from 'zustand';
import type { Node, Edge, OnNodesChange, OnEdgesChange, Connection } from '@xyflow/react';
import { applyNodeChanges, applyEdgeChanges, addEdge } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import { blockDefMap } from '../../blockDefs/registry';

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

  const dashed = { strokeDasharray: '5,5' };

  if (blockType === 'if') {
    if (branchPath === 'else') {
      return {
        label: 'else',
        style: { stroke: 'var(--fc-state-error)', ...dashed },
        labelStyle: { fill: 'var(--fc-state-error)', fontSize: 11, fontWeight: 600 },
      };
    }
    if (branchPath.startsWith('elif/')) {
      const condition = (metadata.condition ?? '').trim();
      return {
        label: condition ? `elif: ${condition}` : 'elif',
        style: { stroke: 'var(--fc-state-warning)', ...dashed },
        labelStyle: { fill: 'var(--fc-state-warning)', fontSize: 11, fontWeight: 600 },
      };
    }
    return {
      label: 'then',
      style: { stroke: 'var(--fc-state-success)', ...dashed },
      labelStyle: { fill: 'var(--fc-state-success)', fontSize: 11, fontWeight: 600 },
    };
  }

  if (blockType === 'foreach' || blockType === 'while') {
    return {
      label: 'do',
      style: { stroke: 'var(--fc-state-warning)', ...dashed },
      labelStyle: { fill: 'var(--fc-state-warning)', fontSize: 11, fontWeight: 600 },
    };
  }

  if (blockType === 'try') {
    if (branchPath === 'catch') {
      return {
        label: 'catch',
        style: { stroke: 'var(--fc-state-error)', ...dashed },
        labelStyle: { fill: 'var(--fc-state-error)', fontSize: 11, fontWeight: 600 },
      };
    }
    if (branchPath === 'finally') {
      return {
        label: 'finally',
        style: { stroke: 'var(--fc-accent)', ...dashed },
        labelStyle: { fill: 'var(--fc-accent)', fontSize: 11, fontWeight: 600 },
      };
    }
    return {
      label: 'do',
      style: { stroke: 'var(--fc-state-success)', ...dashed },
      labelStyle: { fill: 'var(--fc-state-success)', fontSize: 11, fontWeight: 600 },
    };
  }

  if (blockType === 'switch') {
    if (branchPath === 'default' || branchPath === 'else') {
      return {
        label: 'default',
        style: { stroke: 'var(--fc-state-error)', ...dashed },
        labelStyle: { fill: 'var(--fc-state-error)', fontSize: 11, fontWeight: 600 },
      };
    }
    const caseValue = (metadata.caseValue ?? '').trim();
    return {
      label: caseValue ? `case: ${caseValue}` : 'case',
      style: { stroke: 'var(--fc-state-warning)', ...dashed },
      labelStyle: { fill: 'var(--fc-state-warning)', fontSize: 11, fontWeight: 600 },
    };
  }

  if (blockType === 'parallel') {
    const index = parseIndexedBranch(branchPath, 'parallel');
    const branchLabel = index === null ? 'branch' : `branch ${index + 1}`;
    return {
      label: branchLabel,
      style: { stroke: 'var(--fc-cat-network-border)', ...dashed },
      labelStyle: { fill: 'var(--fc-cat-network-border)', fontSize: 11, fontWeight: 600 },
    };
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
    set((state) => ({
      edges: applyEdgeChanges(changes, state.edges),
      ...(hasStructuralChange ? { isDirty: true } : {}),
      ...(changes.length > 0 ? clearedExportStatusState() : {}),
    }));
  },

  onConnect: (connection) => {
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
    set((state) => ({
      edges: state.edges.filter((e) => !idSet.has(e.id)),
      selectedEdgeIds: new Set<string>(),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
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
