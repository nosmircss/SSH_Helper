import type { Edge, Node } from '@xyflow/react';
import { blockDefMap } from '../../blockDefs/registry';
import { branchScopeFromStepPath, branchSortRank } from './branchScope';
import type { LayoutBranch, LayoutTree, LayoutTreeNode } from './types';

const START_ID = '__start__';

function blockTypeOf(node: Node): string {
  return ((node.data as { blockType?: string } | undefined)?.blockType) ?? '';
}
function propsOf(node: Node): Record<string, unknown> {
  return ((node.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};
}
function isContainerNode(node: Node): boolean {
  return !!blockDefMap.get(blockTypeOf(node))?.isContainer;
}
function isComment(node: Node): boolean {
  return node.type === 'comment';
}
function childIndexOf(node: Node): number {
  const sp = propsOf(node)['_stepPath'] as string | undefined;
  if (!sp) return 0;
  const last = sp.split('/').pop()!;
  const n = Number(last);
  return Number.isFinite(n) ? n : 0;
}

export function buildLayoutTree(nodes: Node[], edges: Edge[]): LayoutTree {
  const layoutable = nodes.filter((n) => n.id !== START_ID && !isComment(n));

  // parentId -> child nodes (imported metadata)
  const metaChildren = new Map<string, Node[]>();
  for (const n of layoutable) {
    const parentId = propsOf(n)['_isChildOf'] as string | undefined;
    if (parentId) {
      const arr = metaChildren.get(parentId);
      if (arr) arr.push(n);
      else metaChildren.set(parentId, [n]);
    }
  }

  const claimed = new Set<string>(); // nodes that belong to some container branch
  const building = new Set<string>(); // cycle guard

  function toTreeNode(node: Node): LayoutTreeNode {
    const isContainer = isContainerNode(node);
    let branches: LayoutBranch[] = [];
    if (isContainer && !building.has(node.id)) {
      building.add(node.id);
      branches = buildBranchesMeta(node);
      building.delete(node.id);
    }
    return { id: node.id, node, isContainer, branches };
  }

  function buildBranchesMeta(container: Node): LayoutBranch[] {
    const kids = metaChildren.get(container.id);
    if (!kids || kids.length === 0) return [];
    const containerStepPath = propsOf(container)['_stepPath'] as string | undefined;

    // Group by full branch scope so sibling branches (cases/elifs/parallel arms) stay separate.
    const groups = new Map<string, Node[]>();
    for (const k of kids) {
      const sp = (propsOf(k)['_stepPath'] as string | undefined) ?? '';
      const scope = branchScopeFromStepPath(sp, containerStepPath);
      const arr = groups.get(scope);
      if (arr) arr.push(k);
      else groups.set(scope, [k]);
    }

    const branches: LayoutBranch[] = [];
    for (const [scope, groupKids] of groups) {
      const ordered = [...groupKids].sort((a, b) => childIndexOf(a) - childIndexOf(b));
      ordered.forEach((k) => claimed.add(k.id));
      branches.push({ scope, sortRank: branchSortRank(scope), children: ordered.map(toTreeNode) });
    }
    branches.sort((a, b) => a.sortRank - b.sortRank);
    return branches;
  }

  // Resolve all container branches first so `claimed` is fully populated.
  const treeNodes = new Map<string, LayoutTreeNode>();
  for (const n of layoutable) treeNodes.set(n.id, toTreeNode(n));

  // Spine = top-level nodes (not claimed by any container), in document order.
  const spine = layoutable.filter((n) => !claimed.has(n.id)).map((n) => treeNodes.get(n.id)!);

  return { spine };
}
