import { expect, test, type Locator, type Page } from '@playwright/test';
import {
  createChoiceOptionsUxFixture,
  createImportedChildEditingFixture,
  createPathPropertyFixture,
  createPropertiesTypingFixture,
  createRequiredMarkersFixture,
} from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  type OutgoingHostMessage,
  waitForOutgoingMessage,
} from './support/harness';
import { evaluateParityCases } from './support/parityCli';

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
    // 'GET' is the schema defaultValue for method, so stripDefaultProps removes it from the
    // export payload. The backend treats a missing method as GET (the default).
    expect(httpProps.method).toBeUndefined();
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

test.describe('Flow Canvas Imported Child Editing', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createImportedChildEditingFixture());
    await expect(nodeById(page, 'if-1')).toBeVisible();
    await expect(nodeById(page, 'then-1')).toBeVisible();
  });

  test('imported branch child node properties are editable and persist after reselection', async ({ page }) => {
    const ifNode = nodeById(page, 'if-1');
    const thenNode = nodeById(page, 'then-1');

    await thenNode.click({ force: true });
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    const messageInput = page.getByTestId('properties-field-message-code-input');
    await expect(messageInput).toHaveValue('imported-child-value');
    await typePerKeystroke(messageInput, 'updated-from-canvas');

    await ifNode.click({ force: true });
    await thenNode.click({ force: true });
    await expect(messageInput).toHaveValue('updated-from-canvas');
  });

  test('apply yaml uses edited imported branch child properties via forced graph export', async ({ page }) => {
    const thenNode = nodeById(page, 'then-1');
    await thenNode.click({ force: true });
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    const messageInput = page.getByTestId('properties-field-message-code-input');
    await typePerKeystroke(messageInput, 'updated-from-canvas');

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

    const ifProps = getNodePropsFromMessage(applyMessage, 'if-1');
    expect(ifProps._forceGraphExport).toBe(true);

    const evaluations = evaluateParityCases([{
      name: 'imported-child-edit-forced-export',
      sourceYaml: `
steps:
  - if:
      condition: \${enabled}
      then:
        - print:
            message: updated-from-canvas
`.trim(),
      nodes: toRecordArray(applyMessage.nodes),
      edges: toRecordArray(applyMessage.edges),
    }]).results;

    expect(evaluations).toHaveLength(1);
    const [result] = evaluations;
    expect(result.exportSuccess, `${result.name} export errors: ${result.exportErrors.join(' | ')}`).toBeTruthy();
    expect(result.exportParseError).toBeNull();
    expect(result.exportValidationErrors).toEqual([]);
    expect(result.semanticEquivalent, result.semanticDiff ?? undefined).toBeTruthy();
  });
});

test.describe('Flow Canvas Path Browsing', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createPathPropertyFixture());

    await expect(nodeById(page, 'node-playsound')).toBeVisible();
  });

  test('path fields can request host browse and apply selected path', async ({ page }) => {
    const playsoundNode = nodeById(page, 'node-playsound');
    await playsoundNode.click();
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    const pathInput = page.getByTestId('properties-field-path-text-input');
    const browseButton = page.getByTestId('properties-field-path-text-browse');

    await expect(pathInput).toHaveValue('');
    await expect(browseButton).toBeVisible();

    await clearOutgoingMessages(page);
    await browseButton.click();

    const browseRequest = await waitForOutgoingMessage(page, 'browse-path');
    const requestId = String(browseRequest.requestId ?? '');
    expect(requestId.length).toBeGreaterThan(0);

    const selectedPath = 'C:\\\\Windows\\\\Media\\\\Alarm02.wav';
    await postHostMessage(page, {
      type: 'browse-path-result',
      requestId,
      canceled: false,
      path: selectedPath,
    });

    await expect(pathInput).toHaveValue(selectedPath);
  });
});

test.describe('Flow Canvas Required Markers', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createRequiredMarkersFixture());

    await expect(nodeById(page, 'node-extract')).toBeVisible();
  });

  test('static required stars match parser/runtime required fields', async ({ page }) => {
    await nodeById(page, 'node-extract').click({ force: true });
    await expect(page.getByTestId('properties-panel')).toBeVisible();
    await expectFieldRequired(page, 'properties-field-from-text', true);

    await nodeById(page, 'node-browser-callback').click({ force: true });
    await expectFieldRequired(page, 'properties-field-into-text', true);

    await nodeById(page, 'node-input').click({ force: true });
    await expectFieldRequired(page, 'properties-field-prompt-text', false);
    await expectFieldRequired(page, 'properties-field-into-text', true);

    await nodeById(page, 'node-choose').click({ force: true });
    await expectFieldRequired(page, 'properties-field-prompt-text', false);
    await expectFieldRequired(page, 'properties-field-options-text', true);
    await expectFieldRequired(page, 'properties-field-into-text', true);

    await nodeById(page, 'node-multiselect').click({ force: true });
    await expectFieldRequired(page, 'properties-field-prompt-text', false);
    await expectFieldRequired(page, 'properties-field-options-text', true);
    await expectFieldRequired(page, 'properties-field-into-text', true);

    await nodeById(page, 'node-confirm').click({ force: true });
    await expectFieldRequired(page, 'properties-field-prompt-text', false);
    await expectFieldRequired(page, 'properties-field-into-text', true);

    await nodeById(page, 'node-portcheck').click({ force: true });
    await expectFieldRequired(page, 'properties-field-port-number', false);

    await nodeById(page, 'node-writefile').click({ force: true });
    await expectFieldRequired(page, 'properties-field-content-textarea', false);
  });

  test('conditional required stars update from current property state', async ({ page }) => {
    await nodeById(page, 'node-readfile').click({ force: true });
    await expectFieldRequired(page, 'properties-field-path-text', true);
    const selectFile = page.getByTestId('properties-field-select_file-boolean-input');
    await selectFile.check();
    await expectFieldRequired(page, 'properties-field-path-text', false);

    await nodeById(page, 'node-http-required').click({ force: true });
    const authSelect = page.getByTestId('properties-field-auth-select-input');
    await expectFieldRequired(page, 'properties-field-username-text', false);
    await expectFieldRequired(page, 'properties-field-password-text', false);
    await expectFieldRequired(page, 'properties-field-token-text', false);

    await authSelect.selectOption('basic');
    await expectFieldRequired(page, 'properties-field-username-text', true);
    await expectFieldRequired(page, 'properties-field-password-text', true);
    await expectFieldRequired(page, 'properties-field-token-text', false);

    await authSelect.selectOption('bearer');
    await expectFieldRequired(page, 'properties-field-username-text', false);
    await expectFieldRequired(page, 'properties-field-password-text', false);
    await expectFieldRequired(page, 'properties-field-token-text', true);

    await authSelect.selectOption('none');
    await expectFieldRequired(page, 'properties-field-token-text', false);

    await nodeById(page, 'node-interactive-required').click({ force: true });
    await expectFieldRequired(page, 'properties-field-command-code', false);
    await expectFieldRequired(page, 'properties-field-max_seconds-number', false);
    await expectFieldRequired(page, 'properties-field-max_lines-number', false);

    const showWindow = page.getByTestId('properties-field-show_window-boolean-input');
    await showWindow.uncheck();
    await expectFieldRequired(page, 'properties-field-command-code', true);
    await expectFieldRequired(page, 'properties-field-max_seconds-number', true);
    await expectFieldRequired(page, 'properties-field-max_lines-number', true);

    const maxSeconds = page.getByTestId('properties-field-max_seconds-number-input');
    await maxSeconds.fill('30');
    await expectFieldRequired(page, 'properties-field-command-code', true);
    await expectFieldRequired(page, 'properties-field-max_seconds-number', false);
    await expectFieldRequired(page, 'properties-field-max_lines-number', false);
  });
});

test.describe('Flow Canvas Choice Options UX', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createChoiceOptionsUxFixture());

    await expect(nodeById(page, 'node-choose-ux')).toBeVisible();
  });

  test('legacy options hydrate to static rows and choose default mismatch is shown', async ({ page }) => {
    await nodeById(page, 'node-choose-ux').click({ force: true });
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    await expect(page.getByTestId('properties-field-options-text-mode-static')).toHaveAttribute('data-active', 'true');
    await expect(page.getByTestId('properties-field-options-text-row-0-label')).toHaveValue('alpha');
    await expect(page.getByTestId('properties-field-options-text-row-0-value')).toHaveValue('alpha');
    await expect(page.getByTestId('properties-field-options-text-row-1-label')).toHaveValue('beta');
    await expect(page.getByTestId('properties-field-options-text-row-1-value')).toHaveValue('beta');
    await expect(page.getByTestId('properties-field-default-text-warning')).toContainText('Default value is not in the static options list');
  });

  test('source mode persists scalar options and variable insertion assist', async ({ page }) => {
    await postHostMessage(page, {
      type: 'variables-snapshot',
      variables: {
        interface_list: ['wan1', 'wan2'],
      },
      changedKeys: ['interface_list'],
    });

    await nodeById(page, 'node-choose-ux').click({ force: true });
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    await page.getByTestId('properties-field-options-text-mode-source').click();
    await expect(page.getByTestId('properties-field-options-text-mode-source')).toHaveAttribute('data-active', 'true');

    const sourceInput = page.getByTestId('properties-field-options-text-source-input');
    await sourceInput.fill('${');
    await page.getByTestId('properties-field-options-text-source-insert-var').selectOption('interface_list');
    await expect(sourceInput).toHaveValue('${interface_list}');

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');
    const chooseProps = getNodePropsFromMessage(applyMessage, 'node-choose-ux');
    expect(chooseProps.options).toBe('${interface_list}');
  });

  test('static rows support add/remove/reorder and persist mixed string+object arrays', async ({ page }) => {
    await nodeById(page, 'node-multiselect-ux').click({ force: true });
    await expect(page.getByTestId('properties-panel')).toBeVisible();

    await expect(page.getByTestId('properties-field-options-text-row-0-label')).toHaveValue('one');
    await expect(page.getByTestId('properties-field-options-text-row-1-label')).toHaveValue('Two Label');
    await expect(page.getByTestId('properties-field-options-text-row-1-value')).toHaveValue('two_value');

    await page.getByTestId('properties-field-options-text-add-row').click();
    await page.getByTestId('properties-field-options-text-row-2-label').fill('Three Label');
    await page.getByTestId('properties-field-options-text-row-2-value').fill('three_value');
    await page.getByTestId('properties-field-options-text-row-2-up').click();

    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');
    const multiProps = getNodePropsFromMessage(applyMessage, 'node-multiselect-ux');
    expect(multiProps.options).toEqual([
      'one',
      { label: 'Three Label', value: 'three_value' },
      { label: 'Two Label', value: 'two_value' },
    ]);
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

async function expectFieldRequired(page: Page, fieldTestId: string, required: boolean): Promise<void> {
  const label = page.getByTestId(fieldTestId).locator('label').first();
  if (required) {
    await expect(label).toContainText('*');
    return;
  }

  await expect(label).not.toContainText('*');
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

function toRecordArray(value: unknown): Array<Record<string, unknown>> {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
}
