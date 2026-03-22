import { useEffect } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { copyNodes, pasteNodes } from '../utils/clipboard';
import { messageBus } from '../MessageBus';

/**
 * Returns true if the currently focused element is a text input, textarea,
 * or contenteditable element where typing should not trigger shortcuts.
 */
function isInputFocused(): boolean {
  const el = document.activeElement;
  if (!el) return false;
  const tag = el.tagName.toLowerCase();
  if (tag === 'input' || tag === 'textarea' || tag === 'select') return true;
  if ((el as HTMLElement).isContentEditable) return true;
  return false;
}

/**
 * Central keyboard shortcut handler for the FlowCanvas application.
 * Registers a single keydown listener and dispatches to the appropriate
 * store actions or message bus commands.
 */
export function useKeyboardShortcuts(): void {
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent): void {
      const ctrl = e.ctrlKey || e.metaKey;
      const shift = e.shiftKey;
      const key = e.key.toLowerCase();

      // Ctrl+Z: Undo
      if (ctrl && !shift && key === 'z') {
        e.preventDefault();
        useFlowStore.getState().undo();
        return;
      }

      // Ctrl+Y or Ctrl+Shift+Z: Redo
      if ((ctrl && key === 'y') || (ctrl && shift && key === 'z')) {
        e.preventDefault();
        useFlowStore.getState().redo();
        return;
      }

      // Ctrl+C: Copy selected nodes
      if (ctrl && key === 'c' && !isInputFocused()) {
        const store = useFlowStore.getState();
        if (store.selectedNodeIds.size > 0) {
          e.preventDefault();
          copyNodes(store.nodes, store.edges, store.selectedNodeIds);
        }
        return;
      }

      // Ctrl+V: Paste nodes
      if (ctrl && key === 'v' && !isInputFocused()) {
        const result = pasteNodes();
        if (result) {
          e.preventDefault();
          const store = useFlowStore.getState();
          store.pushSnapshot('Paste');
          store.setNodes([...store.nodes, ...result.nodes]);
          store.setEdges([...store.edges, ...result.edges]);
          store.selectNodes(result.nodes.map((n) => n.id));
        }
        return;
      }

      // Ctrl+F: Toggle search
      if (ctrl && key === 'f') {
        e.preventDefault();
        useFlowStore.getState().toggleSearch();
        return;
      }

      // Delete / Backspace: Remove selected nodes (unless typing in an input)
      if ((key === 'delete' || key === 'backspace') && !isInputFocused()) {
        const store = useFlowStore.getState();
        if (store.selectedNodeIds.size > 0) {
          e.preventDefault();
          store.removeNodes([...store.selectedNodeIds]);
        }
        return;
      }

      // Escape: Close search, clear selection, hide context menu
      if (key === 'escape') {
        const store = useFlowStore.getState();
        if (store.searchVisible) {
          store.closeSearch();
        } else if (store.contextMenu) {
          store.hideContextMenu();
        } else if (store.selectedNodeIds.size > 0) {
          store.clearSelection();
        }
        return;
      }

      // Ctrl+Enter: Test step on first selected node
      if (ctrl && key === 'enter') {
        const store = useFlowStore.getState();
        const firstSelected = [...store.selectedNodeIds][0];
        if (firstSelected) {
          e.preventDefault();
          messageBus.send({ type: 'testStep', stepId: firstSelected });
        }
        return;
      }

      // F5: Run
      if (key === 'f5' && !ctrl && !shift) {
        e.preventDefault();
        messageBus.send({ type: 'run' });
        return;
      }

      // F10: Step
      if (key === 'f10' && !ctrl && !shift) {
        e.preventDefault();
        messageBus.send({ type: 'step' });
        return;
      }

      // F11: Step-into
      if (key === 'f11' && !ctrl && !shift) {
        e.preventDefault();
        messageBus.send({ type: 'stepInto' });
        return;
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);
}
