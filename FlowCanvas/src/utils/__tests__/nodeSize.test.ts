import { describe, it, expect } from 'vitest';
import {
  SPINE_WIDTH, CHILD_WIDTH, COLLAPSED_HEIGHT, nodeWidth, estimateNodeHeight, BLOCK_WIDTH_INSET,
  SUMMARY_ROW_H, SUMMARY_ROW_WRAP_H,
} from '../nodeSize';

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

describe('estimateNodeHeight textScale', () => {
  it('keeps the 52px collapsed floor at and below 1× (icon chip floors the header)', () => {
    expect(estimateNodeHeight('print', { message: 'x' }, false, 0.9)).toBe(52);
    expect(estimateNodeHeight('print', { message: 'x' }, false, 1)).toBe(52);
  });
  it('grows the collapsed estimate by the ~30px text slice above 1×', () => {
    // Only the two text lines scale; paddings/borders/icon chip are fixed.
    expect(estimateNodeHeight('print', { message: 'x' }, false, 1.15)).toBe(52 + 4);  // L (30×0.1499… rounds down)
    expect(estimateNodeHeight('print', { message: 'x' }, false, 1.35)).toBe(52 + 11); // XL
    expect(estimateNodeHeight('print', { message: 'x' }, false, 1.6)).toBe(52 + 18);  // XXL
    expect(estimateNodeHeight('print', { message: 'x' }, false, 2.5)).toBe(52 + 45);  // slider max
  });
  it('expanded height grows with textScale', () => {
    const base = estimateNodeHeight('send', { command: 'a', capture: 'b' }, true, 1);
    const big = estimateNodeHeight('send', { command: 'a', capture: 'b' }, true, 1.15);
    expect(big).toBeGreaterThan(base);
  });
  it('counts a wrapped (long-label) row taller than a single-line row', () => {
    // "Callback Path" (13 chars) fits the label column; "Completion Message" (18) wraps to two
    // lines and the row really renders taller — the estimate must see that or blocks overlap.
    const short = estimateNodeHeight('browser_callback', { callback_path: '/cb' }, true, 1);
    const long = estimateNodeHeight('browser_callback', { completion_message: 'done' }, true, 1);
    expect(long - short).toBe(SUMMARY_ROW_WRAP_H - SUMMARY_ROW_H);
  });
});

describe('BLOCK_WIDTH_INSET', () => {
  it('is the spine-minus-child delta', () => {
    expect(BLOCK_WIDTH_INSET).toBe(SPINE_WIDTH - CHILD_WIDTH); // 30
  });
});
