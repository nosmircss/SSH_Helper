import { expect, test } from '@playwright/test';
import { evaluateParityCases, type ParityEvaluationInput } from './support/parityCli';
import { getIntentionalInvalidQaPresetParityCases } from './support/qaPresetLoader';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  setGraphViaActions,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas QA Preset Negative Parity', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('intentional-invalid qa presets stay invalid after front-end reconstruction', async ({ page }) => {
    const invalidCases = getIntentionalInvalidQaPresetParityCases();
    expect(invalidCases.length).toBeGreaterThan(0);

    const evaluationInputs: ParityEvaluationInput[] = [];
    for (const parityCase of invalidCases) {
      expect(parityCase.sourceParseError, `${parityCase.name} should parse`).toBeNull();
      expect(parityCase.graphBuildError, `${parityCase.name} graph build error`).toBeNull();
      expect(parityCase.sourceValidationErrors.length, `${parityCase.name} should be invalid at source`).toBeGreaterThan(0);

      await setGraphViaActions(page, { nodes: parityCase.nodes, edges: parityCase.edges });
      await clearOutgoingMessages(page);
      await page.getByRole('button', { name: /apply yaml/i }).click();
      const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

      evaluationInputs.push({
        name: parityCase.name,
        sourceYaml: parityCase.sourceYaml,
        nodes: toRecordArray(applyMessage.nodes),
        edges: toRecordArray(applyMessage.edges),
      });
    }

    const evaluations = evaluateParityCases(evaluationInputs).results;
    const failures: string[] = [];
    for (const result of evaluations) {
      const hasDiagnostics = result.exportValidationErrors.length > 0
        || !!result.exportParseError
        || !result.exportSuccess
        || result.exportErrors.length > 0;

      if (!hasDiagnostics) {
        failures.push(`${result.name}: expected invalid diagnostics but export looked valid.`);
      }
    }

    expect(failures).toEqual([]);
  });
});

function toRecordArray(value: unknown): Array<Record<string, unknown>> {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
}
