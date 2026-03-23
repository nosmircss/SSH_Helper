import type { Edge, Node } from '@xyflow/react';

export interface ExecutableGraphPayload {
  nodes: Node[];
  edges: Edge[];
}

/**
 * Filters visual-only nodes so the host receives only executable graph content.
 */
export function buildExecutableGraphPayload(
  nodes: Node[],
  edges: Edge[],
): ExecutableGraphPayload {
  const exportNodes = nodes.filter((n) => {
    const data = n.data as Record<string, unknown> | undefined;
    const blockTypeValue = data?.blockType;
    const blockType = typeof blockTypeValue === 'string' ? blockTypeValue : '';
    const isCommentNode = n.type === 'comment' || blockType === 'comment';
    return !isCommentNode;
  });

  return { nodes: exportNodes, edges };
}
