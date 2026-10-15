import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', '')

  return {
    plugins: [react(), tailwindcss()],
    server: {
      // Development only: forward /api to the backend so the app can call it
      // same-origin. Override the target with VITE_DEV_API_PROXY_TARGET.
      proxy: {
        '/api': {
          target: env.VITE_DEV_API_PROXY_TARGET || 'https://localhost:7280',
          changeOrigin: true,
          // The ASP.NET Core development certificate is self-signed.
          secure: false,
        },
      },
    },
  }
})
