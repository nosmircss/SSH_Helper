// FlowCanvas/src/utils/branchBands.ts
// Round-trip-safe branch-band derivation. Reads ONLY existing transient metadata
// (_isChildOf, _stepPath, _branchLabel) + node.position; writes nothing to node.data.
import type { Node } from '@xyflow/react';

export interface BranchBand {
  id: string;
  parentId: string;
  branchKey: string;
  x: number;
  y: number;
  width: number;
  height: number;
  colorVar: string;
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

const NODE_W = 280; // max width from BaseBlock containerStyle (non-child 280 / child 260)
const NODE_H = 64;  // header + preview estimate
const PAD = 10;

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
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of g.nodes) {
      minX = Math.min(minX, n.position.x);
      minY = Math.min(minY, n.position.y);
      maxX = Math.max(maxX, n.position.x + NODE_W);
      maxY = Math.max(maxY, n.position.y + NODE_H);
    }
    bands.push({
      id: groupId,
      parentId: g.parentId,
      branchKey: g.branchKey,
      x: minX - PAD,
      y: minY - PAD,
      width: (maxX - minX) + PAD * 2,
      height: (maxY - minY) + PAD * 2,
      colorVar: branchColorVar(g.branchKey),
    });
  }
  return bands;
}
