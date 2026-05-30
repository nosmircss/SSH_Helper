import { memo, type CSSProperties, useCallback } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { blockDefMap, categoryColors, type BlockCategory } from '../blockDefs/registry';
import { useFlowStore } from '../stores/useFlowStore';
import './baseblock.css';

export interface BlockNodeData {
  blockType: string;
  label?: string;
  /** Key-value properties for this block instance */
  props?: Record<string, unknown>;
  /** Execution state: idle | running | success | error | skipped | disabled */
  execState?: 'idle' | 'running' | 'success' | 'error' | 'skipped' | 'disabled';
  /** Whether a breakpoint is set on this block */
  breakpoint?: boolean;
  [key: string]: unknown;
}

const execGlowColors: Record<string, string> = {
  running: 'var(--fc-glow-running)',
  success: 'var(--fc-glow-success)',
  error: 'var(--fc-glow-error)',
  skipped: 'var(--fc-glow-skipped)',
  disabled: 'var(--fc-glow-disabled)',
};

// Token-driven heat ramp (Decision #4: no inline hex). cold→mid→hot interpolated
// in OKLCH via color-mix so blocks tint by their relative run duration.
function heatColor(ratio: number): string {
  const r = Math.max(0, Math.min(1, ratio));
  const from = r < 0.5 ? 'var(--fc-heat-cold)' : 'var(--fc-heat-mid)';
  const to = r < 0.5 ? 'var(--fc-heat-mid)' : 'var(--fc-heat-hot)';
  const pct = Math.round((r < 0.5 ? r * 2 : (r - 0.5) * 2) * 100);
  return `color-mix(in oklch, ${to} ${pct}%, ${from})`;
}

function BaseBlock({ data, selected, id }: NodeProps) {
  const blockData = data as BlockNodeData;
  const def = blockDefMap.get(blockData.blockType);
  const toggleBreakpoint = useFlowStore((s) => s.toggleBreakpoint);
  const blockTimings = useFlowStore((s) => s.blockTimings);
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
  const maxDuration = useFlowStore((s) => {
    let max = 0;
    s.blockTimings.forEach((t) => { if (t.duration && t.duration > max) max = t.duration; });
    return max;
  });

  const handleBreakpointToggle = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleBreakpoint(id);
  }, [id, toggleBreakpoint]);

  if (!def) return <div style={{ color: 'var(--fc-state-error-text)' }}>Unknown: {blockData.blockType}</div>;

  const colors = categoryColors[def.category as BlockCategory];
  const execState = blockData.execState || 'idle';
  const hasBreakpoint = blockData.breakpoint;
  const isChild = !!blockData.props?.['_isChildOf'];
  const isDisabled = execState === 'disabled';

  // Duration badge
  const timing = blockTimings.get(id);
  const durationMs = timing?.duration;
  const durationText = durationMs != null
    ? durationMs < 1000 ? `${durationMs}ms` : `${(durationMs / 1000).toFixed(1)}s`
    : null;

  // Run-heatmap tint: only on idle/success blocks so it never overrides the
  // running pulse or the error glow (precedence). Render-time only — never
  // written onto node.data, so the graph snapshot/export stays unchanged.
  const heatTint = (heatmapEnabled && durationMs != null && maxDuration > 0
    && (execState === 'idle' || execState === 'success'))
    ? heatColor(durationMs / maxDuration)
    : undefined;

  // Preview text should follow live editable props for this block type.
  // _preview is importer metadata and can become stale after local edits.
  const previewText = def.previewKey
    ? blockData.props?.[def.previewKey] !== undefined && blockData.props?.[def.previewKey] !== null
      ? String(blockData.props[def.previewKey])
      : null
    : blockData.props?.['_preview']
      ? String(blockData.props['_preview'])
      : null;

  const existingBoxShadow = execState !== 'idle' && execState !== 'disabled'
    ? `0 0 16px ${execGlowColors[execState] || 'none'}`
    : selected
      ? '0 0 12px var(--fc-glow-selected)'
      : 'none';

  const containerStyle: CSSProperties = {
    background: isDisabled ? 'var(--fc-surface-disabled)' : colors.bg,
    border: `2px solid ${selected ? 'var(--fc-border-selected)' : isDisabled ? 'var(--fc-border-muted)' : colors.border}`,
    borderRadius: 8,
    minWidth: isChild ? 160 : 180,
    maxWidth: isChild ? 260 : 280,
    overflow: 'hidden',
    opacity: isDisabled ? 0.5 : isChild ? 0.95 : 1,
    boxShadow: heatTint ? `0 0 0 3px ${heatTint}, ${existingBoxShadow}` : existingBoxShadow,
    transition: 'box-shadow 0.2s, border-color 0.2s, opacity 0.2s',
    position: 'relative',
  };

  // Running pulse animation via inline keyframes
  if (execState === 'running') {
    containerStyle.animation = 'exec-pulse 1.5s ease-in-out infinite';
  }

  const headerStyle: CSSProperties = {
    padding: '4px 8px',
    borderBottom: `1px solid ${colors.border}33`,
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    fontSize: 13,
  };

  const badgeStyle: CSSProperties = {
    background: isDisabled ? 'var(--fc-border-subtle)' : colors.badge,
    color: isDisabled ? 'var(--fc-text-secondary)' : colors.badgeText,
    fontSize: 10,
    fontWeight: 700,
    padding: '2px 6px',
    borderRadius: 3,
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    flexShrink: 0,
  };

  const execIndicator = execState !== 'idle' && execState !== 'disabled' ? (
    <span style={{
      fontSize: 9,
      marginLeft: 'auto',
      display: 'flex',
      alignItems: 'center',
      gap: 3,
      color: execState === 'running' ? 'var(--fc-accent)'
        : execState === 'success' ? 'var(--fc-state-success)'
        : execState === 'skipped' ? 'var(--fc-text-secondary)'
        : 'var(--fc-state-error)',
      fontWeight: 600,
    }}>
      {execState === 'running' && <span style={{ animation: 'spin 1s linear infinite', display: 'inline-block' }}>◌</span>}
      {execState === 'running' ? 'RUNNING'
        : execState === 'success' ? '✓ DONE'
        : execState === 'skipped' ? '— SKIP'
        : '✗ ERROR'}
      {durationText && (
        <span style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {durationText}
        </span>
      )}
    </span>
  ) : isDisabled ? (
    <span style={{ fontSize: 9, marginLeft: 'auto', color: 'var(--fc-text-faint)', fontWeight: 600 }}>
      ⏭ DISABLED
    </span>
  ) : null;

  return (
    <div style={containerStyle}>
      {/* Input handle (top) */}
      <Handle
        type="target"
        position={Position.Top}
        style={{ background: colors.border, width: 8, height: 8, border: 'none' }}
      />

      {/* Header */}
      <div style={headerStyle}>
        {/* Breakpoint gutter */}
        {!isChild && (
          <span
            onClick={handleBreakpointToggle}
            style={{
              width: 10, height: 10, borderRadius: '50%',
              background: hasBreakpoint ? 'var(--fc-state-error)' : 'transparent',
              border: hasBreakpoint ? 'none' : '1px solid var(--fc-border-subtle)',
              flexShrink: 0,
              cursor: 'pointer',
              boxShadow: hasBreakpoint ? '0 0 4px var(--fc-glow-error)' : 'none',
              transition: 'background 0.15s',
            }}
            title="Toggle breakpoint"
          />
        )}

        <span style={badgeStyle}>{def.type}</span>
        <span style={{
          color: isDisabled ? 'var(--fc-text-faint)' : 'var(--fc-text)',
          fontSize: 12,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          textDecoration: isDisabled ? 'line-through' : 'none',
        }}>
          {blockData.label || def.label}
        </span>
        {execIndicator}
      </div>

      {/* Preview content */}
      {previewText && (
        <div style={{
          padding: '4px 8px',
          fontFamily: 'monospace',
          fontSize: 11,
          color: isDisabled ? 'var(--fc-text-disabled)' : colors.text,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}>
          {previewText}
        </div>
      )}

      {/* Output handle (bottom) */}
      <Handle
        type="source"
        position={Position.Bottom}
        style={{ background: colors.border, width: 8, height: 8, border: 'none' }}
      />

      {/* Second output for IF blocks */}
      {def.outputs === 2 && (
        <Handle
          type="source"
          position={Position.Right}
          id="false"
          style={{
            background: 'var(--fc-state-error)', width: 8, height: 8, border: 'none',
            top: '50%',
          }}
        />
      )}

      {/* Continuation handle for container blocks (diamond, bottom-left).
          Position.Left makes edges route leftward first, creating a clear
          corridor that avoids cutting through child blocks. */}
      {def.isContainer && (
        <Handle
          type="source"
          position={Position.Left}
          id="continue"
          style={{
            background: 'var(--fc-accent)',
            width: 10,
            height: 10,
            border: 'none',
            borderRadius: 2,
            transform: 'rotate(45deg)',
            left: -5,
            top: 'auto',
            bottom: -2,
            boxShadow: '0 0 0 5px transparent',
          }}
        />
      )}
    </div>
  );
}

export default memo(BaseBlock);
