import PropTypes from 'prop-types';
import { Navigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import LoadingState from './LoadingState.jsx';
import EmptyState from './EmptyState.jsx';
import { Lock } from 'lucide-react';
import { canAccess } from '../utils/navigation.js';

// Requires a session. While a stored session is being restored, shows a loading state so private content never flickers.
export function PrivateRoute({ children }) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();
  if (isLoading) return <LoadingState />;
  if (!isAuthenticated) return <Navigate to="/" replace state={{ from: location.pathname + location.search }} />;
  return children;
}

PrivateRoute.propTypes = { children: PropTypes.node };

// Sends signed-in users from the login page to the dashboard (or the page they were sent away from).
export function PublicRoute({ children }) {
  const { isAuthenticated, isLoading } = useAuth();
  const location = useLocation();
  if (isLoading) return <LoadingState />;
  if (isAuthenticated) {
    const from = location.state && location.state.from ? location.state.from : '/dashboard/home';
    return <Navigate to={from} replace />;
  }
  return children;
}

PublicRoute.propTypes = { children: PropTypes.node };

// Role gate for a route. The server still enforces every permission; this only hides what the user cannot use.
export function RequireAccess({ item, children }) {
  const auth = useAuth();
  const { t } = useTranslation();
  if (!canAccess(item, auth)) return <EmptyState icon={Lock} title={t('access.deniedTitle')} message={t('access.deniedMessage')} />;
  return children;
}

RequireAccess.propTypes = { item: PropTypes.object, children: PropTypes.node };
