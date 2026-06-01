import type { Edge, Node } from '@xyflow/react';
import { buildLayoutTree } from './treeBuilder';
import type { LayoutBranch, LayoutTree, LayoutTreeNode, Point } from './types';

/** Single source of truth for layout spacing (ported from FlowCanvasBridge.cs). */
export const LAYOUT = {
  NODE_SPACING_Y: 106,
  SINGLE_BRANCH_CHILD_OFFSET: 70,
  NODE_START_X: 250,
  NODE_START_Y: 40,
  CHILD_NODE_MAX_WIDTH: 260,
  COLUMN_GAP: 30,
  get BASE_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 290
  COLUMN_WIDTH_DECAY: 0.92,
  get MIN_COLUMN_WIDTH() { return this.CHILD_NODE_MAX_WIDTH + this.COLUMN_GAP; }, // 290
  MAX_SPREAD_WIDTH: 1400,
  MAX_NESTING_DEPTH: 5,
} as const;

interface SubtreeSize { columns: number; rows: number; }

function nonEmptyBranches(node: LayoutTreeNode): LayoutBranch[] {
  return node.branches.filter((b) => b.children.length > 0);
}

function getColumnWidth(depth: number): number {
  return Math.max(LAYOUT.MIN_COLUMN_WIDTH, LAYOUT.BASE_COLUMN_WIDTH * Math.pow(LAYOUT.COLUMN_WIDTH_DECAY, depth));
}

/** Mirrors C# MeasureSteps: column count + row count of a branch subtree. */
function measureSteps(children: LayoutTreeNode[]): SubtreeSize {
  let columns = 1;
  let rows = 0;
  for (const child of children) {
    rows += 1;
    if (!child.isContainer) continue;
    const branches = nonEmptyBranches(child);
    if (branches.length >= 2) {
      let totalCols = 0;
      let maxRows = 0;
      for (const b of branches) {
        const s = measureSteps(b.children);
        totalCols += s.columns;
        maxRows = Math.max(maxRows, s.rows);
      }
      columns = Math.max(columns, Math.max(2, totalCols));
      rows += maxRows;
    } else {
      for (const b of branches) {
        const s = measureSteps(b.children);
        columns = Math.max(columns, s.columns);
        rows += s.rows;
      }
    }
  }
  return { columns, rows };
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
    y += LAYOUT.NODE_SPACING_Y;
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
  let colWidth = getColumnWidth(depth);
  let totalPixelWidth = totalColumns * colWidth;
  if (totalPixelWidth > LAYOUT.MAX_SPREAD_WIDTH) {
    colWidth = LAYOUT.MAX_SPREAD_WIDTH / totalColumns;
    totalPixelWidth = LAYOUT.MAX_SPREAD_WIDTH;
  }
  let leftEdge = centerX - totalPixelWidth / 2;
  let maxEndY = startY;
  for (let i = 0; i < branches.length; i++) {
    const branchPixelWidth = sizes[i].columns * colWidth;
    const branchCenterX = leftEdge + branchPixelWidth / 2;
    const endY = placeBranchSteps(branches[i].children, depth, branchCenterX, branchCenterX, startY, pos);
    maxEndY = Math.max(maxEndY, endY);
    leftEdge += branchPixelWidth;
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
    currentY += LAYOUT.NODE_SPACING_Y;
    if (node.isContainer && nonEmptyBranches(node).length > 0) {
      currentY = placeContainer(node, 1, LAYOUT.NODE_START_X, currentY, pos);
    }
  }
  return pos;
}
