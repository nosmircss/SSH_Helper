// Single source of truth for Flow Canvas block dimensions. Width is fixed per role;
// height is an ESTIMATE (xyflow exposes no measured height at layout time) used by the
// hierarchical layout and the branch-band geometry so taller/expanded blocks don't overlap.

import { summarizeBlock } from './blockSummary';

export const SPINE_WIDTH = 330;
export const CHILD_WIDTH = 300;

/** Collapsed block height estimate: header (~30) + single preview line (~22). */
export const COLLAPSED_HEIGHT = 52;

/** Expanded summary metrics (mirror BaseBlock's summary layout). */
export const SUMMARY_PAD = 14;     // top+bottom padding of the summary body
export const SUMMARY_ROW_H = 20;   // one label:value row
export const SUMMARY_FOOTER_H = 24; // "N at default" + Edit-in-Properties footer

export function nodeWidth(props: Record<string, unknown> | undefined): number {
  return props && props['_isChildOf'] ? CHILD_WIDTH : SPINE_WIDTH;
}

export function estimateNodeHeight(blockType: string, props: Record<string, unknown>, expanded: boolean): number {
  if (!expanded) return COLLAPSED_HEIGHT;
  const rows = summarizeBlock(blockType, props).rows.length;
  // header (~30) + summary body (pad + rows + footer)
  return 30 + SUMMARY_PAD + rows * SUMMARY_ROW_H + SUMMARY_FOOTER_H;
}
