import { describe, it, expect } from 'vitest';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING, estimateCommentStep } from '../hierarchicalLayout';

describe('estimateCommentStep compact multiline', () => {
  it('grows the compact pill reserve linearly per authored newline', () => {
    const one = estimateCommentStep('one line', true);
    const two = estimateCommentStep('line one\nline two', true);
    const three = estimateCommentStep('a\nb\nc', true);
    expect(two).toBeGreaterThan(one);
    expect(three).toBeGreaterThan(two);
    expect(two - one).toBe(three - two); // linear per extra line
  });
});

describe('computeHierarchicalLayout anchored comments', () => {
  const baseNodes = () => [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'b1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', props: { command: 'x' } } },
  ];
  const edges = [{ id: 'e0', source: '__start__', target: 'b1' }];

  it('places an anchored leading comment above its block, not in the right gutter', () => {
    const nodes = [
      ...baseNodes(),
      { id: 'c1', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c1', kind: 'comment', text: 'note', anchor: { type: 'leading', stepPath: 'steps/0' }, attachedToNodeId: 'b1' } },
    ];
    const out = computeHierarchicalLayout(nodes as never, edges as never, DEFAULT_BLOCK_SIZING);
    const b1 = out.find((n) => n.id === 'b1')!;
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position.x).toBe(b1.position.x);          // same column as its block, not the gutter
    expect(c1.position.y).toBeLessThan(b1.position.y);  // directly above it
  });

  it('places a comment anchored to a deeply-nested branch child above that child', () => {
    // Mirrors the real case: a comment anchored to an if/else branch child node.
    const nodes = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'if1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { condition: 'a', _stepPath: 'steps/0' } } },
      { id: 'thenChild', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 't', _isChildOf: 'if1', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } },
      { id: 'elseChild', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'e', _isChildOf: 'if1', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } },
      { id: 'cElse', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'cElse', kind: 'comment', text: 'nothing to do', anchor: { type: 'leading', stepPath: 'steps/0/else/0' }, attachedToNodeId: 'elseChild' } },
    ];
    const nestedEdges = [
      { id: 'e0', source: '__start__', target: 'if1' },
      { id: 'e1', source: 'if1', target: 'thenChild', label: 'then' },
      { id: 'e2', source: 'if1', target: 'elseChild', sourceHandle: 'false', label: 'else' },
    ];
    const out = computeHierarchicalLayout(nodes as never, nestedEdges as never, DEFAULT_BLOCK_SIZING);
    const elseChild = out.find((n) => n.id === 'elseChild')!;
    const cElse = out.find((n) => n.id === 'cElse')!;
    expect(cElse.position.x).toBe(elseChild.position.x);          // above the branch child, not gutter
    expect(cElse.position.y).toBeLessThan(elseChild.position.y);
  });

  it('places a BRANCH comment above the band header and a LEADING comment inside (above the block)', () => {
    const nodes = [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'if1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { condition: 'a', _stepPath: 'steps/0' } } },
      { id: 'elseChild', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'e', _isChildOf: 'if1', _stepPath: 'steps/0/else/0', _branchLabel: 'else' } } },
      { id: 'cBranch', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'cBranch', kind: 'comment', text: 'b', anchor: { type: 'branch', stepPath: 'steps/0/else/0' }, attachedToNodeId: 'elseChild' } },
      { id: 'cLead', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'cLead', kind: 'comment', text: 'l', anchor: { type: 'leading', stepPath: 'steps/0/else/0' }, attachedToNodeId: 'elseChild' } },
    ];
    const edges = [
      { id: 'e0', source: '__start__', target: 'if1' },
      { id: 'e1', source: 'if1', target: 'elseChild', sourceHandle: 'false', label: 'else' },
    ];
    const out = computeHierarchicalLayout(nodes as never, edges as never, DEFAULT_BLOCK_SIZING);
    const elseChild = out.find((n) => n.id === 'elseChild')!;
    const cBranch = out.find((n) => n.id === 'cBranch')!;
    const cLead = out.find((n) => n.id === 'cLead')!;
    // leading sits just above the block (inside the band, which grows to wrap it)
    expect(cLead.position.y).toBe(elseChild.position.y - 28);
    // branch sits above the (grown) band header: blockY - L*28 - BAND_PAD(18) - HEADROOM(12) - 28
    expect(cBranch.position.y).toBeLessThan(cLead.position.y - 30);
  });

  it('reserves vertical space so a commented block sits lower than an uncommented one', () => {
    const base = () => [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'a', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
      { id: 'b', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
    ];
    const e = [{ id: 'e0', source: '__start__', target: 'a' }, { id: 'e1', source: 'a', target: 'b' }];
    const without = computeHierarchicalLayout(base() as never, e as never, DEFAULT_BLOCK_SIZING);
    const withComment = computeHierarchicalLayout([
      ...base(),
      { id: 'c', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c', kind: 'comment', text: 'x', anchor: { type: 'leading', stepPath: 'steps/1' }, attachedToNodeId: 'b' } },
    ] as never, e as never, DEFAULT_BLOCK_SIZING);
    const bWithout = without.find((n) => n.id === 'b')!.position.y;
    const bWith = withComment.find((n) => n.id === 'b')!.position.y;
    expect(bWith).toBeGreaterThan(bWithout); // 'b' pushed down to make room for its comment pill
  });

  it('reserves more space for a non-compact (card) comment than a compact pill', () => {
    const mk = () => [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'a', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
      { id: 'b', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
      { id: 'c', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c', kind: 'comment', text: 'a longer multi-word note that wraps across lines', anchor: { type: 'leading', stepPath: 'steps/1' }, attachedToNodeId: 'b' } },
    ];
    const e = [{ id: 'e0', source: '__start__', target: 'a' }, { id: 'e1', source: 'a', target: 'b' }];
    const compactOut = computeHierarchicalLayout(mk() as never, e as never, { ...DEFAULT_BLOCK_SIZING, compactComments: true });
    const fullOut = computeHierarchicalLayout(mk() as never, e as never, { ...DEFAULT_BLOCK_SIZING, compactComments: false });
    const bCompact = compactOut.find((n) => n.id === 'b')!.position.y;
    const bFull = fullOut.find((n) => n.id === 'b')!.position.y;
    expect(bFull).toBeGreaterThan(bCompact); // a non-compact card reserves more vertical room than a pill
  });

  it('reserves more vertical room for a multiline compact pill than a single-line one', () => {
    const mk = (text: string) => [
      { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
      { id: 'a', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
      { id: 'b', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
      { id: 'c', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c', kind: 'comment', text, anchor: { type: 'leading', stepPath: 'steps/1' }, attachedToNodeId: 'b' } },
    ];
    const e = [{ id: 'e0', source: '__start__', target: 'a' }, { id: 'e1', source: 'a', target: 'b' }];
    const sizing = { ...DEFAULT_BLOCK_SIZING, compactComments: true };
    const single = computeHierarchicalLayout(mk('one') as never, e as never, sizing);
    const multi = computeHierarchicalLayout(mk('one\ntwo\nthree') as never, e as never, sizing);
    const bSingle = single.find((n) => n.id === 'b')!.position.y;
    const bMulti = multi.find((n) => n.id === 'b')!.position.y;
    expect(bMulti).toBeGreaterThan(bSingle); // multiline pill pushes its block lower
  });

  it('still gutters a free-floating sticky (no anchor)', () => {
    const nodes = [
      ...baseNodes(),
      { id: 's1', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 's1', kind: 'sticky', text: 'todo' } },
    ];
    const out = computeHierarchicalLayout(nodes as never, edges as never, DEFAULT_BLOCK_SIZING);
    const b1 = out.find((n) => n.id === 'b1')!;
    const s1 = out.find((n) => n.id === 's1')!;
    expect(s1.position.x).toBeGreaterThan(b1.position.x); // unanchored sticky stays in the right gutter
  });
});
