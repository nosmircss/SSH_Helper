import type { Node } from '@xyflow/react';
import type { NoteAnchor } from '../../nodes/CommentNode';

const PILL_GAP = 34;

/** Positions anchored comment nodes just above their attached block. Pure; returns a new array. */
export function placeAnchoredComments(nodes: Node[]): Node[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));

  // Count siblings (leading/header comments) per target so we can stack them upward.
  const siblingIndex = new Map<string, number>();
  const getSiblingIndex = (commentId: string, attachedTo: string): number => {
    const key = attachedTo;
    const idx = siblingIndex.get(key) ?? 0;
    siblingIndex.set(key, idx + 1);
    return idx;
  };

  // Two-pass: first pass computes sibling order (in array order), second pass positions.
  // We collect metadata first so we can assign stable indices.
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
    return {
      ...n,
      position: {
        x: target.position.x,
        y: target.position.y - PILL_GAP * (meta.sibIdx + 1),
      },
    };
  });
}
