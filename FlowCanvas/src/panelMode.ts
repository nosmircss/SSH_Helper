export type PanelMode = 'main' | 'runoutput';

/** Decides which React entry to render based on the URL query (?panel=runoutput). */
export function panelFromSearch(search: string): PanelMode {
  return new URLSearchParams(search).get('panel') === 'runoutput' ? 'runoutput' : 'main';
}
