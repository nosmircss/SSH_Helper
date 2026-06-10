// FlowCanvas/src/utils/childMembership.ts
// Container membership (which band a block lives in) is decided by import-only node metadata
// (_isChildOf + index-precise _stepPath), NOT by edges. A block dropped fresh on the canvas has
// neither, so wiring it into a container's branch left it an orphan after Layout. This module
// derives the membership metadata that import WOULD have written when the user wires a fresh block
// to a node inside a container, so layout, bands and the YAML exporter all agree.
//
// Three wiring gestures confer membership (everything else returns null and keeps the old
// edge-only behaviour):
//   1. a container's `continue` handle  → the next sibling AFTER that container,
//   2. a leaf block's bottom handle     → the next sibling AFTER that leaf,
//   3. a container's branch handle      → the FIRST child of that branch (then/else/do/case…).
import type { Connection, Node } from '@xyflow/react';

/** Branch identity for gesture 3, as produced by the store's inferDefaultBranchMetadata. */
export interface BranchMetadataInput {
  branchPath?: string;
  condition?: string;
  caseValue?: string;
}

export interface ChildMembership {
  targetId: string;
  /** Metadata keys to merge into the target node's data.props. */
  props: Record<string, unknown>;
  /** Later siblings in this branch (and their subtrees) shift up by one to make room. */
  renumber: { prefix: string; fromIndex: number };
}

const TRAILING_INDEX = /\/(\d+)$/;
/** Optional metadata copied from a sibling so the new block matches its lane visually. */
const COSMETIC_KEYS = ['_branchLabel', '_branchColor', '_depth'] as const;
/** Marks membership the user authored by wiring (vs import) so deleting the wire can revert it. */
export const MEMBERSHIP_MARKER = '_membershipFromConnect';

function propsOf(n: Node | undefined): Record<string, unknown> {
  return ((n?.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};
}

/**
 * Derive the membership metadata import would have written for a freshly-wired block.
 * Returns null (leave the existing edge-only behaviour untouched) when the gesture can't confer
 * membership: not one of the three handles above, the source is canvas-authored (no `_stepPath`,
 * so its structure lives on edges), the source is top-level (the layout spine walk already places
 * a top-level successor correctly), or the target already carries (still-anchored) membership.
 *
 * `targetIsOrphaned` (target has no incoming edge right now) is the seam for rewiring: a block whose
 * parent branch-entry edge was DELETED still carries its stale imported `_isChildOf`/`_stepPath`, but
 * it no longer belongs to that branch. Wiring it somewhere new should re-home it, not be refused by
 * the don't-clobber guard — otherwise (the reported bug) it stays visually in its old band while its
 * new wire dangles. Fan-in is already forbidden upstream, so a target that still has its entry edge
 * never reaches here on a new connect; an orphaned member here is always safe to re-home.
 */
export function deriveChildMembership(
  nodes: Node[],
  connection: Connection,
  opts: { sourceIsContainer: boolean; branchMetadata?: BranchMetadataInput; targetIsOrphaned?: boolean },
): ChildMembership | null {
  if (!connection.source || !connection.target) return null;
  const source = nodes.find((n) => n.id === connection.source);
  const target = nodes.find((n) => n.id === connection.target);
  if (!source || !target) return null;

  const tProps = propsOf(target);
  const hasMembership = tProps['_isChildOf'] != null || tProps['_stepPath'] != null;
  // Keep a still-anchored member where it is (don't clobber). An orphaned member — its branch-entry
  // edge was deleted — is fair game to re-home into the branch the user is now wiring it into.
  if (hasMembership && !opts.targetIsOrphaned) return null;

  const sProps = propsOf(source);
  const sPath = sProps['_stepPath'];
  if (typeof sPath !== 'string') return null; // canvas-authored source → edge-only behaviour

  const handle = connection.sourceHandle ?? '';

  // Gesture 3 — a container's branch handle (bottom = primary/elif/case, "false" = else): the new
  // block is the FIRST child of that branch.
  if (opts.sourceIsContainer && handle !== 'continue') {
    const scope = opts.branchMetadata?.branchPath;
    if (!scope) return null;
    const prefix = `${sPath}/${scope}`;
    const props: Record<string, unknown> = {
      _isChildOf: source.id,
      _stepPath: `${prefix}/0`,
      [MEMBERSHIP_MARKER]: true,
    };
    return { targetId: target.id, props, renumber: { prefix, fromIndex: 0 } };
  }

  // Gestures 1 & 2 — a container's `continue` handle, or a leaf's bottom handle: the new block is
  // the next sibling AFTER the source, in the source's own branch.
  const isSuccessor = (opts.sourceIsContainer && handle === 'continue') || (!opts.sourceIsContainer && handle === '');
  if (!isSuccessor) return null;

  const match = sPath.match(TRAILING_INDEX);
  if (!match) return null;
  const parentId = sProps['_isChildOf'];
  if (typeof parentId !== 'string' || parentId.length === 0) return null; // top-level → spine walk

  const branchPrefix = sPath.replace(TRAILING_INDEX, '');
  const insertIndex = Number(match[1]) + 1;
  const props: Record<string, unknown> = {
    _isChildOf: parentId,
    _stepPath: `${branchPrefix}/${insertIndex}`,
    [MEMBERSHIP_MARKER]: true,
  };
  for (const key of COSMETIC_KEYS) {
    if (sProps[key] !== undefined) props[key] = sProps[key];
  }
  return { targetId: target.id, props, renumber: { prefix: branchPrefix, fromIndex: insertIndex } };
}

/** Shift a single index segment of a stepPath up by one when it sits at/after the insertion point.
 *  Operates on the segment immediately after `prefix`, so it covers a direct sibling ("…/then/3")
 *  and every node in that sibling's subtree ("…/then/3/then/0") alike. */
function bumpStepPath(stepPath: string, prefix: string, fromIndex: number): string {
  const head = `${prefix}/`;
  if (!stepPath.startsWith(head)) return stepPath;
  const rest = stepPath.slice(head.length);
  const slash = rest.indexOf('/');
  const idxStr = slash === -1 ? rest : rest.slice(0, slash);
  if (!/^\d+$/.test(idxStr)) return stepPath;
  const idx = Number(idxStr);
  if (idx < fromIndex) return stepPath;
  const tail = slash === -1 ? '' : rest.slice(slash);
  return `${prefix}/${idx + 1}${tail}`;
}

/**
 * Apply a derived membership to the node array: write the target's metadata, shift later siblings
 * (and their whole subtrees) up by one index to make room, and flag the ancestor containers for
 * graph re-export so the change survives a YAML round-trip. Pure — returns a new array.
 */
export function applyChildMembership(nodes: Node[], membership: ChildMembership): Node[] {
  const { targetId, props, renumber } = membership;

  // Ancestor containers = the _isChildOf chain above the new parent. Every link is a container
  // (only containers own children), so flag each so export regenerates them from the graph.
  const ancestors = new Set<string>();
  let cursor = props['_isChildOf'];
  while (typeof cursor === 'string' && cursor.length > 0 && !ancestors.has(cursor)) {
    ancestors.add(cursor);
    cursor = propsOf(nodes.find((n) => n.id === cursor))['_isChildOf'];
  }

  return nodes.map((n) => {
    const data = (n.data as Record<string, unknown>) ?? {};
    const existing = (data.props as Record<string, unknown> | undefined) ?? {};

    if (n.id === targetId) {
      return { ...n, data: { ...data, props: { ...existing, ...props } } };
    }

    let nextProps = existing;
    const sp = existing['_stepPath'];
    if (typeof sp === 'string') {
      const bumped = bumpStepPath(sp, renumber.prefix, renumber.fromIndex);
      if (bumped !== sp) nextProps = { ...nextProps, _stepPath: bumped };
    }
    if (ancestors.has(n.id)) {
      nextProps = { ...nextProps, _forceGraphExport: true };
    }
    if (nextProps === existing) return n;
    return { ...n, data: { ...data, props: nextProps } };
  });
}

/**
 * Re-derive contiguous `_stepPath` indices for every container child from the current graph
 * structure (`_isChildOf` + the existing `_stepPath` ordering), per branch scope, recursively.
 *
 * Why this exists: `applyChildMembership` bumps later siblings' indices +1 to make room when a
 * block is wired in, but no delete path renumbers them back DOWN — so removing a nested block
 * leaves a gap (e.g. `do/0, do/2, do/3`). The executor always assigns SEQUENTIAL paths
 * (`do/0, do/1, do/2`) to the exported YAML, so any gap makes the runtime node↔stepPath map
 * disagree with the executor from the gap onward, silently dropping those nodes' step-update /
 * step-output events (dead neon + missing per-block output). Running this after any removal keeps
 * `_stepPath` self-consistent so the map always matches what runs.
 *
 * Pure. Only rewrites `_stepPath`, only on nodes whose index actually changed. Nodes without a
 * `_stepPath` (canvas-authored / freshly dropped) and orphans whose parent link is broken are
 * left untouched — they fall through to the exporter's own top-level fallback.
 */
export function renumberStepPaths(nodes: Node[]): Node[] {
  const oldPath = new Map<string, string>();
  const childrenOf = new Map<string, string[]>();
  const roots: string[] = [];

  for (const n of nodes) {
    if (n.type === 'comment') continue;
    const p = propsOf(n);
    const sp = p['_stepPath'];
    if (typeof sp !== 'string' || sp.length === 0) continue; // not an executable step
    oldPath.set(n.id, sp);
    const parent = p['_isChildOf'];
    if (typeof parent === 'string' && parent.length > 0) {
      const list = childrenOf.get(parent);
      if (list) list.push(n.id);
      else childrenOf.set(parent, [n.id]);
    } else {
      roots.push(n.id);
    }
  }

  const trailingIndex = (id: string): number => {
    const m = oldPath.get(id)!.match(TRAILING_INDEX);
    return m ? Number(m[1]) : 0;
  };

  const newPath = new Map<string, string>();
  const assign = (id: string, assignedPath: string): void => {
    newPath.set(id, assignedPath);
    const kids = childrenOf.get(id);
    if (!kids || kids.length === 0) return;
    const head = `${oldPath.get(id)!}/`;
    // Group children by branch scope (relative path minus the trailing step index). The scope keeps
    // any branch selector indices intact (e.g. "elif/0/then", "cases/2/do") — only the final step
    // index is renumbered.
    const groups = new Map<string, string[]>();
    for (const kid of kids) {
      const kp = oldPath.get(kid);
      if (kp === undefined || !kp.startsWith(head)) continue; // dangling metadata → leave as-is
      const rel = kp.slice(head.length);
      const lastSlash = rel.lastIndexOf('/');
      if (lastSlash === -1) continue; // malformed (no scope segment) → leave as-is
      const scope = rel.slice(0, lastSlash);
      const list = groups.get(scope);
      if (list) list.push(kid);
      else groups.set(scope, [kid]);
    }
    for (const [scope, arr] of groups) {
      arr.sort((a, b) => trailingIndex(a) - trailingIndex(b));
      arr.forEach((kid, i) => assign(kid, `${assignedPath}/${scope}/${i}`));
    }
  };

  roots.sort((a, b) => trailingIndex(a) - trailingIndex(b));
  roots.forEach((id, i) => assign(id, `steps/${i}`));

  let changed = false;
  const result = nodes.map((n) => {
    const np = newPath.get(n.id);
    if (np === undefined) return n;
    const data = (n.data as Record<string, unknown>) ?? {};
    const existing = (data.props as Record<string, unknown> | undefined) ?? {};
    if (existing['_stepPath'] === np) return n;
    changed = true;
    return { ...n, data: { ...data, props: { ...existing, _stepPath: np } } };
  });
  return changed ? result : nodes;
}

/**
 * Revert connect-authored membership when its conferring wire is deleted. Given the ids of removed
 * edges, any target node we previously tagged via the wire (MEMBERSHIP_MARKER) loses its membership
 * metadata and falls back to a top-level orphan — so the band releases it, matching the user's model
 * that the wire is what placed it there. Imported membership (no marker) is never touched. Pure.
 */
export function clearConnectAuthoredMembership(
  nodes: Node[],
  removedEdges: { target?: string | null }[],
): Node[] {
  const reverted = new Set(
    removedEdges.map((e) => e.target).filter((t): t is string => typeof t === 'string'),
  );
  if (reverted.size === 0) return nodes;

  return nodes.map((n) => {
    if (!reverted.has(n.id)) return n;
    const data = (n.data as Record<string, unknown>) ?? {};
    const existing = (data.props as Record<string, unknown> | undefined) ?? {};
    if (existing[MEMBERSHIP_MARKER] !== true) return n; // imported / not wire-authored → keep
    const nextProps = { ...existing };
    delete nextProps['_isChildOf'];
    delete nextProps['_stepPath'];
    delete nextProps[MEMBERSHIP_MARKER];
    for (const key of COSMETIC_KEYS) delete nextProps[key];
    return { ...n, data: { ...data, props: nextProps } };
  });
}
