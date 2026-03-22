import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { viteSingleFile } from 'vite-plugin-singlefile';

export default defineConfig({
  plugins: [react(), viteSingleFile()],
  build: {
    outDir: 'dist',
    // Single-file: all JS and CSS inlined into index.html
    // This ensures WebView2 can load via NavigateToString with zero file dependencies
  },
});
