import { create } from 'zustand';
import { createGraphSlice, type GraphSlice } from './slices/graphSlice';
import { createExecutionSlice, type ExecutionSlice } from './slices/executionSlice';
import { createDebugSlice, type DebugSlice } from './slices/debugSlice';
import { createVariableSlice, type VariableSlice } from './slices/variableSlice';
import { createUndoSlice, type UndoSlice } from './slices/undoSlice';
import { createTimelineSlice, type TimelineSlice } from './slices/timelineSlice';
import { createUISlice, type UISlice } from './slices/uiSlice';
import { createCommentSlice, type CommentSlice } from './slices/commentSlice';

export type FlowStore = GraphSlice &
  ExecutionSlice &
  DebugSlice &
  VariableSlice &
  UndoSlice &
  TimelineSlice &
  UISlice &
  CommentSlice;

export const useFlowStore = create<FlowStore>()((...a) => ({
  ...createGraphSlice(...a),
  ...createExecutionSlice(...a),
  ...createDebugSlice(...a),
  ...createVariableSlice(...a),
  ...createUndoSlice(...a),
  ...createTimelineSlice(...a),
  ...createUISlice(...a),
  ...createCommentSlice(...a),
}));
