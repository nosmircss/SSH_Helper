import type { Node, Edge } from '@xyflow/react';
import { START_NODE_ID } from '../stores/slices/graphSlice';

interface ClipboardData {
  nodes: Node[];
  edges: Edge[];
}

let clipboardData: ClipboardData | null = null;
let idCounter = 0;

/**
 * Copies the selected nodes and their interconnecting edges to the internal clipboard.
 */
export function copyNodes(
  nodes: Node[],
  edges: Edge[],
  selectedIds: Set<string>,
): void {
  const selectedNodes = nodes.filter(
    (n) => selectedIds.has(n.id) && n.id !== START_NODE_ID,
  );

  const selectedEdges = edges.filter(
    (e) => selectedIds.has(e.source) && selectedIds.has(e.target),
  );

  clipboardData = {
    nodes: JSON.parse(JSON.stringify(selectedNodes)),
    edges: JSON.parse(JSON.stringify(selectedEdges)),
  };
}

/**
 * Pastes previously copied nodes and edges with new IDs and offset positions.
 * Returns null if there is nothing on the clipboard.
 */
export function pasteNodes(): { nodes: Node[]; edges: Edge[] } | null {
  if (!clipboardData) return null;

  const timestamp = Date.now();
  const idMap = new Map<string, string>();

  const newNodes = clipboardData.nodes.map((node) => {
    const newId = `node-paste-${timestamp}-${idCounter++}`;
    idMap.set(node.id, newId);

    return {
      ...JSON.parse(JSON.stringify(node)),
      id: newId,
      position: {
        x: node.position.x + 30,
        y: node.position.y + 30,
      },
      selected: false,
    };
  });

  const newEdges = clipboardData.edges.map((edge) => {
    const newId = `edge-paste-${timestamp}-${idCounter++}`;
    return {
      ...JSON.parse(JSON.stringify(edge)),
      id: newId,
      source: idMap.get(edge.source) ?? edge.source,
      target: idMap.get(edge.target) ?? edge.target,
    };
  });

  return { nodes: newNodes, edges: newEdges };
}

/**
 * Returns true if there is data on the internal clipboard.
 */
export function hasClipboardData(): boolean {
  return clipboardData !== null;
}
