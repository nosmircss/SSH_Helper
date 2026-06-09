/** Floating, draggable overlay that hosts RunOutputView when popped out of the dock. */
import { useEffect, useRef, useState } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import RunOutputView from './RunOutputView';

export default function RunOutputPopOut() {
  const poppedOut = useFlowStore((s) => s.runOutputPoppedOut);
  const toggle = useFlowStore((s) => s.toggleRunOutputPoppedOut);
  const [pos, setPos] = useState({ x: 220, y: 80 });
  const drag = useRef<{ dx: number; dy: number } | null>(null);
  const listeners = useRef<{ move: (e: MouseEvent) => void; up: () => void } | null>(null);

  // Tear down any in-flight drag listeners if we get docked mid-drag (poppedOut -> false)
  // or unmount. Without this, releasing the mouse after docking is the only thing that would
  // remove the window listeners, and a true unmount mid-drag would leak them entirely.
  useEffect(() => {
    return () => {
      if (listeners.current) {
        window.removeEventListener('mousemove', listeners.current.move);
        window.removeEventListener('mouseup', listeners.current.up);
        listeners.current = null;
        drag.current = null;
      }
    };
  }, [poppedOut]);

  if (!poppedOut) return null;

  const onMouseDown = (e: React.MouseEvent) => {
    drag.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y };
    const onMove = (ev: MouseEvent) => {
      if (!drag.current) return;
      setPos({ x: ev.clientX - drag.current.dx, y: ev.clientY - drag.current.dy });
    };
    const onUp = () => {
      drag.current = null;
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
        position: 'absolute', left: pos.x, top: pos.y, width: 520, height: 300, zIndex: 20,
        display: 'flex', flexDirection: 'column',
        background: 'var(--fc-panel-bg)', border: '1px solid var(--fc-panel-border)',
        borderRadius: 8, overflow: 'hidden', boxShadow: 'var(--fc-shadow-sm)',
      }}
    >
      <div
        data-testid="run-output-popout-drag"
        onMouseDown={onMouseDown}
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
    </div>
  );
}
