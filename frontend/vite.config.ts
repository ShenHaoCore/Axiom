import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// 开发期由 Vite 代理 /bff，保证 Cookie 与回调落在同一个主机名上。
export default defineConfig({
  base: '/app/',
  plugins: [react()],
  server: {
    port: 3003,
    proxy: {
      '/bff': {
        target: 'http://localhost:5200',
        changeOrigin: false
      }
    }
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true
  }
});
