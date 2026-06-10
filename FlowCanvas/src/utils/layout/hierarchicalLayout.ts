import type { Edge, Node } from '@xyflow/react';
import { buildLayoutTree } from './treeBuilder';
import type { LayoutBranch, LayoutTree, LayoutTreeNode, Point } from './types';
import { estimateNodeHeight, COLLAPSED_HEIGHT, BAND_PAD, BAND_LABEL_HEADROOM, BLOCK_WIDTH_INSET } from '../nodeSize';

/** Single source of truth for layout spacing (ported from FlowCanvasBridge.cs). */
export const LAYOUT = {
  NODE_SPACING_Y: 106,
  // EVERY container indents its branches right of the spine by this offset and routes its
  // continuation straight down the spine gutter from the bottom-CENTER handle (x = node.x +
  // SPINE_WIDTH/2). For that wire to clear the branch band, the body must indent far enough that the
  // band's left wall (childX - BAND_PAD) lands right of the wire — i.e. offset > SPINE_WIDTH/2 +
  // BAND_PAD = 183. 220 leaves a comfortable gutter. (Multi-branch containers used to anchor their
  // first branch under the container with a bottom-LEFT continuation corridor that escaped the band;
  // they now indent like single-branch ones — one routing rule for all.)
  BRANCH_CHILD_OFFSET: 220,
  NODE_START_X: 250,
  NODE_START_Y: 40,
  CHILD_NODE_MAX_WIDTH: 300,
  COLUMN_GAP: 30,
  // Generous clear space between sibling branch lanes (beyond each lane's BAND_PAD), so lanes read
  // as distinct columns with breathing room. The canvas has ample horizontal room to spread into.
  LANE_GAP: 72,
  get BASE_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 330
  COLUMN_WIDTH_DECAY: 0.92,
  get MIN_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 330
  MAX_SPREAD_WIDTH: 1400,
  MAX_NESTING_DEPTH: 5,
} as const;

/** User-facing canvas sizing. Defaults reproduce the historical fixed geometry. */
export interface BlockSizing { blockWidth: number; density: number; textScale: number; compactComments?: boolean; }
export const DEFAULT_BLOCK_SIZING: BlockSizing = { blockWidth: 330, density: 1, textScale: 1, compactComments: true };

interface ResolvedSizing {
  childWidth: number; columnWidth: number; nodeSpacingY: number; branchOffset: number; textScale: number; compactComments: boolean;
}
function resolveSizing(s: BlockSizing): ResolvedSizing {
  const childWidth = s.blockWidth - BLOCK_WIDTH_INSET;            // preserves the 30px child inset
  return {
    childWidth,
    columnWidth: childWidth + LAYOUT.COLUMN_GAP,                  // = blockWidth
    nodeSpacingY: Math.round(LAYOUT.NODE_SPACING_Y * s.density),
    // Branch-gutter invariant (see BRANCH_CHILD_OFFSET comment): offset > blockWidth/2 + BAND_PAD.
    // blockWidth/2 + BAND_PAD + 37 reproduces 330 -> 220 and keeps the gutter at every width.
    branchOffset: Math.round(s.blockWidth / 2 + BAND_PAD + 37),
    textScale: s.textScale,
    compactComments: s.compactComments ?? true,
  };
}
// Set synchronously at the top of computeHierarchicalLayout (no await in the pass => no reentrancy).
// computeBranchBands is a SEPARATE function (different file) with its own param and is unaffected.
let activeSizing: ResolvedSizing = resolveSizing(DEFAULT_BLOCK_SIZING);

// Vertical space one anchored comment occupies above its block (height + a small gap). Compact
// mode renders a slim fixed-height pill; non-compact renders a content-sized card that varies by
// text. Used both to RESERVE room above a commented block (so comments never overlap the block/
// band above) and to STACK multiple comments. Exported so the saved-layout placement pass uses
// the identical metric.
const COMMENT_PILL_STEP = 28; // single-line pill (height + gap)
const COMMENT_PILL_LINE = 16; // each additional pill line
const COMMENT_CARD_GAP = 8;
export function estimateCommentStep(text: string, compact: boolean): number {
  const t = text ?? '';
  if (compact) {
    // A compact pill is content-width (no wrapping), so it only grows by explicit newlines.
    const lineCount = Math.max(1, t.split('\n').length);
    return COMMENT_PILL_STEP + (lineCount - 1) * COMMENT_PILL_LINE;
  }
  const lines = Math.max(1, t.split('\n').length, Math.ceil(t.length / 33));
  return 12 + lines * 17 + COMMENT_CARD_GAP; // card padding + wrapped lines + gap
}
// attachedToNodeId -> total reserved pixels for its anchored comments (pre-summed).
let commentReserveByTarget: Map<string, number> = new Map();
function reserveAbove(id: string): number {
  return commentReserveByTarget.get(id) ?? 0;
}

function verticalGap(): number { return activeSizing.nodeSpacingY - COLLAPSED_HEIGHT; } // preserves collapsed spacing

function advanceFor(n: LayoutTreeNode): number {
  const data = (n.node?.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
  // Collapsed and expanded both go through the textScale-aware estimate + the base gap, so
  // bigger text sizes keep the same breathing room. At textScale 1 a collapsed block reduces
  // to COLLAPSED_HEIGHT + (nodeSpacingY - COLLAPSED_HEIGHT) = nodeSpacingY — the historical step.
  return estimateNodeHeight(data.blockType ?? '', data.props ?? {}, !!data.expanded, activeSizing.textScale) + verticalGap();
}

interface SubtreeSize { columns: number; rows: number; indent: number; }

function nonEmptyBranches(node: LayoutTreeNode): LayoutBranch[] {
  return node.branches.filter((b) => b.children.length > 0);
}

// NOTE: MIN_COLUMN_WIDTH currently equals BASE_COLUMN_WIDTH (330), so the depth decay is
// always clamped away and every column is a fixed 330px — this intentionally matches the
// original C# behaviour. The decay/base/max-spread knobs are retained for parity; lower
// MIN_COLUMN_WIDTH if you want depth-decay to actually take effect.
function getColumnWidth(depth: number): number {
  return Math.max(activeSizing.columnWidth, activeSizing.columnWidth * Math.pow(LAYOUT.COLUMN_WIDTH_DECAY, depth));
}

/** Mirrors C# MeasureSteps: column count + row count of a branch subtree, plus `indent` — the
 *  maximum rightward pixel offset that container nesting adds within the subtree. Every container
 *  shifts its branches right by BRANCH_CHILD_OFFSET each level; multi-branch containers ALSO add
 *  columns for their side-by-side arms. The branch slot width uses `indent` so a sibling branch is
 *  placed clear of deeply-indented nested content. */
function measureSteps(children: LayoutTreeNode[]): SubtreeSize {
  let columns = 1;
  let rows = 0;
  let indent = 0;
  for (const child of children) {
    rows += 1;
    if (!child.isContainer) continue;
    const branches = nonEmptyBranches(child);
    if (branches.length >= 2) {
      let totalCols = 0;
      let maxRows = 0;
      let branchesExtra = 0;
      for (const b of branches) {
        const s = measureSteps(b.children);
        totalCols += s.columns;
        maxRows = Math.max(maxRows, s.rows);
        // Each nested arm pushes the next right by its FULL branchSlotWidth; the column count above
        // only covers the column part, so reserve the non-column part here (the arm's own indent +
        // lane padding). Mirrors placeMultiBranch's `extra`. Using maxIndent alone undercounted the
        // inner lane padding, letting the outer band's right edge spill into the next sibling lane.
        branchesExtra += s.indent + 2 * BAND_PAD + LAYOUT.LANE_GAP;
      }
      columns = Math.max(columns, Math.max(2, totalCols));
      rows += maxRows;
      // This container shifts its own arms right by the offset too (like the single-branch case
      // below), so reserve the offset plus the full nested spread computed above.
      indent = Math.max(indent, activeSizing.branchOffset + branchesExtra);
    } else {
      for (const b of branches) {
        const s = measureSteps(b.children);
        columns = Math.max(columns, s.columns);
        rows += s.rows;
        indent = Math.max(indent, activeSizing.branchOffset + s.indent);
      }
    }
  }
  return { columns, rows, indent };
}

/** Left-to-left horizontal advance reserved for a branch: its column span + its single-branch
 *  indentation + a lane's padding on both sides + a generous inter-lane gap, so adjacent lanes
 *  never collide and read as clearly separate columns. */
function branchSlotWidth(s: SubtreeSize, colWidth: number): number {
  return s.columns * colWidth + s.indent + 2 * BAND_PAD + LAYOUT.LANE_GAP;
}

function placeBranchSteps(
  children: LayoutTreeNode[],
  depth: number,
  childX: number,
  centerX: number,
  startY: number,
  pos: Map<string, Point>,
): number {
  let y = startY;
  for (const child of children) {
    y += reserveAbove(child.id); // make room for this child's leading-comment pills
    pos.set(child.id, { x: childX, y });
    y += advanceFor(child);
    if (child.isContainer && depth < LAYOUT.MAX_NESTING_DEPTH && nonEmptyBranches(child).length > 0) {
      y = placeContainer(child, depth + 1, centerX, y, pos);
    }
  }
  return y;
}

function placeSingleBranch(branch: LayoutBranch, depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const childX = centerX + activeSizing.branchOffset;
  return placeBranchSteps(branch.children, depth, childX, childX, startY, pos);
}

function placeMultiBranch(branches: LayoutBranch[], depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const sizes = branches.map((b) => measureSteps(b.children));
  const totalColumns = sizes.reduce((sum, s) => sum + s.columns, 0);
  // Fixed extra width (the container's own branch indent + per-branch nested indent + lane padding +
  // inter-lane gap) is independent of colWidth; only the column part scales when clamping to
  // MAX_SPREAD_WIDTH. Mirrors leftX's starting offset + branchSlotWidth.
  const extra = activeSizing.branchOffset + sizes.reduce((sum, s) => sum + s.indent + 2 * BAND_PAD + LAYOUT.LANE_GAP, 0);
  let colWidth = getColumnWidth(depth);
  if (totalColumns > 0 && totalColumns * colWidth + extra > LAYOUT.MAX_SPREAD_WIDTH) {
    colWidth = Math.max(activeSizing.childWidth, (LAYOUT.MAX_SPREAD_WIDTH - extra) / totalColumns);
  }
  // Indent the FIRST (primary: then/do/try/case-0) branch right of the container by
  // BRANCH_CHILD_OFFSET — opening the spine gutter so the straight continuation clears the band —
  // and lay the remaining branches out to the right. Each branch reserves its TRUE content width
  // (columns + nested indent + lane padding), so a sibling never overlaps a branch whose nested
  // bodies are indented far to the right. (#45: a nested body only ever moves further RIGHT, never
  // left of its parent's column.)
  let leftX = centerX + activeSizing.branchOffset;
  let maxEndY = startY;
  for (let i = 0; i < branches.length; i++) {
    const endY = placeBranchSteps(branches[i].children, depth, leftX, leftX, startY, pos);
    maxEndY = Math.max(maxEndY, endY);
    leftX += branchSlotWidth(sizes[i], colWidth);
  }
  return maxEndY;
}

function placeContainer(node: LayoutTreeNode, depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const branches = nonEmptyBranches(node);
  if (branches.length === 0) return startY;
  if (branches.length >= 2) return placeMultiBranch(branches, depth, centerX, startY, pos);
  return placeSingleBranch(branches[0], depth, centerX, startY, pos);
}

export function placeTree(tree: LayoutTree): Map<string, Point> {
  const pos = new Map<string, Point>();
  let currentY = LAYOUT.NODE_START_Y + activeSizing.nodeSpacingY;
  for (const node of tree.spine) {
    currentY += reserveAbove(node.id); // make room for this node's leading-comment pills
    pos.set(node.id, { x: LAYOUT.NODE_START_X, y: currentY });
    currentY += advanceFor(node);
    if (node.isContainer && nonEmptyBranches(node).length > 0) {
      currentY = placeContainer(node, 1, LAYOUT.NODE_START_X, currentY, pos);
    }
  }
  return pos;
}

/**
 * Comments are excluded from the layout tree (see treeBuilder), so once the blocks are
 * snapped onto the spine the comments keep their pre-import coordinates — which can leave a
 * comment sitting on top of a block, where its DOM then swallows clicks meant for that block.
 * Park comments in a gutter to the right of the widest placed node so they never overlap.
 */
function placeComments(nodes: Node[], pos: Map<string, Point>): void {
  const comments = nodes.filter((n) => n.type === 'comment');
  if (comments.length === 0 || pos.size === 0) return;
  // A comment may be anchored to a kept-orphan block (keepOrphans leaves it out of `pos`). Fall
  // back to the block's current position so the comment still lands above it, not in the gutter.
  const byId = new Map(nodes.map((n) => [n.id, n] as const));
  const gutterX = Math.max(...[...pos.values()].map((p) => p.x)) + activeSizing.columnWidth;
  let gutterY = LAYOUT.NODE_START_Y;

  const compact = activeSizing.compactComments;

  // 'leading'/'header' comments stack directly above their block (the band, if any, grows to wrap
  // them — see computeBranchBands). 'branch' comments annotate the whole branch and sit ABOVE the
  // band header, which is itself above the leading stack — so they need the total leading height.
  const leadingHeight = new Map<string, number>();
  for (const c of comments) {
    const d = c.data as Record<string, unknown> | undefined;
    const at = d?.attachedToNodeId as string | undefined;
    if (at && (d?.anchor as { type?: string } | undefined)?.type === 'leading') {
      leadingHeight.set(at, (leadingHeight.get(at) ?? 0) + estimateCommentStep(String(d?.text ?? ''), compact));
    }
  }

  const cumLeading = new Map<string, number>(); // running stacked height per target
  const cumBranch = new Map<string, number>();
  for (const c of comments) {
    const data = c.data as Record<string, unknown> | undefined;
    const anchor = (data?.anchor as { type?: string } | undefined)?.type;
    const attachedTo = data?.attachedToNodeId as string | undefined;
    const targetPos = attachedTo ? (pos.get(attachedTo) ?? byId.get(attachedTo)?.position) : undefined;
    const step = estimateCommentStep(String(data?.text ?? ''), compact);

    if (targetPos && (anchor === 'leading' || anchor === 'header')) {
      const cum = (cumLeading.get(attachedTo!) ?? 0) + step;
      cumLeading.set(attachedTo!, cum);
      pos.set(c.id, { x: targetPos.x, y: targetPos.y - cum });
    } else if (targetPos && anchor === 'branch') {
      const bandTop = targetPos.y - (leadingHeight.get(attachedTo!) ?? 0) - BAND_PAD - BAND_LABEL_HEADROOM;
      const cum = (cumBranch.get(attachedTo!) ?? 0) + step;
      cumBranch.set(attachedTo!, cum);
      pos.set(c.id, { x: targetPos.x, y: bandTop - cum });
    } else {
      pos.set(c.id, { x: gutterX, y: gutterY });
      gutterY += activeSizing.nodeSpacingY;
    }
  }
}

/**
 * True when a comment node reserves vertical layout space above its block — i.e. it stacks above
 * the block (leading/header) or above the band (branch). 'inline' comments and free-floating
 * stickies reserve nothing. This is the SINGLE source of truth for the reserve rule below, and it
 * is also what callers should use to decide whether adding/removing a comment warrants a reflow —
 * keeping the reflow trigger from drifting away from what the layout actually reserves.
 */
export function anchorReservesLayoutSpace(node: Node): boolean {
  if (node.type !== 'comment') return false;
  const data = node.data as Record<string, unknown> | undefined;
  if (!data?.attachedToNodeId) return false;
  const t = (data.anchor as { type?: string } | undefined)?.type;
  return t === 'leading' || t === 'header' || t === 'branch';
}

/**
 * Structure-aware layout: rebuild the container/branch tree and position it with the
 * smart-hybrid rules. Returns new node objects with updated positions; nodes not in the
 * tree (the start node, orphans the builder left unplaced) keep their position.
 */
// `sizing` is REQUIRED: every caller must thread the live Display Settings (use selectCanvasSizing
// from settingsSlice for store callers, or DEFAULT_BLOCK_SIZING for the factory geometry). Making it
// required is deliberate — a silent default let the Layout button / expand-all revert wide/roomy
// graphs to 330/1/1 geometry (band overlap + apparent settings reset).
// keepOrphans (default false): forwarded to buildLayoutTree — true preserves the position of
// disconnected/unwired blocks (automatic reflows), false organizes them onto the spine (the
// explicit Auto-Layout button and initial import).
export function computeHierarchicalLayout(
  nodes: Node[],
  edges: Edge[],
  sizing: BlockSizing,
  opts: { keepOrphans?: boolean } = {},
): Node[] {
  activeSizing = resolveSizing(sizing);
  // Tally leading/header comments per target block so placeTree/placeBranchSteps can reserve
  // vertical room above each commented block (pills never overlap the block or band above).
  commentReserveByTarget = new Map();
  for (const n of nodes) {
    if (!anchorReservesLayoutSpace(n)) continue;
    const data = n.data as Record<string, unknown>;
    const attachedTo = data.attachedToNodeId as string;
    const step = estimateCommentStep(String(data.text ?? ''), activeSizing.compactComments);
    commentReserveByTarget.set(attachedTo, (commentReserveByTarget.get(attachedTo) ?? 0) + step);
  }
  const tree = buildLayoutTree(nodes, edges, opts.keepOrphans ?? false);
  const pos = placeTree(tree);
  placeComments(nodes, pos);
  return nodes.map((n) => {
    const p = pos.get(n.id);
    return p ? { ...n, position: p } : n;
  });
}

/**
 * Manual-mode placement for blocks that have no saved position yet (added in the text editor while
 * the preset was Manual). Existing/placed blocks never move. Each new block is dropped just below
 * its predecessor (the source of its incoming edge) in the USER's arrangement, aligned to the
 * predecessor's x, then nudged downward off any block already occupying that point. New blocks are
 * tagged data._justPlaced so the node can show a brief "new" highlight. Comments are handled by the
 * caller (placeAnchoredComments).
 */
export function placeNewBlocksNearNeighbors(
  nodes: Node[],
  edges: Edge[],
  newIds: Set<string>,
  sizing: BlockSizing,
): Node[] {
  if (newIds.size === 0) return nodes;
  const step = Math.round(LAYOUT.NODE_SPACING_Y * sizing.density);
  const predOf = new Map<string, string>(); // target -> source
  for (const e of edges) if (!predOf.has(e.target)) predOf.set(e.target, e.source);

  const posById = new Map<string, Point>();
  for (const n of nodes) if (n.position) posById.set(n.id, { x: n.position.x, y: n.position.y });

  const occupied = (): Point[] =>
    nodes.filter((n) => n.type !== 'comment' && !newIds.has(n.id))
      .map((n) => posById.get(n.id)!).filter(Boolean);

  const ordered = nodes.filter((n) => newIds.has(n.id));
  for (const node of ordered) {
    const pred = predOf.get(node.id);
    const base = (pred && posById.get(pred)) || { x: LAYOUT.NODE_START_X, y: LAYOUT.NODE_START_Y };
    let y = base.y + step;
    const x = base.x;
    const taken = occupied();
    while (taken.some((p) => Math.abs(p.x - x) < 1 && Math.abs(p.y - y) < step - 1)) y += step;
    const p = { x, y };
    posById.set(node.id, p);
  }

  return nodes.map((n) => {
    if (!newIds.has(n.id)) return n;
    const p = posById.get(n.id)!;
    return { ...n, position: p, data: { ...(n.data as object), _justPlaced: true } };
  });
}
