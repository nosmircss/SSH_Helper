import type { Node } from '@xyflow/react';
import type { NoteAnchor } from '../../nodes/CommentNode';
import { BAND_PAD, BAND_LABEL_HEADROOM } from '../nodeSize';
import { branchKeyFromStepPath } from '../branchBands';

// Vertical room per anchored comment pill (must match COMMENT_PILL_STEP in hierarchicalLayout).
const PILL_STEP = 28;

/** Min-Y child of each branch group — a comment anchored to it sits ABOVE the branch band header. */
function bandTopIds(nodes: Node[]): Set<string> {
  const groupMinY = new Map<string, { id: string; y: number }>();
  for (const n of nodes) {
    if (n.type === 'comment') continue;
    const props = (n.data as { props?: Record<string, unknown> } | undefined)?.props;
    const parentId = props?.['_isChildOf'] as string | undefined;
    if (!parentId) continue;
    const branchKey = branchKeyFromStepPath(
      props?.['_stepPath'] as string | undefined,
      props?.['_branchLabel'] as string | undefined,
    );
    const key = `${parentId}::${branchKey}`;
    const cur = groupMinY.get(key);
    if (!cur || n.position.y < cur.y) groupMinY.set(key, { id: n.id, y: n.position.y });
  }
  return new Set([...groupMinY.values()].map((v) => v.id));
}

/**
 * Positions anchored comment nodes above their attached block (used on the saved-user-layout load
 * path, which keeps block positions and only re-anchors comments). A comment on a branch's top
 * child sits above the branch BAND header; others sit above their block. Pure; returns a new array.
 */
export function placeAnchoredComments(nodes: Node[]): Node[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const tops = bandTopIds(nodes);

  type CommentMeta = { nodeIndex: number; attachedTo: string; sibIdx: number };
  const metas: CommentMeta[] = [];
  const sibCount = new Map<string, number>();
  for (let i = 0; i < nodes.length; i++) {
    const n = nodes[i];
    if (n.type !== 'comment') continue;
    const data = n.data as Record<string, unknown> | undefined;
    const anchor = data?.anchor as NoteAnchor | undefined;
    const attachedTo = data?.attachedToNodeId as string | undefined;
    if (!attachedTo || (anchor?.type !== 'leading' && anchor?.type !== 'header')) continue;
    if (!byId.has(attachedTo)) continue;
    const idx = sibCount.get(attachedTo) ?? 0;
    sibCount.set(attachedTo, idx + 1);
    metas.push({ nodeIndex: i, attachedTo, sibIdx: idx });
  }

  if (metas.length === 0) return [...nodes];

  const metaByNodeIndex = new Map(metas.map((m) => [m.nodeIndex, m]));

  return nodes.map((n, i) => {
    const meta = metaByNodeIndex.get(i);
    if (!meta) return n;
    const target = byId.get(meta.attachedTo)!;
    const baseY = tops.has(meta.attachedTo)
      ? target.position.y - BAND_PAD - BAND_LABEL_HEADROOM // above the branch band header
      : target.position.y;                                 // above the block
    return {
      ...n,
      position: { x: target.position.x, y: baseY - PILL_STEP * (meta.sibIdx + 1) },
    };
  });
}
