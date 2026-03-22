import { useState } from 'react';

export interface VariableEntry {
  name: string;
  value: unknown;
  setBy?: string; // block ID that last set this variable
}

interface VariableInspectorProps {
  variables: VariableEntry[];
  visible: boolean;
  onToggle: () => void;
}

/**
 * Docked variable inspector panel — sits below the Properties panel in the right sidebar.
 */
export default function VariableInspector({ variables, visible, onToggle }: VariableInspectorProps) {
  const [filter, setFilter] = useState('');

  if (!visible) return null;

  const filtered = filter
    ? variables.filter((v) => v.name.toLowerCase().includes(filter.toLowerCase()))
    : variables;

  return (
    <div style={{
      borderTop: '1px solid #2a2a4a',
      maxHeight: '40%',
      display: 'flex',
      flexDirection: 'column',
      flexShrink: 0,
    }}>
      <div style={{
        padding: '6px 10px',
        background: '#1a1a3a',
        borderBottom: '1px solid #2a2a4a',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexShrink: 0,
      }}>
        <span style={{ fontSize: 12, color: '#888', fontWeight: 600 }}>Variables</span>
        <button onClick={onToggle} style={{
          background: 'none', border: 'none', color: '#555',
          cursor: 'pointer', fontSize: 14, padding: 0,
        }}>×</button>
      </div>

      <div style={{ padding: '4px 8px', flexShrink: 0 }}>
        <input
          type="text"
          placeholder="Filter..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{
            width: '100%',
            padding: '3px 6px',
            background: '#0d1117',
            border: '1px solid #2a2a4a',
            borderRadius: 3,
            color: '#ccc',
            fontSize: 11,
            outline: 'none',
          }}
        />
      </div>

      <div style={{ overflowY: 'auto', flex: 1, padding: '4px 8px' }}>
        {filtered.length === 0 ? (
          <div style={{ color: '#555', fontSize: 11, padding: '8px 0', textAlign: 'center' }}>
            {variables.length === 0 ? 'No variables set' : 'No matches'}
          </div>
        ) : (
          filtered.map((v) => (
            <div key={v.name} style={{
              fontFamily: 'monospace',
              fontSize: 11,
              lineHeight: 1.8,
              borderBottom: '1px solid #1a1a2e',
              padding: '2px 0',
            }}>
              <span style={{ color: '#e0c040' }}>{v.name}</span>
              <span style={{ color: '#555' }}> = </span>
              <span style={{ color: '#8adb8a' }}>
                {typeof v.value === 'string'
                  ? `"${v.value.length > 40 ? v.value.slice(0, 40) + '...' : v.value}"`
                  : String(v.value)}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
