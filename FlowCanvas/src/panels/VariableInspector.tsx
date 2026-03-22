import { useState } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

/**
 * Docked variable inspector panel — sits below the Properties panel in the right sidebar.
 * Now reads from Zustand store and shows yellow flash on changed variables.
 */
export default function VariableInspector() {
  const variables = useFlowStore((s) => s.variables);
  const togglePanel = useFlowStore((s) => s.togglePanel);
  const [filter, setFilter] = useState('');

  const filtered = filter
    ? variables.filter((v) => v.name.toLowerCase().includes(filter.toLowerCase()))
    : variables;

  return (
    <div style={{
      borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
      maxHeight: '40%',
      display: 'flex',
      flexDirection: 'column',
      flexShrink: 0,
    }}>
      <div style={{
        padding: '6px 10px',
        background: 'var(--fc-header-bg, #1a1a3a)',
        borderBottom: '1px solid var(--fc-panel-border, #2a2a4a)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexShrink: 0,
      }}>
        <span style={{ fontSize: 12, color: 'var(--fc-text-secondary, #888)', fontWeight: 600 }}>Variables</span>
        <button onClick={() => togglePanel('variables')} style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted, #555)',
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
            background: 'var(--fc-input-bg, #0d1117)',
            border: '1px solid var(--fc-panel-border, #2a2a4a)',
            borderRadius: 3,
            color: 'var(--fc-text, #ccc)',
            fontSize: 11,
            outline: 'none',
          }}
        />
      </div>

      <div style={{ overflowY: 'auto', flex: 1, padding: '4px 8px' }}>
        {filtered.length === 0 ? (
          <div style={{ color: 'var(--fc-text-muted, #555)', fontSize: 11, padding: '8px 0', textAlign: 'center' }}>
            {variables.length === 0 ? 'No variables set' : 'No matches'}
          </div>
        ) : (
          filtered.map((v) => (
            <div key={v.name} style={{
              fontFamily: 'monospace',
              fontSize: 11,
              lineHeight: 1.8,
              borderBottom: '1px solid var(--fc-canvas-bg, #1a1a2e)',
              padding: '2px 0',
              transition: 'background-color 0.3s ease',
              backgroundColor: v.changed ? 'rgba(224, 192, 64, 0.15)' : 'transparent',
              borderLeft: v.changed ? '2px solid #e0c040' : '2px solid transparent',
              paddingLeft: v.changed ? 6 : 2,
            }}>
              <span style={{ color: '#e0c040' }}>{v.name}</span>
              <span style={{ color: 'var(--fc-text-muted, #555)' }}> = </span>
              <span style={{
                color: '#8adb8a',
                transition: 'color 0.3s ease',
                fontWeight: v.changed ? 700 : 400,
              }}>
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

// Re-export the type for compatibility
export type { VariableEntry } from '../stores/slices/variableSlice';
