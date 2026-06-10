import type { Node } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import type { IterationRecord } from '../slices/iterationSlice';

/** Loop container block types. Owned here (not edgePath) so edgePath can import it
 *  while this module stays free of edgePath imports (no cycle). */
export const LOOP_TYPES = new Set(['foreach', 'while', 'repeat']);

function propsOf(node: Node | undefined): Record<string, unknown> {
  const data = (node?.data ?? {}) as Record<string, unknown>;
  return (data.props ?? {}) as Record<string, unknown>;
}

function blockTypeOf(node: Node | undefined): string | undefined {
  const data = (node?.data ?? {}) as Record<string, unknown>;
  return typeof data.blockType === 'string' ? data.blockType : undefined;
}

function recordBySeq(state: FlowStore, loopId: string, seq: number): IterationRecord | undefined {
  return (state.iterationLog?.get(loopId) ?? []).find((r) => r.seq === seq);
}

/**
 * The governing iteration record for a node: walk the node's ancestor chain
 * (props._isChildOf) and return the selected record of the INNERMOST loop ancestor
 * that has a non-null selection. Null = aggregate view. Because events are written
 * to every frame on their stack, the innermost selected ancestor's record answers
 * for every node beneath it.
 */
export function selectIterationScope(state: FlowStore, nodeId: string): IterationRecord | null {
  let cur = nodeId;
  const seen = new Set<string>();
  while (!seen.has(cur)) {
    seen.add(cur);
    const node = state.nodes.find((n) => n.id === cur);
    const parentId = propsOf(node)['_isChildOf'];
    if (typeof parentId !== 'string' || parentId.length === 0) return null;
    const parentNode = state.nodes.find((n) => n.id === parentId);
    const bt = blockTypeOf(parentNode);
    if (bt && LOOP_TYPES.has(bt)) {
      const sel = state.iterationSelections?.get(parentId);
      if (sel != null) {
        const rec = recordBySeq(state, parentId, sel);
        if (rec) return rec;
      }
    }
    cur = parentId;
  }
  return null;
}

export interface ActiveIterationContext {
  loopId: string;
  record: IterationRecord;
}

/**
 * The globally active iteration context — drives panels that aren't tied to one node
 * (Variables). Primary: the loop the user last touched, when it still has a selection.
 * Fallback: the DEEPEST loop with a non-null selection (inner-pulls-outer keeps chains
 * consistent; descendant-reset clears stale inners, so this is deterministic).
 * Null = live/aggregate view.
 */
export function selectActiveIterationContext(state: FlowStore): ActiveIterationContext | null {
  const resolve = (loopId: string): ActiveIterationContext | null => {
    const sel = state.iterationSelections?.get(loopId);
    if (sel == null) return null;
    const rec = (state.iterationLog?.get(loopId) ?? []).find((r) => r.seq === sel);
    return rec ? { loopId, record: rec } : null;
  };

  const last = state.lastSelectedLoopId;
  if (last) {
    const ctx = resolve(last);
    if (ctx) return ctx;
  }

  // Fallback: deepest selected loop by parent-chain length.
  let best: ActiveIterationContext | null = null;
  let bestDepth = -1;
  for (const [loopId, sel] of state.iterationSelections ?? []) {
    if (sel == null) continue;
    const ctx = resolve(loopId);
    if (!ctx) continue;
    let depth = 0;
    let p = ctx.record.parent;
    const seen = new Set<number>();
    while (p && !seen.has(p.seq)) {
      depth++;
      seen.add(p.seq);
      p = (state.iterationLog?.get(p.loopId) ?? []).find((r) => r.seq === p!.seq)?.parent ?? null;
    }
    if (depth > bestDepth) { best = ctx; bestDepth = depth; }
  }
  return best;
}

/**
 * The records of `loopId` visible under the current ancestor selections, time-ordered.
 * Unconstrained when no ancestor loop has a selection.
 *
 * Click-time only: invoked on selection changes and when a loop's iteration cluster
 * renders (behind useMemo) — never on the per-edge render path. Returns a FRESH array
 * every call (filter allocates), so callers must memoize the result; feeding it raw
 * into a zustand selector would re-render on every store change.
 */
export function selectVisibleIterations(state: FlowStore, loopId: string): IterationRecord[] {
  const records = state.iterationLog?.get(loopId) ?? [];
  const governing = selectIterationScope(state, loopId);
  if (!governing) return records;
  return records.filter((r) => {
    let p = r.parent;
    const seen = new Set<number>();
    while (p && !seen.has(p.seq)) {
      if (p.seq === governing.seq) return true;
      seen.add(p.seq);
      p = recordBySeq(state, p.loopId, p.seq)?.parent ?? null;
    }
    return false;
  });
}
