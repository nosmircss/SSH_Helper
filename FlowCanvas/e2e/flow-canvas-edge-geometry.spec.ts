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
  const el = page.locator(`.react-flow__node[data-id="${id}"]`);
  await el.waitFor({ state: 'visible' });
  // offsetWidth gives the CSS layout width before any canvas-level zoom transform,
  // which is what we care about (the node's own size, not its zoomed screen size).
  return el.evaluate((node) => (node as HTMLElement).offsetWidth);
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

  test('the Start node shares the uniform top-level width (~280px)', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [
        { id: 'start', type: 'start', position: { x: 200, y: 40 }, data: { blockType: '_start', label: 'S', props: { name: 'S' } } },
        block('first', 200, 340, 'First'),
      ],
      edges: [{ id: 'e-start', source: 'start', target: 'first' }],
    });
    await expect(page.locator('.react-flow__node[data-id="start"]')).toBeVisible();
    const wStart = await nodeWidth(page, 'start');
    expect(wStart).toBeGreaterThan(276);
    expect(wStart).toBeLessThan(290);
  });
});
