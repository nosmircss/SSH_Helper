import { describe, it, expect } from 'vitest';
import { branchScopeFromStepPath, branchScopeFromBranchPath, branchSortRank } from '../branchScope';

describe('branchScopeFromStepPath', () => {
  it('extracts the branch scope between container path and child index', () => {
    expect(branchScopeFromStepPath('steps/0/then/0', 'steps/0')).toBe('then');
    expect(branchScopeFromStepPath('steps/1/cases/2/do/0', 'steps/1')).toBe('cases/2/do');
    expect(branchScopeFromStepPath('steps/0/else/3', 'steps/0')).toBe('else');
  });

  it('falls back to dropping only the trailing index when the prefix does not match', () => {
    expect(branchScopeFromStepPath('then/0', undefined)).toBe('then');
  });
});

describe('branchScopeFromBranchPath', () => {
  it('passes canvas-built branch paths through unchanged', () => {
    expect(branchScopeFromBranchPath('then')).toBe('then');
    expect(branchScopeFromBranchPath('cases/1/do')).toBe('cases/1/do');
    expect(branchScopeFromBranchPath('parallel/0')).toBe('parallel/0');
  });
});

describe('branchSortRank', () => {
  it('orders if branches then < elif < else', () => {
    expect(branchSortRank('then')).toBeLessThan(branchSortRank('elif/0/then'));
    expect(branchSortRank('elif/0/then')).toBeLessThan(branchSortRank('else'));
  });
  it('orders switch cases numerically', () => {
    expect(branchSortRank('cases/0/do')).toBeLessThan(branchSortRank('cases/1/do'));
  });
  it('orders try < catch < finally', () => {
    expect(branchSortRank('try')).toBeLessThan(branchSortRank('catch'));
    expect(branchSortRank('catch')).toBeLessThan(branchSortRank('finally'));
  });
});
