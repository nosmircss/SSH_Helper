import { expect, test, type Locator, type Page } from '@playwright/test';
import { createBranchPathFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getGraphSnapshot,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

function edgePath(page: Page, edgeId: string): Locator {
  return page.locator(`.react-flow__edge[data-id="${edgeId}"] .react-flow__edge-path`);
}
function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

// Resolve any CSS <color> expression (a --fc-* token via var(), or a color-mix(...)) to
// Chromium's serialized <color>, so we can compare it to a path's computed `stroke` (both go
// through the same color serialization).
async function resolveColor(page: Page, expr: string): Promise<string> {
  return page.evaluate((value) => {
    const probe = document.createElement('div');
    probe.style.color = value;
    document.body.appendChild(probe);
    const out = getComputedStyle(probe).color;
    probe.remove();
    return out;
  }, expr);
}
async function strokeOf(page: Page, edgeId: string): Promise<string> {
  return edgePath(page, edgeId).evaluate((el) => getComputedStyle(el as Element).stroke);
}

// Drive a full run that takes the THEN branch, entirely via host messages (no SSH).
async function runThenBranch(page: Page): Promise<void> {
  await postHostMessage(page, { type: 'execution-started' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'running' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'success', duration: 10, branchTaken: 'then' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'running' });
  await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'success', duration: 10 });
  await postHostMessage(page, { type: 'execution-finished' });
}

test.describe('Flow Canvas Execution Path Highlight', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createBranchPathFixture());
    await expect(nodeById(page, 'if-1')).toBeVisible();
  });

  test('lights the taken branch and fades the untaken branch, and persists after the run', async ({ page }) => {
    await runThenBranch(page);

    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
    await expect(edgePath(page, 'edge-if-else')).toHaveClass(/fc-edge-untaken/);
    // The start edge lights once its target (if-1) has run.
    await expect(edgePath(page, 'edge-start-if')).toHaveClass(/fc-edge-onpath/);

    // Color: each lit wire is a bright near-white CORE (color-mix toward white) — mirrors the
    // core mix in AnimatedEdge. A plain/spine edge's hue promotes to the cyan traversed token;
    // an on-path branch edge keeps its branch hue (then=green). Both are brightened identically.
    expect(await strokeOf(page, 'edge-start-if')).toBe(
      await resolveColor(page, 'color-mix(in oklch, var(--fc-edge-traversed), white 30%)'),
    );
    expect(await strokeOf(page, 'edge-if-then')).toBe(
      await resolveColor(page, 'color-mix(in oklch, var(--fc-branch-then), white 30%)'),
    );

    // The arrowhead matches the glow hue (a per-edge marker), not the idle grey — spine arrow is
    // the traversed cyan, branch arrow keeps the branch hue, so the lit tip reads as one wire.
    expect(await edgePath(page, 'edge-start-if').getAttribute('marker-end')).toContain('fc-arrow-onpath-edge-start-if');
    expect(await edgePath(page, 'edge-if-then').getAttribute('marker-end')).toContain('fc-arrow-onpath-edge-if-then');
    const arrowFill = (sel: string) => page.locator(sel).evaluate((el) => getComputedStyle(el as Element).fill);
    expect(await arrowFill('#fc-arrow-onpath-edge-start-if path')).toBe(await resolveColor(page, 'var(--fc-edge-traversed)'));
    expect(await arrowFill('#fc-arrow-onpath-edge-if-then path')).toBe(await resolveColor(page, 'var(--fc-branch-then)'));

    // The untaken branch is visibly faded (real Chromium resolves the class opacity).
    const untakenOpacity = await edgePath(page, 'edge-if-else').evaluate(
      (el) => getComputedStyle(el as Element).opacity,
    );
    expect(Number(untakenOpacity)).toBeCloseTo(0.35, 2);

    // Persists after execution-finished (isRunning is now false).
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
  });

  test('builds the neon path live: the taken arm lights before the container completes', async ({ page }) => {
    // Drive the REAL message order: the container is still 'running' and the THEN child has just
    // started — branchTaken has NOT been sent yet (it only rides the container's completion).
    await postHostMessage(page, { type: 'execution-started' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'running' });

    // The taken arm is already neon — the path builds AS the run reaches it, not at the end.
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
    // The untaken sibling is still dark: the branch hasn't resolved, so it is neither lit nor faded.
    await expect(edgePath(page, 'edge-if-else')).not.toHaveClass(/fc-edge-onpath/);
    await expect(edgePath(page, 'edge-if-else')).not.toHaveClass(/fc-edge-untaken/);

    // Once the container completes (branchTaken arrives), the sibling fades to untaken.
    await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'success', duration: 10 });
    await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'success', duration: 10, branchTaken: 'then' });
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
    await expect(edgePath(page, 'edge-if-else')).toHaveClass(/fc-edge-untaken/);
  });

  test('the single frontier dot rides the deepest running edge (a running container yields to its child)', async ({ page }) => {
    const packetOn = (edgeId: string) =>
      page.locator(`.react-flow__edge[data-id="${edgeId}"] circle.fc-edge-packet`);

    await postHostMessage(page, { type: 'execution-started' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'if-1', state: 'running' });
    // Before any child runs, the dot is on the way INTO the container.
    await expect(packetOn('edge-start-if')).toHaveCount(1);
    await expect(packetOn('edge-if-then')).toHaveCount(0);

    // The THEN child starts: control is now deeper, so the container yields its incoming dot and the
    // single dot moves to the if→then edge — never two dots at once.
    await postHostMessage(page, { type: 'execution-update', stepId: 'then-1', state: 'running' });
    await expect(packetOn('edge-start-if')).toHaveCount(0);
    await expect(packetOn('edge-if-then')).toHaveCount(1);
  });

  test('Clear Path resets the edges but keeps node result badges', async ({ page }) => {
    await runThenBranch(page);
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);

    await page.getByRole('button', { name: '⌫ Clear Path' }).click();

    await expect(edgePath(page, 'edge-if-then')).not.toHaveClass(/fc-edge-onpath/);
    await expect(edgePath(page, 'edge-if-else')).not.toHaveClass(/fc-edge-untaken/);
    // The then-1 node's duration badge from the run is still on screen.
    await expect(nodeById(page, 'then-1').getByText('10ms', { exact: true })).toBeVisible();
  });

  test('a fresh run re-shows the path after a clear', async ({ page }) => {
    await runThenBranch(page);
    await page.getByRole('button', { name: '⌫ Clear Path' }).click();
    await expect(edgePath(page, 'edge-if-then')).not.toHaveClass(/fc-edge-onpath/);

    await runThenBranch(page);
    await expect(edgePath(page, 'edge-if-then')).toHaveClass(/fc-edge-onpath/);
  });

  test('PARITY: clearing the path is render-only and does not mutate the graph snapshot', async ({ page }) => {
    await runThenBranch(page);
    const before = await getGraphSnapshot(page);

    await page.getByRole('button', { name: '⌫ Clear Path' }).click();
    await expect(edgePath(page, 'edge-if-then')).not.toHaveClass(/fc-edge-onpath/);

    const after = await getGraphSnapshot(page);
    expect(JSON.stringify(after)).toBe(JSON.stringify(before));
  });
});
