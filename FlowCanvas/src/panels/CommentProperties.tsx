import { useFlowStore } from '../stores/useFlowStore';
import type { CommentNodeData } from '../nodes/CommentNode';

export function CommentProperties({ nodeId, data }: { nodeId: string; data: CommentNodeData }) {
  const updateComment = useFlowStore((s) => s.updateComment);
  const labelStyle = { fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 } as const;
  const inputStyle = {
    width: '100%', background: 'var(--fc-input-bg)', border: '1px solid var(--fc-border)',
    borderRadius: 4, color: 'var(--fc-text)', fontSize: 12, padding: '6px 8px',
  } as const;
  return (
    <div data-testid="comment-properties" style={{ flex: 1, padding: 16, overflowY: 'auto' }}>
      <label style={labelStyle}>Text</label>
      <textarea
        data-testid="comment-text-input"
        value={String(data.text ?? '')}
        onChange={(e) => updateComment(nodeId, { text: e.target.value })}
        rows={3}
        style={{ ...inputStyle, resize: 'vertical' }}
      />
      <label style={{ ...labelStyle, marginTop: 10 }}>Kind</label>
      <select
        data-testid="comment-kind-input"
        value={(data.kind as string) ?? 'sticky'}
        onChange={(e) => updateComment(nodeId, { kind: e.target.value })}
        style={inputStyle}
      >
        <option value="comment">comment (exports as #)</option>
        <option value="sticky">sticky (visual only)</option>
      </select>
      {data.anchor && (
        <div style={{ marginTop: 10, fontSize: 11, color: 'var(--fc-text-muted)' }}>
          Anchor: {data.anchor.type}{data.anchor.stepPath ? ` · ${data.anchor.stepPath}` : ''}
        </div>
      )}
    </div>
  );
}
