import type { Edge, Node } from '@xyflow/react';
import { buildLayoutTree } from './treeBuilder';
import type { LayoutBranch, LayoutTree, LayoutTreeNode, Point } from './types';
import { estimateNodeHeight, COLLAPSED_HEIGHT, BAND_PAD } from '../nodeSize';

/** Single source of truth for layout spacing (ported from FlowCanvasBridge.cs). */
export const LAYOUT = {
  NODE_SPACING_Y: 106,
  SINGLE_BRANCH_CHILD_OFFSET: 70,
  NODE_START_X: 250,
  NODE_START_Y: 40,
  CHILD_NODE_MAX_WIDTH: 300,
  COLUMN_GAP: 30,
  get BASE_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 330
  COLUMN_WIDTH_DECAY: 0.92,
  get MIN_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 330
  MAX_SPREAD_WIDTH: 1400,
  MAX_NESTING_DEPTH: 5,
} as const;

const VERTICAL_GAP = LAYOUT.NODE_SPACING_Y - COLLAPSED_HEIGHT; // 54 — preserves collapsed spacing

function advanceFor(n: LayoutTreeNode): number {
  const data = (n.node?.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
  if (!data.expanded) return LAYOUT.NODE_SPACING_Y;
  return estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true) + VERTICAL_GAP;
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
  return Math.max(LAYOUT.MIN_COLUMN_WIDTH, LAYOUT.BASE_COLUMN_WIDTH * Math.pow(LAYOUT.COLUMN_WIDTH_DECAY, depth));
}

/** Mirrors C# MeasureSteps: column count + row count of a branch subtree, plus `indent` — the
 *  maximum rightward pixel offset that single-branch nesting (loops / single-then ifs, which shift
 *  their body by SINGLE_BRANCH_CHILD_OFFSET each level) adds within the subtree. Multi-branch
 *  containers add columns (not indent); single-branch ones add indent. The branch slot width uses
 *  `indent` so a sibling branch is placed clear of deeply-indented nested content. */
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
      let maxIndent = 0;
      for (const b of branches) {
        const s = measureSteps(b.children);
        totalCols += s.columns;
        maxRows = Math.max(maxRows, s.rows);
        maxIndent = Math.max(maxIndent, s.indent);
      }
      columns = Math.max(columns, Math.max(2, totalCols));
      rows += maxRows;
      indent = Math.max(indent, maxIndent);
    } else {
      for (const b of branches) {
        const s = measureSteps(b.children);
        columns = Math.max(columns, s.columns);
        rows += s.rows;
        indent = Math.max(indent, LAYOUT.SINGLE_BRANCH_CHILD_OFFSET + s.indent);
      }
    }
  }
  return { columns, rows, indent };
}

/** Left-to-left horizontal advance reserved for a branch: its column span + its single-branch
 *  indentation + a lane's padding on both sides, so adjacent lanes (BAND_PAD each) never collide. */
function branchSlotWidth(s: SubtreeSize, colWidth: number): number {
  return s.columns * colWidth + s.indent + 2 * BAND_PAD;
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
    pos.set(child.id, { x: childX, y });
    y += advanceFor(child);
    if (child.isContainer && depth < LAYOUT.MAX_NESTING_DEPTH && nonEmptyBranches(child).length > 0) {
      y = placeContainer(child, depth + 1, centerX, y, pos);
    }
  }
  return y;
}

function placeSingleBranch(branch: LayoutBranch, depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const childX = centerX + LAYOUT.SINGLE_BRANCH_CHILD_OFFSET;
  return placeBranchSteps(branch.children, depth, childX, childX, startY, pos);
}

function placeMultiBranch(branches: LayoutBranch[], depth: number, centerX: number, startY: number, pos: Map<string, Point>): number {
  const sizes = branches.map((b) => measureSteps(b.children));
  const totalColumns = sizes.reduce((sum, s) => sum + s.columns, 0);
  // Fixed extra width per branch (nested indent + lane padding) is independent of colWidth; only
  // the column part scales when clamping to MAX_SPREAD_WIDTH.
  const extra = sizes.reduce((sum, s) => sum + s.indent + 2 * BAND_PAD, 0);
  let colWidth = getColumnWidth(depth);
  if (totalColumns > 0 && totalColumns * colWidth + extra > LAYOUT.MAX_SPREAD_WIDTH) {
    colWidth = Math.max(LAYOUT.CHILD_NODE_MAX_WIDTH, (LAYOUT.MAX_SPREAD_WIDTH - extra) / totalColumns);
  }
  // Anchor the FIRST (primary: then/do/try/case-0) branch's LEFT edge directly under the container
  // and lay the remaining branches out to the right. Each branch reserves its TRUE content width
  // (columns + single-branch nested indent + lane padding), so a sibling never overlaps a branch
  // whose nested bodies are indented far to the right (and the first branch staying at centerX keeps
  // nested bodies aligned under the container — issue #45 import layout).
  let leftX = centerX;
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
  let currentY = LAYOUT.NODE_START_Y + LAYOUT.NODE_SPACING_Y;
  for (const node of tree.spine) {
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
  const gutterX = Math.max(...[...pos.values()].map((p) => p.x)) + LAYOUT.BASE_COLUMN_WIDTH;
  let y = LAYOUT.NODE_START_Y;
  for (const c of comments) {
    pos.set(c.id, { x: gutterX, y });
    y += LAYOUT.NODE_SPACING_Y;
  }
}

/**
 * Structure-aware layout: rebuild the container/branch tree and position it with the
 * smart-hybrid rules. Returns new node objects with updated positions; nodes not in the
 * tree (the start node, orphans the builder left unplaced) keep their position.
 */
export function computeHierarchicalLayout(nodes: Node[], edges: Edge[]): Node[] {
  const tree = buildLayoutTree(nodes, edges);
  const pos = placeTree(tree);
  placeComments(nodes, pos);
  return nodes.map((n) => {
    const p = pos.get(n.id);
    return p ? { ...n, position: p } : n;
  });
}
