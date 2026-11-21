import { useEffect } from 'react';
import { X } from 'lucide-react';
import { Outlet, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { findRoute } from '../utils/navigation.js';
import Sidebar from './Sidebar.jsx';
import Topbar from './Topbar.jsx';
import ToastHost from './Toast.jsx';

// What the sign-in that opened this session needs to tell the user (a cancelled account deletion), until dismissed.
function SignInNotice() {
  const { t } = useTranslation();
  const { signInNotice, dismissSignInNotice } = useAuth();
  if (signInNotice !== 'deletionCancelled') return null;
  return (
    <div className="notice notice-info sign-in-notice" role="status" data-testid="deletion-cancelled">
      <span>
        <strong>{t('account.delete.cancelledTitle')}</strong> {t('account.delete.cancelledMessage')}
      </span>
      <button type="button" className="icon-button" onClick={dismissSignInNotice} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
        <X size={14} aria-hidden="true" />
      </button>
    </div>
  );
}

// Shell: persistent sidebar and topbar around a scrollable main workspace.
export default function Dashboard() {
  const { t } = useTranslation();
  const location = useLocation();
  const { sidebarCollapsed } = useApp();
  const route = findRoute(location.pathname);
  const title = route ? t('nav.items.' + route.key) : t('app.name');
  const summary = route ? t('routes.' + route.key + '.summary') : '';

  useEffect(() => {
    document.title = title + ' | ' + t('app.name');
  }, [title, t]);

  return (
    <div className={'app-shell' + (sidebarCollapsed ? ' sidebar-collapsed' : '')}>
      <a className="skip-link" href="#main">
        {t('nav.skip')}
      </a>
      <Sidebar />
      <div className="app-main">
        <Topbar title={title} summary={summary} />
        <main id="main" className="workspace" tabIndex={-1}>
          <SignInNotice />
          <Outlet />
        </main>
      </div>
      <ToastHost />
    </div>
  );
}
