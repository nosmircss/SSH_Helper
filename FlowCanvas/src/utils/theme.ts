export interface ThemeColors {
  canvasBg: string;
  panelBg: string;
  panelBorder: string;
  headerBg: string;
  inputBg: string;
  text: string;
  textSecondary: string;
  textMuted: string;
}

export const themes: Record<'dark' | 'light', ThemeColors> = {
  dark: {
    canvasBg: '#1a1a2e',
    panelBg: '#12122a',
    panelBorder: '#2a2a4a',
    headerBg: '#16162a',
    inputBg: '#0d1117',
    text: '#ccc',
    textSecondary: '#888',
    textMuted: '#555',
  },
  light: {
    canvasBg: '#f5f5f8',
    panelBg: '#ffffff',
    panelBorder: '#e0e0e8',
    headerBg: '#f0f0f5',
    inputBg: '#ffffff',
    text: '#333',
    textSecondary: '#666',
    textMuted: '#999',
  },
};

/**
 * Applies the given theme by setting CSS custom properties on the document root
 * and updating the `data-theme` attribute.
 */
export function applyTheme(theme: 'dark' | 'light'): void {
  const t = themes[theme];
  const root = document.documentElement;

  Object.entries(t).forEach(([key, value]) => {
    const cssVar = `--fc-${key.replace(/[A-Z]/g, (m) => '-' + m.toLowerCase())}`;
    root.style.setProperty(cssVar, value);
  });

  root.setAttribute('data-theme', theme);
}
