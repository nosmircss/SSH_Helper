import { describe, it, expect } from 'vitest';
import type { Connection, Node } from '@xyflow/react';
import { deriveChildMembership, applyChildMembership, clearConnectAuthoredMembership, MEMBERSHIP_MARKER } from '../childMembership';

function node(id: string, blockType: string, props: Record<string, unknown>): Node {
  return { id, type: 'block', position: { x: 0, y: 0 }, data: { blockType, props } } as Node;
}
function propsOf(n: Node | undefined): Record<string, unknown> {
  return ((n?.data as { props?: Record<string, unknown> } | undefined)?.props) ?? {};
}
const continueConn = (source: string, target: string): Connection =>
  ({ source, target, sourceHandle: 'continue', targetHandle: null });
const bottomConn = (source: string, target: string): Connection =>
  ({ source, target, sourceHandle: null, targetHandle: null });
const falseConn = (source: string, target: string): Connection =>
  ({ source, target, sourceHandle: 'false', targetHandle: null });
const CONTAINER = { sourceIsContainer: true };
const LEAF = { sourceIsContainer: false };

// Imported outer IF (top-level) with a THEN branch [a, b, innerIf]; innerIf is itself a nested
// container that is LAST in the branch, so its `continue` handle is free to wire a new block onto.
function nestedGraph(): Node[] {
  return [
    node('outerIf', 'if', { _stepPath: 'steps/0' }),
    node('a', 'send', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/0', _branchLabel: 'then', _branchColor: '#abc', _depth: 0 }),
    node('b', 'send', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/1', _branchLabel: 'then', _branchColor: '#abc', _depth: 0 }),
    node('innerIf', 'if', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/2', _branchLabel: 'then', _branchColor: '#abc', _depth: 0 }),
    node('innerThen', 'send', { _isChildOf: 'innerIf', _stepPath: 'steps/0/then/2/then/0', _branchLabel: 'then', _branchColor: '#def', _depth: 1 }),
    node('elseP', 'print', { _isChildOf: 'outerIf', _stepPath: 'steps/0/else/0', _branchLabel: 'else', _branchColor: '#f00', _depth: 0 }),
    node('print', 'print', {}), // fresh, metadata-less
  ];
}

describe('deriveChildMembership — gesture 1: container continue handle', () => {
  it('makes a continue-connected fresh block a sibling AFTER a nested container', () => {
    const m = deriveChildMembership(nestedGraph(), continueConn('innerIf', 'print'), CONTAINER);
    expect(m).not.toBeNull();
    expect(m!.targetId).toBe('print');
    expect(m!.props._isChildOf).toBe('outerIf');
    expect(m!.props._stepPath).toBe('steps/0/then/3'); // innerIf is then/2 → sibling then/3
    expect(m!.props._branchLabel).toBe('then'); // copied from the sibling container
    expect(m!.props[MEMBERSHIP_MARKER]).toBe(true);
    expect(m!.renumber).toEqual({ prefix: 'steps/0/then', fromIndex: 3 });
  });

  it('returns null for a top-level container continue (the spine walk already handles it)', () => {
    const nodes = [node('outerIf', 'if', { _stepPath: 'steps/0' }), node('tail', 'print', {})];
    expect(deriveChildMembership(nodes, continueConn('outerIf', 'tail'), CONTAINER)).toBeNull();
  });

  it('returns null when the source is a canvas-authored container without a _stepPath', () => {
    const nodes = [node('cif', 'if', {}), node('t', 'print', {})];
    expect(deriveChildMembership(nodes, continueConn('cif', 't'), CONTAINER)).toBeNull();
  });

  it('returns null when the target already carries membership (does not clobber)', () => {
    const nodes = nestedGraph().map((n) =>
      n.id === 'print' ? node('print', 'print', { _isChildOf: 'somethingElse', _stepPath: 'steps/9' }) : n,
    );
    expect(deriveChildMembership(nodes, continueConn('innerIf', 'print'), CONTAINER)).toBeNull();
  });
});

describe('deriveChildMembership — gesture 2: leaf bottom handle (add a step after a step)', () => {
  it('makes a fresh block a sibling AFTER a leaf inside a branch', () => {
    // wire b (a leaf at then/1) → print: print becomes then/2, pushing innerIf down.
    const m = deriveChildMembership(nestedGraph(), bottomConn('b', 'print'), LEAF);
    expect(m).not.toBeNull();
    expect(m!.props._isChildOf).toBe('outerIf');
    expect(m!.props._stepPath).toBe('steps/0/then/2');
    expect(m!.renumber).toEqual({ prefix: 'steps/0/then', fromIndex: 2 });
  });

  it('returns null for a top-level leaf (the spine walk follows its plain successor)', () => {
    const nodes = [node('s', 'send', { _stepPath: 'steps/0' }), node('print', 'print', {})];
    expect(deriveChildMembership(nodes, bottomConn('s', 'print'), LEAF)).toBeNull();
  });
});

describe('deriveChildMembership — gesture 3: container branch handle (start a branch)', () => {
  it('makes a fresh block the FIRST child of the else branch via the "false" handle', () => {
    const m = deriveChildMembership(nestedGraph(), falseConn('innerIf', 'print'), {
      sourceIsContainer: true,
      branchMetadata: { branchPath: 'else' },
    });
    expect(m).not.toBeNull();
    expect(m!.props._isChildOf).toBe('innerIf');
    expect(m!.props._stepPath).toBe('steps/0/then/2/else/0');
    expect(m!.props[MEMBERSHIP_MARKER]).toBe(true);
    expect(m!.renumber).toEqual({ prefix: 'steps/0/then/2/else', fromIndex: 0 });
  });

  it('uses the inferred branchPath for an elif arm off the bottom handle', () => {
    const m = deriveChildMembership(nestedGraph(), bottomConn('outerIf', 'print'), {
      sourceIsContainer: true,
      branchMetadata: { branchPath: 'elif/0/then' },
    });
    expect(m!.props._isChildOf).toBe('outerIf');
    expect(m!.props._stepPath).toBe('steps/0/elif/0/then/0');
  });

  it('returns null when no branch metadata resolves (nothing to anchor the child to)', () => {
    expect(deriveChildMembership(nestedGraph(), bottomConn('outerIf', 'print'), { sourceIsContainer: true })).toBeNull();
  });
});

describe('applyChildMembership', () => {
  it('writes the derived membership onto the target node', () => {
    const nodes = nestedGraph();
    const m = deriveChildMembership(nodes, continueConn('innerIf', 'print'), CONTAINER)!;
    const out = applyChildMembership(nodes, m);
    expect(propsOf(out.find((n) => n.id === 'print'))._isChildOf).toBe('outerIf');
    expect(propsOf(out.find((n) => n.id === 'print'))._stepPath).toBe('steps/0/then/3');
  });

  it('flags the ancestor container(s) for graph re-export so the change survives a YAML round-trip', () => {
    const nodes = nestedGraph();
    const m = deriveChildMembership(nodes, continueConn('innerIf', 'print'), CONTAINER)!;
    const out = applyChildMembership(nodes, m);
    expect(propsOf(out.find((n) => n.id === 'outerIf'))._forceGraphExport).toBe(true);
  });

  it('does not disturb siblings before the insertion point', () => {
    const nodes = nestedGraph();
    const m = deriveChildMembership(nodes, continueConn('innerIf', 'print'), CONTAINER)!;
    const out = applyChildMembership(nodes, m);
    expect(propsOf(out.find((n) => n.id === 'a'))._stepPath).toBe('steps/0/then/0');
    expect(propsOf(out.find((n) => n.id === 'b'))._stepPath).toBe('steps/0/then/1');
    expect(propsOf(out.find((n) => n.id === 'innerIf'))._stepPath).toBe('steps/0/then/2');
    expect(propsOf(out.find((n) => n.id === 'innerThen'))._stepPath).toBe('steps/0/then/2/then/0');
  });

  it('renumbers later siblings AND their subtrees when inserting mid-branch', () => {
    const nodes: Node[] = [
      node('outerIf', 'if', { _stepPath: 'steps/0' }),
      node('innerIf', 'if', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/2', _branchLabel: 'then' }),
      node('cfg', 'send', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/3', _branchLabel: 'then' }),
      node('setIf', 'if', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/4', _branchLabel: 'then' }),
      node('setChild', 'send', { _isChildOf: 'setIf', _stepPath: 'steps/0/then/4/then/0', _branchLabel: 'then' }),
      node('print', 'print', {}),
    ];
    const m = deriveChildMembership(nodes, continueConn('innerIf', 'print'), CONTAINER)!;
    const out = applyChildMembership(nodes, m);
    const sp = (id: string) => propsOf(out.find((n) => n.id === id))._stepPath;
    expect(sp('print')).toBe('steps/0/then/3'); // inserted into the vacated slot
    expect(sp('cfg')).toBe('steps/0/then/4'); // bumped 3 → 4
    expect(sp('setIf')).toBe('steps/0/then/5'); // bumped 4 → 5
    expect(sp('setChild')).toBe('steps/0/then/5/then/0'); // subtree index bumped 4 → 5
    expect(sp('innerIf')).toBe('steps/0/then/2'); // unchanged (before the insertion point)
  });

  it('does not bump sibling branches that merely share a path prefix', () => {
    const nodes: Node[] = [
      node('outerIf', 'if', { _stepPath: 'steps/0' }),
      node('innerIf', 'if', { _isChildOf: 'outerIf', _stepPath: 'steps/0/then/0', _branchLabel: 'then' }),
      node('elseChild', 'send', { _isChildOf: 'outerIf', _stepPath: 'steps/0/else/0', _branchLabel: 'else' }),
      node('print', 'print', {}),
    ];
    const m = deriveChildMembership(nodes, continueConn('innerIf', 'print'), CONTAINER)!;
    const out = applyChildMembership(nodes, m);
    expect(propsOf(out.find((n) => n.id === 'elseChild'))._stepPath).toBe('steps/0/else/0');
  });
});

describe('clearConnectAuthoredMembership', () => {
  it('reverts wire-authored membership when the conferring edge is deleted', () => {
    const nodes = applyChildMembership(
      nestedGraph(),
      deriveChildMembership(nestedGraph(), continueConn('innerIf', 'print'), CONTAINER)!,
    );
    expect(propsOf(nodes.find((n) => n.id === 'print'))._isChildOf).toBe('outerIf');

    const out = clearConnectAuthoredMembership(nodes, [{ target: 'print' }]);
    const p = propsOf(out.find((n) => n.id === 'print'));
    expect(p._isChildOf).toBeUndefined();
    expect(p._stepPath).toBeUndefined();
    expect(p[MEMBERSHIP_MARKER]).toBeUndefined();
  });

  it('never touches imported (non-marked) membership', () => {
    const nodes = nestedGraph(); // 'a' is imported, no marker
    const out = clearConnectAuthoredMembership(nodes, [{ target: 'a' }]);
    expect(propsOf(out.find((n) => n.id === 'a'))._isChildOf).toBe('outerIf');
    expect(propsOf(out.find((n) => n.id === 'a'))._stepPath).toBe('steps/0/then/0');
  });

  it('is a no-op when no removed edge targets a node', () => {
    const nodes = nestedGraph();
    expect(clearConnectAuthoredMembership(nodes, [{ target: null }])).toBe(nodes);
  });
});
