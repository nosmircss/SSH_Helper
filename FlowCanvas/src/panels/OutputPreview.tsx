/**
 * Output preview overlay for a block's execution output.
 * Now supports per-block output history from the store.
 */
import { useState } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

interface OutputPreviewProps {
  output: string;
  onClose?: () => void;
  blockLabel?: string;
  nodeId?: string;
}

export default function OutputPreview({ output, onClose, blockLabel, nodeId }: OutputPreviewProps) {
  const blockOutputs = useFlowStore((s) => s.blockOutputs);
  const clearSelection = useFlowStore((s) => s.clearSelection);
  const [historyIndex, setHistoryIndex] = useState(-1); // -1 = latest

  const allOutputs = nodeId ? blockOutputs.get(nodeId) || [] : [];
  const displayOutput = historyIndex >= 0 && historyIndex < allOutputs.length
    ? allOutputs[historyIndex].text
    : output;

  const handleClose = () => {
    if (onClose) onClose();
    else clearSelection();
  };

  return (
    <div style={{
      position: 'absolute',
      left: 0,
      right: 0,
      bottom: 0,
      maxHeight: 200,
      background: 'var(--fc-input-bg, #0d1117)',
      border: '1px solid #2a3a5a',
      borderRadius: '8px 8px 0 0',
      overflow: 'hidden',
      zIndex: 10,
      boxShadow: '0 -4px 20px rgba(0,0,0,0.4)',
    }}>
      <div style={{
        padding: '4px 10px',
        background: 'var(--fc-header-bg, #161b22)',
        borderBottom: '1px solid #21262d',
        display: 'flex',
        alignItems: 'center',
        fontSize: 12,
      }}>
        <span style={{ color: 'var(--fc-text-muted, #666)' }}>Output</span>
        {blockLabel && (
          <span style={{ color: '#4a9eff', marginLeft: 8, fontSize: 11 }}>
            {blockLabel}
          </span>
        )}
        {allOutputs.length > 1 && (
          <span style={{ color: 'var(--fc-text-muted, #666)', marginLeft: 8, fontSize: 10 }}>
            ({historyIndex >= 0 ? historyIndex + 1 : allOutputs.length}/{allOutputs.length})
            <button
              onClick={() => setHistoryIndex(Math.max(0, (historyIndex < 0 ? allOutputs.length - 1 : historyIndex) - 1))}
              style={{ background: 'none', border: 'none', color: '#4a9eff', cursor: 'pointer', fontSize: 10, padding: '0 4px' }}
            >◀</button>
            <button
              onClick={() => {
                const next = (historyIndex < 0 ? allOutputs.length : historyIndex) + 1;
                setHistoryIndex(next >= allOutputs.length ? -1 : next);
              }}
              style={{ background: 'none', border: 'none', color: '#4a9eff', cursor: 'pointer', fontSize: 10, padding: '0 4px' }}
            >▶</button>
          </span>
        )}
        <div style={{ flex: 1 }} />
        <button onClick={() => navigator.clipboard.writeText(displayOutput)} style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted, #666)',
          cursor: 'pointer', fontSize: 11, marginRight: 8,
        }}>Copy</button>
        <button onClick={handleClose} style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted, #666)',
          cursor: 'pointer', fontSize: 14, padding: 0,
        }}>×</button>
      </div>
      <pre style={{
        margin: 0,
        padding: 8,
        fontSize: 11,
        color: 'var(--fc-text, #c9d1d9)',
        lineHeight: 1.5,
        overflowY: 'auto',
        maxHeight: 160,
        fontFamily: 'monospace',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-all',
      }}>
        {displayOutput || '(no output)'}
      </pre>
    </div>
  );
}
