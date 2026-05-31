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

  test('an aligned, downward continuation edge renders as a straight line', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [block('top', 200, 80, SHORT), block('bottom', 200, 360, LONG)],
      edges: [{ id: 'e1', source: 'top', target: 'bottom' }],
    });
    // A purely-vertical SVG path has zero bounding-box width so Playwright's visibility check
    // (getBoundingClientRect) returns hidden even when the path is rendered. Check DOM presence
    // instead, which is all we need before asserting the shape.
    await expect(page.locator('path#e1')).toHaveCount(1);
    // getStraightPath emits exactly one line segment: "M x,yL x,y" — no extra L/Q/C commands.
    await expect(page.locator('path#e1')).toHaveAttribute('d', /^M[\s\d.,-]+L[\s\d.,-]+$/);
  });

  test('an X-offset edge keeps its orthogonal (smoothstep) routing', async ({ page }) => {
    await loadGraphFixture(page, {
      nodes: [block('a', 200, 80, SHORT), block('b', 600, 360, SHORT)],
      edges: [{ id: 'e2', source: 'a', target: 'b' }],
    });
    await expect(page.locator('path#e2')).toBeVisible();
    // smoothstep with borderRadius:8 emits a quadratic-curved corner (Q) on any real bend.
    await expect(page.locator('path#e2')).toHaveAttribute('d', /Q/);
  });
});
