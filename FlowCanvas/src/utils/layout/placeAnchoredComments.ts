import type { Node } from '@xyflow/react';

const PILL_GAP = 34;

/** Positions anchored comment nodes just above their attached block. Pure; returns a new array. */
export function placeAnchoredComments(nodes: Node[]): Node[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  return nodes.map((n) => {
    if (n.type !== 'comment') return n;
    const data = n.data as Record<string, unknown> | undefined;
    const anchor = data?.anchor as { type?: string } | undefined;
    const attachedTo = data?.attachedToNodeId as string | undefined;
    if (!attachedTo || (anchor?.type !== 'leading' && anchor?.type !== 'header')) return n;
    const target = byId.get(attachedTo);
    if (!target) return n;
    return { ...n, position: { x: target.position.x, y: target.position.y - PILL_GAP } };
  });
}
