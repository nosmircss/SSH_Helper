import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../hierarchicalLayout';

const chain = (): { nodes: Node[]; edges: Edge[] } => ({
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
  ] as never,
  edges: [
    { id: 'e0', source: '__start__', target: 'A' },
    { id: 'e1', source: 'A', target: 'B' },
  ] as never,
});

describe('computeHierarchicalLayout sizing param', () => {
  it('default param reproduces todays positions (regression guard)', () => {
    const { nodes, edges } = chain();
    const withParam = computeHierarchicalLayout(nodes, edges, DEFAULT_BLOCK_SIZING);
    const without = computeHierarchicalLayout(nodes, edges);
    expect(withParam.map((n) => n.position)).toEqual(without.map((n) => n.position));
  });

  it('roomy density pushes a downstream block further down', () => {
    const { nodes, edges } = chain();
    const normal = computeHierarchicalLayout(nodes, edges, { blockWidth: 330, density: 1, textScale: 1 });
    const roomy = computeHierarchicalLayout(nodes, edges, { blockWidth: 330, density: 1.2, textScale: 1 });
    const yN = normal.find((n) => n.id === 'B')!.position.y;
    const yR = roomy.find((n) => n.id === 'B')!.position.y;
    expect(yR).toBeGreaterThan(yN);
  });
});
