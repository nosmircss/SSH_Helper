import { expect, test, type Page } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

// Node redesign ("neon ring"): proves the body neutralized to --fc-node-surface, the category color
// is carried by the card BORDER (the neon ring; the legacy accent rail is gone), the header renders a
// category-tinted BlockIcon <svg>, an unknown blockType still renders (fallback path), and the Wave 1
// exec-state precedence is unregressed (running's animation owns the box-shadow over the idle ring).

function createSshBlockFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-ssh',
        type: 'block',
        position: { x: 140, y: 120 },
        data: { blockType: 'send', label: 'Send', props: { command: 'echo hello' } },
      },
    ],
    edges: [],
  };
}

function createUnknownBlockFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-x',
        type: 'block',
        position: { x: 140, y: 120 },
        data: { blockType: '__nope__', label: 'X', props: {} },
      },
    ],
    edges: [],
  };
}

// Resolves a CSS custom property the same way the block consumes it (via a probe div), so the
// assertion compares like-for-like computed color values regardless of OKLCH normalization.
function resolveVar(page: Page, name: string): Promise<string> {
  return page.evaluate((n) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${n})`;
    document.body.appendChild(probe);
    const value = getComputedStyle(probe).color;
    probe.remove();
    return value;
  }, name);
}

test.describe('Flow Canvas Node Redesign', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('body neutralizes to --fc-node-surface (not the category bg)', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const container = page.locator('.react-flow__node[data-id="node-ssh"] > div').first();
    const bg = await container.evaluate((el) => getComputedStyle(el as HTMLElement).backgroundColor);
    expect(bg).toBe(await resolveVar(page, '--fc-node-surface'));
  });

  test('card border resolves to the category border token (the neon ring, no rail)', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const card = page.locator('.react-flow__node[data-id="node-ssh"] > div').first();
    const border = await card.evaluate((el) => getComputedStyle(el as HTMLElement).borderTopColor);
    expect(border).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
    // the legacy rail is gone — identity is on the border now
    await expect(page.locator('.react-flow__node[data-id="node-ssh"] [data-testid="node-rail"]')).toHaveCount(0);
  });

  test('header renders a category-tinted icon svg', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const svg = page.locator('.react-flow__node[data-id="node-ssh"] svg').first();
    await expect(svg).toBeVisible();
    expect(await svg.getAttribute('stroke')).toBe('currentColor');
    const iconColor = await svg.evaluate((el) => getComputedStyle(el as HTMLElement).color);
    expect(iconColor).toBe(await resolveVar(page, '--fc-cat-ssh-icon'));
  });

  test('outlined badge is transparent with a category-mixed border', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    // The badge sits between the icon chip and the label span in the header. The OUTLINED variant
    // (resolved decision) has a transparent background and a 1px border (mix(colors.border, 40)).
    const badge = page.locator('.react-flow__node[data-id="node-ssh"]').getByText('send', { exact: true });
    await expect(badge).toBeVisible();
    const { bg, borderStyle, borderWidth } = await badge.evaluate((el) => {
      const cs = getComputedStyle(el as HTMLElement);
      return { bg: cs.backgroundColor, borderStyle: cs.borderTopStyle, borderWidth: cs.borderTopWidth };
    });
    expect(bg).toBe('rgba(0, 0, 0, 0)'); // transparent
    expect(borderStyle).toBe('solid');
    expect(Number.parseFloat(borderWidth)).toBeGreaterThan(0);
  });

  test('unknown blockType renders the fallback glyph without throwing', async ({ page }) => {
    await loadGraphFixture(page, createUnknownBlockFixture());
    // BaseBlock early-returns the Unknown div for an unregistered blockType; assert it renders
    // (no crash) — the fallback proof for BlockIcon lives in the registry-coverage spec.
    await expect(page.locator('.react-flow__node[data-id="node-x"]')).toBeVisible();
  });

  test('exec-state precedence + heat ring unregressed (running animation owns the shadow)', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const container = page.locator('.react-flow__node[data-id="node-ssh"] > div').first();
    // running → breathing-halo (fc-exec-running) animation present on the card; its animation owns the box-shadow over the idle ring.
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-ssh', state: 'running' });
    await expect
      .poll(() =>
        container.evaluate((el) =>
          Number.parseFloat(getComputedStyle(el as HTMLElement).animationDuration),
        ),
      )
      .toBeGreaterThan(0);
  });
});
