import { expect, test, type Page } from '@playwright/test';
import { createImportedChildEditingFixture, type GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

// Authored default for a new comment. Mirrors DEFAULT_COMMENT_COLOR in src/utils/tokens.ts; the one
// raw hex permitted to appear in resolved styles (it is user data, not a token-layer color).
const DEFAULT_COMMENT_COLOR = '#e0c040';

function createSshBlockFixture(): GraphFixture {
  return {
    nodes: [
      {
        id: 'node-ssh',
        type: 'block',
        position: { x: 140, y: 120 },
        data: {
          blockType: 'send',
          label: 'Send',
          props: { command: 'echo hello' },
        },
      },
      {
        id: 'node-ssh-2',
        type: 'block',
        position: { x: 460, y: 120 },
        data: {
          blockType: 'print',
          label: 'Print',
          props: {},
        },
      },
    ],
    edges: [
      {
        id: 'sweep-edge',
        source: 'node-ssh',
        target: 'node-ssh-2',
        style: { stroke: 'var(--fc-branch-then)' },
      },
    ],
  };
}

// Three nodes so running / success / error can be rendered at once (Wave 2b execution cinematics),
// then re-scanned for raw hex — the comet halo + checkmark mount on these state nodes.
function createExecStateFixture(): GraphFixture {
  return {
    nodes: [
      { id: 'exec-run', type: 'block', position: { x: 80, y: 80 }, data: { blockType: 'send', label: 'Run', props: {} } },
      { id: 'exec-ok', type: 'block', position: { x: 80, y: 220 }, data: { blockType: 'print', label: 'Ok', props: {} } },
      { id: 'exec-err', type: 'block', position: { x: 80, y: 360 }, data: { blockType: 'print', label: 'Err', props: {} } },
    ],
    edges: [],
  };
}

// Shared hex/malformed-var scan (Decision #4). Returns the two offender lists so each consumer can
// assert them empty. Identical logic to the original inline scan, hoisted so the branch-chip test
// can reuse it without duplicating the regex pair.
async function scanForRawColors(page: Page, commentColor: string) {
  return page.evaluate((allowed) => {
    const hexRe = /#[0-9a-fA-F]{3,8}\b/g;
    const malformedRe = /var\([^)]*\)[0-9a-fA-F]{2,8}\b/g;
    const normalize = (s: string) => s.toLowerCase();
    const allow = normalize(allowed);
    const hexHits: string[] = [];
    const malformedHits: string[] = [];
    for (const el of Array.from(document.querySelectorAll<HTMLElement>('[style]'))) {
      const style = el.getAttribute('style') ?? '';
      const tag = el.tagName.toLowerCase();
      for (const m of style.match(hexRe) ?? []) {
        if (normalize(m) === allow) continue;
        hexHits.push(`${tag}: ${m}`);
      }
      for (const m of style.match(malformedRe) ?? []) {
        malformedHits.push(`${tag}: ${m}`);
      }
    }
    return { hexOffenders: hexHits, malformedOffenders: malformedHits };
  }, commentColor);
}

test.describe('Flow Canvas Token Sweep', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('root exposes OKLCH accent token', async ({ page }) => {
    const accent = await page.evaluate(() =>
      getComputedStyle(document.documentElement).getPropertyValue('--fc-accent').trim(),
    );
    expect(accent).not.toBe('');
    expect(accent).toContain('oklch');
  });

  // Resolves a CSS custom property the same way the block consumes it (via a probe div), so the
  // assertion compares like-for-like computed color values regardless of OKLCH normalization.
  const resolveVar = (page: import('@playwright/test').Page, name: string) =>
    page.evaluate((n) => {
      const probe = document.createElement('div');
      probe.style.color = `var(${n})`;
      document.body.appendChild(probe);
      const value = getComputedStyle(probe).color;
      probe.remove();
      return value;
    }, name);

  // Wave 2a redesign: the category color moved OFF the body border (now neutral --fc-node-border)
  // and ONTO the absolutely-positioned accent rail (--fc-cat-ssh-border). This gate still proves
  // the category token is correctly wired and applied — just on the rail, where the design now
  // carries category identity — while the body border reads the neutral surface-border token.
  test('ssh block body border is neutral and the accent rail carries the category token', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());

    const block = page.locator('.react-flow__node[data-id="node-ssh"]');
    await expect(block).toBeVisible();

    // The BaseBlock container (the bordered element) is the node's first child div.
    const container = block.locator('> div').first();

    const renderedBorder = await container.evaluate(
      (el) => getComputedStyle(el as HTMLElement).borderTopColor,
    );
    expect(renderedBorder).not.toBe('');
    expect(renderedBorder).toBe(await resolveVar(page, '--fc-node-border'));

    // The accent rail child span now carries the category color.
    const railBg = await block
      .locator('[data-testid="node-rail"]')
      .evaluate((el) => getComputedStyle(el as HTMLElement).backgroundColor);
    expect(railBg).not.toBe('');
    expect(railBg).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
  });

  // The panel sweep is complete (Task 5); this CI gate enforces Decision #4 — no raw hex in any
  // resolved inline style except the one user-data comment color (DEFAULT_COMMENT_COLOR).
  //
  // Two complementary scans run together because each catches a different failure mode:
  //   (1) a raw '#'-hex literal (a token that never made it into the token layer), and
  //   (2) a malformed `var(--x)<hex-alpha>` concatenation — the regression class from the
  //       hex→var() sweep, where `color + '55'` silently became invalid CSS like
  //       `var(--fc-accent)55`. The browser DROPS such a declaration, so scan (1) alone is
  //       blind to it (no '#'). The Toolbar and Palette render by default in App, so loading
  //       any fixture mounts the exact controls (btnStyle / PaletteItem borders) where the bug
  //       lived — reverting fix #1 makes scan (2) fail here.
  test('no raw hex outside the token layer', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    await expect(page.locator('.react-flow__node[data-id="node-ssh"]')).toBeVisible();
    // Confirm the toolbar + palette shell is present, so their inline styles are in the scan
    // (these are where the var()+hex-alpha regression lived).
    await expect(page.getByText('Flow Canvas v2')).toBeVisible(); // toolbar
    await expect(page.getByText('Blocks', { exact: true })).toBeVisible(); // palette header

    const { hexOffenders, malformedOffenders } = await scanForRawColors(page, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });

  // Wave 2a (Task 7): the Properties branch chip used to consume the raw importer `_branchColor`
  // hex (e.g. '#2ecc71'). Selecting an imported child renders that chip; this scan would FAIL
  // against the old raw hex, locking in the hex→`var(--fc-branch-*)` harmonization.
  test('no raw hex in the Properties branch chip for an imported child', async ({ page }) => {
    await loadGraphFixture(page, createImportedChildEditingFixture());
    const child = page.locator('.react-flow__node[data-id="then-1"]');
    await expect(child).toBeVisible();

    // Select the imported child so Properties renders its branch chip.
    await child.click();
    await expect(page.getByTestId('properties-panel')).toBeVisible();
    await expect(page.getByText('branch', { exact: true })).toBeVisible();

    const { hexOffenders, malformedOffenders } = await scanForRawColors(page, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });

  // Wave 2b: re-run the no-hex scan with nodes in running / success / error so any INLINE [style]
  // on an exec-state node (e.g. the success card's `0 0 10px var(--fc-glow-success)` settle glow)
  // is covered. The comet halo + checkmark colors live in execution-cinematics.css (stylesheet, not
  // inline) — guarded by review + the dist gate, since this DOM [style] scan can't see stylesheet rules.
  test('no raw hex while nodes are running / success / error', async ({ page }) => {
    await loadGraphFixture(page, createExecStateFixture());
    await expect(page.locator('.react-flow__node[data-id="exec-run"]')).toBeVisible();

    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-run', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'success', duration: 300 });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-err', state: 'error' });

    // The comet halo + checkmark are mounted before scanning their styles.
    await expect(page.locator('.react-flow__node[data-id="exec-run"] .fc-run-halo')).toHaveCount(1);
    await expect(page.locator('.react-flow__node[data-id="exec-ok"] svg.fc-check')).toHaveCount(1);

    const { hexOffenders, malformedOffenders } = await scanForRawColors(page, DEFAULT_COMMENT_COLOR);

    expect(hexOffenders, hexOffenders.join('\n')).toEqual([]);
    expect(malformedOffenders, malformedOffenders.join('\n')).toEqual([]);
  });
});
