import { svelte } from '@sveltejs/vite-plugin-svelte';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [svelte(), tailwindcss()],
  resolve: {
    alias: {
      '@hpd-research/hpd-agent-client': fileURLToPath(
        new URL(
          '../vendor/HPD-AI-Framework/typescript/hpd-agent-client/src/index.ts',
          import.meta.url,
        ),
      ),
    },
  },
  server: {
    host: '127.0.0.1',
    port: 4175,
    fs: {
      allow: ['..'],
    },
  },
});
