import { expect, test, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  openDisplaySettings,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

async function hasReducedMotionClass(page: Page): Promise<boolean> {
  return page.evaluate(() => document.body.classList.contains('fc-reduced-motion'));
}

test.describe('Flow Canvas Reduced Motion Kill Switch', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('clicking the toggle adds the body class and emits pref-save', async ({ page }) => {
    expect(await hasReducedMotionClass(page)).toBe(false);

    await openDisplaySettings(page);
    await page.getByRole('switch', { name: 'Reduced motion' }).click();

    await expect.poll(() => hasReducedMotionClass(page)).toBe(true);

    const saved = await waitForOutgoingMessage(page, 'pref-save');
    expect(saved.reducedMotion).toBe(true);
  });

  test('inbound pref-restore sets the class with no pref-save echo', async ({ page }) => {
    await clearOutgoingMessages(page);
    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true });

    await expect.poll(() => hasReducedMotionClass(page)).toBe(true);

    const outgoing = await getOutgoingMessages(page);
    expect(outgoing.some((m) => m.type === 'pref-save')).toBe(false);
  });

  test('running node animation collapses to ~0 with reduced motion on', async ({ page }) => {
    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true });
    await expect.poll(() => hasReducedMotionClass(page)).toBe(true);

    await loadGraphFixture(page, createInteractionFixture());
    const node = page.locator('.react-flow__node[data-id="node-1"]');
    await expect(node).toBeVisible();

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });

    const container = node.locator('> div').first();
    await expect.poll(async () =>
      container.evaluate((el) => {
        const raw = getComputedStyle(el as HTMLElement).animationDuration;
        // animationDuration is reported in seconds, e.g. "0.000001s" for the 0.001ms override.
        return Number.parseFloat(raw);
      }),
    ).toBeLessThan(0.01);
  });
});
