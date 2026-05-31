import { expect, test, type Page } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

const SHORT = 'Hi';
// A label long enough to have expanded the block under the old minWidth:180 setting.
const LONG = 'A deliberately long label that pushes this block out to its maximum rendered width';

function block(id: string, x: number, y: number, label: string): GraphFixture['nodes'][number] {
  return { id, type: 'block', position: { x, y }, data: { blockType: 'print', label, props: { message: label } } };
}

async function nodeWidth(page: Page, id: string): Promise<number> {
  const box = await page.locator(`.react-flow__node[data-id="${id}"]`).boundingBox();
  if (!box) throw new Error(`node ${id} has no bounding box`);
  return box.width;
}

test.describe('Flow Canvas Edge Geometry', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('top-level blocks render at a uniform width regardless of label length', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [block('short', 200, 80, SHORT), block('long', 640, 80, LONG)],
      edges: [],
    });
    await expect(page.locator('.react-flow__node[data-id="short"]')).toBeVisible();
    await expect(page.locator('.react-flow__node[data-id="long"]')).toBeVisible();
    const wShort = await nodeWidth(page, 'short');
    const wLong = await nodeWidth(page, 'long');
    // Should be 0 (minWidth===maxWidth===280); allow 1px for Chromium sub-pixel rounding.
    expect(Math.abs(wShort - wLong)).toBeLessThanOrEqual(1);
  });
});
