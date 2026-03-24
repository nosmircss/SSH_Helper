import { useState, useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

const SENSITIVE_VARIABLE_NAME = /(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key)/i;
const MASKED_VALUE = '"********"';
const TRUNCATE_LENGTH = 40;

/**
 * Docked variable inspector panel - sits below the Properties panel in the right sidebar.
 * Reads from Zustand store and shows yellow flash on changed variables.
 * Click a variable row to expand/collapse the full value.
 */
export default function VariableInspector() {
  const variables = useFlowStore((s) => s.variables);
  const togglePanel = useFlowStore((s) => s.togglePanel);
  const [filter, setFilter] = useState('');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const toggleExpanded = useCallback((name: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }, []);

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
        }}>&times;</button>
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
          filtered.map((v) => {
            const isSensitive = SENSITIVE_VARIABLE_NAME.test(v.name);
            const fullDisplay = formatVariableFull(v.name, v.value);
            const shortDisplay = formatVariableShort(v.name, v.value);
            const isTruncated = !isSensitive && fullDisplay !== shortDisplay;
            const isExpanded = expanded.has(v.name);

            return (
              <div
                key={v.name}
                onClick={isTruncated ? () => toggleExpanded(v.name) : undefined}
                style={{
                  fontFamily: 'monospace',
                  fontSize: 11,
                  lineHeight: 1.8,
                  borderBottom: '1px solid var(--fc-canvas-bg, #1a1a2e)',
                  padding: '2px 0',
                  transition: 'background-color 0.3s ease',
                  backgroundColor: v.changed ? 'rgba(224, 192, 64, 0.15)' : 'transparent',
                  borderLeft: v.changed ? '2px solid #e0c040' : '2px solid transparent',
                  paddingLeft: v.changed ? 6 : 2,
                  cursor: isTruncated ? 'pointer' : 'default',
                }}
              >
                <span style={{ color: '#e0c040' }}>{v.name}</span>
                <span style={{ color: 'var(--fc-text-muted, #555)' }}> = </span>
                {!isExpanded ? (
                  <span style={{
                    color: '#8adb8a',
                    transition: 'color 0.3s ease',
                    fontWeight: v.changed ? 700 : 400,
                  }}>
                    {shortDisplay}
                    {isTruncated && (
                      <span style={{ color: 'var(--fc-text-muted, #555)', fontSize: 9, marginLeft: 4 }}>▶</span>
                    )}
                  </span>
                ) : (
                  <div style={{
                    color: '#8adb8a',
                    fontWeight: v.changed ? 700 : 400,
                    marginTop: 2,
                    padding: '4px 6px',
                    background: 'rgba(0,0,0,0.25)',
                    borderRadius: 3,
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-all',
                    lineHeight: 1.5,
                    maxHeight: 200,
                    overflowY: 'auto',
                  }}>
                    {fullDisplay}
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}

/** Full value display - no truncation, proper object serialization. */
function formatVariableFull(_name: string, value: unknown): string {
  if (typeof value === 'string') return `"${value}"`;
  if (value === null) return 'null';
  if (value === undefined) return 'undefined';
  if (typeof value === 'object') {
    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return String(value);
    }
  }
  return String(value);
}

/** Truncated display for the collapsed row. */
function formatVariableShort(name: string, value: unknown): string {
  if (SENSITIVE_VARIABLE_NAME.test(name)) return MASKED_VALUE;

  if (typeof value === 'string') {
    return `"${value.length > TRUNCATE_LENGTH ? value.slice(0, TRUNCATE_LENGTH) + '...' : value}"`;
  }

  if (value === null) return 'null';
  if (value === undefined) return 'undefined';

  if (typeof value === 'object') {
    try {
      const json = JSON.stringify(value);
      return json.length > TRUNCATE_LENGTH ? json.slice(0, TRUNCATE_LENGTH) + '...' : json;
    } catch {
      return String(value);
    }
  }

  return String(value);
}

// Re-export the type for compatibility
export type { VariableEntry } from '../stores/slices/variableSlice';
