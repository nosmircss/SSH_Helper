import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { computeHierarchicalLayout } from '../../../utils/layout/hierarchicalLayout';
import { selectCanvasSizing } from '../settingsSlice';

// Lay out a 3-node spine (__start__ -> A -> B) using the SAME sizing the action reflows use,
// so reserve/reclaim comparisons are exact.
function seedChain() {
  const nodes = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
  ];
  const edges = [
    { id: 'e0', source: '__start__', target: 'A' },
    { id: 'e1', source: 'A', target: 'B' },
  ];
  useFlowStore.getState().setNodes(nodes as never);
  useFlowStore.getState().setEdges(edges as never);
  const s0 = useFlowStore.getState();
  s0.setNodes(computeHierarchicalLayout(s0.nodes, s0.edges, selectCanvasSizing(s0)));
}

const commentNode = () => useFlowStore.getState().nodes.find((n) => n.type === 'comment');
const blockY = (id: string) => useFlowStore.getState().nodes.find((n) => n.id === id)!.position.y;
const blockX = (id: string) => useFlowStore.getState().nodes.find((n) => n.id === id)!.position.x;

describe('comment add/delete reflow', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set() });
    vi.clearAllMocks();
  });

  it('addComment (anchored) places the comment above its block in the same column, not the gutter', () => {
    seedChain();
    // Seed position is the deliberate +200 gutter offset BlockContextMenu uses; reflow must override it.
    useFlowStore.getState().addComment({ x: 9999, y: 9999 }, 'A', 'comment');
    const c = commentNode()!;
    expect(c.position.x).toBe(blockX('A'));
    expect(c.position.y).toBeLessThan(blockY('A'));
  });

  it('addComment (anchored) reserves space so the downstream block moves down', () => {
    seedChain();
    const beforeB = blockY('B');
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'A', 'comment');
    expect(blockY('B')).toBeGreaterThan(beforeB);
  });

  it('removeComment reclaims the reserved space (downstream block returns to baseline)', () => {
    seedChain();
    const beforeB = blockY('B');
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'A', 'comment');
    expect(blockY('B')).toBeGreaterThan(beforeB);
    useFlowStore.getState().removeComment(commentNode()!.id);
    expect(blockY('B')).toBe(beforeB);
  });

  it('removeNodes on an anchored comment reclaims space (the right-click "Delete Comment" path)', () => {
    seedChain();
    const beforeB = blockY('B');
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'A', 'comment');
    expect(blockY('B')).toBeGreaterThan(beforeB);
    useFlowStore.getState().removeNodes([commentNode()!.id]);
    expect(blockY('B')).toBe(beforeB);
  });

  it('deleting a block cascades its anchored comment and reflows', () => {
    seedChain();
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'B', 'comment');
    expect(commentNode()).toBeTruthy();
    useFlowStore.getState().removeNodes(['B']);
    expect(commentNode()).toBeUndefined(); // comment cascaded out with its host block
  });

  it('free-floating sticky add does NOT reflow: blocks stay put and the sticky keeps its drop position', () => {
    seedChain();
    const beforeA = blockY('A');
    const beforeB = blockY('B');
    useFlowStore.getState().addComment({ x: 777, y: 555 }); // no attachedToNodeId -> free-floating sticky
    expect(blockY('A')).toBe(beforeA);
    expect(blockY('B')).toBe(beforeB);
    expect(commentNode()!.position).toEqual({ x: 777, y: 555 });
  });

  // Inline-anchored comments (import shape: a trailing `# comment` on a step line) carry an
  // attachedToNodeId but reserve NO vertical space. Deleting one must NOT reflow, or it would snap
  // a hand-arranged graph back to algorithmic positions while reclaiming nothing.
  const seedInline = () => {
    const MANUAL = { x: 1234, y: 5678 };
    const nodes = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'A', type: 'block', position: MANUAL, data: { blockType: 'print', props: { message: 'a' } } },
      { id: 'c1', type: 'comment', position: { x: 50, y: 50 }, data: { commentId: 'c1', text: 'inline', kind: 'comment', attachedToNodeId: 'A', anchor: { type: 'inline' } } },
    ];
    const edges = [{ id: 'e0', source: '__start__', target: 'A' }];
    useFlowStore.getState().setNodes(nodes as never);
    useFlowStore.getState().setEdges(edges as never);
    return MANUAL;
  };
  const posA = () => useFlowStore.getState().nodes.find((n) => n.id === 'A')!.position;

  it('removeComment on an INLINE-anchored comment does NOT reflow (manual position preserved)', () => {
    const manual = seedInline();
    useFlowStore.getState().removeComment('c1');
    expect(posA()).toEqual(manual);
  });

  it('removeNodes on an INLINE-anchored comment does NOT reflow (manual position preserved)', () => {
    const manual = seedInline();
    useFlowStore.getState().removeNodes(['c1']);
    expect(posA()).toEqual(manual);
  });

  it('onNodesChange removing an INLINE-anchored comment does NOT reflow (manual position preserved)', () => {
    const manual = seedInline();
    useFlowStore.getState().onNodesChange([{ type: 'remove', id: 'c1' }]);
    expect(posA()).toEqual(manual);
  });

  it('deleting a block still cascades its INLINE comment out (orphan cleanup), reflow or not', () => {
    seedInline();
    useFlowStore.getState().removeNodes(['A']);
    expect(commentNode()).toBeUndefined(); // inline comment removed with its host, not orphaned
  });
});
