import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup, configure } from '@testing-library/react';
import { initI18n } from '../i18n/index.js';

// Lazy route modules can take several seconds to load on shared CI runners.
configure({ asyncUtilTimeout: 5000 });

// Every test renders in English unless it switches locale itself.
await initI18n('en');

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});
