// FlowCanvas/src/utils/branchBands.ts
// Round-trip-safe branch-band derivation. Reads ONLY existing transient metadata
// (_isChildOf, _stepPath, _branchLabel) + node.position; writes nothing to node.data.
import type { Node } from '@xyflow/react';
import { CHILD_WIDTH, COLLAPSED_HEIGHT, estimateNodeHeight, BAND_PAD, BAND_LABEL_HEADROOM } from './nodeSize';
import { branchScopeFromStepPath } from './layout/branchScope';

// Re-exported for back-compat: BAND_PAD now lives in nodeSize (shared with the layout engine).
export { BAND_PAD };

/** Per-depth-level inward inset for nested bands, so each nesting level steps clear of its
 *  parent's left accent. Capped at BAND_PAD - 4 in computeBranchBands to keep children wrapped. */
const NESTED_BAND_INSET = 10;

export interface BranchBand {
  id: string;
  parentId: string;
  branchKey: string;
  x: number;
  y: number;
  width: number;
  height: number;
  colorVar: string;
  depth: number;
  /** Ids of every node this band wraps (the boxed subtree). Drives "drag the band to move it". */
  memberIds: string[];
}

const BRANCH_KEYS = [
  'then', 'else', 'elif', 'do', 'try', 'catch', 'finally', 'case', 'default', 'parallel',
] as const;

/** Map a branch key (or branch label) to its --fc-branch-* token. Single source of color truth
 *  shared by the band layer and the Properties branch chip (replaces the raw _branchColor hex). */
export function branchColorVar(key: string | undefined): string {
  const k = (key ?? '').toLowerCase();
  for (const known of BRANCH_KEYS) {
    if (k === known || k.startsWith(`${known}:`) || k.startsWith(`${known} `)) {
      return `var(--fc-branch-${known})`;
    }
  }
  return 'var(--fc-branch-fallback)';
}

/** Parse the branch segment from a child _stepPath relative to its parent. e.g.
 *  "steps/3/then/0" → "then", "steps/1/cases/0/do/0" → "case", "steps/2/else/0" → "else". */
export function branchKeyFromStepPath(stepPath: string | undefined, branchLabel: string | undefined): string {
  if (stepPath) {
    const segs = stepPath.split('/');
    for (let i = segs.length - 1; i >= 0; i--) {
      const s = segs[i].toLowerCase();
      if (s === 'cases') return 'case';
      for (const known of BRANCH_KEYS) {
        if (s === known) return known;
      }
    }
  }
  // Fall back to the importer's display label (lowercased first word).
  return (branchLabel ?? 'then').split(/[:\s]/)[0].toLowerCase();
}

/** Human pill label for a branch key (single source for the band layer). */
export function branchPillLabel(key: string): string {
  const k = (key ?? '').toLowerCase();
  if (k === 'do') return 'LOOP';
  return k.toUpperCase();
}

/** Branch-nesting depth from a child stepPath: 0 = outermost branch, 1+ = a branch nested
 *  inside another branch. Counts branch-keyword segments (then/else/elif/do/try/catch/finally/
 *  cases/case/default/parallel) and subtracts the group's own branch. Drives the brighter
 *  nested-lane tint. (Replaces the plan's `length - 2`, which marked every band as nested.) */
function branchDepth(stepPath: string | undefined): number {
  if (!stepPath) return 0;
  let count = 0;
  for (const seg of stepPath.split('/')) {
    const s = seg.toLowerCase();
    if (s === 'cases') { count++; continue; }
    for (const known of BRANCH_KEYS) { if (s === known) { count++; break; } }
  }
  return Math.max(0, count - 1);
}

function stepPathOf(n: Node): string | undefined {
  return ((n.data as { props?: Record<string, unknown> } | undefined)?.props?.['_stepPath']) as string | undefined;
}

/** The stepPath prefix that identifies a branch's whole subtree, up to and including the branch
 *  keyword segment. "steps/0/then/0" → "steps/0/then"; "steps/0/then/2/then/0" → "steps/0/then/2/then";
 *  "steps/1/cases/0/0" → "steps/1/cases" (so all switch cases share one lane). Returns undefined when
 *  no branch keyword is present (caller then falls back to the group's direct children). */
function branchSubtreePrefix(stepPath: string | undefined): string | undefined {
  if (!stepPath) return undefined;
  const segs = stepPath.split('/');
  for (let i = segs.length - 1; i >= 0; i--) {
    const s = segs[i].toLowerCase();
    if (s === 'cases' || (BRANCH_KEYS as readonly string[]).includes(s)) {
      return segs.slice(0, i + 1).join('/');
    }
  }
  return undefined;
}

export function computeBranchBands(nodes: Node[], childWidth: number = CHILD_WIDTH): BranchBand[] {
  const boxOf = (n: Node): { w: number; h: number } => {
    const data = (n.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
    const h = data.expanded ? estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true) : COLLAPSED_HEIGHT;
    return { w: childWidth, h };
  };

  const stepPathById = new Map<string, string | undefined>();
  for (const n of nodes) stepPathById.set(n.id, stepPathOf(n));

  // Resolve a child's branch (key + subtree prefix) RELATIVE to its immediate container so a
  // compound branch like elif (path '.../elif/N/then/M') keys as 'elif' instead of the trailing
  // 'then' an end-walk returns. Falls back to the self-contained end-walk when the parent node
  // isn't present (synthetic fixtures) — identical results for then/else/do/case/parallel.
  const resolveBranch = (
    childStepPath: string | undefined,
    parentId: string,
    branchLabel: string | undefined,
  ): { key: string; prefix: string | undefined } => {
    const parentSP = stepPathById.get(parentId);
    if (childStepPath && parentSP && childStepPath.startsWith(`${parentSP}/`)) {
      const seg0 = branchScopeFromStepPath(childStepPath, parentSP).split('/')[0];
      const key = seg0 === 'cases' ? 'case' : seg0;
      if (key === 'case' || (BRANCH_KEYS as readonly string[]).includes(key)) {
        return { key, prefix: `${parentSP}/${seg0}` };
      }
    }
    return { key: branchKeyFromStepPath(childStepPath, branchLabel), prefix: branchSubtreePrefix(childStepPath) };
  };

  const groups = new Map<string, { parentId: string; branchKey: string; nodes: Node[] }>();
  for (const n of nodes) {
    const props = (n.data as { props?: Record<string, unknown> } | undefined)?.props;
    const parentId = props?.['_isChildOf'] as string | undefined;
    if (!parentId) continue;
    const branchKey = resolveBranch(
      props?.['_stepPath'] as string | undefined,
      parentId,
      props?.['_branchLabel'] as string | undefined,
    ).key;
    const groupId = `${parentId}::${branchKey}`;
    if (!groups.has(groupId)) groups.set(groupId, { parentId, branchKey, nodes: [] });
    groups.get(groupId)!.nodes.push(n);
  }

  // Pass 1 — per band: the node-only content box (no nesting awareness yet) plus the metadata
  // needed to (a) detect which other bands nest inside it and (b) size it in pass 2.
  interface Prelim {
    id: string;
    parentId: string;
    branchKey: string;
    depth: number;
    leftInset: number;
    memberIds: string[];
    memberSet: Set<string>;
    nMinX: number; nMinY: number; nMaxX: number; nMaxY: number;
  }
  const prelims: Prelim[] = [];
  for (const [groupId, g] of groups) {
    // A lane must wrap the whole branch SUBTREE: nested-branch bodies are indented to the right
    // and live in their own (child-parent) groups, so a direct-children-only box would clip them.
    // Box over every node whose stepPath falls under this branch's prefix; fall back to direct
    // children when no usable prefix exists.
    const prefix = resolveBranch(stepPathOf(g.nodes[0]), g.parentId, undefined).prefix;
    const boxNodes = prefix
      ? nodes.filter((n) => { const sp = stepPathOf(n); return sp != null && sp.startsWith(`${prefix}/`); })
      : g.nodes;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of boxNodes) {
      const { w, h } = boxOf(n);
      minX = Math.min(minX, n.position.x);
      minY = Math.min(minY, n.position.y);
      maxX = Math.max(maxX, n.position.x + w);
      maxY = Math.max(maxY, n.position.y + h);
    }
    const firstProps = (g.nodes[0]?.data as { props?: Record<string, unknown> } | undefined)?.props;
    const depth = branchDepth(firstProps?.['_stepPath'] as string | undefined);
    // Pull nested bands inward by depth on the LEFT ONLY, so a band that shares its parent's left
    // edge (e.g. a multi-branch first arm sitting at the container's own X) doesn't paint over the
    // parent's left accent — without that inset the nesting reads as one band changing color.
    // Capped at BAND_PAD - 4 so the band still clears its leftmost child.
    const leftInset = Math.min(depth * NESTED_BAND_INSET, BAND_PAD - 4);
    const memberIds = boxNodes.map((n) => n.id);
    prelims.push({
      id: groupId, parentId: g.parentId, branchKey: g.branchKey, depth, leftInset,
      memberIds, memberSet: new Set(memberIds),
      nMinX: minX, nMinY: minY, nMaxX: maxX, nMaxY: maxY,
    });
  }

  // A band B nests inside band A when B's container node (B.parentId) is one of A's members.
  const childBands = new Map<string, Prelim[]>();
  for (const a of prelims) {
    childBands.set(a.id, prelims.filter((b) => b !== a && a.memberSet.has(b.parentId)));
  }

  // Pass 2 — bottom-up sizing. A band must wrap its nested bands with the SAME BAND_PAD it gives
  // blocks, so nested lanes never paint their border flush against the parent's (the right/bottom
  // "touching" the user sees: both edges otherwise land at sharedChildMax + BAND_PAD). Each band's
  // box therefore unions its nested bands' (already-grown) full rects — which carry their own pad
  // and top label headroom — before adding BAND_PAD all round. Nesting runs several levels deep, so
  // relax until nothing changes (rects only ever grow; the cap is a safety net, not the exit).
  const rect = new Map<string, { x: number; y: number; w: number; h: number }>();
  const sizeFrom = (p: Prelim, minX: number, minY: number, maxX: number, maxY: number) => ({
    x: minX - BAND_PAD + p.leftInset,
    y: minY - BAND_PAD - BAND_LABEL_HEADROOM,
    w: (maxX - minX) + BAND_PAD * 2 - p.leftInset,
    h: (maxY - minY) + BAND_PAD * 2 + BAND_LABEL_HEADROOM,
  });
  for (const p of prelims) rect.set(p.id, sizeFrom(p, p.nMinX, p.nMinY, p.nMaxX, p.nMaxY));
  for (let pass = 0; pass <= prelims.length; pass++) {
    let changed = false;
    for (const p of prelims) {
      let minX = p.nMinX, minY = p.nMinY, maxX = p.nMaxX, maxY = p.nMaxY;
      for (const c of childBands.get(p.id)!) {
        const r = rect.get(c.id)!;
        minX = Math.min(minX, r.x);
        minY = Math.min(minY, r.y);
        maxX = Math.max(maxX, r.x + r.w);
        maxY = Math.max(maxY, r.y + r.h);
      }
      const next = sizeFrom(p, minX, minY, maxX, maxY);
      const cur = rect.get(p.id)!;
      if (next.x !== cur.x || next.y !== cur.y || next.w !== cur.w || next.h !== cur.h) {
        rect.set(p.id, next);
        changed = true;
      }
    }
    if (!changed) break;
  }

  return prelims.map((p) => {
    const r = rect.get(p.id)!;
    return {
      id: p.id,
      parentId: p.parentId,
      branchKey: p.branchKey,
      x: r.x, y: r.y, width: r.w, height: r.h,
      colorVar: branchColorVar(p.branchKey),
      depth: p.depth,
      memberIds: p.memberIds,
    };
  });
}
