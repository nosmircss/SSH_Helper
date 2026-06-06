import { expect, test, type Page } from '@playwright/test';
import { createBandDragFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, getGraphSnapshot, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

async function positions(page: Page): Promise<Record<string, { x: number; y: number }>> {
  const snap = await getGraphSnapshot(page);
  const out: Record<string, { x: number; y: number }> = {};
  for (const n of snap.nodes as Array<{ id: string; position: { x: number; y: number } }>) {
    out[n.id] = n.position;
  }
  return out;
}

async function dragThenBand(page: Page, dx: number, dy: number): Promise<void> {
  const handle = page.locator('[data-testid="branch-band-handle"][data-branch="then"]');
  const box = await handle.boundingBox();
  if (!box) throw new Error('THEN band handle has no bounding box');
  const cx = box.x + box.width / 2;
  const cy = box.y + box.height / 2;
  await page.mouse.move(cx, cy);
  await page.mouse.down();
  await page.mouse.move(cx + dx, cy + dy, { steps: 8 });
  await page.mouse.up();
}

test.describe('Flow Canvas — drag a band by its label', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createBandDragFixture());
    await expect(page.locator('.react-flow__node[data-id="then-a"]')).toBeVisible();
    await expect(page.locator('[data-testid="branch-band-handle"][data-branch="then"]')).toBeVisible();
  });

  test('dragging the THEN label moves every block in the band by the same delta', async ({ page }) => {
    const before = await positions(page);
    await dragThenBand(page, 120, 80);
    const after = await positions(page);

    const dxA = after['then-a'].x - before['then-a'].x;
    const dyA = after['then-a'].y - before['then-a'].y;
    const dxB = after['then-b'].x - before['then-b'].x;
    const dyB = after['then-b'].y - before['then-b'].y;

    expect(Math.abs(dxA)).toBeGreaterThan(1); // the band actually moved
    expect(Math.abs(dyA)).toBeGreaterThan(1);
    expect(dxB).toBeCloseTo(dxA, 5);          // both members moved as one unit
    expect(dyB).toBeCloseTo(dyA, 5);

    expect(after['if-1']).toEqual(before['if-1']);       // non-members untouched
    expect(after['else-1']).toEqual(before['else-1']);
    expect(after['after-1']).toEqual(before['after-1']);
    expect(after['__start__']).toEqual(before['__start__']);
  });

  test('one undo reverts the whole band move', async ({ page }) => {
    const before = await positions(page);
    await dragThenBand(page, 100, 60);
    expect((await positions(page))['then-a']).not.toEqual(before['then-a']);

    await page.keyboard.press('Control+z');

    await expect.poll(async () => (await positions(page))['then-a']).toEqual(before['then-a']);
    expect((await positions(page))['then-b']).toEqual(before['then-b']);
  });

  test('the band rectangle stays non-interactive; only the handle is grabbable', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    expect(await band.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents)).toBe('none');
    const handle = page.locator('[data-testid="branch-band-handle"][data-branch="then"]');
    expect(await handle.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents)).toBe('auto');
    expect(await handle.evaluate((el) => getComputedStyle(el as HTMLElement).cursor)).toBe('grab');
  });
});
