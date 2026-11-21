import { useEffect, useRef } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { useSetup } from '../context/SetupContext.jsx';
import { allowedBeforeSetup, findRoute, hubForRoute, hubTabs, tabLabelKey } from '../utils/navigation.js';

// The tab strip of the hub the current page belongs to, above the page. Each tab is a link to its page's own URL, so
// deep links, history, and middle-click keep working. Shown when the user can open two or more of the hub's pages.
// Arrow keys, Home, and End move between tabs; the strip scrolls sideways when it does not fit.
export default function HubTabs() {
  const { t } = useTranslation();
  const auth = useAuth();
  const setup = useSetup();
  const location = useLocation();
  const listRef = useRef(null);
  const route = findRoute(location.pathname);
  const hub = hubForRoute(route);
  const tabs = hubTabs(hub, auth);
  const setupPending = Boolean(setup && setup.status && !setup.status.ready);
  const currentKey = route ? route.key : null;

  // Keep the current tab in view on narrow screens.
  useEffect(() => {
    const list = listRef.current;
    if (!list) return;
    const current = list.querySelector('[aria-current="page"]');
    if (current && typeof current.scrollIntoView === 'function') current.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }, [currentKey]);

  if (!hub || tabs.length < 2) return null;

  function onKeyDown(event) {
    if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return;
    const links = Array.from(listRef.current ? listRef.current.querySelectorAll('a') : []);
    const index = links.indexOf(document.activeElement);
    if (index < 0) return;
    const step = document.documentElement.dir === 'rtl' ? -1 : 1;
    let next = index;
    if (event.key === 'ArrowRight') next = index + step;
    else if (event.key === 'ArrowLeft') next = index - step;
    else if (event.key === 'Home') next = 0;
    else next = links.length - 1;
    next = (next + links.length) % links.length;
    event.preventDefault();
    links[next].focus();
  }

  const hubLabel = t('nav.hubs.' + hub.key);
  return (
    <nav className="hub-tabs" aria-label={t('nav.hubTabs', { hub: hubLabel })} data-testid="hub-tabs">
      <ul ref={listRef} onKeyDown={onKeyDown}>
        {tabs.map((tab) => {
          const label = t(tabLabelKey(tab.key));
          const current = tab.key === currentKey;
          const blocked = setupPending && !allowedBeforeSetup(tab);
          const Icon = tab.icon;
          return (
            <li key={tab.key}>
              <Link
                to={'/dashboard/' + tab.path}
                className={'hub-tab' + (current ? ' active' : '') + (blocked ? ' hub-tab-blocked' : '')}
                aria-current={current ? 'page' : undefined}
                title={blocked ? t('setup.nav.blocked', { name: label }) : undefined}
              >
                <Icon size={14} aria-hidden="true" />
                <span>{label}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
