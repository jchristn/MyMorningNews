import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
      '@components': path.resolve(import.meta.dirname, './src/components'),
      '@views': path.resolve(import.meta.dirname, './src/views'),
      '@context': path.resolve(import.meta.dirname, './src/context'),
      '@utils': path.resolve(import.meta.dirname, './src/utils'),
      '@hooks': path.resolve(import.meta.dirname, './src/hooks')
    }
  },
  server: {
    host: '127.0.0.1',
    port: 12190,
    strictPort: true
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/react') || id.includes('node_modules/react-dom') || id.includes('node_modules/react-router')) return 'vendor';
          return undefined;
        }
      }
    }
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.js'],
    css: false
  }
});
