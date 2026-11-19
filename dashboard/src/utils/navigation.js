import {
  Activity,
  BookOpen,
  Building2,
  CalendarClock,
  ChartColumn,
  ChartPie,
  CirclePlay,
  Coins,
  Compass,
  Cpu,
  FileText,
  Globe,
  Heart,
  History,
  House,
  Info,
  Key,
  KeyRound,
  Lightbulb,
  ListChecks,
  Mail,
  MessageSquare,
  Mic,
  Newspaper,
  Plug,
  Search,
  Send,
  Settings,
  Shield,
  ShieldCheck,
  Target,
  Terminal,
  TrendingUp,
  User,
  Users,
  WandSparkles
} from 'lucide-react';

// Navigation model and route metadata. Every route under /dashboard is listed here; the shell builds the sidebar,
// titles, summaries, and placeholders from it. phase marks routes whose features arrive in a later plan phase.
// access: all (any signed-in user), tenantAdmin (tenant or global administrators), admin (global administrators).
export const NAV_GROUPS = [
  {
    key: 'start',
    items: [
      { path: 'home', key: 'home', icon: House, access: 'all' },
      { path: 'today', key: 'today', icon: Newspaper, access: 'all' }
    ]
  },
  {
    key: 'research',
    items: [
      { path: 'topics', key: 'topics', icon: Lightbulb, access: 'all' },
      { path: 'one-time', key: 'oneTimeRequests', icon: Send, access: 'all' },
      { path: 'runs', key: 'runs', icon: CirclePlay, access: 'all' },
      { path: 'findings', key: 'findings', icon: ListChecks, access: 'all' },
      { path: 'sources', key: 'sources', icon: Globe, access: 'all', phase: 6 },
      { path: 'predictions', key: 'predictions', icon: TrendingUp, access: 'all' },
      { path: 'suggestions', key: 'suggestions', icon: Compass, access: 'all' }
    ]
  },
  {
    key: 'briefings',
    items: [
      { path: 'briefings', key: 'briefings', icon: BookOpen, access: 'all' },
      { path: 'editions', key: 'editions', icon: CalendarClock, access: 'all' },
      { path: 'templates', key: 'templates', icon: FileText, access: 'all' }
    ]
  },
  {
    key: 'insights',
    items: [
      { path: 'insights/relevance', key: 'relevance', icon: Target, access: 'all', phase: 10 },
      { path: 'insights/accuracy', key: 'accuracy', icon: ChartColumn, access: 'all', phase: 10 },
      { path: 'insights/feedback', key: 'feedback', icon: MessageSquare, access: 'all', phase: 10 },
      { path: 'insights/preferences', key: 'preferences', icon: Heart, access: 'all', phase: 10 },
      { path: 'insights/usage', key: 'usage', icon: Coins, access: 'all' }
    ]
  },
  {
    key: 'configuration',
    items: [
      { path: 'models', key: 'models', icon: Cpu, access: 'all' },
      { path: 'prompts', key: 'prompts', icon: WandSparkles, access: 'all' },
      { path: 'credentials-vault', key: 'vault', icon: KeyRound, access: 'all' },
      { path: 'search-providers', key: 'searchProviders', icon: Search, access: 'all', phase: 5 },
      { path: 'mcp-servers', key: 'mcpServers', icon: Plug, access: 'tenantAdmin', phase: 5 },
      { path: 'voices', key: 'voices', icon: Mic, access: 'all', phase: 8 },
      { path: 'email', key: 'email', icon: Mail, access: 'all', phase: 9 }
    ]
  },
  {
    key: 'observability',
    items: [
      { path: 'requests', key: 'requests', icon: History, access: 'tenantAdmin' },
      { path: 'explorer', key: 'explorer', icon: Terminal, access: 'all' },
      { path: 'health', key: 'health', icon: Activity, access: 'all' },
      { path: 'audit', key: 'audit', icon: ShieldCheck, access: 'tenantAdmin' }
    ]
  },
  {
    key: 'administration',
    items: [
      { path: 'admin/tenants', key: 'tenants', icon: Building2, access: 'tenantAdmin' },
      { path: 'admin/users', key: 'users', icon: Users, access: 'tenantAdmin' },
      { path: 'admin/credentials', key: 'credentials', icon: Key, access: 'tenantAdmin' },
      { path: 'admin/roles', key: 'roles', icon: Shield, access: 'tenantAdmin' },
      { path: 'admin/usage', key: 'adminUsage', icon: ChartPie, access: 'admin' }
    ]
  },
  {
    key: 'system',
    items: [
      { path: 'settings', key: 'settings', icon: Settings, access: 'admin' },
      { path: 'server-info', key: 'serverInfo', icon: Info, access: 'all' },
      { path: 'profile', key: 'profile', icon: User, access: 'all' }
    ]
  }
];

export const ALL_ROUTES = NAV_GROUPS.flatMap((g) => g.items.map((item) => ({ ...item, group: g.key })));

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
