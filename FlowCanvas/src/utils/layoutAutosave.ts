import { messageBus } from '../MessageBus';
import { useFlowStore } from '../stores/useFlowStore';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';
import type { CommentData } from './exportGraph';
import { DEFAULT_COMMENT_COLOR } from './tokens';

let debounceTimer: ReturnType<typeof setTimeout> | null = null;

/**
 * Sends a lightweight layout-autosave message to C# with current positions,
 * comments, and disabled blocks. Debounced to avoid flooding during rapid changes.
 */
export function sendLayoutAutosave(): void {
  if (debounceTimer) clearTimeout(debounceTimer);
  debounceTimer = setTimeout(doSend, 300);
}

function doSend(): void {
  debounceTimer = null;
  const state = useFlowStore.getState();
  const positions: Record<string, { x: number; y: number }> = {};
  const comments: CommentData[] = [];

  for (const node of state.nodes) {
    if (node.type === 'comment') {
      const data = node.data as Record<string, unknown> | undefined;
      comments.push({
        id: node.id,
        text: String(data?.text ?? ''),
        color: String(data?.color ?? DEFAULT_COMMENT_COLOR),
        x: node.position?.x ?? 0,
        y: node.position?.y ?? 0,
        width: (node.style?.width as number) ?? 200,
        height: (node.style?.height as number) ?? 100,
        attachedToNodeId: data?.attachedToNodeId ? String(data.attachedToNodeId) : undefined,
        kind: typeof data?.kind === 'string' ? data.kind : undefined,
        anchor: (data?.anchor && typeof data.anchor === 'object')
          ? (data.anchor as { type: string; stepPath?: string; lineOffset?: number })
          : undefined,
      });
    } else {
      positions[node.id] = {
        x: node.position?.x ?? 0,
        y: node.position?.y ?? 0,
      };
    }
  }

  const disabledBlocks = Array.from(state.disabledBlocks);
  const expandedNodes = Array.from(state.expandedNodes);

  messageBus.send({
    type: CANVAS_HOST_MESSAGES.outgoing.layoutAutosave,
    positions,
    comments,
    disabledBlocks,
    expandedNodes,
  });
}
