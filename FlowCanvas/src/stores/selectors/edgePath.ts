import type { Edge } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import { START_NODE_ID } from '../slices/graphSlice';

export type EdgePathStatus = 'on-path' | 'untaken' | 'idle';

// Source states from which control flows onward to a plain successor.
// 'error' halts the trail; 'running' has not completed yet.
const PASS_THROUGH = new Set(['success', 'skipped', 'disabled']);

function branchPathOf(edge: Edge): string | undefined {
  const data = (edge.data ?? {}) as Record<string, unknown>;
  const value = data.branchPath;
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

/**
 * Classify an edge against the last/current run: 'on-path' (traversed), 'untaken'
 * (a sibling branch that did not fire — faded), or 'idle' (never reached / hidden).
 *
 * Derived from state that already persists after a run, so the result survives
 * `execution-finished`. `pathVisible` is the only gate the "Clear Path" control flips.
 * Reads only transient exec maps — never node/edge persisted data — so export is unaffected.
 */
export function selectEdgePathStatus(state: FlowStore, edgeId: string): EdgePathStatus {
  if (!state.pathVisible) return 'idle';

  const edge = state.edges.find((e) => e.id === edgeId);
  if (!edge) return 'idle';

  // The Start node never receives an exec state; its outgoing edge is traversed once
  // the block it points at has entered execution.
  if (edge.source === START_NODE_ID) {
    const targetState = state.blockStates.get(edge.target);
    return targetState && targetState !== 'idle' ? 'on-path' : 'idle';
  }

  const sourceState = state.blockStates.get(edge.source);
  if (!sourceState || sourceState === 'idle' || sourceState === 'running') return 'idle';

  const branchPath = branchPathOf(edge);
  const isBranch = !!branchPath && edge.sourceHandle !== 'continue';

  if (!isBranch) {
    // Plain successor / container continuation: traversed only if the source completed
    // cleanly (or was skipped/disabled). A failed source halts the trail here.
    return PASS_THROUGH.has(sourceState) ? 'on-path' : 'idle';
  }

  // Branch edge of a container block.
  if (sourceState === 'error') return 'idle'; // conditional failed before it branched

  const sourceNode = state.nodes.find((n) => n.id === edge.source);
  const sourceData = (sourceNode?.data ?? {}) as Record<string, unknown>;
  const blockType = typeof sourceData.blockType === 'string' ? sourceData.blockType : undefined;

  // Parallel fans out to every branch — all of them are on the path.
  if (blockType === 'parallel') return 'on-path';

  // A loop body ('do') is on-path once the loop iterated at least once.
  if ((blockType === 'foreach' || blockType === 'while') && branchPath === 'do') {
    return (state.loopIterations.get(edge.source) ?? 0) > 0 ? 'on-path' : 'untaken';
  }

  // Conditional (if / switch / try): compare against the recorded taken branch.
  const taken = state.branchTaken.get(edge.source);
  if (!taken) return 'idle'; // no branch signal — don't guess
  const matches = branchPath === taken || edge.sourceHandle === taken;
  return matches ? 'on-path' : 'untaken';
}
