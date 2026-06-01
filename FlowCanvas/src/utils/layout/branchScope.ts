/**
 * Branch scope = the path segment(s) identifying which branch of a container a child
 * belongs to, e.g. 'then', 'else', 'cases/1/do', 'parallel/0'. Sibling branches have
 * distinct scopes (so switch cases / parallel arms / elifs do not collapse together).
 */
export function branchScopeFromStepPath(
  childStepPath: string,
  containerStepPath: string | undefined,
): string {
  let rest = childStepPath;
  if (containerStepPath && childStepPath.startsWith(`${containerStepPath}/`)) {
    rest = childStepPath.slice(containerStepPath.length + 1);
  }
  // Drop the trailing child index ('/0', '/3', ...).
  return rest.replace(/\/\d+$/, '');
}

/** Canvas-built edges already carry the branch scope as their branchPath. */
export function branchScopeFromBranchPath(branchPath: string): string {
  return branchPath;
}

const HEAD_RANK: Record<string, number> = {
  then: 0, do: 0, loop: 0, try: 0, parallel: 0,
  elif: 1, catch: 1,
  else: 2, finally: 2,
  cases: 3, case: 3,
  default: 4,
};

/** Left-to-right ordering key for a branch scope. */
export function branchSortRank(scope: string): number {
  const segs = scope.split('/');
  const head = segs[0];
  const idx = segs.length > 1 && /^\d+$/.test(segs[1]) ? Number(segs[1]) : 0;
  const rank = HEAD_RANK[head] ?? 9;
  return rank * 1000 + idx;
}
