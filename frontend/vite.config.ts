import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'

// PR 21 — Vite + React + alias @/ (espelha o que tsconfig.app.json define).
// Porta 4201 para não conflitar com qualit-front-v2 (4200) no mesmo workspace.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    host: '127.0.0.1',
    port: 4201,
    strictPort: true,
  },
})
