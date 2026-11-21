import {
  Activity,
  BookOpen,
  Bot,
  Building2,
  CalendarClock,
  ChartColumn,
  ChartLine,
  ChartPie,
  ClipboardCheck,
  CirclePlay,
  Coins,
  Compass,
  Cpu,
  FileText,
  Gift,
  Globe,
  Headphones,
  Heart,
  History,
  House,
  Info,
  Key,
  KeyRound,
  LayoutGrid,
  Lightbulb,
  ListChecks,
  Mail,
  MailCheck,
  MessageSquare,
  Mic,
  Microscope,
  Newspaper,
  Plug,
  Search,
  Send,
  Settings,
  Shield,
  ShieldCheck,
  SlidersHorizontal,
  Target,
  Terminal,
  TicketPercent,
  TrendingUp,
  User,
  UserCog,
  Users,
  WandSparkles
} from 'lucide-react';

// Navigation model and route metadata. Every route under /dashboard is listed in ALL_ROUTES; the shell builds the
// sidebar, the hub tab strips, titles, and summaries from it. Every page keeps its own URL: a hub only groups pages
// for the sidebar and draws their tab strip, so email links and bookmarks keep working.
// access: all (any signed-in user), tenantAdmin (tenant or global administrators), admin (global administrators).
export const ALL_ROUTES = [
  { path: 'home', key: 'home', icon: House, access: 'all' },
  { path: 'today', key: 'today', icon: Newspaper, access: 'all' },

  { path: 'topics', key: 'topics', icon: Lightbulb, access: 'all' },
  { path: 'one-time', key: 'oneTimeRequests', icon: Send, access: 'all' },
  { path: 'runs', key: 'runs', icon: CirclePlay, access: 'all' },
  { path: 'findings', key: 'findings', icon: ListChecks, access: 'all' },
  { path: 'sources', key: 'sources', icon: Globe, access: 'all' },
  { path: 'predictions', key: 'predictions', icon: TrendingUp, access: 'all' },
  { path: 'suggestions', key: 'suggestions', icon: Compass, access: 'all' },

  { path: 'briefings', key: 'briefings', icon: BookOpen, access: 'all' },
  { path: 'listen', key: 'listen', icon: Headphones, access: 'all' },
  { path: 'editions', key: 'editions', icon: CalendarClock, access: 'all' },
  { path: 'templates', key: 'templates', icon: FileText, access: 'all' },

  { path: 'insights/overview', key: 'insightsOverview', icon: LayoutGrid, access: 'all' },
  { path: 'insights/relevance', key: 'relevance', icon: Target, access: 'all' },
  { path: 'insights/accuracy', key: 'accuracy', icon: ChartColumn, access: 'all' },
  { path: 'insights/feedback', key: 'feedback', icon: MessageSquare, access: 'all' },
  { path: 'insights/email', key: 'emailInsights', icon: MailCheck, access: 'all' },
  { path: 'insights/preferences', key: 'preferences', icon: Heart, access: 'all' },
  { path: 'insights/usage', key: 'usage', icon: Coins, access: 'all' },

  { path: 'models', key: 'models', icon: Cpu, access: 'all' },
  { path: 'prompts', key: 'prompts', icon: WandSparkles, access: 'all' },
  { path: 'search-providers', key: 'searchProviders', icon: Search, access: 'all' },
  { path: 'mcp-servers', key: 'mcpServers', icon: Plug, access: 'tenantAdmin' },
  { path: 'mcp', key: 'mcp', icon: Bot, access: 'all' },
  { path: 'voices', key: 'voices', icon: Mic, access: 'all' },
  { path: 'email', key: 'email', icon: Mail, access: 'all' },
  { path: 'credentials-vault', key: 'vault', icon: KeyRound, access: 'all' },

  { path: 'health', key: 'health', icon: Activity, access: 'all' },
  { path: 'requests', key: 'requests', icon: History, access: 'tenantAdmin' },
  { path: 'audit', key: 'audit', icon: ShieldCheck, access: 'tenantAdmin' },
  { path: 'explorer', key: 'explorer', icon: Terminal, access: 'all' },

  // Members see the user directory read-only; only administrators add, edit, or delete users.
  { path: 'admin/users', key: 'users', icon: Users, access: 'all' },
  { path: 'admin/tenants', key: 'tenants', icon: Building2, access: 'tenantAdmin' },
  { path: 'admin/credentials', key: 'credentials', icon: Key, access: 'tenantAdmin' },
  { path: 'admin/roles', key: 'roles', icon: Shield, access: 'tenantAdmin' },
  { path: 'admin/usage', key: 'adminUsage', icon: ChartPie, access: 'admin' },
  { path: 'admin/plans', key: 'plans', icon: Gift, access: 'admin' },
  { path: 'admin/promo-codes', key: 'promoCodes', icon: TicketPercent, access: 'admin' },

  { path: 'setup', key: 'setup', icon: ClipboardCheck, access: 'tenantAdmin' },
  { path: 'profile', key: 'profile', icon: User, access: 'all' },
  { path: 'settings', key: 'settings', icon: Settings, access: 'admin' },
  { path: 'server-info', key: 'serverInfo', icon: Info, access: 'all' }
];

const ROUTES_BY_KEY = new Map(ALL_ROUTES.map((route) => [route.key, route]));

export function routeByKey(key) {
  return ROUTES_BY_KEY.get(key) || null;
}

// Pages linked directly from the top of the sidebar, above every section.
export const NAV_STANDALONE = ['home', 'today'];

// Hubs: one sidebar item each, whose pages are the tabs of the hub's tab strip, in order. A hub links to its first
// tab the user can open, and is hidden when the user can open none of them.
export const NAV_HUBS = [
  { key: 'topics', icon: Lightbulb, tabs: ['topics', 'oneTimeRequests'] },
  { key: 'research', icon: Microscope, tabs: ['runs', 'findings', 'sources', 'predictions', 'suggestions'] },
  { key: 'briefings', icon: BookOpen, tabs: ['briefings', 'listen', 'editions', 'templates'] },
  { key: 'insights', icon: ChartLine, tabs: ['insightsOverview', 'relevance', 'accuracy', 'feedback', 'emailInsights', 'preferences', 'usage'] },
  { key: 'configuration', icon: SlidersHorizontal, tabs: ['models', 'prompts', 'searchProviders', 'mcpServers', 'mcp', 'voices', 'email', 'vault'] },
  { key: 'observability', icon: Activity, tabs: ['health', 'requests', 'audit', 'explorer'] },
  { key: 'administration', icon: UserCog, tabs: ['users', 'tenants', 'credentials', 'roles', 'adminUsage', 'plans', 'promoCodes'] },
  { key: 'settings', icon: Settings, tabs: ['setup', 'profile', 'settings', 'serverInfo'] }
];

// Labelled sidebar sections and the hubs under each.
export const NAV_SECTIONS = [
  { key: 'research', hubs: ['topics', 'research'] },
  { key: 'briefings', hubs: ['briefings', 'insights'] },
  { key: 'configuration', hubs: ['configuration'] },
  { key: 'system', hubs: ['observability', 'administration', 'settings'] }
];

export const SECTION_KEYS = NAV_SECTIONS.map((section) => section.key);

const HUBS_BY_KEY = new Map(NAV_HUBS.map((hub) => [hub.key, hub]));

export function hubByKey(key) {
  return HUBS_BY_KEY.get(key) || null;
}

// Tab labels that differ from the page title: shorter, or told apart from a tab of the same name in another hub.
// Every other tab uses its page title (nav.items.<key>).
const TAB_LABEL_KEYS = {
  requests: 'nav.tabs.requests',
  credentials: 'nav.tabs.credentials',
  roles: 'nav.tabs.roles',
  adminUsage: 'nav.tabs.adminUsage',
  promoCodes: 'nav.tabs.promoCodes',
  settings: 'nav.tabs.settings',
  emailInsights: 'nav.tabs.emailInsights',
  mcp: 'nav.tabs.mcp'
};

export function tabLabelKey(routeKey) {
  return TAB_LABEL_KEYS[routeKey] || 'nav.items.' + routeKey;
}

// While the tenant's setup is not ready, administrators can open only these routes: the Setup screen and the
// configuration it may send them to (model endpoints, the credential vault, search providers, settings), plus their
// profile. Every other route redirects to the Setup screen.
export const SETUP_ALLOWED_ROUTES = ['setup', 'models', 'vault', 'searchProviders', 'settings', 'profile'];

export function allowedBeforeSetup(item) {
  return Boolean(item) && SETUP_ALLOWED_ROUTES.includes(item.key);
}

export function canAccess(item, auth) {
  if (!item || item.access === 'all') return true;
  if (item.access === 'admin') return Boolean(auth && auth.isAdmin);
  return Boolean(auth && (auth.isAdmin || auth.isTenantAdmin));
}

export function findRoute(pathname) {
  const rest = String(pathname || '').replace(/^\/dashboard\/?/, '');
  let best = null;
  for (const route of ALL_ROUTES) {
    if (rest === route.path || rest.startsWith(route.path + '/')) {
      if (!best || route.path.length > best.path.length) best = route;
    }
  }
  return best;
}

// The hub a route belongs to, or null for a standalone page.
export function hubForRoute(route) {
  if (!route) return null;
  return NAV_HUBS.find((hub) => hub.tabs.includes(route.key)) || null;
}

// The hub's tabs (route entries) the user can open, in order.
export function hubTabs(hub, auth) {
  if (!hub) return [];
  return hub.tabs.map(routeByKey).filter((route) => route && canAccess(route, auth));
}

// Where the hub's sidebar item goes: its first tab the user can open, or, while setup is not finished, its first such
// tab that setup leaves open (so the link does not bounce to the Setup screen). Null when the user can open none.
export function hubTarget(hub, auth, setupPending = false) {
  const tabs = hubTabs(hub, auth);
  if (tabs.length === 0) return null;
  if (setupPending) {
    const open = tabs.find(allowedBeforeSetup);
    if (open) return open;
  }
  return tabs[0];
}

// True while setup is not finished and the hub has no tab the user can open before it is.
export function hubBlockedBeforeSetup(hub, auth) {
  return !hubTabs(hub, auth).some(allowedBeforeSetup);
}

// The sidebar for a user: the standalone pages, then each section with the hubs the user can open. Sections with no
// such hub are left out.
export function visibleNav(auth) {
  const standalone = NAV_STANDALONE.map(routeByKey).filter((route) => route && canAccess(route, auth));
  const sections = NAV_SECTIONS.map((section) => ({
    key: section.key,
    hubs: section.hubs.map(hubByKey).filter((hub) => hubTabs(hub, auth).length > 0)
  })).filter((section) => section.hubs.length > 0);
  return { standalone, sections };
}
