import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { renumberStepPaths } from '../childMembership';

function node(id: string, stepPath: string | undefined, isChildOf?: string, type = 'block'): Node {
  const props: Record<string, unknown> = {};
  if (stepPath !== undefined) props._stepPath = stepPath;
  if (isChildOf !== undefined) props._isChildOf = isChildOf;
  return { id, position: { x: 0, y: 0 }, type, data: { blockType: 'send', props } } as Node;
}

const pathOf = (nodes: Node[], id: string) =>
  (nodes.find((n) => n.id === id)!.data as any).props._stepPath as string | undefined;

describe('renumberStepPaths', () => {
  it('closes a gap in a loop body left by a deleted nested block', () => {
    // do/1 was deleted; survivors carry stale do/0, do/2, do/3.
    const nodes = renumberStepPaths([
      node('M', 'steps/0'), // a top-level sibling keeps the foreach at steps/1
      node('F', 'steps/1'),
      node('a', 'steps/1/do/0', 'F'),
      node('b', 'steps/1/do/2', 'F'),
      node('c', 'steps/1/do/3', 'F'),
    ]);
    expect(pathOf(nodes, 'a')).toBe('steps/1/do/0');
    expect(pathOf(nodes, 'b')).toBe('steps/1/do/1');
    expect(pathOf(nodes, 'c')).toBe('steps/1/do/2');
  });

  it('renumbers top-level steps after a deletion', () => {
    const nodes = renumberStepPaths([
      node('m', 'steps/0'),
      node('f', 'steps/2'), // steps/1 deleted
    ]);
    expect(pathOf(nodes, 'm')).toBe('steps/0');
    expect(pathOf(nodes, 'f')).toBe('steps/1');
  });

  it('renumbers each branch independently and recurses into nested containers', () => {
    // if at steps/0; then-branch has a gap (then/0, then/2 -> then/0, then/1) and then/2 is itself
    // a nested if whose own then-child must be rebuilt under the new parent path.
    const nodes = renumberStepPaths([
      node('if', 'steps/0'),
      node('t0', 'steps/0/then/0', 'if'),
      node('nestedIf', 'steps/0/then/2', 'if'),
      node('nt', 'steps/0/then/2/then/0', 'nestedIf'),
      node('e0', 'steps/0/else/1', 'if'), // else gap: should become else/0
    ]);
    expect(pathOf(nodes, 't0')).toBe('steps/0/then/0');
    expect(pathOf(nodes, 'nestedIf')).toBe('steps/0/then/1');
    expect(pathOf(nodes, 'nt')).toBe('steps/0/then/1/then/0'); // rebuilt under renumbered parent
    expect(pathOf(nodes, 'e0')).toBe('steps/0/else/0');
  });

  it('preserves elif/case selector indices, renumbering only the step index', () => {
    const nodes = renumberStepPaths([
      node('sw', 'steps/0'),
      node('c', 'steps/0/cases/2/do/1', 'sw'), // gap at cases/2/do/0
    ]);
    expect(pathOf(nodes, 'c')).toBe('steps/0/cases/2/do/0');
  });

  it('leaves canvas-authored nodes (no _stepPath) and comments untouched', () => {
    const input = [
      node('a', 'steps/0'),
      node('fresh', undefined),               // no _stepPath
      node('cmt', 'steps/0/do/0', 'a', 'comment'),
    ];
    const nodes = renumberStepPaths(input);
    expect(pathOf(nodes, 'fresh')).toBeUndefined();
    // comment node keeps whatever it had; it isn't an executable step
    expect(nodes.find((n) => n.id === 'cmt')).toBe(input[2]);
  });

  it('returns the same array reference when nothing changes (already contiguous)', () => {
    const input = [node('a', 'steps/0'), node('b', 'steps/1')];
    expect(renumberStepPaths(input)).toBe(input);
  });

  it('leaves orphans (dangling parent) untouched', () => {
    const nodes = renumberStepPaths([
      node('a', 'steps/0'),
      node('orphan', 'steps/9/do/3', 'ghost'), // parent "ghost" not present
    ]);
    expect(pathOf(nodes, 'orphan')).toBe('steps/9/do/3');
  });
});
