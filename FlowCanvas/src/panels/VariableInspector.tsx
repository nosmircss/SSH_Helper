import { useState, useCallback, useMemo } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { selectActiveIterationContext, selectVisibleIterations } from '../stores/selectors/iterationScope';
import type { VariableEntry } from '../stores/slices/variableSlice';

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
  // Subscribe to stable map refs + the primitive last-touched id; derive the active context
  // in a memo. selectActiveIterationContext allocates a fresh object each call, so feeding it
  // straight into useFlowStore() would re-render on every store change (infinite loop).
  const log = useFlowStore((s) => s.iterationLog);
  const sels = useFlowStore((s) => s.iterationSelections);
  const nodes = useFlowStore((s) => s.nodes);
  const lastSelectedLoopId = useFlowStore((s) => s.lastSelectedLoopId);
  const setIterationSelection = useFlowStore((s) => s.setIterationSelection);
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

  const iterCtx = useMemo(
    () => selectActiveIterationContext(useFlowStore.getState()),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [log, sels, lastSelectedLoopId],
  );

  // Frozen end-of-iteration snapshot (no changed/setBy — this is history, not live state).
  const snapshotEntries = useMemo<VariableEntry[] | null>(() => {
    if (!iterCtx?.record.variables) return null;
    return Object.entries(iterCtx.record.variables)
      .map(([name, value]) => ({ name, value }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [iterCtx]);

  // Position of the active record within its loop's currently-visible iterations.
  const banner = useMemo(() => {
    if (!iterCtx) return null;
    const visible = selectVisibleIterations(useFlowStore.getState(), iterCtx.loopId);
    const pos = visible.findIndex((r) => r.seq === iterCtx.record.seq);
    return { pos, total: visible.length, record: iterCtx.record };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [iterCtx, log, sels, nodes]);

  const displayEntries = iterCtx ? (snapshotEntries ?? []) : variables;

  const filtered = filter
    ? displayEntries.filter((v) => v.name.toLowerCase().includes(filter.toLowerCase()))
    : displayEntries;

  return (
    <div style={{
      borderTop: '1px solid var(--fc-panel-border)',
      maxHeight: '40%',
      display: 'flex',
      flexDirection: 'column',
      flexShrink: 0,
    }}>
      <div style={{
        padding: '6px 10px',
        background: 'var(--fc-header-bg)',
        borderBottom: '1px solid var(--fc-panel-border)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexShrink: 0,
      }}>
        <span style={{ fontSize: 12, color: 'var(--fc-text-secondary)', fontWeight: 600 }}>Variables</span>
        <button onClick={() => togglePanel('variables')} style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted)',
          cursor: 'pointer', fontSize: 14, padding: 0,
        }}>&times;</button>
      </div>

      {banner && (
        <div
          data-testid="iter-vars-banner"
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 6,
            padding: '5px 10px',
            flexShrink: 0,
            fontSize: 11,
            background: 'var(--fc-surface-0)',
            borderBottom: '1px solid var(--fc-panel-border)',
            color: banner.record.failed ? 'var(--fc-state-error)' : 'var(--fc-text-secondary)',
          }}
        >
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {`⏱ Iteration ${banner.pos + 1}/${banner.total}`}
            {banner.record.label ? ` · ${banner.record.label}` : ''}
            {banner.record.failed ? ' · ⚠ failed' : ''}
          </span>
          <button
            data-testid="iter-vars-live"
            onClick={() => setIterationSelection(iterCtx!.loopId, null)}
            title="Return to live values"
            style={{
              flexShrink: 0,
              background: 'none',
              border: '1px solid var(--fc-panel-border)',
              borderRadius: 3,
              color: 'var(--fc-text-secondary)',
              cursor: 'pointer',
              fontSize: 10,
              padding: '1px 8px',
            }}
          >
            Live
          </button>
        </div>
      )}

      <div style={{ padding: '4px 8px', flexShrink: 0 }}>
        <input
          type="text"
          placeholder="Filter..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          style={{
            width: '100%',
            padding: '3px 6px',
            background: 'var(--fc-input-bg)',
            border: '1px solid var(--fc-panel-border)',
            borderRadius: 3,
            color: 'var(--fc-text)',
            fontSize: 11,
            outline: 'none',
          }}
        />
      </div>

      <div style={{ overflowY: 'auto', flex: 1, padding: '4px 8px' }}>
        {iterCtx && snapshotEntries === null ? (
          <div
            data-testid="iter-vars-empty"
            style={{ color: 'var(--fc-text-muted)', fontSize: 11, padding: '8px 0', textAlign: 'center', fontStyle: 'italic' }}
          >
            (no variable snapshot recorded for this iteration)
          </div>
        ) : filtered.length === 0 ? (
          <div style={{ color: 'var(--fc-text-muted)', fontSize: 11, padding: '8px 0', textAlign: 'center' }}>
            {displayEntries.length === 0 ? 'No variables set' : 'No matches'}
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
                  borderBottom: '1px solid var(--fc-canvas-bg)',
                  padding: '2px 0',
                  transition: 'background-color 0.3s ease',
                  backgroundColor: v.changed ? 'var(--fc-glow-warning-soft)' : 'transparent',
                  borderLeft: v.changed ? '2px solid var(--fc-state-warning)' : '2px solid transparent',
                  paddingLeft: v.changed ? 6 : 2,
                  cursor: isTruncated ? 'pointer' : 'default',
                }}
              >
                <span style={{ color: 'var(--fc-state-warning)' }}>{v.name}</span>
                {isExpanded && (
                  <span style={{ color: 'var(--fc-text-muted)', fontSize: 9, marginLeft: 4, cursor: 'pointer' }} title="Click to collapse">▼</span>
                )}
                <span style={{ color: 'var(--fc-text-muted)' }}> = </span>
                {!isExpanded ? (
                  <span style={{
                    color: 'var(--fc-state-success)',
                    transition: 'color 0.3s ease',
                    fontWeight: v.changed ? 700 : 400,
                  }}>
                    {shortDisplay}
                    {isTruncated && (
                      <span style={{ color: 'var(--fc-text-muted)', fontSize: 9, marginLeft: 4 }}>▶</span>
                    )}
                  </span>
                ) : (
                  <div
                    onClick={(e) => e.stopPropagation()}
                    style={{
                      color: 'var(--fc-state-success)',
                      fontWeight: v.changed ? 700 : 400,
                      marginTop: 2,
                      padding: '4px 6px',
                      background: 'var(--fc-overlay-scrim)',
                      borderRadius: 3,
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-all',
                      lineHeight: 1.5,
                      maxHeight: 200,
                      overflowY: 'auto',
                      userSelect: 'text',
                      cursor: 'text',
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
