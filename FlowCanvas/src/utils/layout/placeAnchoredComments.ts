import type { Node } from '@xyflow/react';
import type { NoteAnchor } from '../../nodes/CommentNode';
import { BAND_PAD, BAND_LABEL_HEADROOM } from '../nodeSize';
import { estimateCommentStep } from './hierarchicalLayout';

/**
 * Positions anchored comment nodes for the saved-user-layout load path (keeps block positions,
 * only re-anchors comments). 'leading'/'header' comments stack directly above their block (the
 * band grows to wrap leading ones — see computeBranchBands); 'branch' comments annotate the branch
 * and sit above the band header. Heights vary by comment (compact pill vs content-sized card), so
 * stacking is cumulative. Pure; returns a new array.
 */
export function placeAnchoredComments(nodes: Node[], compact = true): Node[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));

  const leadingHeight = new Map<string, number>();
  for (const n of nodes) {
    if (n.type !== 'comment') continue;
    const d = n.data as { anchor?: NoteAnchor; attachedToNodeId?: string; text?: string } | undefined;
    if (d?.anchor?.type === 'leading' && d.attachedToNodeId) {
      leadingHeight.set(d.attachedToNodeId, (leadingHeight.get(d.attachedToNodeId) ?? 0) + estimateCommentStep(String(d.text ?? ''), compact));
    }
  }

  const cumLeading = new Map<string, number>();
  const cumBranch = new Map<string, number>();
  return nodes.map((n) => {
    if (n.type !== 'comment') return n;
    const d = n.data as { anchor?: NoteAnchor; attachedToNodeId?: string; text?: string } | undefined;
    const anchor = d?.anchor?.type;
    const attachedTo = d?.attachedToNodeId;
    if (!attachedTo || !byId.has(attachedTo)) return n;
    const target = byId.get(attachedTo)!;
    const step = estimateCommentStep(String(d?.text ?? ''), compact);

    if (anchor === 'leading' || anchor === 'header') {
      const cum = (cumLeading.get(attachedTo) ?? 0) + step;
      cumLeading.set(attachedTo, cum);
      return { ...n, position: { x: target.position.x, y: target.position.y - cum } };
    }
    if (anchor === 'branch') {
      const bandTop = target.position.y - (leadingHeight.get(attachedTo) ?? 0) - BAND_PAD - BAND_LABEL_HEADROOM;
      const cum = (cumBranch.get(attachedTo) ?? 0) + step;
      cumBranch.set(attachedTo, cum);
      return { ...n, position: { x: target.position.x, y: bandTop - cum } };
    }
    return n;
  });
}
