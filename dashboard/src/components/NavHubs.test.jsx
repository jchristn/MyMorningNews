import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppProvider } from '../context/AppContext.jsx';
import { NAV_HUBS, canAccess, hubTabs, hubTarget, routeByKey, visibleNav } from '../utils/navigation.js';
import HubTabs from './HubTabs.jsx';
import Sidebar from './Sidebar.jsx';

// The consolidated navigation: two standalone pages and eight hubs in four sections, each hub's pages as tabs.
const authValue = { isAdmin: true, isTenantAdmin: true };
vi.mock('../context/AuthContext.jsx', () => ({ useAuth: () => authValue }));
const setupValue = { status: { ready: true }, gated: false };
vi.mock('../context/SetupContext.jsx', () => ({ useSetup: () => setupValue }));

const ADMIN = { isAdmin: true, isTenantAdmin: true };
const MEMBER = { isAdmin: false, isTenantAdmin: false };

const SIDEBAR_ITEMS = [
  'Home',
  'Today',
  'Topics',
  'Research',
  'Briefings',
  'Insights',
  'Configuration',
  'Observability',
  'Administration',
  'Settings'
];
const SECTION_HEADINGS = ['Research', 'Briefings', 'Configuration', 'System'];

const HUB_NAMES = {
  topics: 'Topics',
  research: 'Research',
  briefings: 'Briefings',
  insights: 'Insights',
  configuration: 'Configuration',
  observability: 'Observability',
  administration: 'Administration',
  settings: 'Settings'
};

const ADMIN_TABS = {
  topics: ['Topics', 'One-time requests'],
  research: ['Runs', 'Findings', 'Sources', 'Predictions', 'Suggestions'],
  briefings: ['Briefings', 'Listen', 'Editions', 'Templates'],
  insights: ['Overview', 'Relevance', 'Accuracy', 'Feedback', 'Email', 'Preferences', 'Usage'],
  configuration: ['Models', 'Prompts', 'Search providers', 'MCP servers', 'Connect an assistant', 'Voices', 'Email', 'Stored credentials'],
  observability: ['Health', 'Requests', 'Audit', 'API explorer'],
  administration: ['Users', 'Tenants', 'Access keys', 'Roles', 'Usage', 'Plans', 'Promo codes'],
  settings: ['Profile', 'Notifications', 'Server settings', 'Server info', 'Setup']
};

const MEMBER_TABS = {
  ...ADMIN_TABS,
  configuration: ['Models', 'Prompts', 'Search providers', 'Connect an assistant', 'Voices', 'Email', 'Stored credentials'],
  observability: ['Health', 'API explorer'],
  administration: ['Users'],
  settings: ['Profile', 'Notifications', 'Server info']
};

function renderShell(path) {
  return render(
    <AppProvider>
      <MemoryRouter initialEntries={[path]}>
        <Sidebar />
        <HubTabs />
      </MemoryRouter>
    </AppProvider>
  );
}

function mainNav() {
  return screen.getByRole('navigation', { name: 'Main navigation' });
}

function sidebarItems() {
  return within(mainNav())
    .getAllByRole('link')
    .map((link) => link.getAttribute('aria-label') || link.textContent);
}

function sidebarLink(name) {
  return within(mainNav()).getByRole('link', { name });
}

function tabNames(hubName) {
  return within(screen.getByRole('navigation', { name: hubName + ' pages' }))
    .getAllByRole('link')
    .map((link) => link.textContent);
}

beforeEach(() => {
  Object.assign(authValue, ADMIN);
  setupValue.status = { ready: true };
  setupValue.gated = false;
});

describe('sidebar hubs', () => {
  it('lists exactly the two standalone pages and eight hubs for an administrator, under four section headings', () => {
    renderShell('/dashboard/home');
    expect(sidebarItems()).toEqual(SIDEBAR_ITEMS);
    expect(
      within(mainNav())
        .getAllByRole('button')
        .map((button) => button.textContent)
    ).toEqual(SECTION_HEADINGS);
    expect(sidebarLink('Topics')).toHaveAttribute('href', '/dashboard/topics');
    expect(sidebarLink('Research')).toHaveAttribute('href', '/dashboard/runs');
    expect(sidebarLink('Insights')).toHaveAttribute('href', '/dashboard/insights/overview');
    expect(sidebarLink('Observability')).toHaveAttribute('href', '/dashboard/health');
    expect(sidebarLink('Administration')).toHaveAttribute('href', '/dashboard/admin/users');
    // Settings opens on Profile, never on Setup, and a ready workspace shows no setup callout.
    expect(sidebarLink('Settings')).toHaveAttribute('href', '/dashboard/profile');
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
    expect(sidebarLink('Home')).toHaveAttribute('aria-current', 'page');
  });

  it('orders the Settings hub Profile, Server settings, Server info, Setup', () => {
    expect(NAV_HUBS.find((hub) => hub.key === 'settings').tabs).toEqual(['profile', 'notifications', 'settings', 'serverInfo', 'setup']);
    expect(hubTarget(NAV_HUBS.find((hub) => hub.key === 'settings'), ADMIN).key).toBe('profile');
    expect(hubTarget(NAV_HUBS.find((hub) => hub.key === 'settings'), ADMIN, true).key).toBe('profile');
    renderShell('/dashboard/setup');
    expect(tabNames('Settings')).toEqual(['Profile', 'Notifications', 'Server settings', 'Server info', 'Setup']);
  });

  it('keeps every hub for a member, each opening the first page a member can use', () => {
    Object.assign(authValue, MEMBER);
    renderShell('/dashboard/home');
    expect(sidebarItems()).toEqual(SIDEBAR_ITEMS);
    expect(sidebarLink('Administration')).toHaveAttribute('href', '/dashboard/admin/users');
    expect(sidebarLink('Settings')).toHaveAttribute('href', '/dashboard/profile');
  });

  it('gives a member fewer tabs and leaves out the pages only administrators can open', () => {
    expect(visibleNav(MEMBER).sections.flatMap((section) => section.hubs.map((hub) => hub.key))).toEqual(NAV_HUBS.map((hub) => hub.key));
    expect(
      hubTabs(
        NAV_HUBS.find((hub) => hub.key === 'administration'),
        MEMBER
      ).map((route) => route.key)
    ).toEqual(['users']);
    const total = (auth) => NAV_HUBS.reduce((sum, hub) => sum + hubTabs(hub, auth).length, 0);
    expect(total(ADMIN)).toBe(42);
    expect(total(MEMBER)).toBe(31);
  });

  it('hides a hub the user can open none of the pages of', () => {
    const adminOnly = { key: 'adminOnly', tabs: ['settings', 'adminUsage', 'plans'] };
    expect(hubTabs(adminOnly, MEMBER)).toEqual([]);
    expect(hubTarget(adminOnly, MEMBER)).toBeNull();
    expect(hubTarget(adminOnly, ADMIN).key).toBe('settings');
  });

  it('places every dashboard route in the sidebar exactly once, as a standalone page or a hub tab', () => {
    const keys = ['home', 'today', ...NAV_HUBS.flatMap((hub) => hub.tabs)];
    expect(new Set(keys).size).toBe(keys.length);
    expect(keys.every((key) => routeByKey(key))).toBe(true);
    expect(keys).toHaveLength(44);
  });

  it('shows icons only when collapsed, with each label as the name and tooltip', () => {
    window.localStorage.setItem('mymorningnews.sidebarCollapsed', 'true');
    renderShell('/dashboard/findings');
    const links = within(mainNav()).getAllByRole('link');
    expect(links.map((link) => link.getAttribute('aria-label'))).toEqual(SIDEBAR_ITEMS);
    expect(links.map((link) => link.getAttribute('title'))).toEqual(SIDEBAR_ITEMS);
    expect(links.every((link) => link.textContent === '')).toBe(true);
    expect(within(mainNav()).queryAllByRole('button')).toHaveLength(0);
    expect(within(mainNav()).getAllByRole('separator')).toHaveLength(4);
    expect(sidebarLink('Research')).toHaveClass('active');
  });

  it('collapses a section, remembers it, and forgets sections that no longer exist', async () => {
    window.localStorage.setItem('mymorningnews.nav.closedGroups', JSON.stringify(['observability', 'research', 'start']));
    renderShell('/dashboard/home');
    expect(within(mainNav()).queryByRole('link', { name: 'Topics' })).not.toBeInTheDocument();
    expect(within(mainNav()).getByRole('button', { name: 'Research' })).toHaveAttribute('aria-expanded', 'false');
    await waitFor(() => expect(JSON.parse(window.localStorage.getItem('mymorningnews.nav.closedGroups'))).toEqual(['research']));
    await userEvent.click(within(mainNav()).getByRole('button', { name: 'System' }));
    expect(within(mainNav()).queryByRole('link', { name: 'Settings' })).not.toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('mymorningnews.nav.closedGroups'))).toEqual(['research', 'system']);
  });
});

describe('hub tab strips', () => {
  for (const [label, auth, expected] of [
    ['an administrator', ADMIN, ADMIN_TABS],
    ['a member', MEMBER, MEMBER_TABS]
  ]) {
    it('renders each hub tab strip on every page ' + label + ' can open, with the current tab marked', () => {
      Object.assign(authValue, auth);
      for (const hub of NAV_HUBS) {
        for (const key of hub.tabs) {
          const route = routeByKey(key);
          if (!canAccess(route, auth)) continue;
          const { unmount } = renderShell('/dashboard/' + route.path);
          const hubName = HUB_NAMES[hub.key];
          expect(sidebarLink(hubName)).toHaveClass('active');
          if (expected[hub.key].length < 2) {
            expect(screen.queryByTestId('hub-tabs')).not.toBeInTheDocument();
          } else {
            expect(tabNames(hubName)).toEqual(expected[hub.key]);
            const strip = screen.getByRole('navigation', { name: hubName + ' pages' });
            const current = within(strip)
              .getAllByRole('link')
              .filter((link) => link.getAttribute('aria-current') === 'page');
            expect(current).toHaveLength(1);
            expect(current[0]).toHaveAttribute('href', '/dashboard/' + route.path);
          }
          unmount();
        }
      }
    });
  }

  it('shows no tab strip on a standalone page', () => {
    renderShell('/dashboard/today');
    expect(screen.queryByTestId('hub-tabs')).not.toBeInTheDocument();
    expect(sidebarLink('Today')).toHaveClass('active');
  });

  it('keeps the hub active and its tab current on a detail route', () => {
    renderShell('/dashboard/topics/top_1');
    expect(sidebarLink('Topics')).toHaveClass('active');
    expect(sidebarLink('Topics')).toHaveAttribute('aria-current', 'true');
    expect(within(screen.getByRole('navigation', { name: 'Topics pages' })).getByRole('link', { name: 'Topics' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('keeps Briefings active on a briefing and Research active on a filtered page', () => {
    const { unmount } = renderShell('/dashboard/briefings/brf_1');
    expect(sidebarLink('Briefings')).toHaveClass('active');
    expect(within(screen.getByRole('navigation', { name: 'Briefings pages' })).getByRole('link', { name: 'Briefings' })).toHaveAttribute(
      'aria-current',
      'page'
    );
    unmount();
    renderShell('/dashboard/sources?runId=run_1');
    expect(sidebarLink('Research')).toHaveClass('active');
    expect(within(screen.getByRole('navigation', { name: 'Research pages' })).getByRole('link', { name: 'Sources' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('moves between tabs with the arrow keys, Home, and End', async () => {
    renderShell('/dashboard/runs');
    const strip = screen.getByRole('navigation', { name: 'Research pages' });
    const link = (name) => within(strip).getByRole('link', { name });
    link('Runs').focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(link('Findings')).toHaveFocus();
    await userEvent.keyboard('{End}');
    expect(link('Suggestions')).toHaveFocus();
    await userEvent.keyboard('{ArrowRight}');
    expect(link('Runs')).toHaveFocus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(link('Suggestions')).toHaveFocus();
    await userEvent.keyboard('{Home}');
    expect(link('Runs')).toHaveFocus();
  });
});

describe('setup gate in the hub navigation', () => {
  beforeEach(() => {
    setupValue.status = { ready: false };
    setupValue.gated = true;
  });

  it('dims the hubs with no page open before setup, and sends the others to a page that is', () => {
    renderShell('/dashboard/setup');
    expect(screen.getByTestId('setup-callout')).toHaveAttribute('href', '/dashboard/setup');
    for (const name of ['Home', 'Today', 'Topics', 'Research', 'Briefings', 'Insights', 'Observability', 'Administration']) {
      expect(sidebarLink(name)).toHaveClass('nav-item-blocked');
      expect(sidebarLink(name)).toHaveAttribute('title', name + ' opens after setup is finished');
    }
    expect(sidebarLink('Configuration')).not.toHaveClass('nav-item-blocked');
    expect(sidebarLink('Configuration')).toHaveAttribute('href', '/dashboard/models');
    expect(sidebarLink('Settings')).not.toHaveClass('nav-item-blocked');
    expect(sidebarLink('Settings')).toHaveClass('active');
    expect(sidebarLink('Settings')).toHaveAttribute('href', '/dashboard/profile');
  });

  it('keeps the callout and dims nothing after setup was skipped', () => {
    setupValue.gated = false;
    renderShell('/dashboard/topics');
    expect(screen.getByTestId('setup-callout')).toHaveAttribute('href', '/dashboard/setup');
    for (const name of ['Home', 'Today', 'Topics', 'Research', 'Briefings', 'Insights', 'Observability', 'Administration', 'Configuration', 'Settings']) {
      expect(sidebarLink(name)).not.toHaveClass('nav-item-blocked');
    }
    expect(sidebarLink('Topics')).toHaveAttribute('href', '/dashboard/topics');
    const strip = screen.getByRole('navigation', { name: 'Topics pages' });
    expect(within(strip).getByRole('link', { name: 'One-time requests' })).not.toHaveClass('hub-tab-blocked');
  });

  it('shows no callout to a member, who cannot open Setup', () => {
    Object.assign(authValue, MEMBER);
    setupValue.gated = false;
    renderShell('/dashboard/home');
    expect(screen.queryByTestId('setup-callout')).not.toBeInTheDocument();
  });

  it('dims the blocked tabs with the same tooltip', () => {
    renderShell('/dashboard/models');
    const strip = screen.getByRole('navigation', { name: 'Configuration pages' });
    const prompts = within(strip).getByRole('link', { name: 'Prompts' });
    expect(prompts).toHaveClass('hub-tab-blocked');
    expect(prompts).toHaveAttribute('title', 'Prompts opens after setup is finished');
    for (const name of ['Models', 'Search providers', 'Voices', 'Stored credentials']) {
      expect(within(strip).getByRole('link', { name })).not.toHaveClass('hub-tab-blocked');
    }
  });

  it('opens the first open tab for a member who reaches a hub before setup is finished', () => {
    Object.assign(authValue, MEMBER);
    renderShell('/dashboard/profile');
    expect(sidebarLink('Settings')).toHaveAttribute('href', '/dashboard/profile');
    expect(sidebarLink('Configuration')).toHaveAttribute('href', '/dashboard/models');
  });
});
