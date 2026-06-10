import { expect, test, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const frame = (loopId: string, i: number, label?: string) => ({ loopId, i, label });

// Fixture nodes use type 'block' (matches the other e2e fixtures in fixtures/graphs.ts) and carry
// _stepPath/_isChildOf/_branchLabel inside data.props. The 'do' branch label gives F a LOOP band,
// which is what BranchBandsLayer hangs the IterationCluster off (bands are on by default). Branch
// identity (loop body) lives on the children's props; the loop node F is a foreach so it counts as
// a LOOP_TYPE for iteration scoping.
const loopGraph = () => ({
  nodes: [
    {
      id: 'F', type: 'block', position: { x: 0, y: 0 },
      data: { blockType: 'foreach', label: 'For each host', props: { _stepPath: 'steps/0', foreach: 'host in hosts' } },
    },
    {
      // 'send' (a registered leaf block) — the plan template's 'ssh' is not a known block type, so
      // it renders an "Unknown" fallback node without handles (breaking the F→A edge + exec chip).
      id: 'A', type: 'block', position: { x: 220, y: 140 },
      data: { blockType: 'send', label: 'Check disk', props: { _stepPath: 'steps/0/do/0', _isChildOf: 'F', _branchLabel: 'do', command: 'df -h' } },
    },
    {
      id: 'B', type: 'block', position: { x: 220, y: 280 },
      data: { blockType: 'print', label: 'Report', props: { _stepPath: 'steps/0/do/1', _isChildOf: 'F', _branchLabel: 'do' } },
    },
  ],
  edges: [
    { id: 'eFA', source: 'F', target: 'A' },
    { id: 'eAB', source: 'A', target: 'B' },
  ],
});

/** 3 iterations: A runs in all three; B runs in 0 and 2 only; A errors in iteration 1.
 *  Completion events carry per-iteration `variables` (so the Variables panel time-travels),
 *  and B emits step-output in the iterations it ran (so per-block output honesty is testable). */
async function simulateRun(page: Page) {
  await postHostMessage(page, { type: 'execution-started' });
  for (let i = 0; i < 3; i++) {
    const stack = [frame('F', i, `host${i}`)];
    const vars = { host: `host${i}`, disk_pct: 40 + i };
    await postHostMessage(page, { type: 'execution-update', stepId: 'A', state: 'running', iterationStack: stack });
    const aState = i === 1 ? 'error' : 'success';
    await postHostMessage(page, { type: 'execution-update', stepId: 'A', state: aState, duration: 10 + i, iterationStack: stack, variables: vars });
    await postHostMessage(page, { type: 'step-output', stepId: 'A', output: `disk output ${i}`, iterationStack: stack });
    if (i !== 1) {
      await postHostMessage(page, { type: 'execution-update', stepId: 'B', state: 'running', iterationStack: stack });
      await postHostMessage(page, { type: 'execution-update', stepId: 'B', state: 'success', duration: 5, iterationStack: stack, variables: vars });
      await postHostMessage(page, { type: 'step-output', stepId: 'B', output: `report ${i}`, iterationStack: stack });
    }
  }
  await postHostMessage(page, { type: 'execution-update', stepId: 'F', state: 'success', duration: 60, iterationCount: 3, variables: { host: 'final', disk_pct: 99 } });
  await postHostMessage(page, { type: 'execution-finished' });
}

test.describe('Flow Canvas Iteration Stepper', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, loopGraph());
    await expect(page.locator('.react-flow__node[data-id="F"]')).toBeVisible();
    await simulateRun(page);
  });

  test('cluster appears post-run with the iteration count and steps through iterations', async ({ page }) => {
    const cluster = page.getByTestId('iteration-cluster');
    await expect(cluster).toHaveCount(1);
    await expect(page.getByTestId('iter-counter')).toHaveText('3');

    await page.getByTestId('iter-next').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('1/3');
    await expect(page.getByTestId('iter-label')).toHaveText('host0');
  });

  test('stepping re-scopes the neon path to the selected iteration', async ({ page }) => {
    // Aggregate: start→F plus both loop-body edges (F→A, A→B) lit. load-graph auto-adds the
    // start→F spine edge, and it stays on-path through iteration scoping because its source is the
    // Start node (scoped only by whether F ran), so the count is 3, not the template's 2.
    await expect(page.locator('.fc-edge-onpath')).toHaveCount(3);

    // Iteration 2 (i=1): B never ran → start→F and F→A stay lit, A→B drops to idle.
    await page.getByTestId('iter-next').click();
    await page.getByTestId('iter-next').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('2/3');
    await expect(page.locator('.fc-edge-onpath')).toHaveCount(2);

    // Back to ALL restores the aggregate.
    await page.getByTestId('iter-all').click();
    await expect(page.locator('.fc-edge-onpath')).toHaveCount(3);
  });

  test('⚠ jumps to the failed iteration and the block shows its error state', async ({ page }) => {
    await expect(page.getByTestId('iter-fail')).toHaveText(/1/);
    await page.getByTestId('iter-fail').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('2/3');
    await expect(page.getByTestId('iter-label')).toHaveText('host1');

    // A shows ERROR for this iteration; B (never reached) shows no chip.
    await expect(page.locator('.react-flow__node[data-id="A"]')).toContainText('ERROR');
  });

  test('Block Output follows the selected iteration', async ({ page }) => {
    await page.locator('.react-flow__node[data-id="A"]').click(); // selects node + Block tab
    await page.getByTestId('iter-next').click(); // iteration 1 → first output entry
    await expect(page.getByText('(1/3)')).toBeVisible();
    await expect(page.getByText('disk output 0')).toBeVisible();

    await page.getByTestId('iter-next').click();
    await expect(page.getByText('(2/3)')).toBeVisible();
  });

  test('variables panel time-travels with the iteration selection', async ({ page }) => {
    // The Variables panel is visible by default (panelsVisible.variables === true), no toggle needed.
    await page.getByTestId('iter-next').click(); // iteration 1 (i=0)
    const banner = page.getByTestId('iter-vars-banner');
    await expect(banner).toBeVisible();
    await expect(banner).toContainText('1/3');
    await expect(banner).toContainText('host0'); // foreach label
    await expect(page.getByText('"host0"')).toBeVisible(); // snapshot value for `host`

    await page.getByTestId('iter-vars-live').click();
    await expect(page.getByTestId('iter-vars-banner')).toHaveCount(0);
  });

  test('block output is honest about empty iterations', async ({ page }) => {
    await page.locator('.react-flow__node[data-id="B"]').click(); // select B + Block tab

    // Iteration 2 (i=1): B never ran → the panel is still iteration-scoped (chip shows) but
    // there is no entry, so it must say so rather than fall back to the latest output.
    await page.getByTestId('iter-next').click();
    await page.getByTestId('iter-next').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('2/3');
    await expect(page.getByTestId('iter-output-empty')).toBeVisible();
    await expect(page.getByTestId('iter-output-chip')).toBeVisible();

    // Iteration 1 (i=0): B succeeded and emitted `report 0`.
    await page.getByTestId('iter-prev').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('1/3');
    await expect(page.getByText('report 0')).toBeVisible();
    await expect(page.getByTestId('iter-output-empty')).toHaveCount(0);
  });

  test('loop node selected shows the guidance hint', async ({ page }) => {
    await page.locator('.react-flow__node[data-id="F"]').click(); // select the foreach container
    await page.getByTestId('iter-next').click(); // pin an iteration so the loop has a selection
    await expect(page.getByTestId('iter-output-loophint')).toBeVisible();
  });

  test('a new run clears the cluster', async ({ page }) => {
    await expect(page.getByTestId('iteration-cluster')).toHaveCount(1);
    await postHostMessage(page, { type: 'execution-started' });
    await expect(page.getByTestId('iteration-cluster')).toHaveCount(0);
  });

  test('loading a new graph clears stale iteration state', async ({ page }) => {
    await expect(page.getByTestId('iteration-cluster')).toHaveCount(1);
    await loadGraphFixture(page, loopGraph()); // re-load (same shape, new session)
    await expect(page.getByTestId('iteration-cluster')).toHaveCount(0);
  });
});
