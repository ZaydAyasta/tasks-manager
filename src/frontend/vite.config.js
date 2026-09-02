import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
export default defineConfig({ envDir: '../..', plugins: [vue()], test: { environment: 'jsdom', globals: true } })
