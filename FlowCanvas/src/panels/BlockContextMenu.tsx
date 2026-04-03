import { useEffect, useCallback } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { START_NODE_ID } from '../stores/slices/graphSlice';

interface MenuItem {
  label: string;
  icon: string;
  action: () => void;
  separator?: false;
}

interface Separator {
  separator: true;
}

type MenuEntry = MenuItem | Separator;

export default function BlockContextMenu() {
  const contextMenu = useFlowStore((s) => s.contextMenu);
  const hideContextMenu = useFlowStore((s) => s.hideContextMenu);
  const toggleBreakpoint = useFlowStore((s) => s.toggleBreakpoint);

  const addComment = useFlowStore((s) => s.addComment);
  const removeNodes = useFlowStore((s) => s.removeNodes);

  const nodes = useFlowStore((s) => s.nodes);

  const handleClickOutside = useCallback(() => {
    hideContextMenu();
  }, [hideContextMenu]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        hideContextMenu();
      }
    },
    [hideContextMenu],
  );

  useEffect(() => {
    if (!contextMenu) return;

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
  }, [contextMenu, handleClickOutside, handleKeyDown]);

  if (!contextMenu) return null;

  const { x, y, nodeId } = contextMenu;
  const isStartNode = nodeId === START_NODE_ID;

  // Find node position for comment placement
  const node = nodes.find((n) => n.id === nodeId);
  const commentPos = node
    ? { x: node.position.x + 200, y: node.position.y - 20 }
    : { x, y };

  const menuItems: MenuEntry[] = [
    ...(!isStartNode ? [
      {
        label: 'Toggle Breakpoint',
        icon: '\uD83D\uDD34',
        action: () => {
          toggleBreakpoint(nodeId);
          hideContextMenu();
        },
      } as MenuItem,
    ] : []),
    {
      label: 'Add Comment',
      icon: '\uD83D\uDCDD',
      action: () => {
        addComment(commentPos, nodeId);
        hideContextMenu();
      },
    },
    ...(!isStartNode ? [
      { separator: true } as Separator,
      {
        label: 'Delete Block',
        icon: '\uD83D\uDDD1',
        action: () => {
          removeNodes([nodeId]);
          hideContextMenu();
        },
      } as MenuItem,
    ] : []),
  ];

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
      {menuItems.map((item, i) => {
        if (item.separator) {
          return (
            <div
              key={`sep-${i}`}
              style={{
                height: 1,
                background: '#2a2a4a',
                margin: '4px 8px',
              }}
            />
          );
        }

        return (
          <button
            key={item.label}
            onClick={item.action}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              width: '100%',
              padding: '6px 12px',
              background: 'none',
              border: 'none',
              color: item.label === 'Delete Block' ? '#e74c3c' : '#ccc',
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
              {item.icon}
            </span>
            <span>{item.label}</span>
          </button>
        );
      })}
    </div>
  );
}
