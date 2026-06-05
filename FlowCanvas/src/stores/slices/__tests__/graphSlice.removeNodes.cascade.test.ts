import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import type { Node } from '@xyflow/react';
import { useFlowStore } from '../../useFlowStore';

function block(id: string): Node {
  return { id, type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: {} } } as Node;
}

function comment(id: string, attachedToNodeId: string): Node {
  return {
    id,
    type: 'comment',
    position: { x: 0, y: 0 },
    data: { commentId: id, text: 'hi', kind: 'comment', attachedToNodeId, anchor: { type: 'leading' } },
  } as Node;
}

describe('removeNodes — cascade-delete attached comments', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], selectedNodeIds: new Set(), selectedEdgeIds: new Set() });
    vi.clearAllMocks();
  });

  it('removeNodes removes the block AND its attached comment', () => {
    useFlowStore.getState().setNodes([block('b1'), comment('c1', 'b1')]);
    useFlowStore.getState().removeNodes(['b1']);
    const ids = useFlowStore.getState().nodes.map((n) => n.id);
    expect(ids).not.toContain('b1');
    expect(ids).not.toContain('c1');
  });

  it('removeNodes keeps unrelated comments intact', () => {
    useFlowStore.getState().setNodes([block('b1'), block('b2'), comment('c2', 'b2')]);
    useFlowStore.getState().removeNodes(['b1']);
    const ids = useFlowStore.getState().nodes.map((n) => n.id);
    expect(ids).not.toContain('b1');
    expect(ids).toContain('b2');
    expect(ids).toContain('c2');
  });

  it('removeNodes removes multiple attached comments when multiple are linked to the deleted block', () => {
    useFlowStore.getState().setNodes([block('b1'), comment('c1', 'b1'), comment('c2', 'b1')]);
    useFlowStore.getState().removeNodes(['b1']);
    const ids = useFlowStore.getState().nodes.map((n) => n.id);
    expect(ids).not.toContain('b1');
    expect(ids).not.toContain('c1');
    expect(ids).not.toContain('c2');
  });

  it('onNodesChange remove also cascades to attached comments', () => {
    useFlowStore.getState().setNodes([block('b1'), comment('c1', 'b1')]);
    useFlowStore.getState().onNodesChange([{ type: 'remove', id: 'b1' }]);
    const ids = useFlowStore.getState().nodes.map((n) => n.id);
    expect(ids).not.toContain('b1');
    expect(ids).not.toContain('c1');
  });
});
