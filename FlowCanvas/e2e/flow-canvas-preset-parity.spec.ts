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

  test('apply yaml preserves nested if branch shape when if node has stale snippet', async ({ page }) => {
    const sourceYaml = `
---
steps:
  - send:
      command: get system status
      capture: systemstatusoutput
  - extract:
      from: systemstatusoutput
      pattern: 'Hostname: (.+?)$'
      into: foo
  - print:
      message: \${foo}
  - confirm:
      title: test
      prompt: test \${foo}
      into: abc
  - if:
      condition: abc == "true"
      then:
        - ping:
            host: 192.168.1.1
            count: 1
            into: pingresults
      else:
        - ping:
            host: 192.168.1.1
            count: 1
`.trim();

    await setGraphViaActions(page, createIfApplyYamlShapeFixture());
    await clearOutgoingMessages(page);
    await page.getByRole('button', { name: /apply yaml/i }).click();
    const applyMessage = await waitForOutgoingMessage(page, 'apply-yaml');

    const evaluation = evaluateParityCases([{
      name: 'synthetic-if-apply-yaml-shape',
      sourceYaml,
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

function createIfApplyYamlShapeFixture(): { nodes: Array<Record<string, unknown>>; edges: Array<Record<string, unknown>> } {
  return {
    nodes: [
      {
        id: '__start__',
        type: 'start',
        position: { x: 80, y: 40 },
        data: {
          blockType: '_start',
          label: 'Untitled Script',
          props: {},
        },
      },
      {
        id: 'send-1',
        type: 'block',
        position: { x: 100, y: 140 },
        data: {
          blockType: 'send',
          label: 'Send',
          props: {
            command: 'get system status',
            capture: 'systemstatusoutput',
          },
        },
      },
      {
        id: 'extract-1',
        type: 'block',
        position: { x: 100, y: 260 },
        data: {
          blockType: 'extract',
          label: 'Extract',
          props: {
            from: 'systemstatusoutput',
            pattern: 'Hostname: (.+?)$',
            into: 'foo',
          },
        },
      },
      {
        id: 'print-1',
        type: 'block',
        position: { x: 100, y: 380 },
        data: {
          blockType: 'print',
          label: 'Print',
          props: {
            message: '${foo}',
          },
        },
      },
      {
        id: 'confirm-1',
        type: 'block',
        position: { x: 100, y: 500 },
        data: {
          blockType: 'confirm',
          label: 'Confirm',
          props: {
            title: 'test',
            prompt: 'test ${foo}',
            into: 'abc',
          },
        },
      },
      {
        id: 'if-1',
        type: 'block',
        position: { x: 100, y: 620 },
        data: {
          blockType: 'if',
          label: 'If',
          props: {
            condition: 'abc == "true"',
            _yamlSnippet: '- if:\n    condition: abc == "true"\n',
          },
        },
      },
      {
        id: 'ping-then-1',
        type: 'block',
        position: { x: -40, y: 760 },
        data: {
          blockType: 'ping',
          label: 'Ping',
          props: {
            host: '192.168.1.1',
            count: 1,
            into: 'pingresults',
          },
        },
      },
      {
        id: 'ping-else-1',
        type: 'block',
        position: { x: 260, y: 760 },
        data: {
          blockType: 'ping',
          label: 'Ping',
          props: {
            host: '192.168.1.1',
            count: 1,
          },
        },
      },
    ],
    edges: [
      {
        id: 'e-start-send',
        source: '__start__',
        target: 'send-1',
      },
      {
        id: 'e-send-extract',
        source: 'send-1',
        target: 'extract-1',
      },
      {
        id: 'e-extract-print',
        source: 'extract-1',
        target: 'print-1',
      },
      {
        id: 'e-print-confirm',
        source: 'print-1',
        target: 'confirm-1',
      },
      {
        id: 'e-confirm-if',
        source: 'confirm-1',
        target: 'if-1',
      },
      {
        id: 'e-if-then',
        source: 'if-1',
        target: 'ping-then-1',
        data: {
          branchPath: 'then',
        },
      },
      {
        id: 'e-if-else',
        source: 'if-1',
        sourceHandle: 'false',
        target: 'ping-else-1',
        data: {
          branchPath: 'else',
        },
      },
    ],
  };
}
