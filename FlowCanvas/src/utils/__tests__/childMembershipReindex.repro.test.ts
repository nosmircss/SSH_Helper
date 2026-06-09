import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { deriveChildMembership, applyChildMembership, renumberStepPaths } from '../childMembership';

// Reproduces the user's bug: add a block -> connect it into a loop body -> delete it.
// The connect bumps later siblings' _stepPath +1; no delete path renumbers them back down,
// leaving the body's _stepPath indices gapped/inflated => runtime step-path map diverges
// from the executor's sequential paths => neon + per-block output die mid-loop.

function child(id: string, blockType: string, stepPath: string, parent: string): Node {
  return {
    id, position: { x: 0, y: 0 }, type: 'block',
    data: { blockType, props: { _isChildOf: parent, _stepPath: stepPath } },
  } as Node;
}

describe('childMembership add+connect+delete leaves stale _stepPath (repro)', () => {
  it('body _stepPath indices are no longer contiguous after delete', () => {
    const multiselect: Node = { id: 'M', position: { x: 0, y: 0 }, type: 'block',
      data: { blockType: 'multiselect', props: { _stepPath: 'steps/0' } } } as Node;
    const foreach: Node = { id: 'F', position: { x: 0, y: 0 }, type: 'block',
      data: { blockType: 'foreach', props: { _stepPath: 'steps/1' } } } as Node;
    let nodes: Node[] = [
      multiselect,
      foreach,
      child('p', 'print', 'steps/1/do/0', 'F'),
      child('s1', 'send', 'steps/1/do/1', 'F'),
      child('s2', 'send', 'steps/1/do/2', 'F'),
    ];
    // New block dropped fresh (no _stepPath), wired via the print's bottom handle (successor).
    const fresh: Node = { id: 'NEW', position: { x: 0, y: 0 }, type: 'block',
      data: { blockType: 'send', props: {} } } as Node;
    nodes = [...nodes, fresh];

    const membership = deriveChildMembership(
      nodes,
      { source: 'p', target: 'NEW', sourceHandle: '', targetHandle: null },
      { sourceIsContainer: false },
    );
    expect(membership).not.toBeNull();
    nodes = applyChildMembership(nodes, membership!);

    // After connect: NEW=do/1, s1 bumped do/1->do/2, s2 do/2->do/3.
    const pathOf = (id: string) =>
      (nodes.find((n) => n.id === id)!.data as any).props._stepPath as string;
    expect(pathOf('NEW')).toBe('steps/1/do/1');
    expect(pathOf('s1')).toBe('steps/1/do/2');
    expect(pathOf('s2')).toBe('steps/1/do/3');

    // Delete NEW the way removeNodes does: drop the node, then renumber survivors (the fix).
    nodes = renumberStepPaths(nodes.filter((n) => n.id !== 'NEW'));

    const bodyIdx = nodes
      .filter((n) => (n.data as any).props?._isChildOf === 'F')
      .map((n) => Number(((n.data as any).props._stepPath as string).split('/').pop()))
      .sort((a, b) => a - b);

    // FIXED: surviving body children are contiguous 0,1,2 again — matches the executor's sequential
    // path assignment on the exported YAML, so every step's events resolve.
    expect(bodyIdx).toEqual([0, 1, 2]);
    expect(nodes.find((n) => n.id === 'p')!.data as any).toMatchObject({ props: { _stepPath: 'steps/1/do/0' } });
    expect(nodes.find((n) => n.id === 's1')!.data as any).toMatchObject({ props: { _stepPath: 'steps/1/do/1' } });
    expect(nodes.find((n) => n.id === 's2')!.data as any).toMatchObject({ props: { _stepPath: 'steps/1/do/2' } });
  });
});
