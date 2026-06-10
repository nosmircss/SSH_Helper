/**
 * Output preview overlay for a block's execution output.
 * Now supports per-block output history from the store.
 * Vertically resizable via a drag handle at the top edge.
 * Can be pinned open (default) — shows empty state when no block is selected.
 * Tab strip selects between block output and the full run output console.
 */
import { useState, useCallback, useRef, useEffect, useMemo, type ReactNode } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { selectIterationScope, selectVisibleIterations, LOOP_TYPES } from '../stores/selectors/iterationScope';
import RunOutputView from './RunOutputView';

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
  const outputTab = useFlowStore((s) => s.outputTab);
  const setOutputTab = useFlowStore((s) => s.setOutputTab);
  const runOutputUnread = useFlowStore((s) => s.runOutputUnread);
  const poppedOut = useFlowStore((s) => s.runOutputPoppedOut);
  const closeWindow = useFlowStore((s) => s.closeRunOutputWindow);
  const [historyIndex, setHistoryIndex] = useState(-1); // -1 = latest
  const iterScope = useFlowStore((s) => (nodeId ? selectIterationScope(s, nodeId) : null));
  // Stable map/array refs for the iteration-context chip memo (mirrors VariableInspector):
  // feeding a freshly-allocated selector result straight into useFlowStore would re-render
  // on every store change, so subscribe to the raw refs and derive in a useMemo.
  const nodes = useFlowStore((s) => s.nodes);
  const log = useFlowStore((s) => s.iterationLog);
  const sels = useFlowStore((s) => s.iterationSelections);
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

  // Iteration scoping honesty: when a governing iteration record exists but this node has no
  // entry in it (the node was never reached that iteration), the per-block panel must say so
  // rather than silently falling back to the latest output.
  const scopedEntry = nodeId && iterScope ? iterScope.nodes.get(nodeId) : undefined;
  const scopedNoOutput = !!iterScope && !!nodeId && scopedEntry?.outputIdx == null;

  // The selected node's own block type — used for the loop-container affordance.
  const selectedBlockType = nodeId
    ? (() => {
        const data = (nodes.find((n) => n.id === nodeId)?.data ?? {}) as Record<string, unknown>;
        return typeof data.blockType === 'string' ? data.blockType : undefined;
      })()
    : undefined;
  const isLoopNodeSelected = !!selectedBlockType && LOOP_TYPES.has(selectedBlockType);
  const loopHasSelection = isLoopNodeSelected && nodeId != null && sels.get(nodeId) != null;

  // Iteration-context chip: position of the governing record within its loop's visible
  // iterations, plus its label. iterScope is the record but not its loopId, so locate the
  // owning loop by scanning the log for the record's seq (cheap — a few short arrays).
  const iterChip = useMemo(() => {
    if (!iterScope) return null;
    const state = useFlowStore.getState();
    let loopId: string | undefined;
    for (const [lid, records] of state.iterationLog) {
      if (records.some((r) => r.seq === iterScope.seq)) { loopId = lid; break; }
    }
    if (!loopId) return null;
    const visible = selectVisibleIterations(state, loopId);
    const pos = visible.findIndex((r) => r.seq === iterScope.seq);
    return { pos, total: visible.length, label: iterScope.label };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [iterScope, log, sels, nodes]);

  const handleClose = () => {
    if (onClose) onClose();
    else togglePanel('output');
  };

  // Iteration stepper sync: a selected iteration pins the viewer to that iteration's
  // output entry; returning to ALL (or changing the selected node) returns to the latest.
  useEffect(() => {
    if (!nodeId) return;
    if (!iterScope) { setHistoryIndex(-1); return; }
    const idx = iterScope.nodes.get(nodeId)?.outputIdx;
    setHistoryIndex(idx != null ? idx : -1);
  }, [iterScope, nodeId]);

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
      background: 'var(--fc-term-bg)',
      borderTop: '1px solid var(--fc-border)',
      overflow: 'hidden',
      boxShadow: 'var(--fc-shadow-sm)',
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
          background: 'var(--fc-text-muted)',
          opacity: 0.5,
        }} />
      </div>

      {/* Tab strip */}
      <div style={{
        display: 'flex', alignItems: 'stretch', height: 26, flexShrink: 0,
        background: 'var(--fc-term-surface)', borderBottom: '1px solid var(--fc-term-surface-2)',
      }}>
        <TabButton testid="output-tab-block" active={outputTab === 'block'} onClick={() => setOutputTab('block')}>
          Block Output
        </TabButton>
        <TabButton testid="output-tab-run" active={outputTab === 'run'} onClick={() => (poppedOut ? closeWindow() : setOutputTab('run'))}>
          Run Output
          {runOutputUnread && outputTab !== 'run' && (
            <span data-testid="output-tab-run-unread" style={{
              marginLeft: 6, width: 6, height: 6, borderRadius: '50%',
              background: 'var(--fc-accent)', display: 'inline-block',
            }} />
          )}
        </TabButton>
        <div style={{ flex: 1 }} />
        <button onClick={handleClose} title="Unpin output panel" style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted)', cursor: 'pointer', fontSize: 14, padding: '0 8px',
        }}>×</button>
      </div>

      {outputTab === 'block' ? (
        <>
          {/* Header (block) */}
          <div style={{
            padding: '2px 10px',
            background: 'var(--fc-term-surface)',
            borderBottom: '1px solid var(--fc-term-surface-2)',
            display: 'flex',
            alignItems: 'center',
            fontSize: 12,
            height: headerHeight,
            flexShrink: 0,
          }}>
            <span style={{ color: 'var(--fc-text-muted)' }}>Output</span>
            {blockLabel && (
              <span style={{ color: 'var(--fc-accent)', marginLeft: 8, fontSize: 11 }}>
                {blockLabel}
              </span>
            )}
            {iterChip && (
              <span
                data-testid="iter-output-chip"
                style={{
                  marginLeft: 8,
                  padding: '0 6px',
                  borderRadius: 3,
                  fontSize: 10,
                  color: 'var(--fc-text-secondary)',
                  border: '1px solid var(--fc-term-surface-2)',
                  whiteSpace: 'nowrap',
                }}
              >
                {`⏱ ${iterChip.pos + 1}/${iterChip.total}`}{iterChip.label ? ` · ${iterChip.label}` : ''}
              </span>
            )}
            {allOutputs.length > 1 && (
              <span style={{ color: 'var(--fc-text-muted)', marginLeft: 8, fontSize: 10 }}>
                ({historyIndex >= 0 ? historyIndex + 1 : allOutputs.length}/{allOutputs.length})
                <button
                  onClick={() => setHistoryIndex(Math.max(0, (historyIndex < 0 ? allOutputs.length - 1 : historyIndex) - 1))}
                  style={{ background: 'none', border: 'none', color: 'var(--fc-accent)', cursor: 'pointer', fontSize: 10, padding: '0 4px' }}
                >◀</button>
                <button
                  onClick={() => {
                    const next = (historyIndex < 0 ? allOutputs.length : historyIndex) + 1;
                    setHistoryIndex(next >= allOutputs.length ? -1 : next);
                  }}
                  style={{ background: 'none', border: 'none', color: 'var(--fc-accent)', cursor: 'pointer', fontSize: 10, padding: '0 4px' }}
                >▶</button>
              </span>
            )}
            <div style={{ flex: 1 }} />
            {hasOutput && (
              <button onClick={() => navigator.clipboard.writeText(displayOutput)} style={{
                background: 'none', border: 'none', color: 'var(--fc-text-muted)',
                cursor: 'pointer', fontSize: 11, marginRight: 8,
              }}>Copy</button>
            )}
          </div>
          {/* Content (block) */}
          <pre style={{
            margin: 0,
            padding: 8,
            fontSize: 11,
            color: hasOutput ? 'var(--fc-term-text)' : 'var(--fc-text-muted)',
            lineHeight: 1.5,
            overflowY: 'auto',
            flex: 1,
            fontFamily: 'var(--fc-font-mono)',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-all',
          }}>
            {renderBlockBody({
              hasOutput: !!hasOutput,
              displayOutput,
              nodeId,
              scopedNoOutput,
              loopHintActive: isLoopNodeSelected && loopHasSelection && !displayOutput,
            })}
          </pre>
        </>
      ) : (
        <RunOutputView />
      )}
    </div>
  );
}

/** Block-tab body content with the iteration-scoped precedence rules:
 *  loop-container hint > scoped-empty note > actual output > generic empty state. */
function renderBlockBody({ hasOutput, displayOutput, nodeId, scopedNoOutput, loopHintActive }: {
  hasOutput: boolean;
  displayOutput: string;
  nodeId?: string;
  scopedNoOutput: boolean;
  loopHintActive: boolean;
}): ReactNode {
  if (loopHintActive) {
    return (
      <span data-testid="iter-output-loophint">
        (loop container — select a block inside the loop to see its per-iteration output)
      </span>
    );
  }
  if (scopedNoOutput) {
    return <span data-testid="iter-output-empty">(no output in this iteration)</span>;
  }
  if (hasOutput) return displayOutput;
  return nodeId ? '(no output)' : 'Select a block to view its output';
}

function TabButton({ testid, active, onClick, children }: {
  testid: string; active: boolean; onClick: () => void; children: ReactNode;
}) {
  return (
    <button
      data-testid={testid}
      onClick={onClick}
      style={{
        display: 'flex', alignItems: 'center', padding: '0 12px', fontSize: 11, fontWeight: 600,
        background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit',
        color: active ? 'var(--fc-text)' : 'var(--fc-text-muted)',
        borderBottom: `2px solid ${active ? 'var(--fc-accent)' : 'transparent'}`,
      }}
    >
      {children}
    </button>
  );
}
