import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import PropTypes from 'prop-types';
import { AppProvider } from './context/AppContext.jsx';
import { AuthProvider } from './context/AuthContext.jsx';
import { ThemeProvider } from './context/ThemeContext.jsx';
import Dashboard from './components/Dashboard.jsx';
import LoadingState from './components/LoadingState.jsx';
import Login from './components/Login.jsx';
import PhasePlaceholder from './components/PhasePlaceholder.jsx';
import { PrivateRoute, PublicRoute, RequireAccess } from './components/ProtectedRoute.jsx';
import { ALL_ROUTES } from './utils/navigation.js';

const VIEWS = {
  home: lazy(() => import('./views/HomeView.jsx')),
  today: lazy(() => import('./views/TodayView.jsx')),
  briefings: lazy(() => import('./views/BriefingsView.jsx')),
  listen: lazy(() => import('./views/ListenView.jsx')),
  voices: lazy(() => import('./views/VoicesView.jsx')),
  editions: lazy(() => import('./views/EditionsView.jsx')),
  templates: lazy(() => import('./views/TemplatesView.jsx')),
  requests: lazy(() => import('./views/RequestHistoryView.jsx')),
  topics: lazy(() => import('./views/TopicsView.jsx')),
  oneTimeRequests: lazy(() => import('./views/ResearchRequestsView.jsx')),
  runs: lazy(() => import('./views/RunsView.jsx')),
  findings: lazy(() => import('./views/FindingsView.jsx')),
  sources: lazy(() => import('./views/SourcesView.jsx')),
  predictions: lazy(() => import('./views/PredictionsView.jsx')),
  suggestions: lazy(() => import('./views/SuggestionsView.jsx')),
  explorer: lazy(() => import('./views/ApiExplorerView.jsx')),
  health: lazy(() => import('./views/HealthView.jsx')),
  audit: lazy(() => import('./views/AuditView.jsx')),
  tenants: lazy(() => import('./views/TenantsView.jsx')),
  users: lazy(() => import('./views/UsersView.jsx')),
  credentials: lazy(() => import('./views/CredentialsView.jsx')),
  roles: lazy(() => import('./views/RolesView.jsx')),
  settings: lazy(() => import('./views/SettingsView.jsx')),
  serverInfo: lazy(() => import('./views/ServerInfoView.jsx')),
  profile: lazy(() => import('./views/ProfileView.jsx')),
  vault: lazy(() => import('./views/VaultView.jsx')),
  models: lazy(() => import('./views/ModelsView.jsx')),
  prompts: lazy(() => import('./views/PromptsView.jsx')),
  usage: lazy(() => import('./views/UsageView.jsx')),
  adminUsage: lazy(() => import('./views/AdminUsageView.jsx')),
  plans: lazy(() => import('./views/PlansView.jsx')),
  promoCodes: lazy(() => import('./views/PromoCodesView.jsx')),
  email: lazy(() => import('./views/EmailView.jsx')),
  mcp: lazy(() => import('./views/McpView.jsx')),
  insightsOverview: lazy(() => import('./views/insights/OverviewView.jsx')),
  emailInsights: lazy(() => import('./views/insights/EmailInsightsView.jsx')),
  relevance: lazy(() => import('./views/insights/RelevanceView.jsx')),
  accuracy: lazy(() => import('./views/insights/AccuracyView.jsx')),
  feedback: lazy(() => import('./views/insights/FeedbackView.jsx')),
  preferences: lazy(() => import('./views/insights/PreferencesView.jsx'))
};

function RouteElement({ route }) {
  const View = VIEWS[route.key];
  return (
    <RequireAccess item={route}>
      {route.phase || !View ? (
        <PhasePlaceholder routeKey={route.key} phase={route.phase || 12} />
      ) : (
        <Suspense fallback={<LoadingState />}>
          <View />
        </Suspense>
      )}
    </RequireAccess>
  );
}

RouteElement.propTypes = { route: PropTypes.object.isRequired };

export function AppRoutes() {
  return (
    <Routes>
      <Route
        path="/"
        element={
          <PublicRoute>
            <Login />
          </PublicRoute>
        }
      />
      <Route
        path="/dashboard"
        element={
          <PrivateRoute>
            <Dashboard />
          </PrivateRoute>
        }
      >
        <Route index element={<Navigate to="home" replace />} />
        {ALL_ROUTES.map((route) => (
          <Route key={route.path} path={route.path + '/*'} element={<RouteElement route={route} />} />
        ))}
        <Route path="insights" element={<Navigate to="/dashboard/insights/overview" replace />} />
        <Route path="request-history" element={<Navigate to="/dashboard/requests" replace />} />
        <Route path="api-explorer" element={<Navigate to="/dashboard/explorer" replace />} />
        <Route path="admin/assignments" element={<Navigate to="/dashboard/admin/roles" replace />} />
        <Route path="*" element={<Navigate to="home" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <AppProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </AppProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}
