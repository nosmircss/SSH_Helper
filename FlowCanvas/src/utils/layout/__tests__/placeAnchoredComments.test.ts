import { describe, it, expect } from 'vitest';
import { placeAnchoredComments } from '../placeAnchoredComments';

describe('placeAnchoredComments', () => {
  it('places a leading comment above its attached block', () => {
    const nodes = [
      { id: 'b1', type: 'block', position: { x: 100, y: 200 }, data: {} },
      { id: 'c1', type: 'comment', position: { x: 0, y: 0 },
        data: { attachedToNodeId: 'b1', anchor: { type: 'leading' } } },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position.x).toBe(100);
    expect(c1.position.y).toBeLessThan(200);
  });

  it('does not move sticky-kind comments (no anchor)', () => {
    // kind:'sticky' notes have no anchor property — they are free-floating
    const nodes = [
      { id: 'b1', type: 'block', position: { x: 100, y: 200 }, data: {} },
      { id: 'c1', type: 'comment', position: { x: 50, y: 50 },
        data: { attachedToNodeId: 'b1', kind: 'sticky' } },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position.x).toBe(50);
    expect(c1.position.y).toBe(50);
  });

  it('does not move inline-anchor comments', () => {
    // inline is a valid NoteAnchor type but placeAnchoredComments only repositions 'leading' and 'header'
    const nodes = [
      { id: 'b1', type: 'block', position: { x: 100, y: 200 }, data: {} },
      { id: 'c1', type: 'comment', position: { x: 50, y: 50 },
        data: { attachedToNodeId: 'b1', anchor: { type: 'inline', stepPath: 'steps/0' } } },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position.x).toBe(50);
    expect(c1.position.y).toBe(50);
  });

  it('does not move a comment with no attachedToNodeId', () => {
    const nodes = [
      { id: 'c1', type: 'comment', position: { x: 10, y: 20 },
        data: { anchor: { type: 'leading' } } },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position).toEqual({ x: 10, y: 20 });
  });

  it('does not move non-comment nodes', () => {
    const nodes = [
      { id: 'b1', type: 'block', position: { x: 100, y: 200 }, data: {} },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    expect(out[0].position).toEqual({ x: 100, y: 200 });
  });

  it('handles header anchor type', () => {
    const nodes = [
      { id: '__start__', type: 'start', position: { x: 200, y: 0 }, data: {} },
      { id: 'c1', type: 'comment', position: { x: 0, y: 0 },
        data: { attachedToNodeId: '__start__', anchor: { type: 'header' } } },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    const c1 = out.find((n) => n.id === 'c1')!;
    expect(c1.position.x).toBe(200);
    expect(c1.position.y).toBeLessThan(0);
  });

  it('returns a new array (pure function)', () => {
    const nodes = [
      { id: 'b1', type: 'block', position: { x: 0, y: 0 }, data: {} },
    ] as never[];
    const out = placeAnchoredComments(nodes);
    expect(out).not.toBe(nodes);
  });
});
