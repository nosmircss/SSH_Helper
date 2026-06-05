import type { Node } from '@xyflow/react';
import type { NoteAnchor } from '../../nodes/CommentNode';
import { BAND_PAD, BAND_LABEL_HEADROOM } from '../nodeSize';

// Vertical room per anchored comment pill (must match COMMENT_PILL_STEP in hierarchicalLayout).
const PILL_STEP = 28;

/**
 * Positions anchored comment nodes for the saved-user-layout load path (keeps block positions,
 * only re-anchors comments). 'leading'/'header' pills stack directly above their block (the band
 * grows to wrap leading pills — see computeBranchBands); 'branch' pills annotate the branch and
 * sit above the band header. Pure; returns a new array.
 */
export function placeAnchoredComments(nodes: Node[]): Node[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));

  const leadingCount = new Map<string, number>();
  for (const n of nodes) {
    if (n.type !== 'comment') continue;
    const d = n.data as { anchor?: NoteAnchor; attachedToNodeId?: string } | undefined;
    if (d?.anchor?.type === 'leading' && d.attachedToNodeId) {
      leadingCount.set(d.attachedToNodeId, (leadingCount.get(d.attachedToNodeId) ?? 0) + 1);
    }
  }

  const leadingSib = new Map<string, number>();
  const branchSib = new Map<string, number>();
  return nodes.map((n) => {
    if (n.type !== 'comment') return n;
    const d = n.data as { anchor?: NoteAnchor; attachedToNodeId?: string } | undefined;
    const anchor = d?.anchor?.type;
    const attachedTo = d?.attachedToNodeId;
    if (!attachedTo || !byId.has(attachedTo)) return n;
    const target = byId.get(attachedTo)!;

    if (anchor === 'leading' || anchor === 'header') {
      const idx = leadingSib.get(attachedTo) ?? 0;
      leadingSib.set(attachedTo, idx + 1);
      return { ...n, position: { x: target.position.x, y: target.position.y - PILL_STEP * (idx + 1) } };
    }
    if (anchor === 'branch') {
      const idx = branchSib.get(attachedTo) ?? 0;
      branchSib.set(attachedTo, idx + 1);
      const L = leadingCount.get(attachedTo) ?? 0;
      const bandTop = target.position.y - L * PILL_STEP - BAND_PAD - BAND_LABEL_HEADROOM;
      return { ...n, position: { x: target.position.x, y: bandTop - PILL_STEP * (idx + 1) } };
    }
    return n;
  });
}
