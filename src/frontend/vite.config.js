import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '../..', '')

  if (mode === 'production' && !env.VITE_API_BASE_URL?.trim()) {
    throw new Error('VITE_API_BASE_URL must be configured for a production build.')
  }

  return {
    envDir: '../..',
    plugins: [vue()],
    test: { environment: 'jsdom', globals: true }
  }
})
