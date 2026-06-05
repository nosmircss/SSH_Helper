import { describe, it, expect } from 'vitest';
import type { Node, Edge } from '@xyflow/react';
import { placeNewBlocksNearNeighbors, DEFAULT_BLOCK_SIZING } from '../hierarchicalLayout';

const blk = (id: string, x: number, y: number): Node => ({
  id, type: 'block', position: { x, y }, data: { blockType: 'print', props: {} },
});

describe('placeNewBlocksNearNeighbors', () => {
  it('places a new block just below its predecessor and leaves existing blocks untouched', () => {
    const nodes = [blk('A', 100, 100), blk('B', 100, 999)];
    const edges: Edge[] = [{ id: 'e', source: 'A', target: 'B' }];

    const out = placeNewBlocksNearNeighbors(nodes, edges, new Set(['B']), DEFAULT_BLOCK_SIZING);

    const a = out.find((n) => n.id === 'A')!;
    const b = out.find((n) => n.id === 'B')!;
    expect(a.position).toEqual({ x: 100, y: 100 });
    expect(b.position.x).toBe(100);
    expect(b.position.y).toBeGreaterThan(a.position.y);
  });

  it('nudges a new block off an existing block at the same spot', () => {
    const nodes = [blk('A', 100, 100), blk('C', 100, 100), blk('B', 0, 0)];
    const edges: Edge[] = [{ id: 'e1', source: 'A', target: 'B' }];
    const out = placeNewBlocksNearNeighbors(nodes, edges, new Set(['B']), DEFAULT_BLOCK_SIZING);
    const b = out.find((n) => n.id === 'B')!;
    const c = out.find((n) => n.id === 'C')!;
    expect(Math.abs(b.position.y - c.position.y) + Math.abs(b.position.x - c.position.x)).toBeGreaterThan(0);
  });

  it('tags placed new blocks so the UI can highlight them', () => {
    const nodes = [blk('A', 100, 100), blk('B', 0, 0)];
    const edges: Edge[] = [{ id: 'e', source: 'A', target: 'B' }];
    const out = placeNewBlocksNearNeighbors(nodes, edges, new Set(['B']), DEFAULT_BLOCK_SIZING);
    const b = out.find((n) => n.id === 'B')!;
    expect((b.data as Record<string, unknown>)._justPlaced).toBe(true);
  });

  it('falls back to the start position for a new block with no predecessor', () => {
    const nodes = [blk('Z', 0, 0)]; // no incoming edge
    const out = placeNewBlocksNearNeighbors(nodes, [], new Set(['Z']), DEFAULT_BLOCK_SIZING);
    const z = out.find((n) => n.id === 'Z')!;
    expect(z.position.x).toBe(250);   // LAYOUT.NODE_START_X
    expect(z.position.y).toBe(40 + Math.round(106 * 1)); // NODE_START_Y + one step at density 1
  });
});
