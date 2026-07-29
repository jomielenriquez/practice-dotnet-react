import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Proxy API calls to the ASP.NET Core backend so the browser only ever
    // talks to one origin. This keeps CORS out of the picture entirely and
    // mirrors how a reverse proxy would sit in front of both in production.
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})
