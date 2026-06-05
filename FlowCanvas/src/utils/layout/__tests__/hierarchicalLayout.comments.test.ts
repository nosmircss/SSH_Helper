import { describe, it, expect } from 'vitest';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../hierarchicalLayout';

describe('computeHierarchicalLayout anchored comments', () => {
  const baseNodes = () => [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'b1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', props: { command: 'x' } } },
  ];
  const edges = [{ id: 'e0', source: '__start__', target: 'b1' }];

  it('places an anchored leading comment above its block, not in the right gutter', () => {
    const nodes = [
      ...baseNodes(),
      { id: 'c1', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c1', kind: 'comment', text: 'note', anchor: { type: 'leading', stepPath: 'steps/0' }, attachedToNodeId: 'b1' } },
    ];
    const out = computeHierarchicalLayout(nodes as never, edges as never, DEFAULT_BLOCK_SIZING);
    const b1 = out.find((n) => n.id === 'b1')!;
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position.x).toBe(b1.position.x);          // same column as its block, not the gutter
    expect(c1.position.y).toBeLessThan(b1.position.y);  // directly above it
  });

  it('still gutters a free-floating sticky (no anchor)', () => {
    const nodes = [
      ...baseNodes(),
      { id: 's1', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 's1', kind: 'sticky', text: 'todo' } },
    ];
    const out = computeHierarchicalLayout(nodes as never, edges as never, DEFAULT_BLOCK_SIZING);
    const b1 = out.find((n) => n.id === 'b1')!;
    const s1 = out.find((n) => n.id === 's1')!;
    expect(s1.position.x).toBeGreaterThan(b1.position.x); // unanchored sticky stays in the right gutter
  });
});
