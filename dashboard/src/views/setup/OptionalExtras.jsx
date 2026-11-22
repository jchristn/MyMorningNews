import { useState } from 'react';
import PropTypes from 'prop-types';
import { Volume2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import SetupStateBadge from '../../components/SetupStateBadge.jsx';

// Where each optional check is set up. Settings are the operator's, so only global administrators get that link.
const LINKS = {
  Embeddings: { to: '/dashboard/models', key: 'setup.extras.openModels', admin: false },
  Email: { to: '/dashboard/settings', key: 'setup.extras.openSettings', admin: true }
};

// The speech check: the speech server it talks to, its model and default voice, a live test, and where each part is
// changed. The server address is the operator's (Server settings); the voices are the workspace's.
function SpeechExtra({ check, onTest }) {
  const { t } = useTranslation();
  const { isAdmin } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [tested, setTested] = useState(false);

  async function test() {
    setBusy(true);
    setError(null);
    setTested(false);
    try {
      await onTest('Speech');
      setTested(true);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      {check.resourceName || check.model || check.voice ? (
        <dl className="setup-facts">
          {check.resourceName ? (
            <div>
              <dt>{t('setup.speech.server')}</dt>
              <dd className="mono">{check.resourceName}</dd>
            </div>
          ) : null}
          {check.model ? (
            <div>
              <dt>{t('setup.speech.model')}</dt>
              <dd className="mono">{check.model}</dd>
            </div>
          ) : null}
          {check.voice ? (
            <div>
              <dt>{t('setup.speech.voice')}</dt>
              <dd className="mono">{check.voice}</dd>
            </div>
          ) : null}
        </dl>
      ) : null}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <p className="small setup-live" role="status">
        {busy ? t('setup.speech.testing') : tested && check.state === 'Ready' ? t('setup.speech.works') : ''}
      </p>
      <div className="setup-check-actions">
        {onTest ? (
          <button type="button" className="button secondary small" onClick={test} disabled={busy}>
            <Volume2 size={14} aria-hidden="true" /> {t('setup.speech.test')}
          </button>
        ) : null}
        {isAdmin ? (
          <Link to="/dashboard/settings" className="link-button small">
            {t('setup.speech.openSettings')}
          </Link>
        ) : null}
        <Link to="/dashboard/voices" className="link-button small">
          {t('setup.speech.openVoices')}
        </Link>
      </div>
      {!isAdmin ? <p className="muted small">{t('setup.speech.whoCanChange')}</p> : null}
    </>
  );
}

SpeechExtra.propTypes = { check: PropTypes.object.isRequired, onTest: PropTypes.func };

// Embeddings, email, and speech: what each adds, its state, and the server's message. Never blocking. Collapsed while
// setup is being done, open once the page is a status page.
export default function OptionalExtras({ checks, defaultOpen = false, onTest }) {
  const { t } = useTranslation();
  const { isAdmin } = useAuth();
  const [open, setOpen] = useState(defaultOpen);
  if (checks.length === 0) return null;
  const readyCount = checks.filter((c) => c.state === 'Ready').length;
  return (
    <details className="panel setup-extras" data-testid="setup-extras" open={open} onToggle={(event) => setOpen(event.currentTarget.open)}>
      <summary>
        <span className="setup-extras-title">{t('setup.extras.title')}</span>{' '}
        <span className="muted small">{t('setup.extras.count', { ready: readyCount, count: checks.length })}</span>
      </summary>
      <p className="muted small">{t('setup.extras.note')}</p>
      <ul className="setup-extras-list">
        {checks.map((check) => {
          const link = LINKS[check.kind];
          return (
            <li key={check.key} data-testid={'setup-check-' + check.key}>
              <div className="setup-extras-head">
                <strong>{t('setup.kind.' + check.kind, { defaultValue: check.kind || '' })}</strong>
                <SetupStateBadge state={check.state} />
              </div>
              <p className="small">{t('setup.extras.what.' + check.kind, { defaultValue: '' })}</p>
              {check.message ? (
                <p className="muted small" dir="auto">
                  {check.message}
                </p>
              ) : null}
              {check.kind === 'Speech' ? <SpeechExtra check={check} onTest={onTest} /> : null}
              {link && (!link.admin || isAdmin) ? (
                <Link to={link.to} className="link-button small">
                  {t(link.key)}
                </Link>
              ) : null}
            </li>
          );
        })}
      </ul>
    </details>
  );
}

OptionalExtras.propTypes = { checks: PropTypes.arrayOf(PropTypes.object).isRequired, defaultOpen: PropTypes.bool, onTest: PropTypes.func };
