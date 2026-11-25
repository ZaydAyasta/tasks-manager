import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath } from 'node:url'
import { resolveApiBaseUrl } from './src/app/services/api-base-url.js'

const envDir = fileURLToPath(new URL('../../', import.meta.url))

export default defineConfig(({ command, mode }) => {
  if (command === 'build') {
    resolveApiBaseUrl(loadEnv(mode, envDir).VITE_API_BASE_URL, true)
  }

  return {
    envDir,
    plugins: [vue({ template: { compilerOptions: { isCustomElement: tag => tag === 'search' } } })],
    test: { environment: 'jsdom', globals: true }
  }
})
