import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Interaction Correctness', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
    await expect(nodeById(page, 'node-2')).toBeVisible();
    await expect(nodeById(page, 'node-3')).toBeVisible();
  });

  test('node drag undo restores original position', async ({ page }) => {
    const node = nodeById(page, 'node-1');
    const before = await getNodeTranslate(page, 'node-1');

    await dragNodeBy(node, page, 140, 90);
    const afterDrag = await getNodeTranslate(page, 'node-1');
    expect(afterDrag).not.toEqual(before);

    await page.keyboard.press('Control+Z');
    await expect.poll(async () => getNodeTranslate(page, 'node-1')).toEqual(before);
  });

  test('right-click opens context menu without toggling breakpoint', async ({ page }) => {
    const node = nodeById(page, 'node-1');
    await node.click({ button: 'right' });

    await expect(page.getByText('Toggle Breakpoint', { exact: true })).toBeVisible();

    const outgoing = await getOutgoingMessages(page);
    expect(outgoing.some((m) => m.type === 'breakpoint-toggle')).toBeFalsy();

    await page.getByText('Toggle Breakpoint', { exact: true }).click();
    await waitForOutgoingMessage(page, 'breakpoint-toggle');
  });

  test('add comment is persistent through undo and redo', async ({ page }) => {
    await nodeById(page, 'node-2').click({ button: 'right' });
    await page.getByText('Add Comment', { exact: true }).click();

    await expect(commentNodes(page)).toHaveCount(1);

    await page.keyboard.press('Control+Z');
    await expect(commentNodes(page)).toHaveCount(0);

    await page.keyboard.press('Control+Y');
    await expect(commentNodes(page)).toHaveCount(1);
  });

  test('box select sync removes selected nodes via Delete', async ({ page }) => {
    const one = await nodeById(page, 'node-1').boundingBox();
    const two = await nodeById(page, 'node-2').boundingBox();

    if (!one || !two) throw new Error('Expected node bounding boxes for selection test.');

    const startX = Math.min(one.x, two.x) - 30;
    const startY = Math.min(one.y, two.y) - 30;
    const endX = Math.max(one.x + one.width, two.x + two.width) + 30;
    const endY = Math.max(one.y + one.height, two.y + two.height) + 30;

    await page.mouse.move(startX, startY);
    await page.mouse.down();
    await page.mouse.move(endX, endY, { steps: 12 });
    await page.mouse.up();

    await expect(nodeById(page, 'node-1')).toHaveClass(/selected/);
    await expect(nodeById(page, 'node-2')).toHaveClass(/selected/);

    await page.keyboard.press('Delete');

    await expect(nodeById(page, 'node-1')).toHaveCount(0);
    await expect(nodeById(page, 'node-2')).toHaveCount(0);
    await expect(nodeById(page, 'node-3')).toHaveCount(1);
  });
});

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

function commentNodes(page: Page): Locator {
  return page.locator('.react-flow__node-comment');
}

async function dragNodeBy(node: Locator, page: Page, deltaX: number, deltaY: number): Promise<void> {
  const box = await node.boundingBox();
  if (!box) throw new Error('Expected node bounding box for drag operation.');

  const fromX = box.x + box.width / 2;
  const fromY = box.y + box.height / 2;

  await page.mouse.move(fromX, fromY);
  await page.mouse.down();
  await page.mouse.move(fromX + deltaX, fromY + deltaY, { steps: 15 });
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
