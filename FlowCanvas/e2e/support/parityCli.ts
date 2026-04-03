import path from 'path';
import { spawnSync } from 'child_process';

export interface PreparedParityCase {
  name: string;
  classification: 'valid' | 'intentional-invalid';
  isSynthetic: boolean;
  sourceYaml: string;
  sourceParseError: string | null;
  sourceValidationErrors: string[];
  graphBuildError: string | null;
  nodes: Array<Record<string, unknown>>;
  edges: Array<Record<string, unknown>>;
}

export interface PreparedParityPayload {
  cases: PreparedParityCase[];
}

export interface ParityEvaluationInput {
  name: string;
  sourceYaml: string;
  nodes: Array<Record<string, unknown>>;
  edges: Array<Record<string, unknown>>;
}

export interface ParityEvaluationResult {
  name: string;
  sourceParseError: string | null;
  sourceValidationErrors: string[];
  exportSuccess: boolean;
  exportErrors: string[];
  exportWarnings: string[];
  exportYaml: string;
  exportParseError: string | null;
  exportValidationErrors: string[];
  semanticEquivalent: boolean | null;
  semanticDiff: string | null;
}

export interface ParityEvaluationPayload {
  results: ParityEvaluationResult[];
}

const repoRoot = path.resolve(__dirname, '../../..');
const cliProjectPath = path.join(repoRoot, 'FlowCanvas', 'tools', 'FlowCanvasParityCli', 'FlowCanvasParityCli.csproj');
const qaPresetsPath = path.join(repoRoot, 'qa_presets.json');
const parityCliBaseOutputPath = toMsBuildDirectoryPath(path.join(repoRoot, 'artifacts', 'flowcanvas-parity-cli', 'bin'));
let cliBuilt = false;

function toMsBuildDirectoryPath(value: string): string {
  return /[\\/]$/.test(value) ? value : `${value}${path.sep}`;
}

function ensureParityCliBuilt(): void {
  if (cliBuilt) return;

  const build = spawnSync('dotnet', [
    'build',
    cliProjectPath,
    '-p:SkipFlowCanvasBuild=true',
    '-p:EnableWindowsTargeting=true',
    `-p:BaseOutputPath=${parityCliBaseOutputPath}`,
    '-clp:ErrorsOnly',
  ], {
    cwd: repoRoot,
    encoding: 'utf8',
    maxBuffer: 1024 * 1024 * 256,
  });

  if (build.error) {
    throw build.error;
  }

  if (build.status !== 0) {
    throw new Error(
      [
        `FlowCanvasParityCli build failed (exit ${build.status ?? 'unknown'}).`,
        build.stderr?.trim() || build.stdout?.trim() || '(no build output)',
      ].join('\n'),
    );
  }

  cliBuilt = true;
}

function runParityCli(args: string[], input?: string): string {
  ensureParityCliBuilt();

  const commandArgs = [
    'run',
    '--no-build',
    '--project',
    cliProjectPath,
    '-p:SkipFlowCanvasBuild=true',
    '-p:EnableWindowsTargeting=true',
    `-p:BaseOutputPath=${parityCliBaseOutputPath}`,
    '--',
    ...args,
  ];
  const result = spawnSync('dotnet', commandArgs, {
    cwd: repoRoot,
    encoding: 'utf8',
    input,
    maxBuffer: 1024 * 1024 * 256,
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(
      [
        `FlowCanvasParityCli failed (exit ${result.status ?? 'unknown'}).`,
        result.stderr?.trim() || '(no stderr)',
      ].join('\n'),
    );
  }

  return result.stdout.trim();
}

export function prepareParityCases(options?: { includeSyntheticBrowserCallback?: boolean }): PreparedParityPayload {
  const args = ['prepare-cases', qaPresetsPath];
  if (options?.includeSyntheticBrowserCallback) {
    args.push('--include-synthetic-browser-callback');
  }

  const raw = runParityCli(args);
  return JSON.parse(raw) as PreparedParityPayload;
}

export function evaluateParityCases(cases: ParityEvaluationInput[]): ParityEvaluationPayload {
  const payload = JSON.stringify({ cases });
  const raw = runParityCli(['evaluate-cases'], payload);
  return JSON.parse(raw) as ParityEvaluationPayload;
}
