import { describe, it, expect } from 'vitest';
import { SPINE_WIDTH, CHILD_WIDTH, COLLAPSED_HEIGHT, nodeWidth, estimateNodeHeight } from '../nodeSize';

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
  it('estimates collapsed vs expanded height', () => {
    const collapsed = estimateNodeHeight('send', { command: 'x' }, false);
    const expanded = estimateNodeHeight('send', { command: 'x', capture: 'y' }, true);
    expect(collapsed).toBe(52);
    expect(expanded).toBeGreaterThan(collapsed); // header + 2 rows + footer
  });
});
