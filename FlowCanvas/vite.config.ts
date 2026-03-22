import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    // Disable crossorigin attributes — they break virtual host mapping in WebView2
    modulePreload: { polyfill: false },
    rollupOptions: {
      output: {
        manualChunks: undefined,
      },
    },
  },
  // Prevent crossorigin attribute on script/link tags
  html: {
    cspNonce: undefined,
  },
});
