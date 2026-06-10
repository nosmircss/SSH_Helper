import { describe, it, expect } from 'vitest';
import type { Edge, Node } from '@xyflow/react';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../hierarchicalLayout';
import { BLOCK_WIDTH_INSET, COLLAPSED_HEIGHT } from '../../nodeSize';
import { computeBranchBands } from '../../branchBands';

const chain = (): { nodes: Node[]; edges: Edge[] } => ({
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'a' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'b' } } },
  ] as never,
  edges: [
    { id: 'e0', source: '__start__', target: 'A' },
    { id: 'e1', source: 'A', target: 'B' },
  ] as never,
});

describe('computeHierarchicalLayout sizing param', () => {
  it('DEFAULT_BLOCK_SIZING reproduces the historical fixed geometry (regression guard)', () => {
    const { nodes, edges } = chain();
    const out = computeHierarchicalLayout(nodes, edges, DEFAULT_BLOCK_SIZING);
    const a = out.find((n) => n.id === 'A')!.position;
    const b = out.find((n) => n.id === 'B')!.position;
    // NODE_START_X=250; first spine row at NODE_START_Y(40)+nodeSpacingY(106)=146; +106 per collapsed row.
    expect(a).toEqual({ x: 250, y: 146 });
    expect(b).toEqual({ x: 250, y: 252 });
  });

  it('roomy density pushes a downstream block further down', () => {
    const { nodes, edges } = chain();
    const normal = computeHierarchicalLayout(nodes, edges, { blockWidth: 330, density: 1, textScale: 1 });
    const roomy = computeHierarchicalLayout(nodes, edges, { blockWidth: 330, density: 1.2, textScale: 1 });
    const yN = normal.find((n) => n.id === 'B')!.position.y;
    const yR = roomy.find((n) => n.id === 'B')!.position.y;
    expect(yR).toBeGreaterThan(yN);
  });
});

// ---------------------------------------------------------------------------
// Invariant: at max blockWidth (700) a nested-branch layout has NO overlapping nodes.
//
// Fixture: start → if-1 (if/else)
//   then branch: loop-1 (foreach) → body-1 (nested child, deepest indentation)
//   else branch: else-1 (plain block)
//
// This covers: multi-branch layout at max width, nested single-branch inside one arm,
// and the critical invariant that branchOffset scales with blockWidth so wide blocks
// don't bleed into adjacent columns.
// ---------------------------------------------------------------------------

function nestedBranchFixture(): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [
    {
      id: '__start__',
      type: 'start',
      position: { x: 0, y: 0 },
      data: { blockType: '_start', props: {} },
    },
    // if container — spine node (no _isChildOf)
    {
      id: 'if-1',
      type: 'block',
      position: { x: 0, y: 0 },
      data: { blockType: 'if', props: { _stepPath: 'steps/0', condition: 'true' } },
    },
    // foreach container — child of if-1 (then branch)
    {
      id: 'loop-1',
      type: 'block',
      position: { x: 0, y: 0 },
      data: {
        blockType: 'foreach',
        props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then', items: '[]', var: 'item' },
      },
    },
    // body block — child of loop-1 (do branch, deepest nesting)
    {
      id: 'body-1',
      type: 'block',
      position: { x: 0, y: 0 },
      data: {
        blockType: 'print',
        props: { _isChildOf: 'loop-1', _stepPath: 'steps/0/then/0/do/0', _branchLabel: 'do', message: 'hello' },
      },
    },
    // else child — child of if-1 (else branch)
    {
      id: 'else-1',
      type: 'block',
      position: { x: 0, y: 0 },
      data: {
        blockType: 'print',
        props: { _isChildOf: 'if-1', _stepPath: 'steps/0/else/0', _branchLabel: 'else', message: 'no' },
      },
    },
  ] as never;

  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' },
    { id: 'e1', source: 'if-1', target: 'loop-1' },              // then branch
    { id: 'e2', source: 'if-1', target: 'else-1', sourceHandle: 'false' }, // else branch
    { id: 'e3', source: 'loop-1', target: 'body-1' },             // foreach do body
  ] as never;

  return { nodes, edges };
}

describe('no block overlap at wide blockWidths — invariant guard', () => {
  const MAX_WIDTH = 700;

  const rectOf = (n: Node, width: number) => {
    const isChild = !!(n.data as Record<string, unknown> & { props?: Record<string, unknown> })?.props?.['_isChildOf'];
    const w = isChild ? width - BLOCK_WIDTH_INSET : width;
    return { x: n.position.x, y: n.position.y, w, h: COLLAPSED_HEIGHT };
  };

  const overlaps = (
    a: { x: number; y: number; w: number; h: number },
    b: { x: number; y: number; w: number; h: number },
  ) => a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;

  // 700 = the old "Max" preset; 2000 = the width slider's upper bound (BLOCK_WIDTH_MAX).
  it.each([700, 2000])('no two block/start nodes overlap after layout at blockWidth %i', (width) => {
    const { nodes, edges } = nestedBranchFixture();
    const placed = computeHierarchicalLayout(nodes, edges, { blockWidth: width, density: 1, textScale: 1 });

    const layouted = placed.filter((n) => n.type === 'block' || n.type === 'start');

    for (let i = 0; i < layouted.length; i++) {
      for (let j = i + 1; j < layouted.length; j++) {
        const p = layouted[i];
        const q = layouted[j];
        const rp = rectOf(p, width);
        const rq = rectOf(q, width);
        expect(
          overlaps(rp, rq),
          `Nodes "${p.id}" (x=${rp.x} y=${rp.y} w=${rp.w} h=${rp.h}) and ` +
          `"${q.id}" (x=${rq.x} y=${rq.y} w=${rq.w} h=${rq.h}) overlap — ` +
          `branchOffset must scale with blockWidth to keep columns clear`,
        ).toBe(false);
      }
    }
  });

  it('then-branch child and else-branch child are horizontally separated by at least one child width', () => {
    const { nodes, edges } = nestedBranchFixture();
    const placed = computeHierarchicalLayout(nodes, edges, { blockWidth: MAX_WIDTH, density: 1, textScale: 1 });

    const thenHead = placed.find((n) => n.id === 'loop-1')!;
    const elseHead = placed.find((n) => n.id === 'else-1')!;
    const childWidth = MAX_WIDTH - BLOCK_WIDTH_INSET; // 670

    expect(Math.abs(thenHead.position.x - elseHead.position.x)).toBeGreaterThanOrEqual(childWidth);
  });

  it('branch indentation scales with block width (then-branch head moves right at 700 vs 330)', () => {
    // The then-branch head (loop-1) is placed at:
    //   x = NODE_START_X + branchOffset = 250 + round(blockWidth/2 + BAND_PAD + 37)
    // At blockWidth 330: 250 + round(165 + 18 + 37) = 250 + 220 = 470
    // At blockWidth 700: 250 + round(350 + 18 + 37) = 250 + 405 = 655
    // If branchOffset were a fixed 220 both would be 470 — the strict-greater-than catches that.
    const at = (blockWidth: number) => {
      const { nodes, edges } = nestedBranchFixture();
      const placed = computeHierarchicalLayout(nodes, edges, { blockWidth, density: 1, textScale: 1 });
      return placed.find((n) => n.id === 'loop-1')!.position.x;
    };
    const x330 = at(330);
    const x700 = at(700);
    // branchOffset scales with width, so the then-branch head sits further right at wider blockWidth.
    // A fixed offset (e.g. the old 220) would make these equal — this strict-greater guards that.
    expect(x700).toBeGreaterThan(x330);
    // Tighter: delta = branchOffset(700) - branchOffset(330) = 405 - 220 = 185.
    expect(x700 - x330).toBe(185);
  });
});

// ---------------------------------------------------------------------------
// A THEN arm that NESTS a multi-branch container (e.g. an inner if/else) must still
// reserve enough horizontal room that the outer THEN band never overlaps the outer
// ELSE band. The slot reservation in measureSteps used to count only branchOffset +
// maxIndent for a nested multi-branch arm, ignoring the inner arms' own lane padding,
// so the outer THEN band's right pad spilled into the ELSE column.
// ---------------------------------------------------------------------------
function nestedMultiBranchInThen(): { nodes: Node[]; edges: Edge[] } {
  const child = (id: string, parent: string, stepPath: string, label: string): Node => ({
    id, type: 'block', position: { x: 0, y: 0 },
    data: { blockType: 'print', props: { _isChildOf: parent, _stepPath: stepPath, _branchLabel: label, message: id } },
  } as never);
  const nodes: Node[] = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } } as never,
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _stepPath: 'steps/0', condition: 'x' } } } as never,
    child('setA', 'if-1', 'steps/0/then/0', 'then'),
    // nested if (multi-branch) living inside the outer THEN arm
    { id: 'nIf', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', props: { _isChildOf: 'if-1', _stepPath: 'steps/0/then/1', _branchLabel: 'then', condition: 'p' } } } as never,
    child('nThen', 'nIf', 'steps/0/then/1/then/0', 'then'),
    child('nElse', 'nIf', 'steps/0/then/1/else/0', 'else'),
    child('else-1', 'if-1', 'steps/0/else/0', 'else'),
  ];
  const edges: Edge[] = [
    { id: 'e0', source: '__start__', target: 'if-1' },
    { id: 'e1', source: 'if-1', target: 'setA' },
    { id: 'e2', source: 'setA', target: 'nIf' },
    { id: 'e3', source: 'nIf', target: 'nThen' },
    { id: 'e4', source: 'nIf', target: 'nElse', sourceHandle: 'false' },
    { id: 'e5', source: 'if-1', target: 'else-1', sourceHandle: 'false' },
  ] as never;
  return { nodes, edges };
}

describe('nested multi-branch arm does not cause band overlap (max width)', () => {
  it('outer THEN band clears the outer ELSE band when THEN nests an if/else', () => {
    const { nodes, edges } = nestedMultiBranchInThen();
    const placed = computeHierarchicalLayout(nodes, edges, { blockWidth: 700, density: 1, textScale: 1 });
    const bands = computeBranchBands(placed, 700 - BLOCK_WIDTH_INSET);
    const thenB = bands.find((b) => b.parentId === 'if-1' && b.branchKey === 'then')!;
    const elseB = bands.find((b) => b.parentId === 'if-1' && b.branchKey === 'else')!;
    expect(thenB).toBeDefined();
    expect(elseB).toBeDefined();
    // No overlap: the outer ELSE band starts at/after the outer THEN band's right edge.
    expect(elseB.x).toBeGreaterThanOrEqual(thenB.x + thenB.width);
  });
});
