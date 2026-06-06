import { expect, test, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const spineGraph = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'send-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', label: 'Send', props: { command: 'show ver', capture: 'out' } } },
    { id: 'next-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', label: 'Next', props: { message: 'x' } } },
  ],
  edges: [
    { id: 'e0', source: '__start__', target: 'send-1' },
    { id: 'e1', source: 'send-1', target: 'next-1' },
  ],
};

const laneGraph = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'if-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'if', label: 'If', props: { condition: '${x}', _stepPath: 'steps/0' } } },
    { id: 'then-1', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', label: 'Then Send', props: { command: 'a', capture: 'b', _isChildOf: 'if-1', _stepPath: 'steps/0/then/0', _branchLabel: 'then' } } },
  ],
  edges: [
    { id: 'e0', source: '__start__', target: 'if-1' },
    { id: 'e1', source: 'if-1', target: 'then-1' },
  ],
};

async function nodeY(page: Page, id: string): Promise<number> {
  const snap = await getGraphSnapshot(page);
  const n = (snap.nodes as Array<{ id: string; position: { x: number; y: number } }>).find((m) => m.id === id);
  if (!n) throw new Error(`node ${id} not found in snapshot`);
  return n.position.y;
}

test.describe('Flow Canvas in-place expansion', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('expanding a spine block shows its summary and pushes the next block down', async ({ page }) => {
    await postHostMessage(page, { type: 'load-graph', ...spineGraph });
    await expect(page.locator('.react-flow__node[data-id="send-1"]')).toBeVisible();
    const beforeY = await nodeY(page, 'next-1');

    await page.locator('.react-flow__node[data-id="send-1"] [data-testid="expand-toggle"]').click();

    await expect(page.locator('.react-flow__node[data-id="send-1"] [data-testid="block-summary"]')).toBeVisible();
    await expect.poll(async () => await nodeY(page, 'next-1')).toBeGreaterThan(beforeY);
  });

  test('expanding a child block grows its branch lane', async ({ page }) => {
    await postHostMessage(page, { type: 'load-graph', ...laneGraph });
    await expect(page.locator('.react-flow__node[data-id="then-1"]')).toBeVisible();
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    await expect(band).toHaveCount(1);
    const beforeH = await band.evaluate((el) => (el as HTMLElement).offsetHeight);

    await page.locator('.react-flow__node[data-id="then-1"] [data-testid="expand-toggle"]').click();

    await expect(page.locator('.react-flow__node[data-id="then-1"] [data-testid="block-summary"]')).toBeVisible();
    await expect.poll(async () => await band.evaluate((el) => (el as HTMLElement).offsetHeight)).toBeGreaterThan(beforeH);
  });
});
