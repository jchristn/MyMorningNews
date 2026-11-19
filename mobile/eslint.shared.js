// The sharing contract (sharing.config.js): shared modules, and every app module, are host agnostic. React Native has
// no DOM, so app code never reaches for browser globals either.
const BROWSER_GLOBALS = [
  'window',
  'document',
  'localStorage',
  'sessionStorage',
  'navigator',
  'location',
  'history',
  'HTMLElement',
  'MutationObserver',
];

module.exports = {
  rules: {
    'no-restricted-globals': [
      'error',
      ...BROWSER_GLOBALS.map((name) => ({
        name,
        message: name + ' is a browser global; shared and app modules must be host agnostic (see sharing.config.js).',
      })),
    ],
    'no-restricted-syntax': [
      'error',
      {
        selector: "MetaProperty[meta.name='import'][property.name='meta']",
        message: 'import.meta is bundler-specific.',
      },
    ],
  },
  sharedOnlyRules: {
    'no-restricted-imports': [
      'error',
      {
        paths: [
          { name: 'react-dom', message: 'Shared modules must not depend on react-dom.' },
          { name: 'react-router-dom', message: 'Shared modules must not depend on the dashboard router.' },
          { name: 'react', message: 'Shared modules are framework free.' },
          { name: 'react-native', message: 'Shared modules are framework free.' },
          { name: 'i18next', message: 'Shared catalogs are data; each host owns its i18n runtime.' },
        ],
        patterns: [{ group: ['react-dom/*'], message: 'Shared modules must not depend on react-dom.' }],
      },
    ],
  },
};
