import { memo, type CSSProperties } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';

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
    background: 'linear-gradient(135deg, #1a3a2a, #0d2a1a)',
    border: `2px solid ${selected ? '#fff' : '#2ecc71'}`,
    borderRadius: 8,
    minWidth: 260,
    maxWidth: 300,
    overflow: 'hidden',
    boxShadow: selected
      ? '0 0 12px rgba(255,255,255,0.15)'
      : '0 0 12px rgba(46, 204, 113, 0.15)',
    transition: 'box-shadow 0.2s, border-color 0.2s',
  };

  return (
    <div style={containerStyle}>
      <div style={{
        padding: '6px 10px',
        borderBottom: '1px solid rgba(46,204,113,0.2)',
        display: 'flex',
        alignItems: 'center',
        gap: 8,
      }}>
        <span style={{
          background: '#2ecc71',
          color: '#000',
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
          color: '#ccc',
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
              background: 'rgba(46,204,113,0.1)',
              border: '1px solid rgba(46,204,113,0.25)',
              borderRadius: 3,
              padding: '1px 5px',
              fontSize: 9,
              color: '#80d4a0',
            }}>
              {badge}
            </span>
          ))}
        </div>
      )}

      <Handle
        type="source"
        position={Position.Bottom}
        style={{ background: '#2ecc71', width: 8, height: 8, border: 'none' }}
      />
    </div>
  );
}

export default memo(StartNode);
