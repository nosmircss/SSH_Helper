import { computeHierarchicalLayout } from '../utils/layout/hierarchicalLayout';
import { selectCanvasSizing } from './slices/canvasSizing';
import type { FlowStore } from './useFlowStore';

/**
 * Re-runs hierarchical layout on the live store, threading the active canvas sizing.
 * This is what reserves vertical space for anchored comments (pushing the block and
 * everything below it down) and positions each comment directly above its block — and
 * reclaims that space when a comment is removed. No-op on an empty graph.
 *
 * Call AFTER mutating the store (it reads a fresh snapshot via get()). Reflowing
 * re-derives every block position, so callers should gate it to changes that actually
 * affect reserved space — i.e. ANCHORED comment add/remove — to avoid re-guttering
 * free-floating stickies or discarding a manual arrangement for no benefit.
 */
export function reflowLayout(get: () => FlowStore): void {
  const st = get();
  if (st.nodes.length === 0) return;
  // keepOrphans: this is an automatic reflow, so don't yank unwired/manually-placed orphan blocks
  // onto the spine — only the explicit Auto-Layout button organizes them.
  st.setNodes(computeHierarchicalLayout(st.nodes, st.edges, selectCanvasSizing(st), { keepOrphans: true }));
}
