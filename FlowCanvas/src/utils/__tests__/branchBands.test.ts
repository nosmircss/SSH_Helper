// FlowCanvas/src/utils/__tests__/branchBands.test.ts
import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { computeBranchBands, branchPillLabel, BAND_PAD } from '../branchBands';

function child(id: string, parent: string, stepPath: string, x: number, y: number): Node {
  return { id, position: { x, y }, data: { props: { _isChildOf: parent, _stepPath: stepPath } } } as Node;
}

describe('branchBands', () => {
  it('uses 18px padding', () => { expect(BAND_PAD).toBe(18); });

  it('maps branch keys to human pill labels', () => {
    expect(branchPillLabel('then')).toBe('THEN');
    expect(branchPillLabel('else')).toBe('ELSE');
    expect(branchPillLabel('do')).toBe('LOOP');
    expect(branchPillLabel('case')).toBe('CASE');
    expect(branchPillLabel('elif')).toBe('ELIF');
  });

  it('wraps a 300px child with 18px padding on each side', () => {
    const bands = computeBranchBands([child('c1', 'p', 'steps/1/then/0', 100, 200)]);
    expect(bands).toHaveLength(1);
    const b = bands[0];
    expect(b.x).toBe(100 - 18);
    expect(b.width).toBe(300 + 18 * 2); // child width 300 + pad both sides
    expect(b.branchKey).toBe('then');
  });

  it('insets a nested band on the LEFT only, preserving top/right/bottom padding', () => {
    // A nested band that shares its parent's left edge (e.g. a multi-branch then under a loop)
    // would paint over the parent's left accent — hiding the nesting (yellow→green). Inset the
    // LEFT of depth>=1 bands so the parent shows through, but keep full top/right/bottom padding
    // so the pill label clears the first block and blocks aren't crowded at the bottom.
    const outer = computeBranchBands([child('c', 'p', 'steps/0/then/0', 100, 100)])[0];
    expect(outer.x).toBe(100 - 18); // depth 0: flush, no inset
    const nested = computeBranchBands([child('g', 'q', 'steps/0/then/0/else/0', 100, 100)])[0];
    expect(nested.depth).toBeGreaterThanOrEqual(1);
    expect(nested.x).toBeGreaterThan(100 - 18);           // left pulled inward (reveals parent accent)
    expect(nested.y).toBe(100 - 18);                      // top NOT inset (pill clears the first block)
    expect(nested.x + nested.width).toBe(100 + 300 + 18); // right NOT inset (full padding)
    expect(nested.y + nested.height).toBe(100 + 52 + 18); // bottom NOT inset (full padding)
  });

  it('marks the outermost branch depth 0', () => {
    const b = computeBranchBands([child('c1', 'p', 'steps/0/then/0', 0, 0)])[0];
    expect(b.depth).toBe(0);
  });
  it('marks a branch nested inside another branch as depth >= 1', () => {
    const b = computeBranchBands([child('c2', 'q', 'steps/0/then/0/else/0', 0, 0)])[0];
    expect(b.depth).toBeGreaterThanOrEqual(1);
  });

  it('grows the band for an expanded child', () => {
    const collapsed = computeBranchBands([child('c', 'p', 'steps/1/then/0', 100, 200)])[0];
    const expNode = child('c', 'p', 'steps/1/then/0', 100, 200);
    (expNode.data as any).blockType = 'send';
    (expNode.data as any).expanded = true;
    (expNode.data as any).props = { command: 'a', capture: 'b', _isChildOf: 'p', _stepPath: 'steps/1/then/0' };
    const expanded = computeBranchBands([expNode])[0];
    expect(expanded.height).toBeGreaterThan(collapsed.height);
  });

  it('wraps the whole branch subtree, including nested-branch bodies indented to the right', () => {
    // outer THEN: a direct child + a nested IF (direct child) whose THEN body is indented right.
    const direct = child('d', 'p', 'steps/0/then/0', 100, 100);
    const nestedIf = child('nif', 'p', 'steps/0/then/1', 100, 200);
    const nestedBody = child('g', 'nif', 'steps/0/then/1/then/0', 280, 300); // indented +180 right
    const bands = computeBranchBands([direct, nestedIf, nestedBody]);
    const outer = bands.find((b) => b.id === 'p::then')!;
    // Outer lane right edge must cover the indented nested body (280 + 300 child width + 18 pad).
    expect(outer.x + outer.width).toBeGreaterThanOrEqual(280 + 300 + 18);
    // The nested lane itself stays tight around just its own body.
    const nested = bands.find((b) => b.id === 'nif::then')!;
    expect(nested.x + nested.width).toBeLessThan(outer.x + outer.width + 1);
  });

  it('groups all switch cases into one lane that spans every case body', () => {
    const c0 = child('a', 'sw', 'steps/0/cases/0/0', 100, 100);
    const c1 = child('b', 'sw', 'steps/0/cases/1/0', 500, 200); // a later case, further right
    const bands = computeBranchBands([c0, c1]);
    const caseBand = bands.find((b) => b.branchKey === 'case')!;
    expect(caseBand.x + caseBand.width).toBeGreaterThanOrEqual(500 + 300 + 18);
  });
});
