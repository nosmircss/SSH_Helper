import type { Node } from '@xyflow/react';

export interface Point {
  x: number;
  y: number;
}

/** One branch of a container (e.g. then/else/elif-0/case-1/catch/parallel-0). */
export interface LayoutBranch {
  /** Full branch scope, e.g. 'then', 'else', 'cases/1/do', 'parallel/0'. Distinguishes sibling branches. */
  scope: string;
  /** Left-to-right ordering rank (then < elif* < else; try < catch < finally; cases in order). */
  sortRank: number;
  children: LayoutTreeNode[];
}

export interface LayoutTreeNode {
  id: string;
  node: Node;
  isContainer: boolean;
  /** Non-empty only for containers that have children. */
  branches: LayoutBranch[];
}

export interface LayoutTree {
  /** Top-level sequence, excluding the start node. */
  spine: LayoutTreeNode[];
}
