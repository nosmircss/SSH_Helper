import type { PreparedParityCase } from './parityCli';
import { prepareParityCases } from './parityCli';

let cachedCasesWithSynthetic: PreparedParityCase[] | null = null;

function getAllPreparedCases(): PreparedParityCase[] {
  if (cachedCasesWithSynthetic) {
    return cachedCasesWithSynthetic;
  }

  const payload = prepareParityCases({ includeSyntheticBrowserCallback: true });
  cachedCasesWithSynthetic = payload.cases;
  return cachedCasesWithSynthetic;
}

export function getValidQaPresetParityCases(): PreparedParityCase[] {
  return getAllPreparedCases().filter(
    (item) => item.classification === 'valid' && !item.isSynthetic,
  );
}

export function getIntentionalInvalidQaPresetParityCases(): PreparedParityCase[] {
  return getAllPreparedCases().filter(
    (item) => item.classification === 'intentional-invalid' && !item.isSynthetic,
  );
}

export function getSyntheticBrowserCallbackParityCase(): PreparedParityCase {
  const syntheticCase = getAllPreparedCases().find((item) => item.isSynthetic);
  if (!syntheticCase) {
    throw new Error('Synthetic browser_callback parity case was not prepared.');
  }

  return syntheticCase;
}
