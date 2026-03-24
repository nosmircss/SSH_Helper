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
  running: 'rgba(74, 158, 255, 0.4)',
  success: 'rgba(46, 204, 113, 0.3)',
  error: 'rgba(231, 76, 60, 0.3)',
  skipped: 'rgba(150, 150, 150, 0.2)',
  disabled: 'rgba(100, 100, 100, 0.2)',
};

function BaseBlock({ data, selected, id }: NodeProps) {
  const blockData = data as BlockNodeData;
  const def = blockDefMap.get(blockData.blockType);
  const toggleBreakpoint = useFlowStore((s) => s.toggleBreakpoint);
  const blockTimings = useFlowStore((s) => s.blockTimings);

  const handleBreakpointToggle = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    toggleBreakpoint(id);
  }, [id, toggleBreakpoint]);

  if (!def) return <div style={{ color: '#e74c3c' }}>Unknown: {blockData.blockType}</div>;

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

  // Preview text should follow live editable props for this block type.
  // _preview is importer metadata and can become stale after local edits.
  const previewText = def.previewKey
    ? blockData.props?.[def.previewKey] !== undefined && blockData.props?.[def.previewKey] !== null
      ? String(blockData.props[def.previewKey])
      : null
    : blockData.props?.['_preview']
      ? String(blockData.props['_preview'])
      : null;

  const containerStyle: CSSProperties = {
    background: isDisabled ? '#2a2a2a' : colors.bg,
    border: `2px solid ${selected ? '#fff' : isDisabled ? '#555' : colors.border}`,
    borderRadius: 8,
    minWidth: isChild ? 160 : 180,
    maxWidth: isChild ? 260 : 280,
    overflow: 'hidden',
    opacity: isDisabled ? 0.5 : isChild ? 0.95 : 1,
    boxShadow: execState !== 'idle' && execState !== 'disabled'
      ? `0 0 16px ${execGlowColors[execState] || 'none'}`
      : selected
        ? '0 0 12px rgba(255,255,255,0.15)'
        : 'none',
    transition: 'box-shadow 0.2s, border-color 0.2s, opacity 0.2s',
    position: 'relative',
  };

  // Running pulse animation via inline keyframes
  if (execState === 'running') {
    containerStyle.animation = 'exec-pulse 1.5s ease-in-out infinite';
  }

  const headerStyle: CSSProperties = {
    padding: '5px 8px',
    borderBottom: `1px solid ${colors.border}33`,
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    fontSize: 13,
  };

  const badgeStyle: CSSProperties = {
    background: isDisabled ? '#444' : colors.badge,
    color: isDisabled ? '#888' : colors.badgeText,
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
      color: execState === 'running' ? '#4a9eff'
        : execState === 'success' ? '#2ecc71'
        : execState === 'skipped' ? '#888'
        : '#e74c3c',
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
          color: '#888',
          background: '#1a1a2e',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {durationText}
        </span>
      )}
    </span>
  ) : isDisabled ? (
    <span style={{ fontSize: 9, marginLeft: 'auto', color: '#666', fontWeight: 600 }}>
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
              background: hasBreakpoint ? '#e74c3c' : 'transparent',
              border: hasBreakpoint ? 'none' : '1px solid #444',
              flexShrink: 0,
              cursor: 'pointer',
              boxShadow: hasBreakpoint ? '0 0 4px rgba(231,76,60,0.6)' : 'none',
              transition: 'background 0.15s',
            }}
            title="Toggle breakpoint"
          />
        )}

        <span style={badgeStyle}>{def.type}</span>
        <span style={{
          color: isDisabled ? '#666' : '#ccc',
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
          padding: '6px 8px',
          fontFamily: 'monospace',
          fontSize: 11,
          color: isDisabled ? '#555' : colors.text,
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
            background: '#e74c3c', width: 8, height: 8, border: 'none',
            top: '50%',
          }}
        />
      )}
    </div>
  );
}

export default memo(BaseBlock);
