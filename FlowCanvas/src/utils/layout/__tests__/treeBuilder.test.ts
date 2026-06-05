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

// Canvas-built if/else: structure lives on edges (branchPath), no _isChildOf metadata.
function canvasIfElseGraph(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: {} } } as Node,
    { id: 'then-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    { id: 'else-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' } as Edge,
    { id: 'e1', source: 'if-1', target: 'then-1', data: { branchPath: 'then' } } as Edge,
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false', data: { branchPath: 'else' } } as Edge,
  ];
  return { nodes, edges };
}

describe('buildLayoutTree (edge fallback + robustness)', () => {
  it('reconstructs branches from edges when metadata is absent', () => {
    const { nodes, edges } = canvasIfElseGraph();
    const tree = buildLayoutTree(nodes, edges);
    const ifNode = tree.spine.find((n) => n.id === 'if-1')!;
    expect(ifNode.branches.map((b) => b.scope)).toEqual(['then', 'else']);
    expect(tree.spine.map((n) => n.id)).toEqual(['if-1']); // children not on spine
  });

  it('orders the spine by following edges from the start node', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'b', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/1' } } } as Node,
      { id: 'a', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { _stepPath: 'steps/0' } } } as Node,
    ];
    const edges: Edge[] = [
      { id: 'e0', source: '__start__', target: 'a' } as Edge,
      { id: 'e1', source: 'a', target: 'b' } as Edge,
    ];
    expect(buildLayoutTree(nodes, edges).spine.map((n) => n.id)).toEqual(['a', 'b']);
  });

  it('does not loop forever on a while back-edge', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'w', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'while', props: {} } } as Node,
      { id: 'body', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    ];
    const edges: Edge[] = [
      { id: 'e0', source: '__start__', target: 'w' } as Edge,
      { id: 'e1', source: 'w', target: 'body', data: { branchPath: 'do' } } as Edge,
      { id: 'e2', source: 'body', target: 'w' } as Edge, // back-edge
    ];
    const tree = buildLayoutTree(nodes, edges);
    expect(tree.spine.map((n) => n.id)).toEqual(['w']);
    expect(tree.spine[0].branches[0].children.map((c) => c.id)).toEqual(['body']);
  });

  it('keeps orphan (disconnected) nodes on the spine by default', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'orphan', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    ];
    expect(buildLayoutTree(nodes, []).spine.map((n) => n.id)).toEqual(['orphan']);
  });

  it('with keepOrphans, leaves disconnected nodes OFF the spine (so the layout keeps their position)', () => {
    const nodes: Node[] = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start' } } as Node,
      { id: 'orphan', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node,
    ];
    expect(buildLayoutTree(nodes, [], true).spine.map((n) => n.id)).toEqual([]);
  });
});
