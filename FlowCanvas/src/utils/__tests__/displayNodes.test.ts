import { describe, it, expect } from 'vitest';
import type { Node } from '@xyflow/react';
import { contentSizeComment, orderCommentsBehind } from '../displayNodes';

const mk = (id: string, type: string, extra: Partial<Node> = {}): Node =>
  ({ id, type, position: { x: 0, y: 0 }, data: {}, ...extra }) as Node;

describe('contentSizeComment', () => {
  it('strips a comment node fixed box so React Flow auto-measures the card', () => {
    const c = mk('c1', 'comment', { style: { width: 200, height: 100, background: 'yellow' } });
    const out = contentSizeComment(c);
    expect(out.style).not.toHaveProperty('width');
    expect(out.style).not.toHaveProperty('height');
    expect(out.style).toMatchObject({ background: 'yellow' }); // unrelated style preserved
    expect(out.width).toBeUndefined();
    expect(out.height).toBeUndefined();
  });

  it('leaves block nodes untouched', () => {
    const b = mk('b1', 'block', { style: { width: 330 }, width: 330 });
    const out = contentSizeComment(b);
    expect(out).toBe(b); // same reference, no rewrite
  });

  it('handles a comment with no style without crashing', () => {
    const c = mk('c2', 'comment');
    const out = contentSizeComment(c);
    expect(out.width).toBeUndefined();
    expect(out.height).toBeUndefined();
  });
});

describe('orderCommentsBehind', () => {
  it('renders comments before blocks (comments behind, blocks on top)', () => {
    const nodes = [
      mk('blockA', 'block'),
      mk('c1', 'comment'),
      mk('blockB', 'block'),
      mk('c2', 'comment'),
      mk('start', 'start'),
    ];
    const out = orderCommentsBehind(nodes).map((n) => n.id);
    // comments first, in original relative order; everything else after, in original relative order
    expect(out).toEqual(['c1', 'c2', 'blockA', 'blockB', 'start']);
  });

  it('does not mutate the input array', () => {
    const nodes = [mk('blockA', 'block'), mk('c1', 'comment')];
    orderCommentsBehind(nodes);
    expect(nodes.map((n) => n.id)).toEqual(['blockA', 'c1']);
  });
});
