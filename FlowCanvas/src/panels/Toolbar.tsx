import { messageBus } from '../MessageBus';
import { useReactFlow } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { useAutoLayout } from '../hooks/useAutoLayout';

export default function Toolbar() {
  const { getNodes, getEdges } = useReactFlow();
  const selectedNodeIds = useFlowStore((s) => s.selectedNodeIds);
  const variablesVisible = useFlowStore((s) => s.panelsVisible.variables);
  const timelineVisible = useFlowStore((s) => s.panelsVisible.timeline);
  const togglePanel = useFlowStore((s) => s.togglePanel);
  const canUndo = useFlowStore((s) => s.past.length > 0);
  const canRedo = useFlowStore((s) => s.future.length > 0);
  const undo = useFlowStore((s) => s.undo);
  const redo = useFlowStore((s) => s.redo);
  const snapToGrid = useFlowStore((s) => s.snapToGrid);
  const toggleSnapToGrid = useFlowStore((s) => s.toggleSnapToGrid);
  const toggleSearch = useFlowStore((s) => s.toggleSearch);
  const searchVisible = useFlowStore((s) => s.searchVisible);
  const theme = useFlowStore((s) => s.theme);
  const toggleTheme = useFlowStore((s) => s.toggleTheme);
  const isRunning = useFlowStore((s) => s.isRunning);
  const paused = useFlowStore((s) => s.paused);
  const debugAction = useFlowStore((s) => s.debugAction);
  const autoLayout = useAutoLayout();

  const selectedNodeId = selectedNodeIds.size === 1 ? [...selectedNodeIds][0] : null;

  /** Filter out visual-only child nodes before sending to C# for YAML export. */
  const getExportData = () => {
    const allNodes = getNodes();
    const exportNodes = allNodes.filter(n => !(n.data as Record<string, unknown>)?.props || !((n.data as Record<string, unknown>).props as Record<string, unknown>)?._isChildOf);
    return { nodes: exportNodes, edges: getEdges() };
  };

  const handleApplyYaml = () => {
    messageBus.send({ type: 'apply-yaml', ...getExportData() });
  };

  const handleTestStep = () => {
    if (!selectedNodeId) return;
    messageBus.send({ type: 'apply-yaml', ...getExportData() });
    setTimeout(() => messageBus.send({ type: 'test-step', stepId: selectedNodeId }), 50);
  };

  const handleRun = () => {
    messageBus.send({ type: 'apply-yaml', ...getExportData() });
    setTimeout(() => messageBus.send({ type: 'run-request' }), 50);
  };

  const isDark = theme === 'dark';
  const headerBg = isDark ? '#16162a' : '#f0f0f5';
  const borderColor = isDark ? '#2a2a4a' : '#d0d0d8';
  const labelColor = isDark ? '#555' : '#999';

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      padding: '4px 12px',
      background: headerBg,
      borderBottom: `1px solid ${borderColor}`,
      gap: 6,
      fontSize: 12,
      flexShrink: 0,
    }}>
      {/* Execution controls */}
      <button onClick={handleRun} disabled={isRunning} style={btnStyle('#2ecc71', !isRunning)} title="Run script (F5)">
        ▶ Run
      </button>
      <button
        onClick={handleTestStep}
        disabled={!selectedNodeId || isRunning}
        style={btnStyle(selectedNodeId && !isRunning ? '#f0c040' : '#555', !!selectedNodeId && !isRunning)}
        title="Test selected step (Ctrl+Enter)"
      >
        ⏩ Test Step
      </button>

      {/* Debug controls — visible when running or paused */}
      {(isRunning || paused) && (
        <>
          <Separator color={borderColor} />
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            padding: '2px 6px',
            background: paused ? '#2a1a1a' : '#1a2a1a',
            borderRadius: 4,
            border: `1px solid ${paused ? '#e74c3c44' : '#2ecc7144'}`,
          }}>
            {paused && (
              <span style={{ fontSize: 9, color: '#e74c3c', fontWeight: 700, marginRight: 4 }}>
                ⏸ PAUSED
              </span>
            )}
            {!paused && (
              <span style={{ fontSize: 9, color: '#2ecc71', fontWeight: 700, marginRight: 4 }}>
                ● RUNNING
              </span>
            )}
            <button
              onClick={() => debugAction('continue')}
              disabled={!paused}
              style={debugBtnStyle('#2ecc71', paused)}
              title="Continue to next breakpoint"
            >
              ▶ Continue
            </button>
            <button
              onClick={() => debugAction('step')}
              disabled={!paused}
              style={debugBtnStyle('#4a9eff', paused)}
              title="Step to next block (F10)"
            >
              ⏭ Step
            </button>
            <button
              onClick={() => debugAction('step-into')}
              disabled={!paused}
              style={debugBtnStyle('#9b59b6', paused)}
              title="Step into subroutine (F11)"
            >
              ⏬ Into
            </button>
            <button
              onClick={() => debugAction('stop')}
              style={debugBtnStyle('#e74c3c', true)}
              title="Stop execution"
            >
              ⏹ Stop
            </button>
          </div>
        </>
      )}

      <Separator color={borderColor} />

      {/* Edit controls */}
      <button onClick={undo} disabled={!canUndo} style={btnStyle('#4a9eff', canUndo)} title="Undo (Ctrl+Z)">
        ↩
      </button>
      <button onClick={redo} disabled={!canRedo} style={btnStyle('#4a9eff', canRedo)} title="Redo (Ctrl+Y)">
        ↪
      </button>

      <Separator color={borderColor} />

      {/* Canvas controls */}
      <button onClick={autoLayout} style={btnStyle('#9b59b6', true)} title="Auto-organize layout">
        ⊞ Layout
      </button>
      <button onClick={toggleSearch} style={btnStyle(searchVisible ? '#4a9eff' : '#888', true)} title="Search blocks (Ctrl+F)">
        🔍
      </button>
      <button onClick={toggleSnapToGrid} style={btnStyle(snapToGrid ? '#2ecc71' : '#888', true)} title="Snap to grid">
        ⊡ {snapToGrid ? 'Snap' : 'Free'}
      </button>

      <Separator color={borderColor} />

      {/* Panel toggles */}
      <button onClick={handleApplyYaml} style={btnStyle('#4a9eff', true)} title="Apply graph to YAML editor">
        Apply YAML
      </button>
      <button
        onClick={() => togglePanel('variables')}
        style={btnStyle(variablesVisible ? '#e0c040' : '#888', true)}
        title="Toggle variable inspector"
      >
        {variablesVisible ? '🔍 Vars' : '🔍 Vars'}
      </button>
      <button
        onClick={() => togglePanel('timeline')}
        style={btnStyle(timelineVisible ? '#9b59b6' : '#888', true)}
        title="Toggle execution timeline"
      >
        ⏱ Timeline
      </button>
      <button onClick={toggleTheme} style={btnStyle('#888', true)} title="Toggle dark/light theme">
        {theme === 'dark' ? '☀' : '🌙'}
      </button>

      <div style={{ flex: 1 }} />
      <span style={{ color: labelColor, fontSize: 11 }}>Flow Canvas v2</span>
    </div>
  );
}

function Separator({ color }: { color: string }) {
  return <div style={{ width: 1, height: 16, background: color, margin: '0 2px' }} />;
}

function btnStyle(color: string, enabled: boolean): React.CSSProperties {
  return {
    padding: '4px 8px',
    background: enabled ? '#222244' : '#1a1a2e',
    border: `1px solid ${enabled ? color + '55' : '#2a2a4a'}`,
    borderRadius: 4,
    color: enabled ? color : '#444',
    fontSize: 12,
    cursor: enabled ? 'pointer' : 'default',
    fontFamily: 'inherit',
    opacity: enabled ? 1 : 0.4,
  };
}

function debugBtnStyle(color: string, enabled: boolean): React.CSSProperties {
  return {
    padding: '3px 10px',
    background: enabled ? '#1a1a2e' : '#111',
    border: `1px solid ${enabled ? color : '#333'}`,
    borderRadius: 4,
    color: enabled ? color : '#444',
    fontSize: 11,
    fontWeight: 600,
    cursor: enabled ? 'pointer' : 'default',
    fontFamily: 'inherit',
    opacity: enabled ? 1 : 0.3,
  };
}
