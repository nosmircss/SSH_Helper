import { expect, test, type Page } from '@playwright/test';
import { createImportedChildEditingFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

async function resolveVar(page: Page, name: string): Promise<string> {
  return page.evaluate((n) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${n})`;
    document.body.appendChild(probe);
    const v = getComputedStyle(probe).color;
    probe.remove();
    return v;
  }, name);
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
    expect(borderColor).toBe(await resolveVar(page, '--fc-branch-then'));
  });

  test('band is pointer-events:none (does not capture node drag)', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    const pe = await band.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents);
    expect(pe).toBe('none');
  });

  test('toggling branchBandsEnabled hides the layer', async ({ page }) => {
    await expect(page.locator('[data-testid="branch-band"]')).toHaveCount(1);
    await page.getByRole('button', { name: '▭ Bands' }).click();
    await expect(page.locator('[data-testid="branch-band"]')).toHaveCount(0);
  });
});
