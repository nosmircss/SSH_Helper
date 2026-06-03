import type { Node } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import { branchScopeFromStepPath } from '../../utils/layout/branchScope';

function propsOf(node: Node | undefined): Record<string, unknown> {
  return ((node?.data as { props?: Record<string, unknown> } | undefined)?.props ?? {});
}

/**
 * A container is "single-branch" when its visual children occupy at most one branch arm —
 * a then-only IF, a foreach/while/repeat loop, or a single-case switch. Such a container's
 * continuation can leave the bottom-CENTER and run straight down the spine, because its
 * children are indented clear to the right (see SINGLE_BRANCH_CHILD_OFFSET in the layout).
 *
 * Multi-branch containers (if/else, try/catch, switch with >1 case) keep the bottom-LEFT
 * "continue" corridor: their first branch sits directly under the spine, so a straight
 * continuation would cut through it. That corridor already stays inside its own band, so it
 * does not exhibit the "wire outside the band" bug this distinction fixes.
 *
 * Branch identity comes from each child's `_stepPath` (index-precise via branchScopeFromStepPath,
 * so switch cases / elif arms stay distinct), falling back to `_branchLabel` when no path exists.
 *
 * Conservative: returns true only when EXACTLY one arm is identifiable. With zero identifiable arms
 * (an empty container, or a canvas-authored container whose children lack the import-only
 * `_isChildOf`/`_stepPath` metadata) we keep the safe bottom-left corridor — straightening a wire we
 * can't prove is single-branch risks cutting through a multi-branch container's first branch.
 */
export function isSingleBranchContainer(nodes: Node[], nodeId: string): boolean {
  const container = nodes.find((n) => n.id === nodeId);
  const containerStepPath = propsOf(container)['_stepPath'] as string | undefined;
  const scopes = new Set<string>();
  for (const n of nodes) {
    const p = propsOf(n);
    if (p['_isChildOf'] !== nodeId) continue;
    const stepPath = p['_stepPath'];
    if (typeof stepPath === 'string' && stepPath.length > 0) {
      scopes.add(branchScopeFromStepPath(stepPath, containerStepPath));
    } else {
      const label = p['_branchLabel'];
      if (typeof label === 'string') scopes.add(label.toLowerCase());
    }
    if (scopes.size > 1) return false; // two distinct arms ⇒ multi-branch; stop early
  }
  return scopes.size === 1;
}

export function selectIsSingleBranchContainer(state: FlowStore, nodeId: string): boolean {
  return isSingleBranchContainer(state.nodes, nodeId);
}
