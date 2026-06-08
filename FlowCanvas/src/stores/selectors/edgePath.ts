import type { Edge, Node } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import type { BlockExecState } from '../slices/executionSlice';
import { START_NODE_ID } from '../slices/graphSlice';

export type EdgePathStatus = 'on-path' | 'untaken' | 'idle';

// Source states from which control flows onward to a plain successor.
// 'error' halts the trail; 'running' has not completed yet.
// 'disabled' nodes are skipped but let the trail continue (same as 'skipped').
const PASS_THROUGH = new Set<BlockExecState>(['success', 'skipped', 'disabled']);
const LOOP_TYPES = new Set(['foreach', 'while', 'repeat']);

function propsOf(node: Node | undefined): Record<string, unknown> {
  const data = (node?.data ?? {}) as Record<string, unknown>;
  return (data.props ?? {}) as Record<string, unknown>;
}

function strProp(node: Node | undefined, key: string): string | undefined {
  const value = propsOf(node)[key];
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function blockTypeOf(node: Node | undefined): string | undefined {
  const data = (node?.data ?? {}) as Record<string, unknown>;
  return typeof data.blockType === 'string' ? data.blockType : undefined;
}

function branchPathOf(edge: Edge): string | undefined {
  const data = (edge.data ?? {}) as Record<string, unknown>;
  const value = data.branchPath;
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

/**
 * Imported correlation: does a child's `_stepPath` fall inside the container's `taken`
 * branch scope? e.g. container "steps/3", child "steps/3/cases/2/do/0", taken "cases/2/do"
 * → true. The trailing slash on the prefix prevents "steps/3" matching "steps/30"; keeping
 * the index ("cases/2") disambiguates switch cases and `then` vs `elif/0/then`. Mirrors
 * FlowCanvasBridge.ExtractBranchKeyFromStepPath (but index-precise).
 */
function childInTakenScope(childStepPath: string, containerStepPath: string, taken: string): boolean {
  const prefix = containerStepPath.endsWith('/') ? containerStepPath : containerStepPath + '/';
  if (!childStepPath.startsWith(prefix)) return false;
  const relative = childStepPath.slice(prefix.length);
  return relative === taken || relative.startsWith(taken + '/');
}

/**
 * Is this edge one of the source container's branch arms (vs a plain successor / spine /
 * continuation)? Dual-origin, same as the status logic: canvas-built edges carry
 * `data.branchPath`; imported preset edges carry none, so their branch identity is the target
 * being a visual child of the source (`props._isChildOf === source`). Shared by both the path
 * status and the lit-wire hue — branch arms keep their branch color, everything else promotes to
 * the cyan traversed token. NB: this is keyed on structure, not stroke color, precisely because
 * imported edges use literal grey hex (`#555`/`#666` from FlowCanvasBridge), not the idle token.
 */
function edgeIsBranch(edge: Edge, targetNode: Node | undefined): boolean {
  const branchPath = branchPathOf(edge);
  const isInCanvasBranch = !!branchPath && edge.sourceHandle !== 'continue';
  const isImportedBranch = strProp(targetNode, '_isChildOf') === edge.source;
  return isInCanvasBranch || isImportedBranch;
}

export function selectEdgeIsBranch(state: FlowStore, edgeId: string): boolean {
  const edge = state.edges.find((e) => e.id === edgeId);
  if (!edge) return false;
  const targetNode = state.nodes.find((n) => n.id === edge.target);
  return edgeIsBranch(edge, targetNode);
}

/**
 * Is this edge the SINGLE live frontier for the run packet (the travelling dot)? True when the
 * edge's target block is currently running AND control has not descended past it — i.e. the target
 * has no running successor. While a container is running, its only running successors are its
 * children (its continuation sibling has not been reached yet), so "has a running successor" means
 * "control is deeper inside" — and that running child's own incoming edge carries the dot instead.
 * This keeps exactly one dot, on the DEEPEST running block, so nested containers yield their
 * incoming dot rather than showing a second one. Edge-derived (no _stepPath dependency) so it holds
 * for canvas-built and imported graphs alike, at any nesting depth. Returns a boolean →
 * referentially stable, no extra renders.
 */
export function selectEdgeIsRunningFrontier(state: FlowStore, edgeId: string): boolean {
  const edge = state.edges.find((e) => e.id === edgeId);
  if (!edge) return false;
  if (state.blockStates.get(edge.target) !== 'running') return false;
  return !state.edges.some(
    (e) => e.source === edge.target && state.blockStates.get(e.target) === 'running',
  );
}

/**
 * Classify an edge against the last/current run: 'on-path' (traversed), 'untaken'
 * (a sibling branch that did not fire — faded), or 'idle' (never reached / hidden).
 *
 * Derived from state that already persists after a run, so the result survives
 * `execution-finished`. `pathVisible` is the only gate the "Clear Path" control flips.
 * Reads only transient exec maps + visual-only node props — never mutates anything and
 * never writes to node/edge data — so YAML/graph export is unaffected.
 *
 * Branch edges come from two graph origins:
 *  - canvas-built: edge carries `data.branchPath` (matched directly against branchTaken);
 *  - imported presets: edge has NO branchPath — its branch identity is on the target child's
 *    `props._stepPath` (`_isChildOf === source`), correlated against the container's `_stepPath`.
 */
export function selectEdgePathStatus(state: FlowStore, edgeId: string): EdgePathStatus {
  if (!state.pathVisible) return 'idle';

  const edge = state.edges.find((e) => e.id === edgeId);
  if (!edge) return 'idle';

  // The Start node never receives an exec state; its outgoing edge is traversed once
  // the block it points at has entered execution.
  if (edge.source === START_NODE_ID) {
    const targetState = state.blockStates.get(edge.target);
    return targetState && targetState !== 'idle' ? 'on-path' : 'idle';
  }

  const sourceNode = state.nodes.find((n) => n.id === edge.source);
  const targetNode = state.nodes.find((n) => n.id === edge.target);
  const blockType = blockTypeOf(sourceNode);

  // Branch detection (shared with selectEdgeIsBranch). Only the FIRST edge of each branch
  // (container → first child) satisfies the imported test; within-branch child→child edges have
  // _isChildOf pointing at the container, not the source. branchPath is reused below to match the
  // specific arm against branchTaken.
  const branchPath = branchPathOf(edge);
  const isBranch = edgeIsBranch(edge, targetNode);

  // Live build: a branch arm lights on-path the moment its child block is REACHED (any non-idle
  // exec state), WITHOUT waiting for the container's completion. branchTaken arrives only on the
  // container's completion message — long after the taken arm's child has started — so relying on
  // it alone makes the whole branch neon snap in at the end. The child only runs when its arm is
  // actually taken, so this is a safe live proxy: the taken arm lights as the run reaches it; the
  // untaken arm's child never runs (stays idle) so it falls through to the branchTaken logic below,
  // which fades it once the container resolves. (Plain/spine edges already build live via the
  // sourceState pass-through below; only branch arms were blocked by the container staying
  // 'running' for the whole time its children execute.)
  if (isBranch) {
    const targetState = state.blockStates.get(edge.target);
    if (targetState && targetState !== 'idle') return 'on-path';
  }

  const sourceState = state.blockStates.get(edge.source);
  if (!sourceState || sourceState === 'idle' || sourceState === 'running') return 'idle';

  if (!isBranch) {
    // Plain successor / container continuation: traversed only if the source completed
    // cleanly (or was skipped/disabled). A failed source halts the trail here.
    return PASS_THROUGH.has(sourceState) ? 'on-path' : 'idle';
  }

  if (sourceState === 'error') return 'idle'; // container failed before it branched

  // Parallel fans out to every branch — all of them are on the path.
  if (blockType === 'parallel') return 'on-path';

  // A loop has a single body branch — on-path once it iterated at least once.
  if (blockType && LOOP_TYPES.has(blockType)) {
    return (state.loopIterations.get(edge.source) ?? 0) > 0 ? 'on-path' : 'untaken';
  }

  // Conditional (if / switch / try): compare against the recorded taken branch.
  const taken = state.branchTaken.get(edge.source);
  if (!taken) return 'idle'; // no branch signal — don't guess

  let matched: boolean;
  if (branchPath) {
    // Canvas-built: branchPath is the scope key directly.
    matched = branchPath === taken;
  } else {
    // Imported: correlate the target child's _stepPath against the container's _stepPath.
    const childStepPath = strProp(targetNode, '_stepPath');
    const containerStepPath = strProp(sourceNode, '_stepPath');
    if (childStepPath && containerStepPath) {
      matched = childInTakenScope(childStepPath, containerStepPath, taken);
    } else if (edge.sourceHandle === 'false') {
      matched = taken === 'else'; // last-ditch: the if "false" handle is the else branch
    } else {
      return 'idle'; // can't correlate this branch — don't guess
    }
  }
  return matched ? 'on-path' : 'untaken';
}
