import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { blockDefMap } from '../../blockDefs/registry';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';
import { reflowLayout } from '../reflow';
import { flushLayoutAutosave } from '../../utils/layoutAutosave';

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

/** Cap for the inverse-zoom UI scale: full screen-size compensation down to zoom ≈ 1/2.25,
 *  then flow-space chrome shrinks gracefully instead of dwarfing the blocks it belongs to. */
export const UI_SCALE_MAX = 2.25;

export interface UISlice {
  theme: 'dark' | 'light';
  reducedMotion: boolean;
  heatmapEnabled: boolean;
  branchBandsEnabled: boolean;
  compactCommentsEnabled: boolean;
  layoutMode: 'auto' | 'manual';          // active preset's mode (drives reflow gating + toolbar)
  defaultLayoutMode: 'auto' | 'manual';   // global default for new/unset presets (settings popover)
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
  // Run Output tab view state
  outputTab: 'block' | 'run';
  runOutputColor: boolean;
  runOutputWrap: boolean;
  runOutputFollow: boolean;
  runOutputUnread: boolean;
  runOutputPoppedOut: boolean;
  exportStatus: {
    hasErrors: boolean;
    errors: string[];
    warnings: string[];
  };
  diagnostics: NodeDiagnostic[];
  connectionNotice: { message: string; nonce: number } | null;
  /** Inverse-zoom scale (1 → UI_SCALE_MAX) keeping flow-space chrome (connection handles,
   *  band pills, iteration steppers) a near-constant screen size as the viewport zooms out.
   *  Quantized to 0.05 steps so pan/zoom frames don't churn subscribers. */
  uiZoomScale: number;

  setTheme: (theme: 'dark' | 'light') => void;
  toggleTheme: () => void;
  setReducedMotion: (value: boolean) => void;
  toggleReducedMotion: () => void;
  restoreReducedMotion: (value: boolean) => void;
  toggleHeatmap: () => void;
  restoreHeatmapEnabled: (enabled: boolean) => void;
  toggleBranchBands: () => void;
  restoreBranchBands: (value: boolean) => void;
  toggleCompactComments: () => void;
  restoreCompactComments: (value: boolean) => void;
  setLayoutMode: (mode: 'auto' | 'manual') => void;       // user toggle: echoes + side effects
  restoreLayoutMode: (mode: 'auto' | 'manual') => void;   // host-driven (load-graph), no echo
  setDefaultLayoutMode: (mode: 'auto' | 'manual') => void;
  restoreDefaultLayoutMode: (mode: 'auto' | 'manual') => void;
  toggleSnapToGrid: () => void;
  restoreSnapToGrid: (value: boolean) => void;
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
  setOutputTab: (tab: 'block' | 'run') => void;
  setRunOutputUnread: (unread: boolean) => void;
  toggleRunOutputColor: () => void;
  toggleRunOutputWrap: () => void;
  toggleRunOutputFollow: () => void;
  restoreRunOutputPrefs: (prefs: Partial<{ runOutputColor: boolean; runOutputWrap: boolean; runOutputFollow: boolean }>) => void;
  openRunOutputWindow: () => void;
  closeRunOutputWindow: () => void;
  setRunOutputPoppedOut: (v: boolean) => void;
  setExportStatus: (status: UISlice['exportStatus']) => void;
  clearExportStatus: () => void;
  setDiagnostics: (d: NodeDiagnostic[]) => void;
  showConnectionNotice: (message: string) => void;
  clearConnectionNotice: () => void;
  /** Feed the current viewport zoom (from ReactFlow onMove); derives + stores uiZoomScale. */
  syncUiZoomScale: (zoom: number) => void;
}

export const createUISlice: StateCreator<FlowStore, [], [], UISlice> = (set, get) => ({
  theme: 'dark',
  reducedMotion: false,
  heatmapEnabled: false,
  branchBandsEnabled: true,
  compactCommentsEnabled: true,
  layoutMode: 'auto',
  defaultLayoutMode: 'auto',
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
  outputTab: 'block',
  runOutputColor: true,
  runOutputWrap: false,
  runOutputFollow: true,
  runOutputUnread: false,
  runOutputPoppedOut: false,
  exportStatus: {
    hasErrors: false,
    errors: [],
    warnings: [],
  },
  diagnostics: [],
  connectionNotice: null,
  uiZoomScale: 1,

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

  toggleBranchBands: () => set((s) => {
    const next = !s.branchBandsEnabled;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, branchBandsEnabled: next });
    return { branchBandsEnabled: next };
  }),
  restoreBranchBands: (value) => set({ branchBandsEnabled: value }),

  toggleCompactComments: () => {
    const next = !get().compactCommentsEnabled;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, compactCommentsEnabled: next });
    set({ compactCommentsEnabled: next });
    // Comment height changes (pill <-> card), so reflow to re-reserve their vertical space.
    reflowLayout(get);
  },
  restoreCompactComments: (value) => {
    set({ compactCommentsEnabled: value });
    // Reflow so comment spacing matches the restored setting (cards reserve more than pills).
    // Without this, an import reflow that ran under the default setting leaves cards overlapping
    // until the user presses Auto-Layout. Safe whether this fires before or after load-graph.
    reflowLayout(get);
  },

  setLayoutMode: (mode) => {
    if (get().layoutMode === mode) return;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.setLayoutMode, mode });
    set({ layoutMode: mode });
    if (mode === 'auto') {
      // Auto-flow tidies immediately; the arrangement stays in host storage for a later flip back.
      if (get().nodes.length > 0) reflowLayout(get);
    } else {
      // Switching INTO Manual freezes the current on-screen positions as the saved layout.
      flushLayoutAutosave();
    }
  },
  restoreLayoutMode: (mode) => set({ layoutMode: mode }), // host-driven, no echo/reflow

  setDefaultLayoutMode: (mode) => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, defaultLayoutMode: mode });
    set({ defaultLayoutMode: mode });
  },
  restoreDefaultLayoutMode: (mode) => set({ defaultLayoutMode: mode }),

  toggleSnapToGrid: () => set((s) => {
    const next = !s.snapToGrid;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, snapToGrid: next });
    return { snapToGrid: next };
  }),
  restoreSnapToGrid: (value) => set({ snapToGrid: value }),

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

  setOutputTab: (tab) => set((s) => ({
    outputTab: tab,
    runOutputUnread: tab === 'run' ? false : s.runOutputUnread,
  })),

  setRunOutputUnread: (unread) => set({ runOutputUnread: unread }),

  toggleRunOutputColor: () => set((s) => {
    const next = !s.runOutputColor;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, runOutputColor: next });
    return { runOutputColor: next };
  }),

  toggleRunOutputWrap: () => set((s) => {
    const next = !s.runOutputWrap;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, runOutputWrap: next });
    return { runOutputWrap: next };
  }),

  toggleRunOutputFollow: () => set((s) => {
    const next = !s.runOutputFollow;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, runOutputFollow: next });
    return { runOutputFollow: next };
  }),

  restoreRunOutputPrefs: (prefs) => set((s) => ({
    runOutputColor: prefs.runOutputColor ?? s.runOutputColor,
    runOutputWrap: prefs.runOutputWrap ?? s.runOutputWrap,
    runOutputFollow: prefs.runOutputFollow ?? s.runOutputFollow,
  })),

  openRunOutputWindow: () => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.openRunOutputWindow });
    // The output is now visible in the separate window, so any pending unread dot is moot.
    set({ runOutputPoppedOut: true, outputTab: 'block', runOutputUnread: false });
  },
  closeRunOutputWindow: () => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.closeRunOutputWindow });
    // Docking back to the Run tab; clear unread like setOutputTab('run') would.
    set({ runOutputPoppedOut: false, outputTab: 'run', runOutputUnread: false });
  },
  setRunOutputPoppedOut: (v) => set({ runOutputPoppedOut: v }),

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

  syncUiZoomScale: (zoom) => {
    if (!Number.isFinite(zoom) || zoom <= 0) return;
    const next = Math.round(Math.min(UI_SCALE_MAX, Math.max(1, 1 / zoom)) * 20) / 20;
    if (get().uiZoomScale !== next) set({ uiZoomScale: next });
  },
});
