import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  base: '/Sistema_Rastreamento_Alvos_Arduino/',
  plugins: [react()],
})
