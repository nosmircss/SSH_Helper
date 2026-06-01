import { useReactFlow } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';

export default function ProblemsPanel() {
  const visible = useFlowStore((s) => s.panelsVisible.problems);
  const diagnostics = useFlowStore((s) => s.diagnostics);
  const selectNode = useFlowStore((s) => s.selectNode);
  const getNode = useFlowStore((s) => s.nodes);
  const { setCenter } = useReactFlow();
  const reducedMotion = useFlowStore((s) => s.reducedMotion);

  if (!visible || diagnostics.length === 0) return null;

  // Stable error-before-warning ordering (preserves the bridge's relative order).
  const ordered = [...diagnostics].sort(
    (a, b) => (a.severity === 'error' ? 0 : 1) - (b.severity === 'error' ? 0 : 1),
  );

  const focus = (nodeId?: string) => {
    if (!nodeId) return;
    const node = getNode.find((n) => n.id === nodeId);
    if (!node) return; // node may have been deleted since export
    selectNode(nodeId);
    setCenter(node.position.x, node.position.y, { zoom: 1, duration: reducedMotion ? 0 : 400 });
  };

  return (
    <div style={{
      position: 'absolute', bottom: 16, left: 16, width: 360, maxHeight: 240, overflowY: 'auto',
      background: 'var(--fc-surface-1)', border: '1px solid var(--fc-border)',
      borderRadius: 'var(--fc-radius-md)', boxShadow: 'var(--fc-shadow-sm)', zIndex: 20,
      fontSize: 'var(--fc-fs-body)', color: 'var(--fc-text)',
    }}>
      <div style={{ padding: '6px 10px', borderBottom: '1px solid var(--fc-border)', fontWeight: 600 }}>
        Problems ({ordered.length})
      </div>
      {ordered.map((d, i) => (
        <div
          key={i}
          onClick={() => focus(d.nodeId)}
          title={d.nodeId ? 'Click to select & center this block' : undefined}
          style={{
            display: 'flex', gap: 8, padding: '6px 10px',
            cursor: d.nodeId ? 'pointer' : 'default',
            background: 'var(--fc-diag-row-bg)',
            borderLeft: `3px solid ${d.severity === 'error' ? 'var(--fc-diag-error)' : 'var(--fc-diag-warning)'}`,
          }}
        >
          <span aria-hidden>{d.severity === 'error' ? '✕' : '⚠'}</span>
          <span>{d.message}</span>
        </div>
      ))}
    </div>
  );
}
