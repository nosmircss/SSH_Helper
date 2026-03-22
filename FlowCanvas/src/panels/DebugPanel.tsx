import { useFlowStore } from '../stores/useFlowStore';

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
      background: 'var(--fc-panel-bg, #12122a)',
      border: '1px solid var(--fc-panel-border, #2a2a4a)',
      borderRadius: 8,
      overflow: 'hidden',
      zIndex: 10,
      boxShadow: '0 4px 20px rgba(0,0,0,0.4)',
    }}>
      {/* Header */}
      <div style={{
        padding: '6px 10px',
        background: paused ? '#2a1a1a' : 'var(--fc-header-bg, #1a1a3a)',
        borderBottom: '1px solid var(--fc-panel-border, #2a2a4a)',
        display: 'flex',
        alignItems: 'center',
        gap: 6,
      }}>
        {paused && (
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: '#e74c3c',
            boxShadow: '0 0 6px rgba(231,76,60,0.6)',
          }} />
        )}
        {isRunning && !paused && (
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: '#4a9eff',
            boxShadow: '0 0 6px rgba(74,158,255,0.6)',
            animation: 'pulse 1s ease-in-out infinite',
          }} />
        )}
        <span style={{
          fontSize: 12,
          color: paused ? '#e74c3c' : '#4a9eff',
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
        <button onClick={() => debugAction('continue')} disabled={!paused} style={ctrlBtn('#2ecc71', paused)}>
          ▶ Continue
        </button>
        <button onClick={() => debugAction('step')} disabled={!paused} style={ctrlBtn('#4a9eff', paused)}>
          ⏭ Step
        </button>
        <button onClick={() => debugAction('step-into')} disabled={!paused} style={ctrlBtn('#9b59b6', paused)}>
          ⏬ Into
        </button>
        <button onClick={() => debugAction('stop')} style={ctrlBtn('#e74c3c', true)}>
          ⏹ Stop
        </button>
      </div>

      {/* Call Stack */}
      {callStack.length > 0 && (
        <div style={{ padding: '0 10px 8px', borderTop: '1px solid var(--fc-panel-border, #2a2a4a)', paddingTop: 8 }}>
          <div style={{ fontSize: 10, color: 'var(--fc-text-muted, #666)', textTransform: 'uppercase', letterSpacing: '0.8px', marginBottom: 4 }}>
            Call Stack
          </div>
          <div style={{ fontFamily: 'monospace', fontSize: 11, lineHeight: 1.8, color: 'var(--fc-text-secondary, #888)' }}>
            {callStack.map((entry, i) => (
              <div key={i} style={{
                color: i === 0 ? 'var(--fc-text, #fff)' : 'var(--fc-text-muted, #666)',
                background: i === 0 ? '#1a2744' : 'transparent',
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
    background: enabled ? '#222244' : '#1a1a2e',
    border: `1px solid ${enabled ? color + '55' : '#2a2a4a'}`,
    borderRadius: 4,
    color: enabled ? color : '#444',
    fontSize: 11,
    cursor: enabled ? 'pointer' : 'default',
    fontFamily: 'inherit',
    opacity: enabled ? 1 : 0.4,
  };
}
