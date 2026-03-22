import { memo, type CSSProperties, useCallback } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { blockDefMap, categoryColors, type BlockCategory } from '../blockDefs/registry';
import { messageBus } from '../MessageBus';

export interface BlockNodeData {
  blockType: string;
  label?: string;
  /** Key-value properties for this block instance */
  props?: Record<string, unknown>;
  /** Execution state: idle | running | success | error */
  execState?: 'idle' | 'running' | 'success' | 'error';
  /** Whether a breakpoint is set on this block */
  breakpoint?: boolean;
  [key: string]: unknown;
}

const execGlowColors: Record<string, string> = {
  running: 'rgba(74, 158, 255, 0.4)',
  success: 'rgba(46, 204, 113, 0.3)',
  error: 'rgba(231, 76, 60, 0.3)',
};

function BaseBlock({ data, selected, id }: NodeProps) {
  const blockData = data as BlockNodeData;
  const def = blockDefMap.get(blockData.blockType);

  const handleBreakpointToggle = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    messageBus.send({ type: 'breakpoint-toggle', stepId: id });
  }, [id]);

  if (!def) return <div style={{ color: '#e74c3c' }}>Unknown: {blockData.blockType}</div>;

  const colors = categoryColors[def.category as BlockCategory];
  const execState = blockData.execState || 'idle';
  const hasBreakpoint = blockData.breakpoint;

  // Preview text from the primary property
  const previewText = def.previewKey && blockData.props?.[def.previewKey]
    ? String(blockData.props[def.previewKey])
    : null;

  const containerStyle: CSSProperties = {
    background: colors.bg,
    border: `2px solid ${selected ? '#fff' : colors.border}`,
    borderRadius: 8,
    minWidth: 180,
    maxWidth: 280,
    overflow: 'hidden',
    boxShadow: execState !== 'idle'
      ? `0 0 16px ${execGlowColors[execState]}`
      : selected
        ? '0 0 12px rgba(255,255,255,0.15)'
        : 'none',
    transition: 'box-shadow 0.2s, border-color 0.2s',
  };

  const headerStyle: CSSProperties = {
    padding: '5px 8px',
    borderBottom: `1px solid ${colors.border}33`,
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    fontSize: 13,
  };

  const badgeStyle: CSSProperties = {
    background: colors.badge,
    color: colors.badgeText,
    fontSize: 10,
    fontWeight: 700,
    padding: '2px 6px',
    borderRadius: 3,
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    flexShrink: 0,
  };

  const execIndicator = execState !== 'idle' ? (
    <span style={{
      fontSize: 9,
      marginLeft: 'auto',
      color: execState === 'running' ? '#4a9eff'
        : execState === 'success' ? '#2ecc71'
        : '#e74c3c',
      fontWeight: 600,
    }}>
      {execState === 'running' ? '● RUNNING'
        : execState === 'success' ? '✓ DONE'
        : '✗ ERROR'}
    </span>
  ) : null;

  return (
    <div style={containerStyle} onContextMenu={handleBreakpointToggle}>
      {/* Input handle (top) */}
      <Handle
        type="target"
        position={Position.Top}
        style={{ background: colors.border, width: 8, height: 8, border: 'none' }}
      />

      {/* Header */}
      <div style={headerStyle}>
        {/* Breakpoint gutter — click to toggle */}
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
          title="Toggle breakpoint (right-click)"
        />

        <span style={badgeStyle}>{def.type}</span>
        <span style={{ color: '#ccc', fontSize: 12, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
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
          color: colors.text,
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

      {/* Second output handle for IF blocks (true/false) */}
      {def.outputs === 2 && (
        <Handle
          type="source"
          position={Position.Bottom}
          id="false"
          style={{
            background: '#e74c3c', width: 8, height: 8, border: 'none',
            left: '75%',
          }}
        />
      )}
    </div>
  );
}

export default memo(BaseBlock);
