import { messageBus } from '../MessageBus';
import { useReactFlow } from '@xyflow/react';

interface ToolbarProps {
  selectedNodeId: string | null;
  variablesVisible: boolean;
  onToggleVariables: () => void;
}

export default function Toolbar({ selectedNodeId, variablesVisible, onToggleVariables }: ToolbarProps) {
  const { getNodes, getEdges } = useReactFlow();

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
    // Apply graph first, then request test step
    messageBus.send({ type: 'apply-yaml', ...getExportData() });
    setTimeout(() => messageBus.send({ type: 'test-step', stepId: selectedNodeId }), 50);
  };

  const handleRun = () => {
    // First apply the current graph as YAML, then request execution
    messageBus.send({ type: 'apply-yaml', ...getExportData() });
    // Small delay to let YAML apply before triggering run
    setTimeout(() => messageBus.send({ type: 'run-request' }), 50);
  };

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      padding: '4px 12px',
      background: '#16162a',
      borderBottom: '1px solid #2a2a4a',
      gap: 6,
      fontSize: 12,
      flexShrink: 0,
    }}>
      <button onClick={handleRun} style={btnStyle('#2ecc71')}>
        ▶ Run
      </button>
      <button
        onClick={handleTestStep}
        disabled={!selectedNodeId}
        style={btnStyle(selectedNodeId ? '#f0c040' : '#555')}
        title="Execute this step and prerequisites on a test host (Ctrl+Enter)"
      >
        ⏩ Test Step
      </button>
      <div style={{ width: 1, height: 16, background: '#2a2a4a', margin: '0 4px' }} />
      <button onClick={handleApplyYaml} style={btnStyle('#4a9eff')}>
        Apply to YAML
      </button>
      <button
        onClick={onToggleVariables}
        style={btnStyle(variablesVisible ? '#e0c040' : '#888')}
      >
        {variablesVisible ? '🔍 Hide Vars' : '🔍 Variables'}
      </button>
      <div style={{ flex: 1 }} />
      <span style={{ color: '#555', fontSize: 11 }}>Flow Canvas</span>
    </div>
  );
}

function btnStyle(color: string): React.CSSProperties {
  return {
    padding: '4px 12px',
    background: '#222244',
    border: `1px solid ${color}55`,
    borderRadius: 4,
    color,
    fontSize: 12,
    cursor: 'pointer',
    fontFamily: 'inherit',
    opacity: color === '#555' ? 0.5 : 1,
  };
}
