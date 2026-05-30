import { expect, test, type Locator, type Page } from '@playwright/test';
import {
  createConnectionGuardFixture,
  createFanInLoadFixture,
  type GraphFixture,
} from './fixtures/graphs';
import { evaluateParityCases } from './support/parityCli';
import {
  clearOutgoingMessages,
  connectViaActions,
  getGraphSnapshot,
  installHostMessageCapture,
  loadGraphFixture,
  setGraphViaActions,
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

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

function sourceHandle(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"] .react-flow__handle.source`).first();
}

function targetHandle(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"] .react-flow__handle.target`).first();
}

function edgeCount(graph: GraphFixture): number {
  return graph.edges.length;
}

// Reads the live connectionNotice slice from the store. v12 fires onConnect (which adds the
// edge) BEFORE onConnectEnd, so a regressed onConnectEnd would set this even for a valid drag.
function currentNoticeMessage(page: Page): Promise<string | null> {
  return page.evaluate(() => {
    const globalWindow = window as Window & {
      __FLOWCANVAS_TEST_HOOKS__?: { getConnectionNotice?: () => { message: string } | null };
    };
    const getNotice = globalWindow.__FLOWCANVAS_TEST_HOOKS__?.getConnectionNotice;
    if (typeof getNotice !== 'function') {
      throw new Error('Missing test hook getConnectionNotice.');
    }
    return getNotice()?.message ?? null;
  });
}

function hasEdge(graph: GraphFixture, source: string, target: string): boolean {
  return graph.edges.some((e) => (e as Record<string, unknown>).source === source && (e as Record<string, unknown>).target === target);
}

function toRecordArray(value: unknown): Array<Record<string, unknown>> {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
}

// A container `if` graph with then (empty bottom handle), else (`false`) and continue branches
// pre-wired. Proves valid container shapes survive the guard and round-trip with semantic parity.
function createIfContainerFixture(): GraphFixture {
  return {
    nodes: [
      { id: '__start__', type: 'start', position: { x: 80, y: 20 }, data: { blockType: '_start', label: 'Start', props: {} } },
      { id: 'if-1', type: 'block', position: { x: 80, y: 160 }, data: { blockType: 'if', label: 'If', props: { condition: 'x > 0' } } },
      { id: 'then-1', type: 'block', position: { x: -40, y: 320 }, data: { blockType: 'print', label: 'Then', props: { message: 'then' } } },
      { id: 'else-1', type: 'block', position: { x: 240, y: 320 }, data: { blockType: 'print', label: 'Else', props: { message: 'else' } } },
      { id: 'after-1', type: 'block', position: { x: 80, y: 480 }, data: { blockType: 'print', label: 'After', props: { message: 'after' } } },
    ],
    edges: [
      { id: 'e-start-if', source: '__start__', target: 'if-1' },
      { id: 'e-if-then', source: 'if-1', target: 'then-1', data: { branchPath: 'then' } },
      { id: 'e-if-else', source: 'if-1', sourceHandle: 'false', target: 'else-1', data: { branchPath: 'else' } },
      { id: 'e-if-continue', source: 'if-1', sourceHandle: 'continue', target: 'after-1' },
    ],
  };
}

test.describe('Flow Canvas connection guards (gestures + parity)', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('rejected drags leave the graph unchanged and show a notice', async ({ page }) => {
    await setGraphViaActions(page, createConnectionGuardFixture());
    await expect(nodeById(page, 'g-a')).toBeVisible();
    await expect(nodeById(page, 'g-free')).toBeVisible();

    const baseline = await getGraphSnapshot(page);
    const baselineCount = edgeCount(baseline);

    // self-loop: g-a source → g-a target
    await sourceHandle(page, 'g-a').dragTo(targetHandle(page, 'g-a'));
    await expect(page.getByText('A block cannot connect to itself.')).toBeVisible();
    expect(edgeCount(await getGraphSnapshot(page))).toBe(baselineCount);

    // duplicate: g-a source → g-b (edge already exists)
    await sourceHandle(page, 'g-a').dragTo(targetHandle(page, 'g-b'));
    expect(edgeCount(await getGraphSnapshot(page))).toBe(baselineCount);

    // second plain successor: g-a already → g-b; g-a source → g-free is rejected
    await sourceHandle(page, 'g-a').dragTo(targetHandle(page, 'g-free'));
    expect(edgeCount(await getGraphSnapshot(page))).toBe(baselineCount);

    // fan-in: g-b already has incoming; g-free source → g-b is rejected
    await sourceHandle(page, 'g-free').dragTo(targetHandle(page, 'g-b'));
    expect(edgeCount(await getGraphSnapshot(page))).toBe(baselineCount);

    // cycle: g-a reaches g-c, so g-c source → g-a would close a loop
    await sourceHandle(page, 'g-c').dragTo(targetHandle(page, 'g-a'));
    expect(edgeCount(await getGraphSnapshot(page))).toBe(baselineCount);
  });

  test('a VALID real drag adds the edge and never flashes a notice', async ({ page }) => {
    // g-c (chain tail, no successor) → g-free (no incoming): a clean accepted connection.
    // Regression guard for the onConnectEnd false-positive: v12 fires onConnect (which ADDS
    // the edge) BEFORE onConnectEnd, so a verdict recomputed against post-add edges would
    // wrongly trip the duplicate/fan-in checks and flash 'That connection already exists.'
    await setGraphViaActions(page, createConnectionGuardFixture());
    await expect(nodeById(page, 'g-c')).toBeVisible();
    await expect(nodeById(page, 'g-free')).toBeVisible();

    const baselineCount = edgeCount(await getGraphSnapshot(page));

    await sourceHandle(page, 'g-c').dragTo(targetHandle(page, 'g-free'));

    // The edge was actually added (proves onConnect accepted it)…
    await expect
      .poll(async () => hasEdge(await getGraphSnapshot(page), 'g-c', 'g-free'))
      .toBeTruthy();
    expect(edgeCount(await getGraphSnapshot(page))).toBe(baselineCount + 1);

    // …and NO notice was raised — not even transiently. onConnectEnd must stay silent for
    // a connection onConnect already accepted.
    expect(await currentNoticeMessage(page)).toBeNull();
    await expect(page.getByText('That connection already exists.')).toHaveCount(0);
    await expect(page.getByText('A block can have only one incoming connection.')).toHaveCount(0);
  });

  test('a graph still exports cleanly after a rejected drag', async ({ page }) => {
    await setGraphViaActions(page, createConnectionGuardFixture());
    await expect(nodeById(page, 'g-a')).toBeVisible();

    // Reject a self-loop, then export.
    await sourceHandle(page, 'g-a').dragTo(targetHandle(page, 'g-a'));

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

    const evaluation = evaluateParityCases([{
      name: 'connection-guard-rejected-still-exports',
      sourceYaml: '',
      nodes: toRecordArray(applyMessage.nodes),
      edges: toRecordArray(applyMessage.edges),
    }]).results;

    expect(evaluation).toHaveLength(1);
    const [result] = evaluation;
    expect(result.exportSuccess, result.exportErrors.join(' | ')).toBeTruthy();
    expect(result.exportParseError).toBeNull();
    expect(result.exportValidationErrors).toEqual([]);
  });

  test('valid container then/else/continue branches are NOT blocked and produce identical edges', async ({ page }) => {
    // Start with only the if container wired to start; connect the three branches via onConnect.
    await setGraphViaActions(page, {
      nodes: createIfContainerFixture().nodes,
      edges: [{ id: 'e-start-if', source: '__start__', target: 'if-1' }],
    });
    await expect(nodeById(page, 'if-1')).toBeVisible();

    // then (empty bottom handle), else (`false`), continue — all valid container branches.
    await connectViaActions(page, { source: 'if-1', target: 'then-1' });
    await connectViaActions(page, { source: 'if-1', sourceHandle: 'false', target: 'else-1' });
    await connectViaActions(page, { source: 'if-1', sourceHandle: 'continue', target: 'after-1' });

    const graph = await getGraphSnapshot(page);
    expect(hasEdge(graph, 'if-1', 'then-1'), 'then branch should connect').toBeTruthy();
    expect(hasEdge(graph, 'if-1', 'else-1'), 'else branch should connect').toBeTruthy();
    expect(hasEdge(graph, 'if-1', 'after-1'), 'continue branch should connect').toBeTruthy();
    // start→if plus the three new branch edges.
    expect(edgeCount(graph)).toBe(4);
  });

  test('valid container branch shape round-trips with semantic parity', async ({ page }) => {
    const sourceYaml = `
---
steps:
  - if:
      condition: x > 0
      then:
        - print:
            message: then
      else:
        - print:
            message: else
  - print:
      message: after
`.trim();

    await setGraphViaActions(page, createIfContainerFixture());
    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

    const evaluation = evaluateParityCases([{
      name: 'connection-guard-if-container-parity',
      sourceYaml,
      nodes: toRecordArray(applyMessage.nodes),
      edges: toRecordArray(applyMessage.edges),
    }]).results;

    expect(evaluation).toHaveLength(1);
    const [result] = evaluation;
    expect(result.exportSuccess, result.exportErrors.join(' | ')).toBeTruthy();
    expect(result.exportParseError).toBeNull();
    expect(result.exportValidationErrors).toEqual([]);
    expect(result.semanticEquivalent, result.semanticDiff ?? undefined).toBeTruthy();
  });

  test('the guard does NOT gate the load path — pre-existing fan-in loads intact', async ({ page }) => {
    await loadGraphFixture(page, createFanInLoadFixture());
    await expect(nodeById(page, 'g-sink')).toBeVisible();

    const graph = await getGraphSnapshot(page);
    expect(hasEdge(graph, 'g-src1', 'g-sink'), 'first fan-in edge should load').toBeTruthy();
    expect(hasEdge(graph, 'g-src2', 'g-sink'), 'second fan-in edge should load').toBeTruthy();
  });
});
