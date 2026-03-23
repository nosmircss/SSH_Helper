import { expect, test, type Locator, type Page } from '@playwright/test';
import { createPropertiesTypingFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  type OutgoingHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Properties Typing', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createPropertiesTypingFixture());

    await expect(nodeById(page, 'node-send')).toBeVisible();
    await expect(nodeById(page, 'node-http')).toBeVisible();
  });

  test('text-like and select properties persist after rapid reselection', async ({ page }) => {
    const sendNode = nodeById(page, 'node-send');
    const httpNode = nodeById(page, 'node-http');

    await sendNode.click();
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    const displayNameInput = page.getByTestId('properties-display-name-input');
    await typePerKeystroke(displayNameInput, 'Send Updated');
    await expect(sendNode).toContainText('Send Updated');

    const commandInput = page.getByTestId('properties-field-command-code-input');
    await typePerKeystroke(commandInput, 'show version');
    await expect(sendNode).toContainText('show version');
    await expect(sendNode).not.toContainText('old command from import');

    const expectInput = page.getByTestId('properties-field-expect-text-input');
    await typePerKeystroke(expectInput, 'Version\\s+\\d+');
    const onErrorSelect = page.getByTestId('properties-field-on_error-select-input');
    await onErrorSelect.selectOption('continue');
    await expect(onErrorSelect).toHaveValue('continue');

    await httpNode.click();
    const bodyInput = page.getByTestId('properties-field-body-textarea-input');
    await typePerKeystroke(bodyInput, 'line one\nline two');
    const methodSelect = page.getByTestId('properties-field-method-select-input');
    await methodSelect.selectOption('POST');
    await expect(methodSelect).toHaveValue('POST');

    await sendNode.click();
    await expect(displayNameInput).toHaveValue('Send Updated');
    await expect(commandInput).toHaveValue('show version');
    await expect(expectInput).toHaveValue('Version\\s+\\d+');
    await expect(onErrorSelect).toHaveValue('continue');

    await httpNode.click();
    await expect(bodyInput).toHaveValue('line one\nline two');
    await expect(methodSelect).toHaveValue('POST');
  });

  test('first select interaction on a default-backed field persists explicit value', async ({ page }) => {
    const httpNode = nodeById(page, 'node-http');

    await httpNode.click();
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    const methodSelect = page.getByTestId('properties-field-method-select-input');
    await expect(methodSelect).toHaveValue('GET');

    // User explicitly interacts without changing away from fallback default.
    await methodSelect.focus();
    await page.keyboard.press('Tab');

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');
    const httpProps = getNodePropsFromMessage(applyMessage, 'node-http');
    expect(httpProps.method).toBe('GET');
  });

  test('first select change persists immediately in outgoing payload', async ({ page }) => {
    const sendNode = nodeById(page, 'node-send');

    await sendNode.click();
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    const onErrorSelect = page.getByTestId('properties-field-on_error-select-input');
    await onErrorSelect.selectOption('continue');
    await expect(onErrorSelect).toHaveValue('continue');

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');
    const sendProps = getNodePropsFromMessage(applyMessage, 'node-send');
    expect(sendProps.on_error).toBe('continue');
  });
});

async function typePerKeystroke(locator: Locator, value: string): Promise<void> {
  await locator.click();
  await locator.fill('');
  await expect(locator).toHaveValue('');

  let current = '';
  for (const ch of value) {
    current += ch;
    await locator.type(ch);
    await expect(locator).toHaveValue(current);
  }
}

function nodeById(page: Page, nodeId: string): Locator {
  return page.locator(`.react-flow__node[data-id="${nodeId}"]`);
}

function getNodePropsFromMessage(
  message: OutgoingHostMessage,
  nodeId: string,
): Record<string, unknown> {
  const nodes = Array.isArray(message.nodes)
    ? (message.nodes as Array<Record<string, unknown>>)
    : [];
  const node = nodes.find((candidate) => String(candidate.id ?? '') === nodeId);
  if (!node || typeof node !== 'object') {
    return {};
  }

  const data = (node.data ?? {}) as Record<string, unknown>;
  const props = data.props;
  return props && typeof props === 'object'
    ? (props as Record<string, unknown>)
    : {};
}
