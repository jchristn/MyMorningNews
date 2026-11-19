import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../App.jsx';
import { AppProvider } from '../context/AppContext.jsx';
import { AuthProvider, STORAGE_KEYS } from '../context/AuthContext.jsx';
import { ThemeProvider } from '../context/ThemeContext.jsx';
import { fakeClient } from '../test/fakeClient.js';
import { setActiveLocale } from './index.js';
import en from './locales/en.json';

// Renders real pages in the pseudo-locales. Any English UI text that survives is hard-coded.
function renderAt(path, signedIn) {
  if (signedIn) window.localStorage.setItem(STORAGE_KEYS.token, 'tok_1');
  return render(
    <ThemeProvider>
      <AuthProvider clientFactory={() => fakeClient()}>
        <AppProvider>
          <MemoryRouter initialEntries={[path]}>
            <AppRoutes />
          </MemoryRouter>
        </AppProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}

const ENGLISH_UI = [en.login.signIn, en.login.email, en.login.serverUrl, en.nav.items.home, en.nav.groups.administration, en.nav.items.settings];

function leakedEnglish() {
  const text = document.body.textContent;
  return ENGLISH_UI.filter((s) => new RegExp('(^|[^\\p{L}])' + s + '($|[^\\p{L}])', 'u').test(text));
}

afterEach(async () => {
  await setActiveLocale('en');
});

describe('pseudo-locale smoke', () => {
  it('renders the login page fully transformed in qps-ploc', async () => {
    await setActiveLocale('qps-ploc');
    renderAt('/', false);
    expect(await screen.findByRole('button', { name: /\[Şīɠƞ īƞ/ })).toBeInTheDocument();
    expect(leakedEnglish()).toEqual([]);
  });

  it('renders the dashboard shell fully transformed in qps-ploc', async () => {
    await setActiveLocale('qps-ploc');
    renderAt('/dashboard/home', true);
    await screen.findAllByRole('navigation');
    expect(leakedEnglish()).toEqual([]);
  });

  it('renders right to left in qps-rtl', async () => {
    await setActiveLocale('qps-rtl');
    renderAt('/', false);
    await screen.findAllByRole('button');
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.body.textContent).toContain('‫');
  });

  it('interpolates placeholders in both pseudo-locales', async () => {
    for (const locale of ['qps-ploc', 'qps-rtl']) {
      await setActiveLocale(locale);
      const { unmount } = renderAt('/dashboard/admin/users', true);
      await screen.findAllByRole('navigation');
      expect(document.body.textContent).not.toMatch(/\{[a-zA-Z]+\}/);
      unmount();
      window.localStorage.clear();
    }
  });
});
