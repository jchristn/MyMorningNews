import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { TOPIC } from '../test/researchFixtures.js';
import { setActiveLocale } from './index.js';
import ar from './locales/ar.json';
import en from './locales/en.json';

// Renders the key views right to left, in Arabic and in the RTL pseudo-locale: the shell and navigation, a table
// with a row action menu, a form, and Insights. Each must render without crashing, with dir="rtl" on the document,
// with every placeholder interpolated, and (in Arabic) without English navigation text.

const ANALYTICS = {
  overview: { activeTopics: 2, runs: 4, runsCompleted: 3, runsPartiallySucceeded: 1, runsFailed: 0, findingsShown: 12, findingsRated: 3, relevancePrecision: 0.5, modelCalls: 9, modelCostUsd: 1.25 }
};

const VIEWS = [
  {
    name: 'shell and navigation',
    path: '/dashboard/home',
    overrides: {},
    ready: async () => screen.findAllByRole('navigation')
  },
  {
    name: 'topics table',
    path: '/dashboard/topics',
    overrides: { listTopics: vi.fn(async () => page([TOPIC, { ...TOPIC, id: 'top_2', name: 'Heat pumps', status: 'Paused' }])) },
    ready: async () => {
      const row = (await screen.findByText(TOPIC.name, { selector: 'strong' })).closest('tr');
      await userEvent.click(within(row).getAllByRole('button').at(-1));
      return screen.findAllByRole('menuitem');
    }
  },
  {
    name: 'profile form',
    path: '/dashboard/profile',
    overrides: { getMe: vi.fn(async () => ({ id: 'usr_admin', email: 'admin@mymorningnews', displayName: 'Admin' })) },
    ready: async () => screen.findAllByRole('textbox')
  },
  {
    name: 'Insights overview',
    path: '/dashboard/insights/overview',
    overrides: { getAnalytics: vi.fn(async (report) => ANALYTICS[report] || {}), listTopics: vi.fn(async () => page([TOPIC])) },
    ready: async () => screen.findByTestId('overview-tiles')
  }
];

const ENGLISH_NAV = [en.nav.items.home, en.nav.items.topics, en.nav.groups.administration, en.nav.items.settings];

afterEach(async () => {
  await setActiveLocale('en');
  window.localStorage.clear();
});

describe.each(['ar', 'qps-rtl'])('right-to-left rendering in %s', (locale) => {
  it.each(VIEWS)('renders the $name', async (view) => {
    await setActiveLocale(locale);
    renderRoute(view.path, view.overrides);
    await view.ready();
    expect(document.documentElement.dir).toBe('rtl');
    expect(document.body.textContent).not.toMatch(/\{[a-zA-Z]+(,[^}]*)?\}/);
    if (locale === 'ar') {
      expect(document.documentElement.lang).toBe('ar');
      expect(screen.getAllByText(ar.nav.items.home).length).toBeGreaterThan(0);
      const text = document.body.textContent;
      expect(ENGLISH_NAV.filter((s) => new RegExp('(^|[^\\p{L}])' + s + '($|[^\\p{L}])', 'u').test(text))).toEqual([]);
    }
  });
});

describe('stylesheets', () => {
  // Physical left and right break mirroring. Logical properties (margin-inline-start, inset-inline-end, text-align:
  // start) flip on their own; the few physical values that remain carry a [dir='rtl'] override.
  const stylesDir = join(import.meta.dirname, '..', 'styles');
  const files = [join(import.meta.dirname, '..', 'index.css'), ...readdirSync(stylesDir).map((f) => join(stylesDir, f))];
  const PHYSICAL = /(?:^|[;{\s])(?:(?:margin|padding|border)-(?:left|right)(?:-[a-z]+)?|left|right|float|clear)\s*:|text-align\s*:\s*(?:left|right)/m;

  it.each(files.map((f) => [f.split('/').slice(-2).join('/'), f]))('%s uses logical direction properties', (_, file) => {
    const css = readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
    const offending = css.split('\n').filter((line) => PHYSICAL.test(line));
    expect(offending).toEqual([]);
  });

  it('gives each physical inset shadow a right-to-left counterpart', () => {
    const css = files.map((f) => readFileSync(f, 'utf8')).join('\n');
    const blocks = [...css.matchAll(/([^{}]+)\{[^}]*box-shadow:\s*inset\s+\d+px\s+0\s+0[^}]*\}/g)].map((m) => m[1].trim());
    for (const selector of blocks.filter((s) => !s.startsWith('[dir'))) {
      expect([selector, css.includes("[dir='rtl'] " + selector.split(',')[0].trim())]).toEqual([selector, true]);
    }
  });
});
