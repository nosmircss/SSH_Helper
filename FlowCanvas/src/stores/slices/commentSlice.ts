import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';

export interface CanvasComment {
  id: string;
  text: string;
  position: { x: number; y: number };
  width: number;
  height: number;
  color: string;
  attachedToNodeId?: string;
}

export interface CommentSlice {
  comments: CanvasComment[];

  addComment: (position: { x: number; y: number }, attachedToNodeId?: string) => void;
  updateComment: (id: string, updates: Partial<CanvasComment>) => void;
  removeComment: (id: string) => void;
}

let commentCounter = 0;

export const createCommentSlice: StateCreator<FlowStore, [], [], CommentSlice> = (set, get) => ({
  comments: [],

  addComment: (position, attachedToNodeId) => {
    get().pushSnapshot('Add comment');
    const comment: CanvasComment = {
      id: `comment-${Date.now()}-${commentCounter++}`,
      text: '',
      position,
      width: 200,
      height: 100,
      color: '#e0c040',
      attachedToNodeId,
    };
    set((s) => ({ comments: [...s.comments, comment] }));
  },

  updateComment: (id, updates) => {
    set((s) => ({
      comments: s.comments.map((c) => (c.id === id ? { ...c, ...updates } : c)),
    }));
  },

  removeComment: (id) => {
    get().pushSnapshot('Remove comment');
    set((s) => ({ comments: s.comments.filter((c) => c.id !== id) }));
  },
});
