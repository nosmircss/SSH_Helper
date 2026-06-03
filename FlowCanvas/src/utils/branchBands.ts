// FlowCanvas/src/utils/branchBands.ts
// Round-trip-safe branch-band derivation. Reads ONLY existing transient metadata
// (_isChildOf, _stepPath, _branchLabel) + node.position; writes nothing to node.data.
import type { Node } from '@xyflow/react';
import { CHILD_WIDTH, COLLAPSED_HEIGHT, estimateNodeHeight, BAND_PAD, BAND_LABEL_HEADROOM } from './nodeSize';

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

/** Per-node box used for band geometry. Width is fixed per role; height uses the expanded
 *  estimate when the node is expanded, or COLLAPSED_HEIGHT otherwise. */
function nodeBox(n: Node): { w: number; h: number } {
  const data = (n.data ?? {}) as { blockType?: string; expanded?: boolean; props?: Record<string, unknown> };
  const h = data.expanded ? estimateNodeHeight(data.blockType ?? '', data.props ?? {}, true) : COLLAPSED_HEIGHT;
  return { w: CHILD_WIDTH, h };
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

export function computeBranchBands(nodes: Node[]): BranchBand[] {
  const groups = new Map<string, { parentId: string; branchKey: string; nodes: Node[] }>();
  for (const n of nodes) {
    const props = (n.data as { props?: Record<string, unknown> } | undefined)?.props;
    const parentId = props?.['_isChildOf'] as string | undefined;
    if (!parentId) continue;
    const branchKey = branchKeyFromStepPath(
      props?.['_stepPath'] as string | undefined,
      props?.['_branchLabel'] as string | undefined,
    );
    const groupId = `${parentId}::${branchKey}`;
    if (!groups.has(groupId)) groups.set(groupId, { parentId, branchKey, nodes: [] });
    groups.get(groupId)!.nodes.push(n);
  }

  const bands: BranchBand[] = [];
  for (const [groupId, g] of groups) {
    // A lane must wrap the whole branch SUBTREE: nested-branch bodies are indented to the right
    // and live in their own (child-parent) groups, so a direct-children-only box would clip them.
    // Box over every node whose stepPath falls under this branch's prefix; fall back to direct
    // children when no usable prefix exists.
    const prefix = branchSubtreePrefix(stepPathOf(g.nodes[0]));
    const boxNodes = prefix
      ? nodes.filter((n) => { const sp = stepPathOf(n); return sp != null && sp.startsWith(`${prefix}/`); })
      : g.nodes;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of boxNodes) {
      const { w, h } = nodeBox(n);
      minX = Math.min(minX, n.position.x);
      minY = Math.min(minY, n.position.y);
      maxX = Math.max(maxX, n.position.x + w);
      maxY = Math.max(maxY, n.position.y + h);
    }
    const firstProps = (g.nodes[0]?.data as { props?: Record<string, unknown> } | undefined)?.props;
    const depth = branchDepth(firstProps?.['_stepPath'] as string | undefined);
    // Pull nested bands inward by depth on the LEFT ONLY, so a band that shares its parent's left
    // edge (e.g. a multi-branch first arm sitting at the container's own X) doesn't paint over the
    // parent's left accent — without that inset the nesting reads as one band changing color. Top,
    // right and bottom keep full BAND_PAD so the pill label clears the first block and the bottom
    // isn't crowded. Capped at BAND_PAD - 4 so the band still clears its leftmost child.
    const leftInset = Math.min(depth * NESTED_BAND_INSET, BAND_PAD - 4);
    bands.push({
      id: groupId,
      parentId: g.parentId,
      branchKey: g.branchKey,
      x: minX - BAND_PAD + leftInset,
      y: minY - BAND_PAD - BAND_LABEL_HEADROOM,
      width: (maxX - minX) + BAND_PAD * 2 - leftInset,
      height: (maxY - minY) + BAND_PAD * 2 + BAND_LABEL_HEADROOM,
      colorVar: branchColorVar(g.branchKey),
      depth,
    });
  }
  return bands;
}
