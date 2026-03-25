import { expect, test } from '@playwright/test';
import { evaluateParityCases, type ParityEvaluationInput } from './support/parityCli';
import {
  getSyntheticBrowserCallbackParityCase,
  getValidQaPresetParityCases,
} from './support/qaPresetLoader';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  setGraphViaActions,
  waitForOutgoingMessage,
} from './support/harness';

test.describe('Flow Canvas QA Preset Parity', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('all valid qa presets roundtrip through front-end actions with semantic parity', async ({ page }) => {
    const validCases = getValidQaPresetParityCases();
    expect(validCases.length).toBeGreaterThan(0);

    const evaluationInputs: ParityEvaluationInput[] = [];

    for (const parityCase of validCases) {
      expect(parityCase.sourceParseError, `${parityCase.name} source parse error`).toBeNull();
      expect(parityCase.sourceValidationErrors, `${parityCase.name} source validation`).toEqual([]);
      expect(parityCase.graphBuildError, `${parityCase.name} graph build error`).toBeNull();

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
      if (!result.exportSuccess) {
        failures.push(`${result.name}: export failed: ${result.exportErrors.join(' | ')}`);
        continue;
      }

      if (result.exportParseError) {
        failures.push(`${result.name}: export parse error: ${result.exportParseError}`);
        continue;
      }

      if (result.exportValidationErrors.length > 0) {
        failures.push(`${result.name}: export validation errors: ${result.exportValidationErrors.join(' | ')}`);
      }

      if (result.semanticEquivalent !== true) {
        failures.push(`${result.name}: semantic mismatch: ${result.semanticDiff ?? 'unknown diff'}`);
      }
    }

    expect(failures).toEqual([]);
  });

  test('synthetic browser_callback case passes parity checks', async ({ page }) => {
    const syntheticCase = getSyntheticBrowserCallbackParityCase();
    expect(syntheticCase.classification).toBe('valid');
    expect(syntheticCase.sourceParseError).toBeNull();
    expect(syntheticCase.sourceValidationErrors).toEqual([]);
    expect(syntheticCase.graphBuildError).toBeNull();

    await setGraphViaActions(page, { nodes: syntheticCase.nodes, edges: syntheticCase.edges });
    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

    const evaluation = evaluateParityCases([{
      name: syntheticCase.name,
      sourceYaml: syntheticCase.sourceYaml,
      nodes: toRecordArray(applyMessage.nodes),
      edges: toRecordArray(applyMessage.edges),
    }]).results;

    expect(evaluation).toHaveLength(1);
    const [result] = evaluation;
    expect(result.exportSuccess, `${result.name} export errors: ${result.exportErrors.join(' | ')}`).toBeTruthy();
    expect(result.exportParseError).toBeNull();
    expect(result.exportValidationErrors).toEqual([]);
    expect(result.semanticEquivalent, result.semanticDiff ?? undefined).toBeTruthy();
  });
});

function toRecordArray(value: unknown): Array<Record<string, unknown>> {
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is Record<string, unknown> => !!item && typeof item === 'object');
}
