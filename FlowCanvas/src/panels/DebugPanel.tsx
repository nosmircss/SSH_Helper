import { messageBus } from '../MessageBus';

export interface DebugState {
  paused: boolean;
  pausedAtStepId?: string;
  callStack: string[];
}

interface DebugPanelProps {
  debugState: DebugState;
  visible: boolean;
}

export default function DebugPanel({ debugState, visible }: DebugPanelProps) {
  if (!visible) return null;

  const handleContinue = () => messageBus.send({ type: 'debug-action', action: 'continue' });
  const handleStep = () => messageBus.send({ type: 'debug-action', action: 'step' });
  const handleStepInto = () => messageBus.send({ type: 'debug-action', action: 'step-into' });
  const handleStop = () => messageBus.send({ type: 'debug-action', action: 'stop' });

  return (
    <div style={{
      position: 'absolute',
      left: 190,
      bottom: 10,
      width: 280,
      background: '#12122a',
      border: '1px solid #2a2a4a',
      borderRadius: 8,
      overflow: 'hidden',
      zIndex: 10,
      boxShadow: '0 4px 20px rgba(0,0,0,0.4)',
    }}>
      {/* Header */}
      <div style={{
        padding: '6px 10px',
        background: debugState.paused ? '#2a1a1a' : '#1a1a3a',
        borderBottom: '1px solid #2a2a4a',
        display: 'flex',
        alignItems: 'center',
        gap: 6,
      }}>
        {debugState.paused && (
          <span style={{
            width: 8, height: 8, borderRadius: '50%',
            background: '#e74c3c',
            boxShadow: '0 0 6px rgba(231,76,60,0.6)',
          }} />
        )}
        <span style={{
          fontSize: 12,
          color: debugState.paused ? '#e74c3c' : '#888',
          fontWeight: 600,
        }}>
          {debugState.paused ? 'PAUSED' : 'Debugger'}
        </span>
      </div>

      {/* Controls */}
      <div style={{
        padding: '8px 10px',
        display: 'flex',
        gap: 4,
        flexWrap: 'wrap',
      }}>
        <button onClick={handleContinue} disabled={!debugState.paused} style={ctrlBtn('#2ecc71', debugState.paused)}>
          ▶ Continue
        </button>
        <button onClick={handleStep} disabled={!debugState.paused} style={ctrlBtn('#4a9eff', debugState.paused)}>
          ⏭ Step
        </button>
        <button onClick={handleStepInto} disabled={!debugState.paused} style={ctrlBtn('#9b59b6', debugState.paused)}>
          ⏬ Into
        </button>
        <button onClick={handleStop} style={ctrlBtn('#e74c3c', true)}>
          ⏹ Stop
        </button>
      </div>

      {/* Call Stack */}
      {debugState.callStack.length > 0 && (
        <div style={{ padding: '0 10px 8px', borderTop: '1px solid #2a2a4a', paddingTop: 8 }}>
          <div style={{ fontSize: 10, color: '#666', textTransform: 'uppercase', letterSpacing: '0.8px', marginBottom: 4 }}>
            Call Stack
          </div>
          <div style={{ fontFamily: 'monospace', fontSize: 11, lineHeight: 1.8, color: '#888' }}>
            {debugState.callStack.map((entry, i) => (
              <div key={i} style={{
                color: i === 0 ? '#fff' : '#666',
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
