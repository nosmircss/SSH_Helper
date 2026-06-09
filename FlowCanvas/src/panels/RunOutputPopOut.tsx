/** Floating, draggable + resizable overlay that hosts RunOutputView when popped out of the dock. */
import { useEffect, useRef, useState } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import RunOutputView from './RunOutputView';

const MIN_W = 260;
const MIN_H = 140;

export default function RunOutputPopOut() {
  const poppedOut = useFlowStore((s) => s.runOutputPoppedOut);
  const toggle = useFlowStore((s) => s.toggleRunOutputPoppedOut);
  const [pos, setPos] = useState({ x: 220, y: 80 });
  const [size, setSize] = useState({ w: 520, h: 300 });
  const listeners = useRef<{ move: (e: MouseEvent) => void; up: () => void } | null>(null);

  // Tear down any in-flight move/resize listeners if we get docked mid-gesture (poppedOut -> false)
  // or unmount. Without this, the window listeners would only be removed on the next mouseup.
  useEffect(() => {
    return () => {
      if (listeners.current) {
        window.removeEventListener('mousemove', listeners.current.move);
        window.removeEventListener('mouseup', listeners.current.up);
        listeners.current = null;
      }
    };
  }, [poppedOut]);

  if (!poppedOut) return null;

  // One gesture driver for both dragging (move) and resizing. Start values are captured at
  // mousedown, so each move event is computed from a stable origin (no stale-closure drift).
  const beginGesture = (mode: 'move' | 'resize') => (e: React.MouseEvent) => {
    e.preventDefault();
    const startX = e.clientX, startY = e.clientY;
    const x0 = pos.x, y0 = pos.y, w0 = size.w, h0 = size.h;
    const onMove = (ev: MouseEvent) => {
      if (mode === 'move') {
        setPos({ x: x0 + (ev.clientX - startX), y: y0 + (ev.clientY - startY) });
      } else {
        setSize({
          w: Math.max(MIN_W, w0 + (ev.clientX - startX)),
          h: Math.max(MIN_H, h0 + (ev.clientY - startY)),
        });
      }
    };
    const onUp = () => {
      listeners.current = null;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
    listeners.current = { move: onMove, up: onUp };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  };

  return (
    <div
      data-testid="run-output-popout"
      style={{
        position: 'absolute', left: pos.x, top: pos.y, width: size.w, height: size.h, zIndex: 20,
        display: 'flex', flexDirection: 'column',
        background: 'var(--fc-panel-bg)', border: '1px solid var(--fc-panel-border)',
        borderRadius: 8, overflow: 'hidden', boxShadow: 'var(--fc-shadow-sm)',
      }}
    >
      <div
        data-testid="run-output-popout-drag"
        onMouseDown={beginGesture('move')}
        style={{
          display: 'flex', alignItems: 'center', height: 24, padding: '0 8px', cursor: 'move',
          background: 'var(--fc-header-bg)', borderBottom: '1px solid var(--fc-panel-border)',
          fontSize: 11, fontWeight: 600, color: 'var(--fc-text-secondary)', flexShrink: 0,
        }}
      >
        ⠿ Run Output
        <div style={{ flex: 1 }} />
        <button data-testid="run-output-popout-dock" onClick={toggle} title="Dock back into the bottom panel" style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted)', cursor: 'pointer', fontSize: 12, padding: 0,
        }}>⤢ Dock</button>
      </div>
      <RunOutputView />
      {/* Bottom-right resize handle */}
      <div
        data-testid="run-output-popout-resize"
        onMouseDown={beginGesture('resize')}
        style={{
          position: 'absolute', right: 0, bottom: 0, width: 16, height: 16, cursor: 'nwse-resize',
          background: 'linear-gradient(135deg, transparent 50%, var(--fc-text-muted) 50%, var(--fc-text-muted) 60%, transparent 60%)',
          opacity: 0.6, zIndex: 1,
        }}
      />
    </div>
  );
}
