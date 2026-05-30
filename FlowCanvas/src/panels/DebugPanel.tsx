import { useFlowStore } from '../stores/useFlowStore';
import { mix } from '../utils/tokens';

export default function DebugPanel() {
  const paused = useFlowStore((s) => s.paused);
  const callStack = useFlowStore((s) => s.callStack);
  const isRunning = useFlowStore((s) => s.isRunning);
  const debugAction = useFlowStore((s) => s.debugAction);

  // Show debug panel when paused or running
  if (!paused && !isRunning) return null;

  return (
    <div style={{
      position: 'absolute',
      left: 190,
      bottom: 10,
      width: 280,
      background: 'var(--fc-panel-bg)',
      border: '1px solid var(--fc-panel-border)',
      borderRadius: 8,
      overflow: 'hidden',
      zIndex: 10,
      boxShadow: 'var(--fc-shadow-sm)',
    }}>
      {/* Header */}
      <div style={{
        padding: '6px 10px',
        background: paused ? 'var(--fc-glow-error)' : 'var(--fc-header-bg)',
        borderBottom: '1px solid var(--fc-panel-border)',
        display: 'flex',
        alignItems: 'center',
        gap: 6,
      }}>
        {paused && (
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: 'var(--fc-state-error)',
            boxShadow: '0 0 6px var(--fc-glow-error)',
          }} />
        )}
        {isRunning && !paused && (
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: 'var(--fc-accent)',
            boxShadow: '0 0 6px var(--fc-glow-running-max)',
            animation: 'pulse 1s ease-in-out infinite',
          }} />
        )}
        <span style={{
          fontSize: 12,
          color: paused ? 'var(--fc-state-error)' : 'var(--fc-accent)',
          fontWeight: 600,
        }}>
          {paused ? 'PAUSED' : 'RUNNING'}
        </span>
      </div>

      {/* Controls */}
      <div style={{
        padding: '8px 10px',
        display: 'flex',
        gap: 4,
        flexWrap: 'wrap',
      }}>
        <button onClick={() => debugAction('continue')} disabled={!paused} style={ctrlBtn('var(--fc-state-success)', paused)}>
          ▶ Continue
        </button>
        <button onClick={() => debugAction('step')} disabled={!paused} style={ctrlBtn('var(--fc-accent)', paused)}>
          ⏭ Step
        </button>
        <button onClick={() => debugAction('stop')} style={ctrlBtn('var(--fc-state-error)', true)}>
          ⏹ Stop
        </button>
      </div>

      {/* Call Stack */}
      {callStack.length > 0 && (
        <div style={{ padding: '0 10px 8px', borderTop: '1px solid var(--fc-panel-border)', paddingTop: 8 }}>
          <div style={{ fontSize: 10, color: 'var(--fc-text-muted)', textTransform: 'uppercase', letterSpacing: '0.8px', marginBottom: 4 }}>
            Call Stack
          </div>
          <div style={{ fontFamily: 'monospace', fontSize: 11, lineHeight: 1.8, color: 'var(--fc-text-secondary)' }}>
            {callStack.map((entry, i) => (
              <div key={i} style={{
                color: i === 0 ? 'var(--fc-border-selected)' : 'var(--fc-text-muted)',
                background: i === 0 ? 'var(--fc-accent-surface)' : 'transparent',
                padding: '1px 4px',
                borderRadius: 3,
              }}>
                {i === 0 ? '→ ' : '  '}{entry}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// Re-export DebugState type for compatibility
export type DebugState = {
  paused: boolean;
  pausedAtStepId?: string;
  callStack: string[];
};

function ctrlBtn(color: string, enabled: boolean): React.CSSProperties {
  return {
    padding: '3px 8px',
    background: enabled ? 'var(--fc-surface-2)' : 'var(--fc-surface-1)',
    border: `1px solid ${enabled ? mix(color, 33) : 'var(--fc-border)'}`,
    borderRadius: 4,
    color: enabled ? color : 'var(--fc-text-disabled)',
    fontSize: 11,
    cursor: enabled ? 'pointer' : 'default',
    fontFamily: 'inherit',
    opacity: enabled ? 1 : 0.4,
  };
}
