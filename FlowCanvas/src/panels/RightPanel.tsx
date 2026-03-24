import { useState, useCallback, useRef, useEffect, type ReactNode } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

interface RightPanelProps {
  children: ReactNode;
}

/**
 * Resizable right panel with a drag handle on the left edge.
 * Contains the Properties panel and Variable Inspector.
 */
export default function RightPanel({ children }: RightPanelProps) {
  const storeWidth = useFlowStore((s) => s.panelSizes.rightPanelWidth);
  const setPanelSize = useFlowStore((s) => s.setPanelSize);
  const [width, setWidth] = useState(storeWidth);
  const widthRef = useRef(width);
  const isDragging = useRef(false);
  const startX = useRef(0);
  const startWidth = useRef(0);

  // Keep ref in sync for the mouseup handler
  useEffect(() => { widthRef.current = width; }, [width]);

  // Sync from store when restored from WinForms
  useEffect(() => { setWidth(storeWidth); }, [storeWidth]);

  const onMouseDown = useCallback((e: React.MouseEvent) => {
    isDragging.current = true;
    startX.current = e.clientX;
    startWidth.current = width;
    e.preventDefault();

    const onMouseMove = (e: MouseEvent) => {
      if (!isDragging.current) return;
      const delta = startX.current - e.clientX; // dragging left = wider
      const newWidth = Math.max(200, Math.min(600, startWidth.current + delta));
      setWidth(newWidth);
    };

    const onMouseUp = () => {
      if (isDragging.current) {
        isDragging.current = false;
        // Persist final width to store (which notifies WinForms)
        setPanelSize('rightPanelWidth', widthRef.current);
      }
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
  }, [width, setPanelSize]);

  return (
    <div style={{
      width,
      flexShrink: 0,
      display: 'flex',
      flexDirection: 'column',
      position: 'relative',
      height: '100%',
    }}>
      {/* Drag handle */}
      <div
        onMouseDown={onMouseDown}
        style={{
          position: 'absolute',
          left: 0,
          top: 0,
          bottom: 0,
          width: 4,
          cursor: 'col-resize',
          background: 'transparent',
          zIndex: 20,
        }}
        onMouseEnter={(e) => (e.currentTarget.style.background = '#4a9eff55')}
        onMouseLeave={(e) => { if (!isDragging.current) e.currentTarget.style.background = 'transparent'; }}
      />
      <div style={{
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        borderLeft: '1px solid #2a2a4a',
        background: '#12122a',
      }}>
        {children}
      </div>
    </div>
  );
}
