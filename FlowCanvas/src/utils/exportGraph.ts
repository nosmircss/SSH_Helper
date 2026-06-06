import type { Edge, Node } from '@xyflow/react';
import { blockDefMap } from '../blockDefs/registry';
import { useFlowStore } from '../stores/useFlowStore';
import { DEFAULT_COMMENT_COLOR } from './tokens';
import type { NoteAnchor } from '../nodes/CommentNode';

export interface CommentData {
  id: string;
  text: string;
  color: string;
  x: number;
  y: number;
  width: number;
  height: number;
  attachedToNodeId?: string;
  kind?: string;
  anchor?: NoteAnchor;
}

export interface ExecutableGraphPayload {
  nodes: Node[];
  edges: Edge[];
  comments: CommentData[];
  disabledBlocks: string[];
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function areEquivalentValues(left: unknown, right: unknown): boolean {
  if (left === right) {
    return true;
  }

  if (typeof left === 'number' && typeof right === 'number') {
    return Number.isNaN(left) && Number.isNaN(right);
  }

  if (Array.isArray(left) && Array.isArray(right)) {
    if (left.length !== right.length) {
      return false;
    }

    return left.every((item, index) => areEquivalentValues(item, right[index]));
  }

  if (isPlainObject(left) && isPlainObject(right)) {
    const leftKeys = Object.keys(left);
    const rightKeys = Object.keys(right);
    if (leftKeys.length !== rightKeys.length) {
      return false;
    }

    return leftKeys.every((key) =>
      Object.prototype.hasOwnProperty.call(right, key) && areEquivalentValues(left[key], right[key]),
    );
  }

  return false;
}

function stripDefaultProps(node: Node): Node {
  const data = isPlainObject(node.data) ? node.data : undefined;
  const blockType = typeof data?.blockType === 'string' ? data.blockType : '';
  const props = isPlainObject(data?.props) ? data.props : undefined;
  const def = blockDefMap.get(blockType);

  if (!def || !props) {
    return node;
  }

  let changed = false;
  const nextProps: Record<string, unknown> = { ...props };

  for (const propDef of def.properties) {
    if (propDef.defaultValue === undefined) {
      continue;
    }

    if (!Object.prototype.hasOwnProperty.call(nextProps, propDef.key)) {
      continue;
    }

    if (!areEquivalentValues(nextProps[propDef.key], propDef.defaultValue)) {
      continue;
    }

    delete nextProps[propDef.key];
    changed = true;
  }

  if (!changed) {
    return node;
  }

  return {
    ...node,
    data: {
      ...data,
      props: nextProps,
    },
  };
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
        color: String(data?.color ?? DEFAULT_COMMENT_COLOR),
        x: n.position?.x ?? 0,
        y: n.position?.y ?? 0,
        width: (n.style?.width as number) ?? 200,
        height: (n.style?.height as number) ?? 100,
        attachedToNodeId: data?.attachedToNodeId ? String(data.attachedToNodeId) : undefined,
        kind: typeof data?.kind === 'string' ? data.kind : undefined,
        anchor: (data?.anchor && typeof data.anchor === 'object')
          ? (data.anchor as NoteAnchor)
          : undefined,
      });
    } else {
      exportNodes.push(stripDefaultProps(n));
    }
  }

  const disabledBlocks = Array.from(useFlowStore.getState().disabledBlocks);

  return { nodes: exportNodes, edges, comments, disabledBlocks };
}
