import { useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeAutoLayout } from '../utils/autoLayout';

/**
 * Returns a stable callback that computes and applies an automatic
 * top-to-bottom dagre layout to all nodes in the flow store.
 * Pushes an undo snapshot before applying the layout.
 */
export function useAutoLayout(): () => void {
  return useCallback(() => {
    const store = useFlowStore.getState();
    store.pushSnapshot('Auto-layout');
    const layouted = computeAutoLayout(store.nodes, store.edges);
    store.setNodes(layouted, { markDirty: true });
  }, []);
}
