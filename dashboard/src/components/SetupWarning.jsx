import { TriangleAlert, X } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { pendingChecks, useSetup } from '../context/SetupContext.jsx';
import { affectedBySetup, findRoute } from '../utils/navigation.js';
import { checkTitle } from './SetupStateBadge.jsx';

// The names of the missing parts as one phrase in the reader's language ("Model: Anthropic and Search").
export function listPhrase(items, language) {
  try {
    return new Intl.ListFormat(language, { style: 'long', type: 'conjunction' }).format(items);
  } catch {
    return items.join(', ');
  }
}

// After someone leaves setup unfinished, the pages where that bites (topics, runs, briefings) say what is missing and
// what will not work, with the way to finish it. A request the server refused for the same reason shows its message
// on any page. Dismissed until the next refusal or page load; the sidebar callout stays either way.
export default function SetupWarning() {
  const { t, i18n } = useTranslation();
  const setup = useSetup();
  const location = useLocation();
  const route = findRoute(location.pathname);
  if (!setup || !setup.status || setup.status.ready || setup.gated || setup.warningDismissed) return null;
  if (route && route.key === 'setup') return null;
  const refused = setup.blockedMessage !== null;
  if (!refused && !affectedBySetup(route)) return null;
  const pending = pendingChecks(setup.status);
  const missing = listPhrase(
    pending.map((check) => checkTitle(check, t)),
    i18n.language
  );
  return (
    <div className="notice notice-warning setup-warning" role="status" data-testid="setup-warning">
      <TriangleAlert size={16} aria-hidden="true" />
      <div className="setup-warning-text">
        <strong>{t('setup.warning.title')}</strong>{' '}
        {refused && setup.blockedMessage ? <span dir="auto">{setup.blockedMessage}</span> : <span>{pending.length > 0 ? t('setup.warning.missing', { items: missing }) : t('setup.warning.generic')}</span>}{' '}
        {setup.status.canConfigure ? (
          <Link to="/dashboard/setup" data-testid="setup-warning-link">
            {t('setup.required.open')}
          </Link>
        ) : (
          <span>{t('setup.required.askAdmin')}</span>
        )}
      </div>
      <button type="button" className="icon-button" onClick={setup.dismissWarning} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
        <X size={14} aria-hidden="true" />
      </button>
    </div>
  );
}
