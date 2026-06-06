import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { computeBranchBands } from '../branchBands';

const nodes = (): Node[] => ([
  { id: 'IF', type: 'block', position: { x: 250, y: 100 }, data: { blockType: 'if', props: {} } },
  { id: 'C1', type: 'block', position: { x: 470, y: 200 },
    data: { blockType: 'print', props: { _isChildOf: 'IF', _stepPath: 'steps/0/then/0' } } },
] as never);

describe('computeBranchBands childWidth', () => {
  it('band width grows with childWidth', () => {
    const narrow = computeBranchBands(nodes(), 300)[0];
    const wide = computeBranchBands(nodes(), 670)[0];
    expect(wide.width).toBeGreaterThan(narrow.width);
  });
  it('default arg reproduces the 300-wide band', () => {
    const def = computeBranchBands(nodes())[0];
    const explicit = computeBranchBands(nodes(), 300)[0];
    expect(def.width).toBe(explicit.width);
  });
});
