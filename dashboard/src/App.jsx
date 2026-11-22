import { lazy, Suspense } from 'react';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import PropTypes from 'prop-types';
import { AppProvider } from './context/AppContext.jsx';
import { AuthProvider } from './context/AuthContext.jsx';
import { SetupProvider, useSetup } from './context/SetupContext.jsx';
import { ThemeProvider } from './context/ThemeContext.jsx';
import Dashboard from './components/Dashboard.jsx';
import LoadingState from './components/LoadingState.jsx';
import AccountDeletedPage from './components/auth/AccountDeletedPage.jsx';
import ForgotPasswordPage from './components/auth/ForgotPasswordPage.jsx';
import ResetPasswordPage from './components/auth/ResetPasswordPage.jsx';
import SignupPage from './components/auth/SignupPage.jsx';
import VerifyEmailPage from './components/auth/VerifyEmailPage.jsx';
import Login from './components/Login.jsx';
import { PrivateRoute, PublicRoute, RequireAccess } from './components/ProtectedRoute.jsx';
import SetupGate from './components/SetupGate.jsx';
import { ALL_ROUTES, allowedBeforeSetup } from './utils/navigation.js';

const VIEWS = {
  home: lazy(() => import('./views/HomeView.jsx')),
  setup: lazy(() => import('./views/SetupView.jsx')),
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
  mcpServers: lazy(() => import('./views/McpServersView.jsx')),
  searchProviders: lazy(() => import('./views/SearchProvidersView.jsx')),
  insightsOverview: lazy(() => import('./views/insights/OverviewView.jsx')),
  emailInsights: lazy(() => import('./views/insights/EmailInsightsView.jsx')),
  relevance: lazy(() => import('./views/insights/RelevanceView.jsx')),
  accuracy: lazy(() => import('./views/insights/AccuracyView.jsx')),
  feedback: lazy(() => import('./views/insights/FeedbackView.jsx')),
  preferences: lazy(() => import('./views/insights/PreferencesView.jsx'))
};

function RouteElement({ route }) {
  const View = VIEWS[route.key];
  const setup = useSetup();
  // First run: until every required check passes or the person skips setup, the product routes lead to the Setup
  // screen. A workspace that is ready, or whose setup was skipped, never redirects.
  if (setup && setup.gated && !allowedBeforeSetup(route)) return <Navigate to="/dashboard/setup" replace />;
  return (
    <RequireAccess item={route}>
      {View ? (
        <Suspense fallback={<LoadingState />}>
          <View />
        </Suspense>
      ) : null}
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
        path="/signup"
        element={
          <PublicRoute>
            <SignupPage />
          </PublicRoute>
        }
      />
      <Route
        path="/forgot-password"
        element={
          <PublicRoute>
            <ForgotPasswordPage />
          </PublicRoute>
        }
      />
      <Route path="/verify" element={<VerifyEmailPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/account-deleted" element={<AccountDeletedPage />} />
      <Route path="/login" element={<Navigate to="/" replace />} />
      <Route
        path="/dashboard"
        element={
          <PrivateRoute>
            <SetupProvider>
              <SetupGate>
                <Dashboard />
              </SetupGate>
            </SetupProvider>
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
