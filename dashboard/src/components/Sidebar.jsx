import { NavLink } from 'react-router-dom';
import { ChevronDown, ChevronRight, ClipboardCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { useSetup } from '../context/SetupContext.jsx';
import useLocalStorage from '../hooks/useLocalStorage.js';
import { NAV_GROUPS, allowedBeforeSetup, canAccess } from '../utils/navigation.js';

export const APP_VERSION = '0.1.0-alpha';

export default function Sidebar() {
  const { t } = useTranslation();
  const auth = useAuth();
  const { sidebarCollapsed, mobileNavOpen, setMobileNavOpen } = useApp();
  const [closedGroups, setClosedGroups] = useLocalStorage('mymorningnews.nav.closedGroups', []);
  const setup = useSetup();
  // Setup comes first: until it is ready, a callout leads the navigation and the routes it blocks are dimmed.
  const setupPending = Boolean(setup && setup.status && !setup.status.ready);
  const calloutLabel = t('setup.nav.callout');

  function toggleGroup(key) {
    setClosedGroups((list) => (list.includes(key) ? list.filter((k) => k !== key) : [...list, key]));
  }

  return (
    <>
      {mobileNavOpen ? <div className="sidebar-scrim" onClick={() => setMobileNavOpen(false)} aria-hidden="true" /> : null}
      <aside className={'sidebar' + (sidebarCollapsed ? ' collapsed' : '') + (mobileNavOpen ? ' mobile-open' : '')}>
        <div className="sidebar-brand">
          <img src="/assets/glyph.png" alt="" width="28" height="28" />
          {!sidebarCollapsed ? <span>{t('app.name')}</span> : null}
        </div>
        <nav className="sidebar-nav" aria-label={t('nav.label')}>
          {setupPending ? (
            <NavLink
              to="/dashboard/setup"
              className="nav-item nav-setup-callout"
              title={sidebarCollapsed ? calloutLabel : undefined}
              aria-label={sidebarCollapsed ? calloutLabel : undefined}
              onClick={() => setMobileNavOpen(false)}
              data-testid="setup-callout"
            >
              <ClipboardCheck size={16} aria-hidden="true" />
              {!sidebarCollapsed ? <span className="nav-item-label">{calloutLabel}</span> : null}
            </NavLink>
          ) : null}
          {NAV_GROUPS.map((group) => {
            const items = group.items.filter((item) => canAccess(item, auth));
            if (items.length === 0) return null;
            const closed = closedGroups.includes(group.key) && !sidebarCollapsed;
            return (
              <div className="nav-group" key={group.key}>
                {!sidebarCollapsed ? (
                  <button type="button" className="nav-group-label" onClick={() => toggleGroup(group.key)} aria-expanded={!closed}>
                    {closed ? <ChevronRight size={12} aria-hidden="true" /> : <ChevronDown size={12} aria-hidden="true" />}
                    {t('nav.groups.' + group.key)}
                  </button>
                ) : (
                  <div className="nav-group-divider" role="separator" />
                )}
                {!closed
                  ? items.map((item) => {
                      const label = t('nav.items.' + item.key);
                      const blocked = setupPending && !allowedBeforeSetup(item);
                      return (
                        <NavLink
                          key={item.path}
                          to={'/dashboard/' + item.path}
                          className={({ isActive }) => 'nav-item' + (isActive ? ' active' : '') + (blocked ? ' nav-item-blocked' : '')}
                          title={blocked ? t('setup.nav.blocked', { name: label }) : sidebarCollapsed ? label : undefined}
                          aria-label={sidebarCollapsed ? label : undefined}
                          onClick={() => setMobileNavOpen(false)}
                        >
                          <item.icon size={16} aria-hidden="true" />
                          {!sidebarCollapsed ? <span className="nav-item-label">{label}</span> : null}
                        </NavLink>
                      );
                    })
                  : null}
              </div>
            );
          })}
        </nav>
        <div className="sidebar-footer">
          {!sidebarCollapsed ? (
            <>
              <span className="muted small">{t('nav.version', { version: APP_VERSION })}</span>
              <span className="badge badge-info small">{t('nav.environment')}</span>
            </>
          ) : null}
        </div>
      </aside>
    </>
  );
}
