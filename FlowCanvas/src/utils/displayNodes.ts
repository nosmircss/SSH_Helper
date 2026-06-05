import type { Node } from '@xyflow/react';

/**
 * Comment nodes render as fit-content pills/cards, but a legacy/persisted fixed box (the old
 * 200x100 default, re-applied on layout restore) makes React Flow give the node an oversized
 * INVISIBLE hit area. That box overlaps the neighbouring block below and the branch-band label
 * handle, so a drag aimed at the block (or the band) is hijacked by the comment. Clear all explicit
 * sizing so React Flow auto-measures the visible card instead. `measured` is left untouched so RF
 * manages re-measurement (clearing it every render would thrash the ResizeObserver).
 */
export function contentSizeComment<T extends Node>(node: T): T {
  if (node.type !== 'comment') return node;
  let style = node.style;
  if (style && ('width' in style || 'height' in style)) {
    const next = { ...style } as Record<string, unknown>;
    delete next.width;
    delete next.height;
    style = next as typeof node.style;
  }
  return { ...node, width: undefined, height: undefined, style };
}

/**
 * Stable-sort comments before blocks so blocks render LAST (on top). React Flow stacks later nodes
 * above earlier ones when no zIndex is set, so this guarantees a block wins the pointer in any
 * residual overlap region — clicking a block never grabs a comment sitting above it. Comments stay
 * fully visible (they sit above their block, so nothing covers their own area).
 */
export function orderCommentsBehind<T extends Node>(nodes: T[]): T[] {
  const rank = (n: T) => (n.type === 'comment' ? 0 : 1);
  return [...nodes].sort((a, b) => rank(a) - rank(b));
}
