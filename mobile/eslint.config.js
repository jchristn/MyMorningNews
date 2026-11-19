// ESLint (flat config): Expo's rules for the app, plus the host-agnostic contract (eslint.shared.js).
const { defineConfig } = require('eslint/config');
const expoConfig = require('eslint-config-expo/flat');
const globals = require('globals');
const shared = require('./eslint.shared.js');

module.exports = defineConfig([
  expoConfig,
  {
    ignores: ['dist/*', 'ios/*', 'android/*', '.expo/*', 'coverage/*', 'node_modules/*', 'qa/*'],
  },
  {
    files: ['*.js', 'scripts/**/*.js'],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    files: ['**/__tests__/**/*.{js,jsx}', 'jest.setup.js'],
    languageOptions: { globals: { ...globals.jest, ...globals.node } },
  },
  {
    files: ['src/**/*.{js,jsx}'],
    rules: {
      'no-console': ['error', { allow: ['warn', 'error'] }],
      'import/no-unresolved': 'off',
    },
  },
  {
    files: ['src/**/*.{js,jsx}'],
    ignores: ['src/**/__tests__/**'],
    rules: shared.rules,
  },
]);
