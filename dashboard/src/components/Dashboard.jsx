import { useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { findRoute } from '../utils/navigation.js';
import Sidebar from './Sidebar.jsx';
import Topbar from './Topbar.jsx';
import ToastHost from './Toast.jsx';

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
          <Outlet />
        </main>
      </div>
      <ToastHost />
    </div>
  );
}
