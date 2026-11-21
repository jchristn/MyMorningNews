import { useEffect } from 'react';
import PropTypes from 'prop-types';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { ChevronDown, ChevronRight, ClipboardCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { useSetup } from '../context/SetupContext.jsx';
import useLocalStorage from '../hooks/useLocalStorage.js';
import { SECTION_KEYS, allowedBeforeSetup, findRoute, hubBlockedBeforeSetup, hubForRoute, hubTarget, visibleNav } from '../utils/navigation.js';

export const APP_VERSION = '0.1.0-alpha';

// One sidebar row: an icon and a label, or only the icon (with the label as its name and tooltip) when collapsed.
function NavRow({ to, icon: Icon, label, active, current, blocked, collapsed, onNavigate }) {
  const { t } = useTranslation();
  return (
    <Link
      to={to}
      className={'nav-item' + (active ? ' active' : '') + (blocked ? ' nav-item-blocked' : '')}
      aria-current={current ? 'page' : active ? 'true' : undefined}
      title={blocked ? t('setup.nav.blocked', { name: label }) : collapsed ? label : undefined}
      aria-label={collapsed ? label : undefined}
      onClick={onNavigate}
    >
      <Icon size={16} aria-hidden="true" />
      {!collapsed ? <span className="nav-item-label">{label}</span> : null}
    </Link>
  );
}

NavRow.propTypes = {
  to: PropTypes.string.isRequired,
  icon: PropTypes.elementType.isRequired,
  label: PropTypes.string.isRequired,
  active: PropTypes.bool,
  current: PropTypes.bool,
  blocked: PropTypes.bool,
  collapsed: PropTypes.bool,
  onNavigate: PropTypes.func
};

export default function Sidebar() {
  const { t } = useTranslation();
  const auth = useAuth();
  const location = useLocation();
  const { sidebarCollapsed, mobileNavOpen, setMobileNavOpen } = useApp();
  const [closedGroups, setClosedGroups] = useLocalStorage('mymorningnews.nav.closedGroups', []);
  const setup = useSetup();
  // Setup comes first: until it is ready, a callout leads the navigation and the routes it blocks are dimmed.
  const setupPending = Boolean(setup && setup.status && !setup.status.ready);
  const calloutLabel = t('setup.nav.callout');
  const nav = visibleNav(auth);
  const currentRoute = findRoute(location.pathname);
  const currentHub = hubForRoute(currentRoute);
  const closeDrawer = () => setMobileNavOpen(false);

  // Sections were regrouped: forget collapsed sections that no longer exist.
  const stored = Array.isArray(closedGroups) ? closedGroups : [];
  const hasStale = !Array.isArray(closedGroups) || stored.some((key) => !SECTION_KEYS.includes(key));
  useEffect(() => {
    if (hasStale) setClosedGroups((list) => (Array.isArray(list) ? list.filter((key) => SECTION_KEYS.includes(key)) : []));
  }, [hasStale, setClosedGroups]);

  function toggleGroup(key) {
    setClosedGroups((list) => {
      const valid = Array.isArray(list) ? list : [];
      return valid.includes(key) ? valid.filter((k) => k !== key) : [...valid, key];
    });
  }

  return (
    <>
      {mobileNavOpen ? <div className="sidebar-scrim" onClick={closeDrawer} aria-hidden="true" /> : null}
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
              onClick={closeDrawer}
              data-testid="setup-callout"
            >
              <ClipboardCheck size={16} aria-hidden="true" />
              {!sidebarCollapsed ? <span className="nav-item-label">{calloutLabel}</span> : null}
            </NavLink>
          ) : null}
          <div className="nav-group nav-standalone">
            {nav.standalone.map((route) => {
              const active = Boolean(currentRoute && currentRoute.key === route.key);
              return (
                <NavRow
                  key={route.key}
                  to={'/dashboard/' + route.path}
                  icon={route.icon}
                  label={t('nav.items.' + route.key)}
                  active={active}
                  current={active}
                  blocked={setupPending && !allowedBeforeSetup(route)}
                  collapsed={sidebarCollapsed}
                  onNavigate={closeDrawer}
                />
              );
            })}
          </div>
          {nav.sections.map((section) => {
            const closed = stored.includes(section.key) && !sidebarCollapsed;
            return (
              <div className="nav-group" key={section.key}>
                {!sidebarCollapsed ? (
                  <button type="button" className="nav-group-label" onClick={() => toggleGroup(section.key)} aria-expanded={!closed}>
                    {closed ? <ChevronRight size={12} aria-hidden="true" /> : <ChevronDown size={12} aria-hidden="true" />}
                    {t('nav.sections.' + section.key)}
                  </button>
                ) : (
                  <div className="nav-group-divider" role="separator" />
                )}
                {!closed
                  ? section.hubs.map((hub) => {
                      const target = hubTarget(hub, auth, setupPending);
                      const to = '/dashboard/' + target.path;
                      const active = Boolean(currentHub && currentHub.key === hub.key);
                      return (
                        <NavRow
                          key={hub.key}
                          to={to}
                          icon={hub.icon}
                          label={t('nav.hubs.' + hub.key)}
                          active={active}
                          current={active && location.pathname === to}
                          blocked={setupPending && hubBlockedBeforeSetup(hub, auth)}
                          collapsed={sidebarCollapsed}
                          onNavigate={closeDrawer}
                        />
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
