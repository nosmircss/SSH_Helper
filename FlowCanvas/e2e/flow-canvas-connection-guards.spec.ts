import { expect, test } from '@playwright/test';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  waitForOutgoingMessage,
} from './support/harness';

interface RuleConnection {
  source: string | null;
  target: string | null;
  sourceHandle?: string | null;
  targetHandle?: string | null;
}

interface RuleNode {
  id: string;
  data?: Record<string, unknown>;
}

interface RuleEdge {
  source: string;
  target: string;
  sourceHandle?: string | null;
  targetHandle?: string | null;
}

interface RuleVerdict {
  ok: boolean;
  reason?: string;
}

interface RuleCase {
  name: string;
  connection: RuleConnection;
  nodes: RuleNode[];
  edges: RuleEdge[];
  expected: boolean;
}

function plainNode(id: string): RuleNode {
  return { id, data: { blockType: 'print' } };
}

function containerNode(id: string): RuleNode {
  return { id, data: { blockType: 'if' } };
}

// A → B → C chain (plain blocks). Used as the existing-graph baseline for several cases.
function chainNodes(): RuleNode[] {
  return [plainNode('a'), plainNode('b'), plainNode('c')];
}

function chainEdges(): RuleEdge[] {
  return [
    { source: 'a', target: 'b' },
    { source: 'b', target: 'c' },
  ];
}

const cases: RuleCase[] = [
  {
    name: 'self-loop is rejected',
    connection: { source: 'a', target: 'a' },
    nodes: [plainNode('a')],
    edges: [],
    expected: false,
  },
  {
    name: 'duplicate edge is rejected',
    connection: { source: 'a', target: 'b' },
    nodes: [plainNode('a'), plainNode('b')],
    edges: [{ source: 'a', target: 'b' }],
    expected: false,
  },
  {
    name: 'second plain successor from a non-container is rejected',
    connection: { source: 'a', target: 'c' },
    nodes: [plainNode('a'), plainNode('b'), plainNode('c')],
    edges: [{ source: 'a', target: 'b' }],
    expected: false,
  },
  {
    name: 'fan-in (second edge into the same target) is rejected',
    connection: { source: 'a', target: 'c' },
    nodes: [plainNode('a'), plainNode('b'), plainNode('c')],
    edges: [{ source: 'b', target: 'c' }],
    expected: false,
  },
  {
    name: 'cycle (target reaches source) is rejected',
    connection: { source: 'c', target: 'a' },
    nodes: chainNodes(),
    edges: chainEdges(),
    expected: false,
  },
  {
    name: 'edge into __start__ is rejected',
    connection: { source: 'a', target: '__start__' },
    nodes: [{ id: '__start__', data: { blockType: '_start' } }, plainNode('a')],
    edges: [],
    expected: false,
  },
  {
    name: 'container if: then + else on distinct handles is allowed',
    connection: { source: 'if-1', target: 'else-target', sourceHandle: 'false' },
    nodes: [containerNode('if-1'), plainNode('then-target'), plainNode('else-target')],
    edges: [{ source: 'if-1', target: 'then-target', sourceHandle: 'true' }],
    expected: true,
  },
  {
    name: 'container continue edge (extra empty-handle branch) is allowed',
    connection: { source: 'if-1', target: 'after' },
    nodes: [containerNode('if-1'), plainNode('child'), plainNode('after')],
    edges: [{ source: 'if-1', target: 'child' }],
    expected: true,
  },
  {
    name: 'first plain successor is allowed',
    connection: { source: 'a', target: 'b' },
    nodes: [plainNode('a'), plainNode('b')],
    edges: [],
    expected: true,
  },
];

test.describe('Flow Canvas connection guards (isConnectionAllowed predicate)', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  for (const c of cases) {
    test(c.name, async ({ page }) => {
      const verdict = await page.evaluate(
        ({ connection, nodes, edges }) => {
          const globalWindow = window as Window & {
            __FLOWCANVAS_TEST_HOOKS__?: {
              isConnectionAllowed?: (conn: unknown, nodes: unknown[], edges: unknown[]) => RuleVerdict;
            };
          };
          const predicate = globalWindow.__FLOWCANVAS_TEST_HOOKS__?.isConnectionAllowed;
          if (typeof predicate !== 'function') {
            throw new Error('Missing test hook isConnectionAllowed.');
          }
          return predicate(connection, nodes, edges);
        },
        { connection: c.connection, nodes: c.nodes, edges: c.edges },
      );

      expect(verdict.ok, `${c.name} → reason: ${verdict.reason ?? '(none)'}`).toBe(c.expected);
    });
  }
});
