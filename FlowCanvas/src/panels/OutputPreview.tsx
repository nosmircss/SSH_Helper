/**
 * Output preview overlay for a block's execution output.
 * Now supports per-block output history from the store.
 * Vertically resizable via a drag handle at the top edge.
 * Can be pinned open (default) — shows empty state when no block is selected.
 */
import { useState, useCallback, useRef, useEffect } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

interface OutputPreviewProps {
  output: string;
  onClose?: () => void;
  blockLabel?: string;
  nodeId?: string;
}

const MIN_HEIGHT = 80;
const MAX_HEIGHT = 600;

export default function OutputPreview({ output, onClose, blockLabel, nodeId }: OutputPreviewProps) {
  const blockOutputs = useFlowStore((s) => s.blockOutputs);
  const togglePanel = useFlowStore((s) => s.togglePanel);
  const storeHeight = useFlowStore((s) => s.panelSizes.outputHeight);
  const setPanelSize = useFlowStore((s) => s.setPanelSize);
  const [historyIndex, setHistoryIndex] = useState(-1); // -1 = latest
  const [height, setHeight] = useState(storeHeight);
  const heightRef = useRef(height);
  const dragging = useRef(false);
  const startY = useRef(0);
  const startHeight = useRef(0);

  // Keep ref in sync with state for the mouseup handler
  useEffect(() => { heightRef.current = height; }, [height]);

  // Sync from store when restored from WinForms
  useEffect(() => {
    setHeight(storeHeight);
  }, [storeHeight]);

  const allOutputs = nodeId ? blockOutputs.get(nodeId) || [] : [];
  const displayOutput = historyIndex >= 0 && historyIndex < allOutputs.length
    ? allOutputs[historyIndex].text
    : output;

  const handleClose = () => {
    if (onClose) onClose();
    else togglePanel('output');
  };

  // Reset history index when selected node changes
  useEffect(() => {
    setHistoryIndex(-1);
  }, [nodeId]);

  const onMouseDown = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    dragging.current = true;
    startY.current = e.clientY;
    startHeight.current = height;
  }, [height]);

  useEffect(() => {
    const onMouseMove = (e: MouseEvent) => {
      if (!dragging.current) return;
      // Dragging up increases height
      const delta = startY.current - e.clientY;
      const newHeight = Math.min(MAX_HEIGHT, Math.max(MIN_HEIGHT, startHeight.current + delta));
      setHeight(newHeight);
    };
    const onMouseUp = () => {
      if (dragging.current) {
        dragging.current = false;
        // Persist final height to store (which notifies WinForms)
        setPanelSize('outputHeight', heightRef.current);
      }
    };
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };
  }, [setPanelSize]);

  const headerHeight = 28;
  const hasOutput = nodeId && displayOutput;

  return (
    <div style={{
      height,
      flexShrink: 0,
      background: 'var(--fc-input-bg, #0d1117)',
      borderTop: '1px solid #2a3a5a',
      overflow: 'hidden',
      boxShadow: '0 -4px 20px rgba(0,0,0,0.4)',
      display: 'flex',
      flexDirection: 'column',
    }}>
      {/* Resize handle */}
      <div
        onMouseDown={onMouseDown}
        style={{
          height: 6,
          cursor: 'ns-resize',
          background: 'transparent',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
        }}
      >
        <div style={{
          width: 36,
          height: 3,
          borderRadius: 2,
          background: 'var(--fc-text-muted, #666)',
          opacity: 0.5,
        }} />
      </div>
      {/* Header */}
      <div style={{
        padding: '2px 10px',
        background: 'var(--fc-header-bg, #161b22)',
        borderBottom: '1px solid #21262d',
        display: 'flex',
        alignItems: 'center',
        fontSize: 12,
        height: headerHeight,
        flexShrink: 0,
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
        {hasOutput && (
          <button onClick={() => navigator.clipboard.writeText(displayOutput)} style={{
            background: 'none', border: 'none', color: 'var(--fc-text-muted, #666)',
            cursor: 'pointer', fontSize: 11, marginRight: 8,
          }}>Copy</button>
        )}
        <button onClick={handleClose} title="Unpin output panel" style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted, #666)',
          cursor: 'pointer', fontSize: 14, padding: 0,
        }}>×</button>
      </div>
      {/* Content */}
      <pre style={{
        margin: 0,
        padding: 8,
        fontSize: 11,
        color: hasOutput ? 'var(--fc-text, #c9d1d9)' : 'var(--fc-text-muted, #666)',
        lineHeight: 1.5,
        overflowY: 'auto',
        flex: 1,
        fontFamily: 'monospace',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-all',
      }}>
        {hasOutput ? displayOutput : (nodeId ? '(no output)' : 'Select a block to view its output')}
      </pre>
    </div>
  );
}
