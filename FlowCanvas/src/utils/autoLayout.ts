import dagre from '@dagrejs/dagre';
import type { Node, Edge } from '@xyflow/react';

const DEFAULT_NODE_WIDTH = 200;
const DEFAULT_NODE_HEIGHT = 60;

/**
 * Computes a clean top-to-bottom layout for the given nodes and edges using dagre.
 * Nodes that are children of other nodes (indicated by `_isChildOf` in their data props)
 * are excluded from the layout and retain their original positions.
 */
export function computeAutoLayout(nodes: Node[], edges: Edge[]): Node[] {
  const g = new dagre.graphlib.Graph();
  g.setDefaultEdgeLabel(() => ({}));
  g.setGraph({
    rankdir: 'TB',
    ranksep: 80,
    nodesep: 40,
  });

  const childNodeIds = new Set<string>();

  for (const node of nodes) {
    const props = (node.data as Record<string, unknown>)?.props as
      | Record<string, unknown>
      | undefined;

    if (props?._isChildOf) {
      childNodeIds.add(node.id);
      continue;
    }

    const width = node.measured?.width ?? node.width ?? DEFAULT_NODE_WIDTH;
    const height = node.measured?.height ?? node.height ?? DEFAULT_NODE_HEIGHT;

    g.setNode(node.id, { width, height });
  }

  for (const edge of edges) {
    if (!childNodeIds.has(edge.source) && !childNodeIds.has(edge.target)) {
      g.setEdge(edge.source, edge.target);
    }
  }

  dagre.layout(g);

  return nodes.map((node) => {
    if (childNodeIds.has(node.id)) {
      return node;
    }

    const dagreNode = g.node(node.id);
    if (!dagreNode) {
      return node;
    }

    const width = node.measured?.width ?? node.width ?? DEFAULT_NODE_WIDTH;
    const height = node.measured?.height ?? node.height ?? DEFAULT_NODE_HEIGHT;

    return {
      ...node,
      position: {
        x: dagreNode.x - width / 2,
        y: dagreNode.y - height / 2,
      },
    };
  });
}
