import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const nodeById = (page: Page, id: string): Locator => page.locator(`.react-flow__node[data-id="${id}"]`);
const cardOf = (page: Page, id: string): Locator => nodeById(page, id).locator('> div').first();

// fc-exec-error sets two comma-separated tracks (shake 0.5s, ripple 0.6s), so animationDuration is
// e.g. "0.5s, 0.6s"; Number.parseFloat intentionally reads only the first track — enough to prove
// live (> 0.01s) vs reduced-motion-collapsed (~0.000001s). fc-exec-running is a single 2.4s track.
async function animationDurationSec(card: Locator): Promise<number> {
  return card.evaluate((el) => Number.parseFloat(getComputedStyle(el as HTMLElement).animationDuration));
}
async function hasReducedMotion(page: Page): Promise<boolean> {
  return page.evaluate(() => document.body.classList.contains('fc-reduced-motion'));
}

test.describe('Flow Canvas Execution Cinematics', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
  });

  test('running: breathing card class + comet halo child, both live', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });

    const card = cardOf(page, 'node-1');
    await expect.poll(() => card.evaluate((el) => el.classList.contains('fc-exec-running'))).toBe(true);
    await expect(nodeById(page, 'node-1').locator('.fc-run-halo')).toHaveCount(1);
    expect(await animationDurationSec(card)).toBeGreaterThan(0.01); // breathing not collapsed
  });

  test('running under reduced motion: no comet child, breathing collapses', async ({ page }) => {
    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true });
    await expect.poll(() => hasReducedMotion(page)).toBe(true);

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    const card = cardOf(page, 'node-1');
    await expect.poll(() => card.evaluate((el) => el.classList.contains('fc-exec-running'))).toBe(true);
    await expect(nodeById(page, 'node-1').locator('.fc-run-halo')).toHaveCount(0); // comet not rendered
    await expect.poll(() => animationDurationSec(card)).toBeLessThan(0.01); // blanket collapses it
  });

  test('success: the exec indicator draws an SVG checkmark and the card settles to a green glow', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 500 });

    const check = nodeById(page, 'node-1').locator('svg.fc-check');
    await expect(check).toHaveCount(1);
    const animName = await check.locator('path').evaluate((el) => getComputedStyle(el as Element).animationName);
    expect(animName).toContain('fc-check-draw');
    // success card settles (0.2s box-shadow transition) to the soft INLINE glow
    // `0 0 10px var(--fc-glow-success)` — poll until stable, then lock the 10px settle radius
    // (running uses 8/20px, error 8px) so the heat-stack / inline-path split can't silently regress.
    await expect
      .poll(() => cardOf(page, 'node-1').evaluate((el) => getComputedStyle(el as HTMLElement).boxShadow))
      .toContain('10px');
  });

  test('error: shake + ripple class is live and collapses under reduced motion', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'error' });
    const card = cardOf(page, 'node-1');
    await expect.poll(() => card.evaluate((el) => el.classList.contains('fc-exec-error'))).toBe(true);
    expect(await animationDurationSec(card)).toBeGreaterThan(0.01);

    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true });
    await expect.poll(() => hasReducedMotion(page)).toBe(true);
    await expect.poll(() => animationDurationSec(card)).toBeLessThan(0.01);
  });

  test('count-up: the badge ticks up live while running, then locks to the final duration', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    const badge = nodeById(page, 'node-1').getByTestId('exec-duration-badge');
    await expect(badge).toBeVisible();

    const readMs = async (): Promise<number> => {
      const t = (await badge.textContent()) ?? '';
      const m = t.match(/([\d.]+)\s*(ms|s)/);
      if (!m) return 0;
      return m[2] === 's' ? Number.parseFloat(m[1]) * 1000 : Number.parseFloat(m[1]);
    };
    // `start` (set in the running handler) and the ticker both use the page's Date.now() clock, so
    // elapsed is monotonic. Settle briefly so `first` is a real mid-run value (not the 0ms mount
    // frame), then prove a strict increase over a 220ms gap (sub-second format is exact ms).
    await page.waitForTimeout(80);
    const first = await readMs();
    await page.waitForTimeout(220);
    const second = await readMs();
    expect(first).toBeGreaterThan(0);
    expect(second).toBeGreaterThan(first);

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 1500 });
    await expect(nodeById(page, 'node-1').getByText('1.5s', { exact: true })).toBeVisible();
  });

  test('count-up under reduced motion: no live value while running', async ({ page }) => {
    await postHostMessage(page, { type: 'pref-restore', reducedMotion: true });
    await expect.poll(() => hasReducedMotion(page)).toBe(true);

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'running' });
    await page.waitForTimeout(150);
    await expect(nodeById(page, 'node-1').getByTestId('exec-duration-badge')).toHaveCount(0);
  });
});
