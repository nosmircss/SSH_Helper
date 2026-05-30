import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { blockDefMap } from '../../blockDefs/registry';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';

export interface PanelSizes {
  rightPanelWidth: number;
  outputHeight: number;
}

export interface NodeDiagnostic {
  nodeId?: string;
  severity: 'error' | 'warning';
  message: string;
}

export const DEFAULT_PANEL_SIZES: PanelSizes = {
  rightPanelWidth: 600,
  outputHeight: 200,
};

export interface UISlice {
  theme: 'dark' | 'light';
  reducedMotion: boolean;
  heatmapEnabled: boolean;
  branchBandsEnabled: boolean;
  snapToGrid: boolean;
  gridSize: number;
  searchQuery: string;
  searchResults: string[];
  searchIndex: number;
  searchVisible: boolean;
  contextMenu: { x: number; y: number; nodeId: string } | null;
  edgeContextMenu: { x: number; y: number; edgeId: string } | null;
  panelsVisible: {
    variables: boolean;
    debug: boolean;
    output: boolean;
    timeline: boolean;
    problems: boolean;
  };
  panelSizes: PanelSizes;
  exportStatus: {
    hasErrors: boolean;
    errors: string[];
    warnings: string[];
  };
  diagnostics: NodeDiagnostic[];
  connectionNotice: { message: string; nonce: number } | null;

  setTheme: (theme: 'dark' | 'light') => void;
  toggleTheme: () => void;
  setReducedMotion: (value: boolean) => void;
  toggleReducedMotion: () => void;
  restoreReducedMotion: (value: boolean) => void;
  toggleHeatmap: () => void;
  restoreHeatmapEnabled: (enabled: boolean) => void;
  toggleBranchBands: () => void;
  toggleSnapToGrid: () => void;
  setSearchQuery: (query: string) => void;
  nextSearchResult: () => void;
  prevSearchResult: () => void;
  toggleSearch: () => void;
  closeSearch: () => void;
  showContextMenu: (x: number, y: number, nodeId: string) => void;
  hideContextMenu: () => void;
  showEdgeContextMenu: (x: number, y: number, edgeId: string) => void;
  hideEdgeContextMenu: () => void;
  togglePanel: (panel: keyof UISlice['panelsVisible']) => void;
  setPanelSize: (key: keyof PanelSizes, value: number) => void;
  restorePanelSizes: (sizes: Partial<PanelSizes>) => void;
  setExportStatus: (status: UISlice['exportStatus']) => void;
  clearExportStatus: () => void;
  setDiagnostics: (d: NodeDiagnostic[]) => void;
  showConnectionNotice: (message: string) => void;
  clearConnectionNotice: () => void;
}

export const createUISlice: StateCreator<FlowStore, [], [], UISlice> = (set, get) => ({
  theme: 'dark',
  reducedMotion: false,
  heatmapEnabled: false,
  branchBandsEnabled: true,
  snapToGrid: false,
  gridSize: 20,
  searchQuery: '',
  searchResults: [],
  searchIndex: 0,
  searchVisible: false,
  contextMenu: null,
  edgeContextMenu: null,
  panelsVisible: {
    variables: true,
    debug: false,
    output: true,
    timeline: false,
    problems: false,
  },
  panelSizes: { ...DEFAULT_PANEL_SIZES },
  exportStatus: {
    hasErrors: false,
    errors: [],
    warnings: [],
  },
  diagnostics: [],
  connectionNotice: null,

  setTheme: (theme) => set({ theme }),
  toggleTheme: () => set((s) => ({ theme: s.theme === 'dark' ? 'light' : 'dark' })),

  setReducedMotion: (value) => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.prefSave, reducedMotion: value });
    set({ reducedMotion: value });
  },
  toggleReducedMotion: () => get().setReducedMotion(!get().reducedMotion),
  restoreReducedMotion: (value) => set({ reducedMotion: value }), // host-driven, no echo

  // Reuses the existing layout-save channel so the host persists the toggle
  // through WindowState (fewer message types). Restore is host-driven, no echo.
  toggleHeatmap: () => set((s) => {
    const next = !s.heatmapEnabled;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, heatmapEnabled: next });
    return { heatmapEnabled: next };
  }),
  restoreHeatmapEnabled: (enabled) => set({ heatmapEnabled: enabled }),

  // Transient view preference (default-on, v1). Unlike heatmap it does not persist through
  // WindowState — keeps the C# surface untouched for Wave 2a (trivial follow-on if requested).
  toggleBranchBands: () => set((s) => ({ branchBandsEnabled: !s.branchBandsEnabled })),

  toggleSnapToGrid: () => set((s) => ({ snapToGrid: !s.snapToGrid })),

  setSearchQuery: (query) => {
    const nodes = get().nodes;
    const q = query.toLowerCase();
    const results = q
      ? nodes
          .filter((n) => {
            const data = n.data as Record<string, unknown>;
            const label = String(data.label || '').toLowerCase();
            const blockType = String(data.blockType || '').toLowerCase();
            const props = data.props as Record<string, unknown> | undefined;
            const propsStr = props ? JSON.stringify(props).toLowerCase() : '';
            const def = blockDefMap.get(String(data.blockType));
            const defLabel = def ? def.label.toLowerCase() : '';
            return label.includes(q) || blockType.includes(q) || propsStr.includes(q) || defLabel.includes(q);
          })
          .map((n) => n.id)
      : [];
    set({ searchQuery: query, searchResults: results, searchIndex: 0 });
  },

  nextSearchResult: () => {
    set((s) => ({
      searchIndex: s.searchResults.length > 0
        ? (s.searchIndex + 1) % s.searchResults.length
        : 0,
    }));
  },

  prevSearchResult: () => {
    set((s) => ({
      searchIndex: s.searchResults.length > 0
        ? (s.searchIndex - 1 + s.searchResults.length) % s.searchResults.length
        : 0,
    }));
  },

  toggleSearch: () => {
    set((s) => {
      if (s.searchVisible) {
        return { searchVisible: false, searchQuery: '', searchResults: [], searchIndex: 0 };
      }
      return { searchVisible: true };
    });
  },

  closeSearch: () => {
    set({ searchVisible: false, searchQuery: '', searchResults: [], searchIndex: 0 });
  },

  showContextMenu: (x, y, nodeId) => set({ contextMenu: { x, y, nodeId }, edgeContextMenu: null }),
  hideContextMenu: () => set({ contextMenu: null }),
  showEdgeContextMenu: (x, y, edgeId) => set({ edgeContextMenu: { x, y, edgeId }, contextMenu: null }),
  hideEdgeContextMenu: () => set({ edgeContextMenu: null }),

  togglePanel: (panel) => {
    set((s) => ({
      panelsVisible: {
        ...s.panelsVisible,
        [panel]: !s.panelsVisible[panel],
      },
    }));
  },

  setPanelSize: (key, value) => {
    set((s) => {
      const panelSizes = { ...s.panelSizes, [key]: value };
      // Notify WinForms so it can persist the sizes
      messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, panelSizes });
      return { panelSizes };
    });
  },

  restorePanelSizes: (sizes) => {
    set((s) => ({
      panelSizes: { ...s.panelSizes, ...sizes },
    }));
  },

  setExportStatus: (status) => {
    set({ exportStatus: status });
  },

  clearExportStatus: () => {
    set({
      exportStatus: {
        hasErrors: false,
        errors: [],
        warnings: [],
      },
      diagnostics: [],
    });
  },

  setDiagnostics: (d) => set({ diagnostics: d }),

  showConnectionNotice: (message) =>
    set((s) => ({ connectionNotice: { message, nonce: (s.connectionNotice?.nonce ?? 0) + 1 } })),
  clearConnectionNotice: () => set({ connectionNotice: null }),
});
