import { expect, test, type Page } from '@playwright/test';
import { createImportedChildEditingFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, openDisplaySettings, waitForOutgoingMessage,
} from './support/harness';

// Resolve an arbitrary color expression (e.g. a var(--fc-*) or color-mix(...)) to its
// computed value via a throwaway probe element.
async function resolveColor(page: Page, expr: string): Promise<string> {
  return page.evaluate((e) => {
    const probe = document.createElement('div');
    probe.style.color = e;
    document.body.appendChild(probe);
    const v = getComputedStyle(probe).color;
    probe.remove();
    return v;
  }, expr);
}

test.describe('Flow Canvas Branch Bands', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createImportedChildEditingFixture());
    await expect(page.locator('.react-flow__node[data-id="then-1"]')).toBeVisible();
  });

  test('renders a then-branch band behind the child', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    await expect(band).toBeVisible();
    const borderColor = await band.evaluate((el) => getComputedStyle(el as HTMLElement).borderLeftColor);
    // The left accent is now mix(branch, 70%) (labeled-lane redesign), not the pure branch color.
    expect(borderColor).toBe(await resolveColor(page, 'color-mix(in oklch, var(--fc-branch-then) 70%, transparent)'));
  });

  test('band is pointer-events:none (does not capture node drag)', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    const pe = await band.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents);
    expect(pe).toBe('none');
  });

  test('toggling branchBandsEnabled hides the layer', async ({ page }) => {
    await expect(page.locator('[data-testid="branch-band"]')).toHaveCount(1);
    await openDisplaySettings(page);
    await page.getByRole('switch', { name: 'Branch bands' }).click();
    await expect(page.locator('[data-testid="branch-band"]')).toHaveCount(0);
  });
});
