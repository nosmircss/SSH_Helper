import { messageBus } from '../MessageBus';
import { useReactFlow } from '@xyflow/react';

export default function Toolbar() {
  const { getNodes, getEdges } = useReactFlow();

  const handleApplyYaml = () => {
    const nodes = getNodes();
    const edges = getEdges();
    messageBus.send({
      type: 'apply-yaml',
      nodes,
      edges,
    });
  };

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      padding: '4px 12px',
      background: '#16162a',
      borderBottom: '1px solid #2a2a4a',
      gap: 8,
      fontSize: 12,
      flexShrink: 0,
    }}>
      <button onClick={handleApplyYaml} style={btnStyle('#4a9eff')}>
        Apply to YAML
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
  };
}
