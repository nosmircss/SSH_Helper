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
    expect(pos.get('body1')!.x).toBe(LAYOUT.NODE_START_X + LAYOUT.BRANCH_CHILD_OFFSET);
    expect(pos.get('body2')!.y).toBeGreaterThan(pos.get('body1')!.y);
  });
});

import { buildLayoutTree } from '../treeBuilder';
import { computeHierarchicalLayout } from '../hierarchicalLayout';

describe('placeTree — multi-branch (fans into side-by-side columns)', () => {
  it('indents every branch right of the spine, opening a clear continuation gutter', () => {
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
    // Like single-branch containers, the FIRST branch is indented right of the container so the
    // continuation runs straight down the spine gutter on the left, clear of the band (Option B —
    // unifies multi- and single-branch routing; the former "anchored under the container" choice
    // sent the continuation down the bottom-left corridor that escaped the band).
    expect(pos.get('t1')!.x).toBeCloseTo(LAYOUT.NODE_START_X + LAYOUT.BRANCH_CHILD_OFFSET, 5);
    expect(pos.get('e1')!.x).toBeGreaterThan(pos.get('t1')!.x);
    // The band's left wall lands right of the straight spine continuation (no overlap / no escape).
    expect(pos.get('t1')!.x - BAND_PAD).toBeGreaterThan(LAYOUT.NODE_START_X + SPINE_WIDTH / 2);
  });

  it('keeps a multi-branch container nested in a single-branch container right of the spine (issue #45: never left of it)', () => {
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

    const loopBodyX = LAYOUT.NODE_START_X + LAYOUT.BRANCH_CHILD_OFFSET; // where the if sits
    // The if itself sits in the loop body's column…
    expect(pos.get('if')!.x).toBeCloseTo(loopBodyX, 5);
    // …and its own branches indent one level further right (never left of the loop body — #45).
    expect(pos.get('t1')!.x).toBeCloseTo(loopBodyX + LAYOUT.BRANCH_CHILD_OFFSET, 5);
    expect(pos.get('t1')!.x).toBeGreaterThanOrEqual(loopBodyX);
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

describe('column width tracks child width', () => {
  it('multi-branch columns are at least CHILD_NODE_MAX_WIDTH + COLUMN_GAP apart', () => {
    expect(LAYOUT.CHILD_NODE_MAX_WIDTH).toBe(300);
    expect(LAYOUT.MIN_COLUMN_WIDTH).toBe(330);
  });
});

describe('placeTree — height-aware vertical spacing', () => {
  it('advances further past an expanded node than a collapsed one', () => {
    const exp: LayoutTreeNode = { id: 'x', node: { id: 'x', data: { blockType: 'send', expanded: true, props: { command: 'a', capture: 'b' } } } as never, isContainer: false, branches: [] };
    const after = leaf('after');
    const pos = placeTree({ spine: [exp, after] });
    expect(pos.get('after')!.y - pos.get('x')!.y).toBeGreaterThan(LAYOUT.NODE_SPACING_Y);
  });
});

import { BAND_PAD, SPINE_WIDTH } from '../../nodeSize';

describe('placeTree — single-branch continuation clears its branch band (regression: wire outside band)', () => {
  it('indents a single THEN branch far enough that the band starts right of the IF continuation wire', () => {
    // then-only IF: the continuation leaves the IF's bottom-CENTER and runs straight down the
    // spine. The THEN band's left wall is childX - BAND_PAD. If the child indents too little, the
    // band overlaps the spine and the straight wire renders inside/across it (the reported bug).
    const ifNode: LayoutTreeNode = {
      id: 'if', node: { id: 'if' } as never, isContainer: true,
      branches: [{ scope: 'then', sortRank: 0, children: [leaf('t1')] }],
    };
    const pos = placeTree({ spine: [ifNode] });
    const continuationX = pos.get('if')!.x + SPINE_WIDTH / 2; // bottom-center handle
    const bandLeft = pos.get('t1')!.x - BAND_PAD;
    expect(bandLeft).toBeGreaterThan(continuationX);
  });
});

describe('placeTree — branch lanes reserve room for nested indentation', () => {
  it('places a sibling branch clear of a branch whose content is indented by nested containers', () => {
    // outer IF: then = [ loop{ do: [body] } ] (single-branch nesting indents body to the right), else = [e]
    const nestedLoop: LayoutTreeNode = {
      id: 'loop', node: { id: 'loop' } as never, isContainer: true,
      branches: [{ scope: 'do', sortRank: 0, children: [leaf('body')] }],
    };
    const ifNode: LayoutTreeNode = {
      id: 'if', node: { id: 'if' } as never, isContainer: true,
      branches: [
        { scope: 'then', sortRank: 0, children: [nestedLoop] },
        { scope: 'else', sortRank: 2000, children: [leaf('e')] },
      ],
    };
    const pos = placeTree({ spine: [ifNode] });
    const thenSubtreeRight = pos.get('body')!.x + LAYOUT.CHILD_NODE_MAX_WIDTH; // nested body's right edge
    const elseLeft = pos.get('e')!.x;
    // The else lane must begin clear of the then subtree's right edge plus both lanes' padding.
    expect(elseLeft).toBeGreaterThanOrEqual(thenSubtreeRight + 2 * BAND_PAD);
  });
});
