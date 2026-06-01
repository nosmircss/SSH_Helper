import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { buildLayoutTree } from '../treeBuilder';

// Imported if/else: container at steps/0 with one then-child and one else-child.
function ifElseGraph(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _stepPath: 'steps/0' } } } as Node,
    { id: 'then-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } } as Node,
    { id: 'else-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } } as Node,
    { id: 'tail-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/1' } } } as Node,
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' } as Edge,
    { id: 'e1', source: 'if-1', target: 'then-1' } as Edge,
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false' } as Edge,
    { id: 'e3', source: 'if-1', target: 'tail-1', sourceHandle: 'continue' } as Edge,
  ];
  return { nodes, edges };
}

describe('buildLayoutTree (metadata)', () => {
  it('puts top-level steps on the spine, excluding the start node', () => {
    const { nodes, edges } = ifElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    expect(tree.spine.map((n) => n.id)).toEqual(['if-1', 'tail-1']);
  });

  it('attaches then/else as separate, correctly ordered branches of the container', () => {
    const { nodes, edges } = ifElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    const ifNode = tree.spine.find((n) => n.id === 'if-1')!;
    expect(ifNode.isContainer).toBe(true);
    expect(ifNode.branches.map((b) => b.scope)).toEqual(['then', 'else']);
    expect(ifNode.branches[0].children.map((c) => c.id)).toEqual(['then-1']);
    expect(ifNode.branches[1].children.map((c) => c.id)).toEqual(['else-1']);
  });

  it('does not put branch children on the spine', () => {
    const { nodes, edges } = ifElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    expect(tree.spine.map((n) => n.id)).not.toContain('then-1');
    expect(tree.spine.map((n) => n.id)).not.toContain('else-1');
  });
});
