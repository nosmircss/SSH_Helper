import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';
import { DEFAULT_COMMENT_COLOR } from '../../utils/tokens';
import { reflowLayout } from '../reflow';
import { anchorReservesLayoutSpace } from '../../utils/layout/hierarchicalLayout';

export interface CommentSlice {
  addComment: (position: { x: number; y: number }, attachedToNodeId?: string, kind?: 'comment' | 'sticky') => void;
  updateComment: (id: string, updates: Record<string, unknown>) => void;
  removeComment: (id: string) => void;
}

let commentCounter = 0;

export const createCommentSlice: StateCreator<FlowStore, [], [], CommentSlice> = (set, get) => ({
  addComment: (position, attachedToNodeId, kind) => {
    get().pushSnapshot('Add comment');
    const id = `comment-${Date.now()}-${commentCounter++}`;
    const commentNode = {
      id,
      type: 'comment',
      position,
      data: {
        commentId: id,
        text: '',
        color: DEFAULT_COMMENT_COLOR,
        attachedToNodeId,
        kind: kind ?? 'sticky',
        ...(attachedToNodeId ? { anchor: { type: 'leading' as const } } : {}),
      },
      // No fixed width/height: comments render as fit-content pills/cards, so React Flow auto-measures
      // the visible card. A fixed box would create an oversized invisible hit area that hijacks drags
      // meant for the block beneath or the branch-band handle (see utils/displayNodes.ts).
    };
    set((s) => ({ nodes: [...s.nodes, commentNode] }));
    // A space-reserving comment (leading/header/branch) needs the layout to make room above its
    // block, so reflow to place it there and push everything below down. Comments that reserve
    // nothing (inline, or free-floating stickies) skip the reflow so they neither re-gutter nor
    // discard a manual arrangement. anchorReservesLayoutSpace is the same rule the layout reserves by.
    if (anchorReservesLayoutSpace(commentNode as never)) reflowLayout(get);
    sendLayoutAutosave();
  },

  updateComment: (id, updates) => {
    set((s) => ({
      nodes: s.nodes.map((n) => {
        if (n.id !== id) return n;

        const nextData = { ...(n.data as Record<string, unknown>), ...updates };
        const nextNode = {
          ...n,
          data: nextData,
        };

        if (updates.position && typeof updates.position === 'object') {
          nextNode.position = updates.position as { x: number; y: number };
        }
        if (typeof updates.width === 'number' || typeof updates.height === 'number') {
          nextNode.style = {
            ...(n.style ?? {}),
            ...(typeof updates.width === 'number' ? { width: updates.width } : {}),
            ...(typeof updates.height === 'number' ? { height: updates.height } : {}),
          };
        }

        return nextNode;
      }),
    }));
    sendLayoutAutosave();
  },

  removeComment: (id) => {
    get().pushSnapshot('Remove comment');
    const removed = get().nodes.find((n) => n.id === id);
    const wasReserving = !!removed && anchorReservesLayoutSpace(removed);
    set((s) => ({
      nodes: s.nodes.filter((n) => n.id !== id),
      selectedNodeIds: new Set([...s.selectedNodeIds].filter((selectedId) => selectedId !== id)),
    }));
    // Reclaim space only if the comment actually reserved any (inline/free-floating reserved none).
    if (wasReserving) reflowLayout(get);
    sendLayoutAutosave();
  },
});

