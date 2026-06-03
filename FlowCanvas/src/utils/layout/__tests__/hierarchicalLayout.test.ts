import { describe, it, expect } from 'vitest';
import type { LayoutTree, LayoutTreeNode } from '../types';
import { placeTree, LAYOUT } from '../hierarchicalLayout';

const leaf = (id: string): LayoutTreeNode => ({ id, node: { id } as never, isContainer: false, branches: [] });

describe('placeTree — spine', () => {
  it('stacks spine nodes vertically at the spine X, spaced by NodeSpacingY', () => {
    const tree: LayoutTree = { spine: [leaf('a'), leaf('b'), leaf('c')] };
    const pos = placeTree(tree);
    expect(pos.get('a')!.x).toBe(LAYOUT.NODE_START_X);
    expect(pos.get('b')!.x).toBe(LAYOUT.NODE_START_X);
    expect(pos.get('b')!.y - pos.get('a')!.y).toBe(LAYOUT.NODE_SPACING_Y);
    expect(pos.get('c')!.y - pos.get('b')!.y).toBe(LAYOUT.NODE_SPACING_Y);
  });
});

describe('placeTree — single-branch container (loop indents right)', () => {
  it('indents the loop body to the right of the container center', () => {
    const loop: LayoutTreeNode = {
      id: 'loop', node: { id: 'loop' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [leaf('body1'), leaf('body2')] }],
    };
    const pos = placeTree({ spine: [loop] });
    expect(pos.get('body1')!.x).toBe(LAYOUT.NODE_START_X + LAYOUT.SINGLE_BRANCH_CHILD_OFFSET);
    expect(pos.get('body2')!.y).toBeGreaterThan(pos.get('body1')!.y);
  });
});

import { buildLayoutTree } from '../treeBuilder';
import { computeHierarchicalLayout } from '../hierarchicalLayout';

describe('placeTree — multi-branch (fans into side-by-side columns)', () => {
  it('anchors the first branch under the container and spreads the rest to the right', () => {
    const ifNode: LayoutTreeNode = {
      id: 'if', node: { id: 'if' } as never, isContainer: true,
      branches: [
        { scope: 'then', sortRank: 0, children: [leaf('t1')] },
        { scope: 'else', sortRank: 2000, children: [leaf('e1')] },
      ],
    };
    const pos = placeTree({ spine: [ifNode] });
    // Both branch heads share the same Y (sibling columns start level).
    expect(pos.get('t1')!.y).toBe(pos.get('e1')!.y);
    // then is left of else.
    expect(pos.get('t1')!.x).toBeLessThan(pos.get('e1')!.x);
    // The primary (then) branch stays directly under the container's X; else is offset right.
    // This keeps a multi-branch container nested inside another container from shoving its body
    // left of the parent's column (issue #45 import layout).
    expect(pos.get('t1')!.x).toBeCloseTo(LAYOUT.NODE_START_X, 5);
    expect(pos.get('e1')!.x).toBeGreaterThan(LAYOUT.NODE_START_X);
  });

  it('keeps a multi-branch container nested in a single-branch container aligned under it (issue #45)', () => {
    // foreach { do: [ if { then: [t], else: [e] } ] }
    const inner: LayoutTreeNode = {
      id: 'if', node: { id: 'if' } as never, isContainer: true,
      branches: [
        { scope: 'then', sortRank: 0, children: [leaf('t1'), leaf('t2')] },
        { scope: 'else', sortRank: 2000, children: [leaf('e1')] },
      ],
    };
    const loop: LayoutTreeNode = {
      id: 'loop', node: { id: 'loop' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [inner] }],
    };
    const pos = placeTree({ spine: [loop] });

    const loopBodyX = LAYOUT.NODE_START_X + LAYOUT.SINGLE_BRANCH_CHILD_OFFSET; // where the if sits
    // The if's primary (then) branch stays in the loop body's column, NOT shoved left of it.
    expect(pos.get('if')!.x).toBeCloseTo(loopBodyX, 5);
    expect(pos.get('t1')!.x).toBeCloseTo(loopBodyX, 5);
    expect(pos.get('t1')!.x).toBeGreaterThanOrEqual(LAYOUT.NODE_START_X); // never left of the spine
    // else spreads to the right of the then column.
    expect(pos.get('e1')!.x).toBeGreaterThan(pos.get('t1')!.x);
  });

  it('recurses into a nested container inside a branch', () => {
    const inner: LayoutTreeNode = {
      id: 'inner', node: { id: 'inner' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [leaf('deep')] }],
    };
    const outer: LayoutTreeNode = {
      id: 'outer', node: { id: 'outer' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [inner] }],
    };
    const pos = placeTree({ spine: [outer] });
    expect(pos.get('deep')).toBeDefined();
    expect(pos.get('deep')!.y).toBeGreaterThan(pos.get('inner')!.y);
  });
});

import type { Edge, Node } from '@xyflow/react';

function importedIfElse(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 999, y: 999 }, data: { blockType: '_start' } } as Node,
    { id: 'if-1', type: 'block', position: { x: 7, y: 7 }, data: { blockType: 'if', props: { _stepPath: 'steps/0' } } } as Node,
    { id: 'then-1', type: 'block', position: { x: 7, y: 7 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } } as Node,
    { id: 'else-1', type: 'block', position: { x: 7, y: 7 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } } as Node,
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' } as Edge,
    { id: 'e1', source: 'if-1', target: 'then-1' } as Edge,
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false' } as Edge,
  ];
  return { nodes, edges };
}

describe('computeHierarchicalLayout', () => {
  it('repositions layoutable nodes and leaves the start node untouched', () => {
    const { nodes, edges } = importedIfElse();
    const out = computeHierarchicalLayout(nodes, edges);
    const start = out.find((n) => n.id === '__start__')!;
    expect(start.position).toEqual({ x: 999, y: 999 }); // start node not in spine, untouched
    const ifNode = out.find((n) => n.id === 'if-1')!;
    expect(ifNode.position.x).toBe(LAYOUT.NODE_START_X);
  });

  it('produces no overlapping branch children', () => {
    const { nodes, edges } = importedIfElse();
    const out = computeHierarchicalLayout(nodes, edges);
    const t = out.find((n) => n.id === 'then-1')!.position;
    const e = out.find((n) => n.id === 'else-1')!.position;
    // Different columns, far enough apart to not overlap (>= one node width).
    expect(Math.abs(t.x - e.x)).toBeGreaterThanOrEqual(LAYOUT.CHILD_NODE_MAX_WIDTH);
  });
});
