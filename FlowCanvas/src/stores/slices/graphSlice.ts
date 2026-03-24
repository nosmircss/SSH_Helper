import type { StateCreator } from 'zustand';
import type { Node, Edge, OnNodesChange, OnEdgesChange, Connection } from '@xyflow/react';
import { applyNodeChanges, applyEdgeChanges, addEdge } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';

function clearedExportStatusState(): Pick<FlowStore, 'exportStatus'> {
  return {
    exportStatus: {
      hasErrors: false,
      errors: [],
      warnings: [],
    },
  };
}

interface SetGraphOptions {
  markDirty?: boolean;
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
    set((state) => {
      const nextNodes = applyNodeChanges(changes, state.nodes);
      const hasSelectionChange = changes.some((c) => c.type === 'select');
      const hasGraphMutation = changes.some((c) => c.type !== 'select');
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
    set((state) => ({
      edges: addEdge({ ...connection, style: { stroke: '#666' } }, state.edges),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
  },

  addNode: (node) => {
    get().pushSnapshot('Add block');
    set((state) => ({ nodes: [...state.nodes, node], isDirty: true, ...clearedExportStatusState() }));
  },

  removeNodes: (ids) => {
    get().pushSnapshot('Delete blocks');
    const idSet = new Set(ids);
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
      nodes: state.nodes.map((n) => {
        if (n.id !== id) return n;
        const currentData = (n.data as Record<string, unknown>) ?? {};
        const currentProps = (currentData.props as Record<string, unknown> | undefined) ?? {};
        return {
          ...n,
          data: {
            ...currentData,
            props: { ...currentProps, [key]: value },
          },
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
