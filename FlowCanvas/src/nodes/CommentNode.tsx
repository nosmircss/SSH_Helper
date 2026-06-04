import { memo, useState, useRef, useEffect, useCallback } from 'react';
import type { NodeProps } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { DEFAULT_COMMENT_COLOR } from '../utils/tokens';

export interface NoteAnchor {
  type: 'header' | 'leading' | 'inline';
  stepPath?: string;
  lineOffset?: number;
}

export interface CommentNodeData {
  commentId: string;
  text: string;
  color?: string;
  kind?: 'comment' | 'sticky';
  anchor?: NoteAnchor;
  attachedToNodeId?: string;
  [key: string]: unknown;
}

function CommentNode({ data, id }: NodeProps) {
  const commentData = data as CommentNodeData;
  const updateComment = useFlowStore((s) => s.updateComment);
  const removeComment = useFlowStore((s) => s.removeComment);
  const compact = useFlowStore((s) => s.compactCommentsEnabled);

  const kind = (commentData.kind as 'comment' | 'sticky' | undefined) ?? 'sticky';
  const anchorType = commentData.anchor?.type;
  const isComment = kind === 'comment';
  const renderPill = compact && isComment && (anchorType === 'leading' || anchorType === 'header');

  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(commentData.text || '');
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const commentId = commentData.commentId || id;
  const color = commentData.color || DEFAULT_COMMENT_COLOR;

  useEffect(() => {
    setText(commentData.text || '');
  }, [commentData.text]);

  useEffect(() => {
    if (editing && textareaRef.current) {
      textareaRef.current.focus();
      textareaRef.current.select();
    }
  }, [editing]);

  const handleDoubleClick = useCallback(() => {
    setEditing(true);
  }, []);

  const handleBlur = useCallback(() => {
    setEditing(false);
    updateComment(commentId, { text });
  }, [commentId, text, updateComment]);

  const handleDelete = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      e.preventDefault();
      removeComment(commentId);
    },
    [commentId, removeComment],
  );

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Escape') {
        setEditing(false);
        setText(commentData.text || '');
      }
    },
    [commentData.text],
  );

  if (renderPill && !editing) {
    return (
      <div
        data-testid="comment-pill"
        onDoubleClick={handleDoubleClick}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 6,
          background: 'var(--fc-comment-pill-bg)',
          borderLeft: '3px solid var(--fc-comment-pill-accent)',
          borderRadius: 3, padding: '2px 9px', fontFamily: 'ui-monospace, Consolas, monospace',
          fontSize: 11.5, color: 'var(--fc-comment-pill-ink)', cursor: 'grab',
        }}
        title="Double-click to edit"
      >
        <span style={{ color: 'var(--fc-accent)', fontWeight: 700 }}>#</span>
        {text || 'comment'}
      </div>
    );
  }

  return (
    <div
      data-testid="comment-full"
      onDoubleClick={handleDoubleClick}
      style={{
        background: `${color}cc`,
        borderRadius: 6,
        minWidth: 150,
        minHeight: 80,
        padding: 10,
        position: 'relative',
        cursor: editing ? 'text' : 'grab',
        boxShadow: 'var(--fc-shadow-sm)',
      }}
    >
      {/* Delete button */}
      <button
        onClick={handleDelete}
        style={{
          position: 'absolute',
          top: 4,
          right: 4,
          width: 18,
          height: 18,
          background: 'var(--fc-comment-btn-scrim)',
          border: 'none',
          borderRadius: 3,
          color: 'var(--fc-comment-ink)',
          fontSize: 12,
          lineHeight: '16px',
          textAlign: 'center',
          cursor: 'pointer',
          padding: 0,
          opacity: 0.6,
          transition: 'opacity 0.15s',
        }}
        onMouseEnter={(e) => {
          (e.currentTarget as HTMLElement).style.opacity = '1';
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLElement).style.opacity = '0.6';
        }}
        title="Delete comment"
      >
        &#10005;
      </button>

      {editing ? (
        <textarea
          ref={textareaRef}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onBlur={handleBlur}
          onKeyDown={handleKeyDown}
          style={{
            width: '100%',
            minHeight: 60,
            background: 'var(--fc-comment-field-bg)',
            border: 'none',
            borderRadius: 3,
            color: 'var(--fc-comment-ink)',
            fontSize: 12,
            lineHeight: 1.4,
            padding: 4,
            outline: 'none',
            resize: 'both',
            fontFamily: 'inherit',
          }}
        />
      ) : (
        <div
          style={{
            color: 'var(--fc-comment-ink)',
            fontSize: 12,
            lineHeight: 1.4,
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            minHeight: 40,
            paddingRight: 16,
          }}
        >
          {text || 'Double-click to edit...'}
        </div>
      )}
    </div>
  );
}

export default memo(CommentNode);
