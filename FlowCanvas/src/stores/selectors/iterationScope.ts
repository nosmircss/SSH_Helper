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

/**
 * The records of `loopId` visible under the current ancestor selections, time-ordered.
 * Unconstrained when no ancestor loop has a selection.
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
