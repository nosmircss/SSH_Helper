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

export default function VariableInspector({ variables, visible, onToggle }: VariableInspectorProps) {
  const [filter, setFilter] = useState('');

  const filtered = filter
    ? variables.filter((v) => v.name.toLowerCase().includes(filter.toLowerCase()))
    : variables;

  return (
    <div style={{
      position: 'absolute',
      right: 240,
      top: 50,
      width: visible ? 200 : 0,
      maxHeight: 'calc(100% - 100px)',
      background: '#12122a',
      border: visible ? '1px solid #2a2a4a' : 'none',
      borderRadius: 8,
      overflow: 'hidden',
      transition: 'width 0.2s',
      zIndex: 10,
      boxShadow: visible ? '0 4px 20px rgba(0,0,0,0.4)' : 'none',
    }}>
      {visible && (
        <>
          <div style={{
            padding: '6px 10px',
            background: '#1a1a3a',
            borderBottom: '1px solid #2a2a4a',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}>
            <span style={{ fontSize: 12, color: '#888', fontWeight: 600 }}>Variables</span>
            <button onClick={onToggle} style={{
              background: 'none', border: 'none', color: '#555',
              cursor: 'pointer', fontSize: 14, padding: 0,
            }}>×</button>
          </div>

          <div style={{ padding: '4px 8px' }}>
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

          <div style={{ overflowY: 'auto', maxHeight: 300, padding: '4px 8px' }}>
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
                      ? `"${v.value.length > 30 ? v.value.slice(0, 30) + '...' : v.value}"`
                      : String(v.value)}
                  </span>
                </div>
              ))
            )}
          </div>
        </>
      )}
    </div>
  );
}
