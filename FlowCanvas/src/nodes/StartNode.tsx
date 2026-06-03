import { memo, type CSSProperties } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';

export interface StartNodeData {
  blockType: '_start';
  label?: string;
  props?: {
    name?: string;
    description?: string;
    environment?: string;
    version?: number;
    debug?: boolean;
    nobanner?: boolean;
    compact_errors?: boolean;
    suppress_missing_column_warning?: boolean;
    library?: boolean;
    vars?: Record<string, unknown>;
    imports?: Array<string | { path?: string; as?: string }>;
    _yamlSnippet?: string;
  };
  [key: string]: unknown;
}

const FLAG_KEYS: { key: string; label: string }[] = [
  { key: 'debug', label: 'debug' },
  { key: 'nobanner', label: 'nobanner' },
  { key: 'compact_errors', label: 'compact-errors' },
  { key: 'suppress_missing_column_warning', label: 'no-warn' },
  { key: 'library', label: 'library' },
];

function StartNode({ data, selected }: NodeProps) {
  const blockWidth = useFlowStore((s) => s.blockWidth);
  const startData = data as StartNodeData;
  const props = startData.props ?? {};
  const scriptName = props.name || startData.label || 'Untitled Script';

  const activeBadges: string[] = [];
  for (const flag of FLAG_KEYS) {
    if (props[flag.key as keyof typeof props]) {
      activeBadges.push(flag.label);
    }
  }

  const varsCount = props.vars ? Object.keys(props.vars).length : 0;
  const importsCount = props.imports ? props.imports.length : 0;
  if (varsCount > 0) activeBadges.push(`${varsCount} var${varsCount !== 1 ? 's' : ''}`);
  if (importsCount > 0) activeBadges.push(`${importsCount} import${importsCount !== 1 ? 's' : ''}`);

  const containerStyle: CSSProperties = {
    background: 'linear-gradient(135deg, var(--fc-start-grad-from), var(--fc-start-grad-to))',
    border: `2px solid ${selected ? 'var(--fc-border-selected)' : 'var(--fc-start-accent)'}`,
    borderRadius: 8,
    minWidth: blockWidth,
    maxWidth: blockWidth,
    overflow: 'hidden',
    boxShadow: selected
      ? '0 0 12px var(--fc-glow-selected)'
      : '0 0 12px var(--fc-glow-start)',
    transition: 'box-shadow 0.2s, border-color 0.2s',
    position: 'relative',
  };

  const railStyle: CSSProperties = {
    position: 'absolute', left: 0, top: 0, bottom: 0,
    width: 'var(--fc-rail-w)', background: 'var(--fc-start-accent)',
    borderTopLeftRadius: 8, borderBottomLeftRadius: 8, pointerEvents: 'none',
  };

  return (
    <div style={containerStyle}>
      <span style={railStyle} />
      <div style={{
        padding: '6px 10px',
        paddingLeft: 'calc(10px + var(--fc-rail-w))',
        borderBottom: '1px solid var(--fc-start-chip-border)',
        display: 'flex',
        alignItems: 'center',
        gap: 8,
      }}>
        <span style={{
          background: 'var(--fc-start-accent)',
          color: 'var(--fc-start-badge-text)',
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
          letterSpacing: '0.5px',
          flexShrink: 0,
        }}>
          START
        </span>
        <span style={{
          color: 'var(--fc-text)',
          fontSize: 12,
          fontWeight: 600,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}>
          {scriptName}
        </span>
      </div>

      {activeBadges.length > 0 && (
        <div style={{ padding: '6px 10px', display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {activeBadges.map((badge) => (
            <span key={badge} style={{
              background: 'var(--fc-start-chip-bg)',
              border: '1px solid var(--fc-start-chip-border)',
              borderRadius: 3,
              padding: '1px 5px',
              fontSize: 9,
              color: 'var(--fc-start-chip-text)',
            }}>
              {badge}
            </span>
          ))}
        </div>
      )}

      {/* Invariant: Start is source-only. Adding a target handle is caught by flow-canvas-connection-guards.spec.ts. */}
      <Handle
        type="source"
        position={Position.Bottom}
        style={{ background: 'var(--fc-start-accent)', width: 8, height: 8, border: 'none' }}
      />
    </div>
  );
}

export default memo(StartNode);
