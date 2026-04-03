import type { Edge, Node } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';

export interface CommentData {
  id: string;
  text: string;
  color: string;
  x: number;
  y: number;
  width: number;
  height: number;
  attachedToNodeId?: string;
}

export interface ExecutableGraphPayload {
  nodes: Node[];
  edges: Edge[];
  comments: CommentData[];
  disabledBlocks: string[];
}

/**
 * Splits nodes into executable graph content and visual-only data (comments, disabled state).
 * The host receives executable nodes for YAML export, plus layout data for persistence.
 */
export function buildExecutableGraphPayload(
  nodes: Node[],
  edges: Edge[],
): ExecutableGraphPayload {
  const exportNodes: Node[] = [];
  const comments: CommentData[] = [];

  for (const n of nodes) {
    const data = n.data as Record<string, unknown> | undefined;
    const blockTypeValue = data?.blockType;
    const blockType = typeof blockTypeValue === 'string' ? blockTypeValue : '';
    const isCommentNode = n.type === 'comment' || blockType === 'comment';

    if (isCommentNode) {
      comments.push({
        id: n.id,
        text: String(data?.text ?? ''),
        color: String(data?.color ?? '#e0c040'),
        x: n.position?.x ?? 0,
        y: n.position?.y ?? 0,
        width: (n.style?.width as number) ?? 200,
        height: (n.style?.height as number) ?? 100,
        attachedToNodeId: data?.attachedToNodeId ? String(data.attachedToNodeId) : undefined,
      });
    } else {
      exportNodes.push(n);
    }
  }

  const disabledBlocks = Array.from(useFlowStore.getState().disabledBlocks);

  return { nodes: exportNodes, edges, comments, disabledBlocks };
}
