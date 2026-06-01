import type { Edge, Node } from '@xyflow/react';
import { blockDefMap } from '../../blockDefs/registry';
import { branchScopeFromBranchPath, branchScopeFromStepPath, branchSortRank } from './branchScope';
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
  const byId = new Map(nodes.map((n) => [n.id, n] as const));

  const metaChildren = new Map<string, Node[]>();
  for (const n of layoutable) {
    const parentId = propsOf(n)['_isChildOf'] as string | undefined;
    if (parentId) {
      const arr = metaChildren.get(parentId);
      if (arr) arr.push(n);
      else metaChildren.set(parentId, [n]);
    }
  }

  const outBySource = new Map<string, Edge[]>();
  for (const e of edges) {
    const arr = outBySource.get(e.source);
    if (arr) arr.push(e);
    else outBySource.set(e.source, [e]);
  }

  const claimed = new Set<string>();
  const building = new Set<string>();

  function branchPathOf(edge: Edge): string | undefined {
    return (edge.data as { branchPath?: string } | undefined)?.branchPath;
  }

  function toTreeNode(node: Node): LayoutTreeNode {
    const isContainer = isContainerNode(node);
    let branches: LayoutBranch[] = [];
    if (isContainer && !building.has(node.id)) {
      building.add(node.id);
      branches = buildBranchesMeta(node);
      if (branches.length === 0) branches = buildBranchesEdges(node);
      building.delete(node.id);
    }
    return { id: node.id, node, isContainer, branches };
  }

  function buildBranchesMeta(container: Node): LayoutBranch[] {
    const kids = metaChildren.get(container.id);
    if (!kids || kids.length === 0) return [];
    const containerStepPath = propsOf(container)['_stepPath'] as string | undefined;
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

  // Canvas-built fallback: branches come from the container's non-'continue' outgoing
  // edges; each branch chain is followed via plain forward edges until it joins back or
  // is already claimed. Loop back-edges are stopped by the `building`/`claimed` guards.
  function buildBranchesEdges(container: Node): LayoutBranch[] {
    const out = (outBySource.get(container.id) ?? []).filter((e) => e.sourceHandle !== 'continue');
    if (out.length === 0) return [];
    const branches: LayoutBranch[] = [];
    for (const edge of out) {
      const scope = branchScopeFromBranchPath(branchPathOf(edge) ?? edge.sourceHandle ?? 'then');
      const chain: LayoutTreeNode[] = [];
      let cursor: string | undefined = edge.target;
      const localSeen = new Set<string>([container.id]);
      while (cursor && !claimed.has(cursor) && !localSeen.has(cursor) && !building.has(cursor)) {
        const node = byId.get(cursor);
        if (!node) break;
        localSeen.add(cursor);
        claimed.add(cursor);
        chain.push(toTreeNode(node));
        // Continue down a single plain forward edge; stop at branch/continue forks or joins.
        const next: Edge[] = (outBySource.get(cursor) ?? []).filter(
          (e) => e.sourceHandle !== 'continue' && e.sourceHandle !== 'false' && branchPathOf(e) === undefined,
        );
        cursor = next.length === 1 ? next[0].target : undefined;
      }
      if (chain.length > 0) {
        branches.push({ scope, sortRank: branchSortRank(scope), children: chain });
      }
    }
    branches.sort((a, b) => a.sortRank - b.sortRank);
    return branches;
  }

  const treeNodes = new Map<string, LayoutTreeNode>();
  for (const n of layoutable) treeNodes.set(n.id, toTreeNode(n));

  // Edge-ordered spine: walk from the start node's successor following non-branch,
  // non-back forward edges; append any unclaimed/unvisited nodes (orphans) afterward.
  const spine: LayoutTreeNode[] = [];
  const onSpine = new Set<string>();
  function pushSpine(id: string) {
    if (onSpine.has(id) || claimed.has(id)) return;
    const tn = treeNodes.get(id);
    if (!tn) return;
    onSpine.add(id);
    spine.push(tn);
  }

  const startOut = outBySource.get(START_ID) ?? [];
  let cursor: string | undefined = startOut[0]?.target;
  const walkSeen = new Set<string>();
  while (cursor && !walkSeen.has(cursor)) {
    walkSeen.add(cursor);
    pushSpine(cursor);
    const node = byId.get(cursor);
    const isContainer = node ? isContainerNode(node) : false;
    const out: Edge[] = outBySource.get(cursor) ?? [];
    // The continuation out of a container is its 'continue' handle; otherwise the plain
    // next edge whose target isn't a claimed branch child.
    const cont: Edge | undefined = isContainer
      ? out.find((e) => e.sourceHandle === 'continue')
      : out.find((e) => !claimed.has(e.target) && branchPathOf(e) === undefined && e.sourceHandle !== 'false');
    cursor = cont?.target;
    if (cursor && (onSpine.has(cursor) || claimed.has(cursor))) cursor = undefined;
  }

  // Any remaining top-level nodes (disconnected/orphans) keep their place at the end.
  for (const n of layoutable) if (!claimed.has(n.id) && !onSpine.has(n.id)) pushSpine(n.id);

  return { spine };
}
