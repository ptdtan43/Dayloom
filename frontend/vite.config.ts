import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // Same-origin in dev too (docs/DESIGN.md §0): the browser only talks to Vite, which forwards /api to ASP.NET Core.
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
})
