import { messageBus } from '../MessageBus';
import { useReactFlow } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { useAutoLayout } from '../hooks/useAutoLayout';
import { CANVAS_HOST_MESSAGES } from '../communication-message-types';
import { buildExecutableGraphPayload } from '../utils/exportGraph';

export default function Toolbar() {
  const { getNodes, getEdges } = useReactFlow();
  const selectedNodeIds = useFlowStore((s) => s.selectedNodeIds);
  const variablesVisible = useFlowStore((s) => s.panelsVisible.variables);
  const timelineVisible = useFlowStore((s) => s.panelsVisible.timeline);
  const outputVisible = useFlowStore((s) => s.panelsVisible.output);
  const togglePanel = useFlowStore((s) => s.togglePanel);
  const canUndo = useFlowStore((s) => s.past.length > 0);
  const canRedo = useFlowStore((s) => s.future.length > 0);
  const undo = useFlowStore((s) => s.undo);
  const redo = useFlowStore((s) => s.redo);
  const snapToGrid = useFlowStore((s) => s.snapToGrid);
  const toggleSnapToGrid = useFlowStore((s) => s.toggleSnapToGrid);
  const reducedMotion = useFlowStore((s) => s.reducedMotion);
  const toggleReducedMotion = useFlowStore((s) => s.toggleReducedMotion);
  const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
  const toggleHeatmap = useFlowStore((s) => s.toggleHeatmap);
  const problemsVisible = useFlowStore((s) => s.panelsVisible.problems);
  const diagnostics = useFlowStore((s) => s.diagnostics);
  const errorCount = diagnostics.filter((d) => d.severity === 'error').length;
  const toggleSearch = useFlowStore((s) => s.toggleSearch);
  const searchVisible = useFlowStore((s) => s.searchVisible);

  const isRunning = useFlowStore((s) => s.isRunning);
  const paused = useFlowStore((s) => s.paused);
  const debugAction = useFlowStore((s) => s.debugAction);
  const autoLayout = useAutoLayout();

  const selectedNodeId = selectedNodeIds.size === 1 ? [...selectedNodeIds][0] : null;
  const exportStatus = useFlowStore((s) => s.exportStatus);
  const targetHost = useFlowStore((s) => s.targetHost);

  const isDirty = useFlowStore((s) => s.isDirty);

  const getExportData = () => {
    return buildExecutableGraphPayload(getNodes(), getEdges());
  };

  const handleApplyYaml = () => {
    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.applyYaml,
      graphChanged: true,
      ...getExportData(),
    });
  };

  const handleTestStep = () => {
    if (!selectedNodeId) return;
    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.executeCanvas,
      mode: 'test-step',
      stepId: selectedNodeId,
      graphChanged: isDirty,
      ...getExportData(),
    });
  };

  const handleRun = () => {
    messageBus.send({
      type: CANVAS_HOST_MESSAGES.outgoing.executeCanvas,
      mode: 'run',
      graphChanged: isDirty,
      ...getExportData(),
    });
  };

  const headerBg = 'var(--fc-header-bg)';
  const borderColor = 'var(--fc-border)';
  const labelColor = 'var(--fc-text-disabled)';

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
      <button
        onClick={handleRun}
        disabled={isRunning || exportStatus.hasErrors || !targetHost}
        style={btnStyle('var(--fc-state-success)', !isRunning && !exportStatus.hasErrors && !!targetHost)}
        title={
          exportStatus.hasErrors
            ? 'Fix export errors before run'
            : targetHost
              ? `Run on ${targetHost.ip} (F5)`
              : 'No target host — select a host in the main grid (F5)'
        }
      >
        ▶ Run
      </button>
      <button
        onClick={handleTestStep}
        disabled={!selectedNodeId || isRunning || exportStatus.hasErrors}
        style={btnStyle(
          selectedNodeId && !isRunning && !exportStatus.hasErrors ? 'var(--fc-state-warning)' : 'var(--fc-text-disabled)',
          !!selectedNodeId && !isRunning && !exportStatus.hasErrors,
        )}
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
            background: paused ? 'var(--fc-glow-error)' : 'var(--fc-glow-success)',
            borderRadius: 4,
            border: `1px solid ${paused ? 'var(--fc-glow-error)' : 'var(--fc-glow-success)'}`,
          }}>
            {paused && (
              <span style={{ fontSize: 9, color: 'var(--fc-state-error)', fontWeight: 700, marginRight: 4 }}>
                ⏸ PAUSED
              </span>
            )}
            {!paused && (
              <span style={{ fontSize: 9, color: 'var(--fc-state-success)', fontWeight: 700, marginRight: 4 }}>
                ● RUNNING
              </span>
            )}
            <button
              onClick={() => debugAction('continue')}
              disabled={!paused}
              style={debugBtnStyle('var(--fc-state-success)', paused)}
              title="Continue to next breakpoint"
            >
              ▶ Continue
            </button>
            <button
              onClick={() => debugAction('step')}
              disabled={!paused}
              style={debugBtnStyle('var(--fc-accent)', paused)}
              title="Step to next block (F10)"
            >
              ⏭ Step
            </button>
            <button
              onClick={() => debugAction('stop')}
              style={debugBtnStyle('var(--fc-state-error)', true)}
              title="Stop execution"
            >
              ⏹ Stop
            </button>
          </div>
        </>
      )}

      <Separator color={borderColor} />

      {/* Edit controls */}
      <button onClick={undo} disabled={!canUndo} style={btnStyle('var(--fc-accent)', canUndo)} title="Undo (Ctrl+Z)">
        ↩
      </button>
      <button onClick={redo} disabled={!canRedo} style={btnStyle('var(--fc-accent)', canRedo)} title="Redo (Ctrl+Y)">
        ↪
      </button>

      <Separator color={borderColor} />

      {/* Canvas controls */}
      <button onClick={autoLayout} style={btnStyle('var(--fc-cat-data-border)', true)} title="Auto-organize layout">
        ⊞ Layout
      </button>
      <button onClick={toggleSearch} style={btnStyle(searchVisible ? 'var(--fc-accent)' : 'var(--fc-text-muted)', true)} title="Search blocks (Ctrl+F)">
        🔍
      </button>
      <button onClick={toggleSnapToGrid} style={btnStyle(snapToGrid ? 'var(--fc-state-success)' : 'var(--fc-text-muted)', true)} title="Snap to grid">
        ⊡ {snapToGrid ? 'Snap' : 'Free'}
      </button>
      <button
        onClick={toggleReducedMotion}
        style={btnStyle(reducedMotion ? 'var(--fc-state-success)' : 'var(--fc-text-muted)', true)}
        title={reducedMotion ? 'Motion reduced — click to enable animations' : 'Reduce motion — disable animations'}
      >
        {reducedMotion ? '⏸ Calm' : '▶ Motion'}
      </button>

      <Separator color={borderColor} />

      {/* Panel toggles */}
      <button onClick={handleApplyYaml} style={btnStyle('var(--fc-accent)', true)} title="Apply graph to YAML editor">
        Apply YAML
      </button>
      <button
        onClick={() => togglePanel('variables')}
        style={btnStyle(variablesVisible ? 'var(--fc-state-warning)' : 'var(--fc-text-muted)', true)}
        title="Toggle variable inspector"
      >
        {variablesVisible ? '🔍 Vars' : '🔍 Vars'}
      </button>
      <button
        onClick={() => togglePanel('output')}
        style={btnStyle(outputVisible ? 'var(--fc-accent)' : 'var(--fc-text-muted)', true)}
        title="Toggle output panel"
      >
        ▤ Output
      </button>
      <button
        onClick={() => togglePanel('timeline')}
        style={btnStyle(timelineVisible ? 'var(--fc-cat-data-border)' : 'var(--fc-text-muted)', true)}
        title="Toggle execution timeline"
      >
        ⏱ Timeline
      </button>
      <button
        onClick={toggleHeatmap}
        style={btnStyle(heatmapEnabled ? 'var(--fc-accent)' : 'var(--fc-text-muted)', true)}
        title="Toggle run heatmap (color blocks by duration)"
      >
        🔥 Heatmap
      </button>
      <button
        onClick={() => togglePanel('problems')}
        style={btnStyle(problemsVisible ? 'var(--fc-accent)' : 'var(--fc-text-muted)', true)}
        title="Toggle Problems panel (click a row to jump to the block)"
      >
        ⚠ Problems
        {errorCount > 0 && (
          <span style={{
            marginLeft: 4,
            padding: '0 5px',
            borderRadius: 8,
            background: 'var(--fc-diag-error)',
            color: 'var(--fc-on-accent)',
            fontSize: 10,
            fontWeight: 700,
          }}>
            {errorCount}
          </span>
        )}
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
    background: enabled ? 'var(--fc-surface-2)' : 'var(--fc-surface-1)',
    border: `1px solid ${enabled ? color + '55' : 'var(--fc-border)'}`,
    borderRadius: 4,
    color: enabled ? color : 'var(--fc-text-disabled)',
    fontSize: 12,
    cursor: enabled ? 'pointer' : 'default',
    fontFamily: 'inherit',
    opacity: enabled ? 1 : 0.4,
  };
}

function debugBtnStyle(color: string, enabled: boolean): React.CSSProperties {
  return {
    padding: '3px 10px',
    background: enabled ? 'var(--fc-surface-1)' : 'var(--fc-surface-0)',
    border: `1px solid ${enabled ? color : 'var(--fc-surface-3)'}`,
    borderRadius: 4,
    color: enabled ? color : 'var(--fc-text-disabled)',
    fontSize: 11,
    fontWeight: 600,
    cursor: enabled ? 'pointer' : 'default',
    fontFamily: 'inherit',
    opacity: enabled ? 1 : 0.3,
  };
}
