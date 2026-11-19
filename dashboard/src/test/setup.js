import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';
import { initI18n } from '../i18n/index.js';

// Every test renders in English unless it switches locale itself.
await initI18n('en');

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});
