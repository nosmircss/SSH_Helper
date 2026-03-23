import { expect, test } from '@playwright/test';
import { createRunParityFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  getOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Browser Harness', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('toolbar Run and keyboard F5 emit equivalent execute-canvas payloads', async ({ page }) => {
    await loadGraphFixture(page, createRunParityFixture());
    await postHostMessage(page, {
      type: 'set-target-host',
      host: {
        ip: '10.0.0.10',
        port: 22,
        username: 'runner',
        variables: {},
      },
    });

    await expect(page.getByText('Alpha', { exact: true })).toBeVisible();
    await expect(page.getByText('Beta', { exact: true })).toBeVisible();
    await expect(page.getByText('10.0.0.10', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: /run/i }).click();
    const toolbarRun = await waitForOutgoingMessage(page, 'execute-canvas');
    expect(toolbarRun.mode).toBe('run');

    // Comment nodes are visual-only and must not be exported to host execution payloads.
    const toolbarNodes = (toolbarRun.nodes as Array<{ id?: string; type?: string }>) ?? [];
    expect(toolbarNodes.some((node) => node.id === 'comment-1' || node.type === 'comment')).toBeFalsy();

    await clearOutgoingMessages(page);
    await page.keyboard.press('F5');
    const keyboardRun = await waitForOutgoingMessage(page, 'execute-canvas');
    expect(keyboardRun.mode).toBe('run');

    expect({
      mode: keyboardRun.mode,
      payload: normalizeGraphPayload(keyboardRun.nodes, keyboardRun.edges),
    }).toEqual({
      mode: toolbarRun.mode,
      payload: normalizeGraphPayload(toolbarRun.nodes, toolbarRun.edges),
    });
  });

  test('toolbar Test Step and keyboard Ctrl+Enter target the same selected node', async ({ page }) => {
    await loadGraphFixture(page, createRunParityFixture());
    await expect(page.getByText('Beta', { exact: true })).toBeVisible();

    await page.getByText('Beta', { exact: true }).click();

    await page.getByRole('button', { name: /test step/i }).click();
    const toolbarTestStep = await waitForOutgoingMessage(page, 'execute-canvas');
    expect(toolbarTestStep.mode).toBe('test-step');
    expect(toolbarTestStep.stepId).toBe('node-2');

    await clearOutgoingMessages(page);
    await page.keyboard.press('Control+Enter');
    const keyboardMessages = await getOutgoingMessages(page);
    const keyboardTestStep = keyboardMessages.find(
      (m) => m.type === 'execute-canvas' && m.mode === 'test-step',
    );
    expect(keyboardTestStep).toBeTruthy();

    expect({
      stepId: keyboardTestStep?.stepId,
      payload: normalizeGraphPayload(keyboardTestStep?.nodes, keyboardTestStep?.edges),
    }).toEqual({
      stepId: toolbarTestStep.stepId,
      payload: normalizeGraphPayload(toolbarTestStep.nodes, toolbarTestStep.edges),
    });
  });

  test('run re-enables after editing graph following export error', async ({ page }) => {
    await loadGraphFixture(page, createRunParityFixture());
    await postHostMessage(page, {
      type: 'set-target-host',
      host: {
        ip: '10.0.0.10',
        port: 22,
        username: 'runner',
        variables: {},
      },
    });

    const runButton = page.getByRole('button', { name: /run/i });
    await expect(runButton).toBeEnabled();

    const alertPromise = page.waitForEvent('dialog');
    await postHostMessage(page, {
      type: 'apply-result',
      success: false,
      errors: ["Block 'print' is missing required option(s): message."],
      warnings: [],
      nodeStepMap: {},
    });
    const alert = await alertPromise;
    await alert.accept();

    await expect(runButton).toBeDisabled();

    await page.getByText('Alpha', { exact: true }).click();
    const messageInput = page.getByTestId('properties-field-message-code-input');
    await expect(messageInput).toBeVisible();
    await messageInput.fill(' ');

    await expect(runButton).toBeEnabled();
  });
});

function normalizeGraphPayload(
  nodesValue: unknown,
  edgesValue: unknown,
): { nodes: unknown[]; edges: unknown[] } {
  const nodes = Array.isArray(nodesValue) ? nodesValue : [];
  const edges = Array.isArray(edgesValue) ? edgesValue : [];

  return {
    nodes: nodes.map((node) => {
      if (!node || typeof node !== 'object') return node;
      const { selected: _selected, className: _className, ...rest } = node as Record<string, unknown>;
      return rest;
    }),
    edges,
  };
}
