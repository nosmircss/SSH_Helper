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
});
