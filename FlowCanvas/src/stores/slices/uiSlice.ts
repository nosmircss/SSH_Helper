import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { blockDefMap } from '../../blockDefs/registry';

export interface UISlice {
  theme: 'dark' | 'light';
  snapToGrid: boolean;
  gridSize: number;
  searchQuery: string;
  searchResults: string[];
  searchIndex: number;
  searchVisible: boolean;
  contextMenu: { x: number; y: number; nodeId: string } | null;
  panelsVisible: {
    variables: boolean;
    debug: boolean;
    output: boolean;
    timeline: boolean;
  };

  setTheme: (theme: 'dark' | 'light') => void;
  toggleTheme: () => void;
  toggleSnapToGrid: () => void;
  setSearchQuery: (query: string) => void;
  nextSearchResult: () => void;
  prevSearchResult: () => void;
  toggleSearch: () => void;
  closeSearch: () => void;
  showContextMenu: (x: number, y: number, nodeId: string) => void;
  hideContextMenu: () => void;
  togglePanel: (panel: keyof UISlice['panelsVisible']) => void;
}

export const createUISlice: StateCreator<FlowStore, [], [], UISlice> = (set, get) => ({
  theme: 'dark',
  snapToGrid: false,
  gridSize: 20,
  searchQuery: '',
  searchResults: [],
  searchIndex: 0,
  searchVisible: false,
  contextMenu: null,
  panelsVisible: {
    variables: true,
    debug: false,
    output: false,
    timeline: false,
  },

  setTheme: (theme) => set({ theme }),
  toggleTheme: () => set((s) => ({ theme: s.theme === 'dark' ? 'light' : 'dark' })),

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

  showContextMenu: (x, y, nodeId) => set({ contextMenu: { x, y, nodeId } }),
  hideContextMenu: () => set({ contextMenu: null }),

  togglePanel: (panel) => {
    set((s) => ({
      panelsVisible: {
        ...s.panelsVisible,
        [panel]: !s.panelsVisible[panel],
      },
    }));
  },
});
