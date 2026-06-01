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
  it('places then/else at the same start Y in distinct X columns, centered on the container', () => {
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
    // The two columns are centered on the container's X.
    const mid = (pos.get('t1')!.x + pos.get('e1')!.x) / 2;
    expect(mid).toBeCloseTo(LAYOUT.NODE_START_X, 5);
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
