// Lints the dashboard modules this app shares (sharing.config.js) against the sharing contract only: no browser
// globals, no import.meta, no framework imports. Run from this folder: npm run lint:shared. While sharedDirs is empty
// there is nothing to lint and the command reports that.
const path = require('path');
const { defineConfig } = require('eslint/config');
const shared = require('./eslint.shared.js');
const { sharedDirs } = require('./sharing.config');

const base = path.join('dashboard', 'src', 'shared');

module.exports = defineConfig([
  {
    basePath: base,
    files: sharedDirs.map((d) => `${d}/**/*.{js,jsx}`),
    ignores: ['**/*.test.js', '**/*.test.jsx'],
    languageOptions: { ecmaVersion: 'latest', sourceType: 'module' },
    linterOptions: { reportUnusedDisableDirectives: 'off' },
    rules: { ...shared.rules, ...shared.sharedOnlyRules },
  },
]);
