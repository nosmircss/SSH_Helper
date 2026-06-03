import { describe, it, expect } from 'vitest';
import { SPINE_WIDTH, CHILD_WIDTH, COLLAPSED_HEIGHT, nodeWidth } from '../nodeSize';

describe('nodeSize', () => {
  it('exposes the new fixed widths', () => {
    expect(SPINE_WIDTH).toBe(330);
    expect(CHILD_WIDTH).toBe(300);
    expect(COLLAPSED_HEIGHT).toBe(52);
  });
  it('nodeWidth picks child vs spine by _isChildOf', () => {
    expect(nodeWidth({ _isChildOf: 'p' })).toBe(300);
    expect(nodeWidth({})).toBe(330);
    expect(nodeWidth(undefined)).toBe(330);
  });
});
