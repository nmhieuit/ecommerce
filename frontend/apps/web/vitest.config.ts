import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/setup.ts'],
    // Playwright owns e2e/; Vitest must not try to run those specs.
    include: ['tests/**/*.test.{ts,tsx}'],
    css: false,
    // lcov is what SonarQube's JS/TS analyzer consumes (sonar-project.properties,
    // spec 012-sonarqube-quality-gate). Only emitted when the run passes --coverage.
    coverage: {
      provider: 'v8',
      reporter: ['text-summary', 'lcov'],
      reportsDirectory: './coverage',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/generated/**', 'src/**/*.d.ts'],
    },
  },
});
