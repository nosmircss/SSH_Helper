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
