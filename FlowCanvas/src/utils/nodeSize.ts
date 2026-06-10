// Single source of truth for Flow Canvas block dimensions. Width is fixed per role;
// height is an ESTIMATE (xyflow exposes no measured height at layout time) used by the
// hierarchical layout and the branch-band geometry so taller/expanded blocks don't overlap.

import { summarizeBlock } from './blockSummary';

export const SPINE_WIDTH = 330;
export const CHILD_WIDTH = 300;
/** Spine→child width delta (children inside containers are this much narrower). Single source. */
export const BLOCK_WIDTH_INSET = SPINE_WIDTH - CHILD_WIDTH; // 30

/** Collapsed block height estimate at textScale 1: header (~30) + single preview line (~22). */
export const COLLAPSED_HEIGHT = 52;

/** Text-driven slice of a collapsed block (header label/badge line + preview line, ~30px at 1×).
 *  Paddings, borders, and the fixed 20px icon chip don't scale with text, so only this slice
 *  grows past 1×; below 1× the icon chip floors the header and the historical 52 stands. */
const COLLAPSED_TEXT_SLICE = 30;

/** Padding a branch lane adds around its content on every side. Lives here (not in branchBands)
 *  so the hierarchical layout can reserve horizontal room for lanes without depending on the
 *  band layer. The layout and the band geometry MUST use the same value. */
export const BAND_PAD = 18;

/** Extra vertical room added to the TOP of a branch band only (render-only — NOT consumed by
 *  the hierarchical layout engine) so the draggable label pill clears the first child block. */
export const BAND_LABEL_HEADROOM = 12;

/** Expanded summary metrics (mirror BaseBlock's summary layout). */
export const SUMMARY_PAD = 14;     // top+bottom padding of the summary body
export const SUMMARY_ROW_H = 20;   // one single-line label:value row
export const SUMMARY_ROW_WRAP_H = 32; // a row whose label wraps to two lines
export const SUMMARY_FOOTER_H = 24; // "N at default" + Edit-in-Properties footer
/** Label column fits ~16 chars per line (96px at the 10.5px/600 label font; the column width
 *  scales with textScale in BaseBlock, so the chars-per-line budget is scale-invariant). */
const SUMMARY_LABEL_LINE_CHARS = 16;

export function nodeWidth(props: Record<string, unknown> | undefined): number {
  return props && props['_isChildOf'] ? CHILD_WIDTH : SPINE_WIDTH;
}

export function estimateNodeHeight(
  blockType: string,
  props: Record<string, unknown>,
  expanded: boolean,
  textScale = 1,
): number {
  if (!expanded) return COLLAPSED_HEIGHT + Math.round(COLLAPSED_TEXT_SLICE * Math.max(0, textScale - 1));
  // Long labels wrap to a second line and the row really renders taller — count them, or the
  // estimate undershoots and the next block overlaps (worst with many wrapped rows at XL/XXL).
  const rowsH = summarizeBlock(blockType, props).rows.reduce(
    (sum, r) => sum + (r.label.length > SUMMARY_LABEL_LINE_CHARS ? SUMMARY_ROW_WRAP_H : SUMMARY_ROW_H),
    0,
  );
  // header (~30) + summary body (pad + rows + footer); the summary text scales, so does its height.
  return Math.round((30 + SUMMARY_PAD + rowsH + SUMMARY_FOOTER_H) * textScale);
}
