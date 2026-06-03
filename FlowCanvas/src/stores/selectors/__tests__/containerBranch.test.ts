import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { isSingleBranchContainer } from '../containerBranch';

const container = (id: string, stepPath: string): Node =>
  ({ id, position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _stepPath: stepPath } } } as Node);
const child = (id: string, parent: string, stepPath: string): Node =>
  ({ id, position: { x: 0, y: 0 }, data: { props: { _isChildOf: parent, _stepPath: stepPath } } } as Node);

describe('isSingleBranchContainer', () => {
  it('treats a then-only IF as single-branch', () => {
    const nodes = [container('if', 'steps/0'), child('t', 'if', 'steps/0/then/0')];
    expect(isSingleBranchContainer(nodes, 'if')).toBe(true);
  });

  it('treats a then+else IF as multi-branch', () => {
    const nodes = [
      container('if', 'steps/0'),
      child('t', 'if', 'steps/0/then/0'),
      child('e', 'if', 'steps/0/else/0'),
    ];
    expect(isSingleBranchContainer(nodes, 'if')).toBe(false);
  });

  it('treats a loop body (multiple steps, one arm) as single-branch', () => {
    const nodes = [
      container('lp', 'steps/0'),
      child('b1', 'lp', 'steps/0/do/0'),
      child('b2', 'lp', 'steps/0/do/1'),
    ];
    expect(isSingleBranchContainer(nodes, 'lp')).toBe(true);
  });

  it('treats a switch with multiple cases as multi-branch', () => {
    const nodes = [
      container('sw', 'steps/0'),
      child('c0', 'sw', 'steps/0/cases/0/0'),
      child('c1', 'sw', 'steps/0/cases/1/0'),
    ];
    expect(isSingleBranchContainer(nodes, 'sw')).toBe(false);
  });

  it('treats a try/catch as multi-branch', () => {
    const nodes = [
      container('tr', 'steps/0'),
      child('t', 'tr', 'steps/0/try/0'),
      child('c', 'tr', 'steps/0/catch/0'),
    ];
    expect(isSingleBranchContainer(nodes, 'tr')).toBe(false);
  });

  it('treats a container with no identifiable branch children as NOT single-branch (safe Left-corridor default)', () => {
    // Canvas-authored children may lack _isChildOf/_stepPath (import-only metadata). With no arm
    // identifiable we must NOT straighten the continuation, or a multi-branch container whose first
    // branch sits under the spine would get a wire cutting through it. Default to the safe corridor.
    expect(isSingleBranchContainer([container('if', 'steps/0')], 'if')).toBe(false);
  });
});
