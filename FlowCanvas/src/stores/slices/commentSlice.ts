import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import { sendLayoutAutosave } from '../../utils/layoutAutosave';
import { DEFAULT_COMMENT_COLOR } from '../../utils/tokens';

export interface CommentSlice {
  addComment: (position: { x: number; y: number }, attachedToNodeId?: string) => void;
  updateComment: (id: string, updates: Record<string, unknown>) => void;
  removeComment: (id: string) => void;
}

let commentCounter = 0;

export const createCommentSlice: StateCreator<FlowStore, [], [], CommentSlice> = (set, get) => ({
  addComment: (position, attachedToNodeId) => {
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
      },
      style: {
        width: 200,
        height: 100,
      },
    };
    set((s) => ({ nodes: [...s.nodes, commentNode] }));
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
    set((s) => ({
      nodes: s.nodes.filter((n) => n.id !== id),
      selectedNodeIds: new Set([...s.selectedNodeIds].filter((selectedId) => selectedId !== id)),
    }));
    sendLayoutAutosave();
  },
});

