import { expect, test, type Page } from '@playwright/test';
import { clearOutgoingMessages, installHostMessageCapture, waitForOutgoingMessage } from './support/harness';

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

  test('block width is a 300-2000 slider that persists once on commit', async ({ page }) => {
    await openPopover(page);
    const slider = page.locator('[data-testid="setting-block-width"] input[type="range"]');
    await expect(slider).toBeVisible();
    await expect(slider).toHaveAttribute('min', '300');
    await expect(slider).toHaveAttribute('max', '2000');

    await clearOutgoingMessages(page);
    await slider.focus();
    await page.keyboard.press('End'); // keyboard preview to the max…
    await slider.blur();              // …commit persists via layout-save
    const saved = await waitForOutgoingMessage(page, 'layout-save');
    expect(saved.blockWidth).toBe(2000);
  });

  test('text size is a 0.9-2.5 slider that persists once on commit', async ({ page }) => {
    await openPopover(page);
    const slider = page.locator('[data-testid="setting-text-size"] input[type="range"]');
    await expect(slider).toBeVisible();
    await expect(slider).toHaveAttribute('min', '0.9');
    await expect(slider).toHaveAttribute('max', '2.5');

    await clearOutgoingMessages(page);
    await slider.focus();
    await page.keyboard.press('End');
    await slider.blur();
    const saved = await waitForOutgoingMessage(page, 'layout-save');
    expect(saved.textScale).toBe(2.5);
  });

  test('canvas density presets fit inside the popover (Roomy not squished off-edge)', async ({ page }) => {
    const dialog = await openPopover(page);
    const dialogBox = await dialog.boundingBox();
    expect(dialogBox).not.toBeNull();

    const labels = await assertChipsFitWithin(page, dialogBox!, 'setting-canvas-density');
    expect(labels).toEqual(['Tight', 'Normal', 'Roomy']);
  });
});
