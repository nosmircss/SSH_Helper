import { useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeHierarchicalLayout } from '../utils/layout/hierarchicalLayout';

/**
 * Returns a stable callback that re-lays the whole graph with the structure-aware
 * hierarchical engine. Always overrides the current arrangement (explicit user action);
 * pushes an undo snapshot first so it is reversible.
 */
export function useAutoLayout(): () => void {
  return useCallback(() => {
    const store = useFlowStore.getState();
    store.pushSnapshot('Auto-layout');
    const layouted = computeHierarchicalLayout(store.nodes, store.edges);
    store.setNodes(layouted, { markDirty: true });
  }, []);
}
