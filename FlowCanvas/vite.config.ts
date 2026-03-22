import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    // Single-page app: inline everything into one HTML file for easy embedding
    rollupOptions: {
      output: {
        manualChunks: undefined,
      },
    },
  },
});
