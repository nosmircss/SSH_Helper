import { expect, test, type Locator, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas Variable Inspector', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('masks password-like variables while leaving non-sensitive values visible', async ({ page }) => {
    await postHostMessage(page, {
      type: 'variables-snapshot',
      variables: {
        username: 'chris',
        password: 'super-secret-password',
      },
    });

    const usernameRow = variableRow(page, 'username');
    const passwordRow = variableRow(page, 'password');

    await expect(usernameRow).toContainText('"chris"');
    await expect(passwordRow).toContainText('"********"');
    await expect(passwordRow).not.toContainText('super-secret-password');
  });
});

function variableRow(page: Page, variableName: string): Locator {
  return page
    .locator('div')
    .filter({ hasText: new RegExp(`^\\s*${variableName}\\s*=`) })
    .first();
}
