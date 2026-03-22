/**
 * Output preview overlay for a block's execution output.
 * Shows inline on blocks after Test Step execution.
 */

interface OutputPreviewProps {
  output: string;
  onClose: () => void;
  blockLabel?: string;
}

export default function OutputPreview({ output, onClose, blockLabel }: OutputPreviewProps) {
  return (
    <div style={{
      position: 'absolute',
      left: 0,
      right: 240,
      bottom: 0,
      maxHeight: 200,
      background: '#0d1117',
      border: '1px solid #2a3a5a',
      borderRadius: '8px 8px 0 0',
      overflow: 'hidden',
      zIndex: 10,
      boxShadow: '0 -4px 20px rgba(0,0,0,0.4)',
    }}>
      <div style={{
        padding: '4px 10px',
        background: '#161b22',
        borderBottom: '1px solid #21262d',
        display: 'flex',
        alignItems: 'center',
        fontSize: 12,
      }}>
        <span style={{ color: '#666' }}>Output</span>
        {blockLabel && (
          <span style={{ color: '#4a9eff', marginLeft: 8, fontSize: 11 }}>
            {blockLabel}
          </span>
        )}
        <div style={{ flex: 1 }} />
        <button onClick={() => navigator.clipboard.writeText(output)} style={{
          background: 'none', border: 'none', color: '#666',
          cursor: 'pointer', fontSize: 11, marginRight: 8,
        }}>Copy</button>
        <button onClick={onClose} style={{
          background: 'none', border: 'none', color: '#666',
          cursor: 'pointer', fontSize: 14, padding: 0,
        }}>×</button>
      </div>
      <pre style={{
        margin: 0,
        padding: 8,
        fontSize: 11,
        color: '#c9d1d9',
        lineHeight: 1.5,
        overflowY: 'auto',
        maxHeight: 160,
        fontFamily: 'monospace',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-all',
      }}>
        {output || '(no output)'}
      </pre>
    </div>
  );
}
