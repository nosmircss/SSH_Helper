import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture, type GraphFixture } from './fixtures/graphs';
import { evaluateParityCases } from './support/parityCli';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  setGraphViaActions,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Gesture Smoke', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await setGraphViaActions(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
    await expect(nodeById(page, 'node-2')).toBeVisible();
    await expect(nodeById(page, 'node-3')).toBeVisible();
  });

  test('drag, connect, and property edit path keeps exported graph valid', async ({ page }) => {
    const before = await getNodeTranslate(page, 'node-1');
    await dragNodeBy(nodeById(page, 'node-1'), page, 120, 70);
    await expect.poll(async () => getNodeTranslate(page, 'node-1')).not.toEqual(before);

    const sourceHandle = page.locator('.react-flow__node[data-id="node-1"] .react-flow__handle.source').first();
    const targetHandle = page.locator('.react-flow__node[data-id="node-3"] .react-flow__handle.target').first();
    await sourceHandle.dragTo(targetHandle);

    const graphAfterConnect = await getGraphSnapshot(page);
    expect(graphAfterConnect.edges.length).toBeGreaterThanOrEqual(3);

    await nodeById(page, 'node-1').click();
    const messageInput = page.getByTestId('properties-field-message-code-input');
    await expect(messageInput).toBeVisible();
    await messageInput.fill('gesture-smoke-updated');

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

    const evaluation = evaluateParityCases([{
      name: 'gesture-smoke-export',
      sourceYaml: '',
      nodes: toRecordArray(applyMessage.nodes),
      edges: toRecordArray(applyMessage.edges),
    }]).results;

    expect(evaluation).toHaveLength(1);
    const [result] = evaluation;
    expect(result.exportSuccess, `${result.exportErrors.join(' | ')}`).toBeTruthy();
    expect(result.exportParseError).toBeNull();
    expect(result.exportValidationErrors).toEqual([]);
  });

  test('edge context menu edits branch metadata for container edges', async ({ page }) => {
    await setGraphViaActions(page, createIfBranchFixture());
    await expect(nodeById(page, 'if-node')).toBeVisible();
    await expect(nodeById(page, 'then-node')).toBeVisible();

    const edgePath = page
      .locator('.react-flow__edge[data-id="edge-if-then"] path.react-flow__edge-path')
      .first();
    await edgePath.click({ button: 'right', force: true });

    const modeInput = page.getByTestId('edge-branch-mode-input');
    await expect(modeInput).toBeVisible();
    await modeInput.selectOption('elif');
    await page.getByTestId('edge-branch-index-input').fill('0');
    await page.getByTestId('edge-branch-condition-input').fill('value > 10');
    await page.getByTestId('edge-branch-save-btn').click();

    const graph = await getGraphSnapshot(page);
    const editedEdge = graph.edges.find((candidate) => candidate.id === 'edge-if-then');
    expect(editedEdge).toBeTruthy();
    expect((editedEdge?.data as Record<string, unknown> | undefined)?.branchPath).toBe('elif/0/then');
    expect((editedEdge?.data as Record<string, unknown> | undefined)?.condition).toBe('value > 10');
    expect(editedEdge?.label).toBe('elif: value > 10');
  });

  test('start advanced yaml editors persist values through property edits', async ({ page }) => {
    await setGraphViaActions(page, createStartOnlyFixture());
    await nodeById(page, '__start__').click();

    await page.getByTestId('start-vars-yaml-input').fill('alpha: 1');
    await page.getByTestId('start-imports-yaml-input').fill('- path: C:\\\\tmp\\\\lib.yaml');
    await page.getByTestId('start-subroutines-yaml-input').fill('demo:\n  steps:\n    - print: "ok"');

    const snapshot = await getGraphSnapshot(page);
    const startNode = snapshot.nodes.find((node) => node.id === '__start__');
    const props = (startNode?.data as Record<string, unknown> | undefined)?.props as Record<string, unknown> | undefined;

    expect(props?.vars_yaml).toBe('alpha: 1');
    expect(props?.imports_yaml).toBe('- path: C:\\\\tmp\\\\lib.yaml');
    expect(props?.subroutines_yaml).toBe('demo:\n  steps:\n    - print: "ok"');
  });
});

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

async function dragNodeBy(node: Locator, page: Page, deltaX: number, deltaY: number): Promise<void> {
  const box = await node.boundingBox();
  if (!box) throw new Error('Expected node bounding box for drag operation.');

  const fromX = box.x + box.width / 2;
  const fromY = box.y + box.height / 2;

  await page.mouse.move(fromX, fromY);
  await page.mouse.down();
  await page.mouse.move(fromX + deltaX, fromY + deltaY, { steps: 14 });
  await page.mouse.up();
}

async function getNodeTranslate(page: Page, nodeId: string): Promise<{ x: number; y: number }> {
  return page.evaluate((id) => {
    const el = document.querySelector(`.react-flow__node[data-id="${id}"]`) as HTMLElement | null;
    if (!el) throw new Error(`Node '${id}' not found.`);

    const transform = el.style.transform || '';
    const match = transform.match(/translate\(([-\d.]+)px,\s*([-\d.]+)px\)/);
    if (!match) throw new Error(`Unable to parse transform '${transform}' for node '${id}'.`);

    return {
      x: Number(match[1]),
      y: Number(match[2]),
    };
  }, nodeId);
}

function toRecordArray(value: unknown): Array<Record<string, unknown>> {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
}

function createIfBranchFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 40 },
        data: {
          blockType: '_start',
          label: 'Start',
          props: {},
        },
      },
      {
        id: 'if-node',
        type: 'block',
        position: { x: 160, y: 160 },
        data: {
          blockType: 'if',
          label: 'If',
          props: { condition: 'value > 0' },
        },
      },
      {
        id: 'then-node',
        type: 'block',
        position: { x: 160, y: 320 },
        data: {
          blockType: 'print',
          label: 'Then',
          props: { message: 'then' },
        },
      },
    ],
    edges: [
      {
        id: 'edge-start-if',
        source: '__start__',
        target: 'if-node',
      },
      {
        id: 'edge-if-then',
        source: 'if-node',
        target: 'then-node',
        data: {
          branchPath: 'then',
        },
      },
    ],
  };
}

function createStartOnlyFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 40 },
        data: {
          blockType: '_start',
          label: 'Start',
          props: {},
        },
      },
    ],
    edges: [],
  };
}
