import { expect, test, type Page } from '@playwright/test';
import { installHostMessageCapture, waitForOutgoingMessage } from './support/harness';

// Guards the settings popover against the chip-clipping regression: the segmented
// controls must lay their option chips out so EVERY chip sits inside the popover and
// none has ellipsis-truncated text. A unit test (jsdom) can't see layout/overflow, so
// this lives in e2e where real pixel boxes exist. The popover is a plain DOM overlay
// (not inside React Flow's zoom transform), so getBoundingClientRect is in CSS pixels.

async function openPopover(page: Page) {
  await page.locator('button[title="Display settings"]').click();
  const dialog = page.locator('div[role="dialog"][aria-label="Display settings"]');
  await expect(dialog).toBeVisible();
  return dialog;
}

/** For a segmented control, every chip button must sit within the popover horizontally
 *  and not be text-truncated (scrollWidth > clientWidth means an ellipsis was applied). */
async function assertChipsFitWithin(page: Page, dialogBox: { x: number; width: number }, testId: string) {
  const chips = page.locator(`[data-testid="${testId}"] button`);
  const boxes = await chips.evaluateAll((els) =>
    els.map((e) => {
      const r = e.getBoundingClientRect();
      return { text: e.textContent ?? '', left: r.left, right: r.right, scrollW: e.scrollWidth, clientW: e.clientWidth };
    }),
  );
  expect(boxes.length).toBeGreaterThan(0);
  const right = dialogBox.x + dialogBox.width;
  for (const b of boxes) {
    expect(b.right, `"${b.text}" overflows the popover right edge`).toBeLessThanOrEqual(right + 1);
    expect(b.left, `"${b.text}" overflows the popover left edge`).toBeGreaterThanOrEqual(dialogBox.x - 1);
    expect(b.scrollW, `"${b.text}" chip text is truncated`).toBeLessThanOrEqual(b.clientW + 1);
  }
  return boxes.map((b) => b.text);
}

test.describe('Flow Canvas settings popover', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
  });

  test('all five block-width presets fit inside the popover (no clipping)', async ({ page }) => {
    const dialog = await openPopover(page);
    const dialogBox = await dialog.boundingBox();
    expect(dialogBox).not.toBeNull();

    const labels = await assertChipsFitWithin(page, dialogBox!, 'setting-block-width');
    expect(labels).toEqual(['Compact', 'Normal', 'Wide', 'Extra', 'Max']);
  });

  test('canvas density presets fit inside the popover (Roomy not squished off-edge)', async ({ page }) => {
    const dialog = await openPopover(page);
    const dialogBox = await dialog.boundingBox();
    expect(dialogBox).not.toBeNull();

    const labels = await assertChipsFitWithin(page, dialogBox!, 'setting-canvas-density');
    expect(labels).toEqual(['Tight', 'Normal', 'Roomy']);
  });
});
