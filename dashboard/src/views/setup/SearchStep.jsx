import PropTypes from 'prop-types';
import { RefreshCw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import SetupStateBadge from '../../components/SetupStateBadge.jsx';
import SetupStep from './SetupStep.jsx';
import { progressState } from './setupState.js';

export function searchProviderName(check, t) {
  if (!check || !check.provider) return check && check.resourceName ? check.resourceName : '';
  return t('searchProviders.kinds.' + check.provider, { defaultValue: check.resourceName || check.provider });
}

// Step 2, Web search. The bundled SearXNG is checked automatically, so this is usually done already; otherwise it
// shows the server's message with Test again and a link to Search providers.
export default function SearchStep({ check, canConfigure, verifying, onVerify }) {
  const { t } = useTranslation();
  const f = useFormat();
  const state = check ? check.state : 'Missing';
  const ready = state === 'Ready';
  return (
    <SetupStep number={2} id="setup-step-search" title={t('setup.search.heading')} state={progressState(state)}>
      <div className="stack-tight" data-testid="setup-check-search">
        {ready ? (
          <>
            <p className="setup-search-ok">{t('setup.search.ready', { provider: searchProviderName(check, t) })}</p>
            {check.verifiedUtc ? (
              <p className="muted small">{t('setup.connected.verified', { time: f.relative(check.verifiedUtc) })}</p>
            ) : null}
          </>
        ) : (
          <>
            <div className="setup-summary-state">
              <SetupStateBadge state={state} />
            </div>
            <p className="muted">{t('setup.search.intro')}</p>
            {check && check.message ? (
              <p className={'setup-check-message' + (state === 'Unverified' ? ' muted' : ' notice notice-danger')} dir="auto">
                {check.message}
              </p>
            ) : null}
            {canConfigure ? (
              <div className="setup-check-actions">
                <button type="button" className="button secondary" onClick={onVerify} disabled={verifying}>
                  <RefreshCw size={14} className={verifying ? 'spin' : undefined} aria-hidden="true" /> {t('setup.connected.testAgain')}
                </button>
                <Link to="/dashboard/search-providers" className="button secondary">
                  {t('setup.search.open')}
                </Link>
              </div>
            ) : null}
          </>
        )}
      </div>
    </SetupStep>
  );
}

SearchStep.propTypes = {
  check: PropTypes.object,
  canConfigure: PropTypes.bool,
  verifying: PropTypes.bool,
  onVerify: PropTypes.func
};
