import { useEffect, useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

export default function EdgeContextMenu() {
  const edgeContextMenu = useFlowStore((s) => s.edgeContextMenu);
  const hideEdgeContextMenu = useFlowStore((s) => s.hideEdgeContextMenu);
  const removeEdges = useFlowStore((s) => s.removeEdges);

  const handleClickOutside = useCallback(() => {
    hideEdgeContextMenu();
  }, [hideEdgeContextMenu]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        hideEdgeContextMenu();
      }
    },
    [hideEdgeContextMenu],
  );

  useEffect(() => {
    if (!edgeContextMenu) return;

    // Delay listener attachment to avoid closing immediately from the triggering right-click
    const timer = setTimeout(() => {
      document.addEventListener('click', handleClickOutside);
      document.addEventListener('contextmenu', handleClickOutside);
      document.addEventListener('keydown', handleKeyDown);
    }, 0);

    return () => {
      clearTimeout(timer);
      document.removeEventListener('click', handleClickOutside);
      document.removeEventListener('contextmenu', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [edgeContextMenu, handleClickOutside, handleKeyDown]);

  if (!edgeContextMenu) return null;

  const { x, y, edgeId } = edgeContextMenu;

  return (
    <div
      style={{
        position: 'fixed',
        left: x,
        top: y,
        zIndex: 50,
        background: '#12122a',
        border: '1px solid #2a2a4a',
        borderRadius: 6,
        padding: '4px 0',
        minWidth: 180,
        boxShadow: '0 6px 20px rgba(0, 0, 0, 0.5)',
      }}
    >
      <button
        onClick={() => {
          removeEdges([edgeId]);
          hideEdgeContextMenu();
        }}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          width: '100%',
          padding: '6px 12px',
          background: 'none',
          border: 'none',
          color: '#e74c3c',
          fontSize: 12,
          cursor: 'pointer',
          textAlign: 'left',
          transition: 'background 0.1s',
        }}
        onMouseEnter={(e) => {
          (e.currentTarget as HTMLElement).style.background = '#1e1e3a';
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLElement).style.background = 'none';
        }}
      >
        <span style={{ fontSize: 14, width: 20, textAlign: 'center' }}>
          {'\u2702'}
        </span>
        <span>Delete Connection</span>
      </button>
    </div>
  );
}
