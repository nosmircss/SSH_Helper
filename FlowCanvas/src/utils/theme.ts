// FlowCanvas/src/utils/theme.ts
/** Token VALUES live in styles/tokens.css. This only flips the data-theme attribute,
 *  which is the future light/high-contrast swap point (Decision #4). */
export function applyTheme(theme: 'dark' | 'light'): void {
  document.documentElement.setAttribute('data-theme', theme);
}
