import { useState } from 'react';
import PropTypes from 'prop-types';
import { KeyRound, RefreshCw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useSetup } from '../../context/SetupContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import PasswordInput from '../../components/PasswordInput.jsx';
import SetupStateBadge, { checkTitle } from '../../components/SetupStateBadge.jsx';

// The provider name as the rest of the dashboard shows it: model providers and search kinds are translated enums.
function providerLabel(check, t) {
  if (!check.provider) return null;
  if (check.kind === 'Model') return t('enums.provider.' + check.provider, { defaultValue: check.provider });
  if (check.kind === 'Search') return t('searchProviders.kinds.' + check.provider, { defaultValue: check.provider });
  return check.provider;
}

// Paste a key for one model endpoint, then store, attach, and verify it with a live call (several seconds).
function ModelKeyForm({ check }) {
  const { t } = useTranslation();
  const setup = useSetup();
  const [apiKey, setApiKey] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const id = 'setup-key-' + check.key.replace(/[^A-Za-z0-9_-]/g, '-');
  const name = check.resourceName || t('setup.kind.Model');

  async function submit(event) {
    event.preventDefault();
    const value = apiKey.trim();
    if (!value || !check.resourceId) return;
    setBusy(true);
    setError(null);
    try {
      await setup.saveModelKey(check.resourceId, value);
      setApiKey('');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="setup-key-form" onSubmit={submit}>
      <FormField id={id} label={t('setup.key.label', { name })} help={t('setup.key.help')}>
        <PasswordInput id={id} value={apiKey} onChange={setApiKey} autoComplete="off" disabled={busy} required />
      </FormField>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {busy ? <LoadingState compact label={t('setup.key.saving')} /> : null}
      <div className="setup-check-actions">
        <button type="submit" className="button primary" disabled={busy || !apiKey.trim() || !check.resourceId}>
          <KeyRound size={14} aria-hidden="true" /> {busy ? t('common.working') : t('setup.key.save')}
        </button>
      </div>
    </form>
  );
}

ModelKeyForm.propTypes = { check: PropTypes.object.isRequired };

// One setup check: its state, the server's message as given, and the way to fix it for administrators.
export default function SetupCheckCard({ check, canConfigure, onVerify, verifying }) {
  const { t } = useTranslation();
  const f = useFormat();
  const provider = providerLabel(check, t);
  const tasks = Array.isArray(check.tasks) ? check.tasks : [];
  const notReady = check.state !== 'Ready';
  // Only model endpoints take a key here; a search provider's key is set on the Search providers page.
  const acceptsKey = canConfigure && check.kind === 'Model' && check.needsApiKey && !check.usesPlatformKey && notReady && Boolean(check.resourceId);
  const canVerify = canConfigure && (check.state === 'Unverified' || check.state === 'Failed') && (check.hasApiKey || !check.needsApiKey);
  const showSearchLink = canConfigure && check.kind === 'Search' && (check.state === 'Failed' || check.state === 'Missing');
  const showEndpointLink = canConfigure && check.kind === 'Model' && notReady && Boolean(check.resourceId);
  // No endpoint serves the task at all (key "model:none"): the fix is on the Models page.
  const showModelsLink = canConfigure && check.kind === 'Model' && notReady && !check.resourceId;

  return (
    <article className={'panel setup-check setup-check-' + String(check.state || '').toLowerCase()} data-testid={'setup-check-' + check.key}>
      <div className="setup-check-head">
        <h3>{checkTitle(check, t)}</h3>
        <SetupStateBadge state={check.state} />
      </div>
      <div className="setup-check-meta muted small">
        {provider && check.model ? <span>{t('setup.check.providerModel', { provider, model: check.model })}</span> : provider ? <span>{t('setup.check.provider', { provider })}</span> : null}
        {tasks.length > 0 ? <span>{t('setup.check.tasks', { tasks: f.list(tasks.map((task) => t('enums.task.' + task, { defaultValue: task }))) })}</span> : null}
        {check.verifiedUtc ? <span>{t('setup.check.verified', { time: f.dateTime(check.verifiedUtc) })}</span> : null}
      </div>
      {check.message ? (
        <p className={'setup-check-message' + (check.state === 'Failed' ? ' notice notice-danger' : '')} dir="auto">
          {check.message}
        </p>
      ) : null}
      {check.usesPlatformKey && notReady ? <p className="muted small">{t('setup.check.platformKey')}</p> : null}
      {acceptsKey ? <ModelKeyForm check={check} /> : null}
      {canVerify || showSearchLink || showEndpointLink || showModelsLink ? (
        <div className="setup-check-actions">
          {canVerify ? (
            <button type="button" className="button secondary" onClick={onVerify} disabled={verifying}>
              <RefreshCw size={14} className={verifying ? 'spin' : undefined} aria-hidden="true" /> {check.state === 'Failed' ? t('setup.check.retry') : t('setup.check.verify')}
            </button>
          ) : null}
          {showSearchLink ? (
            <Link to="/dashboard/search-providers" className="button secondary">
              {t('setup.check.openSearch')}
            </Link>
          ) : null}
          {showModelsLink ? (
            <Link to="/dashboard/models" className="button secondary">
              {t('setup.check.openModels')}
            </Link>
          ) : null}
          {showEndpointLink ? (
            <Link to={'/dashboard/models?endpoint=' + encodeURIComponent(check.resourceId)} className="link-button small">
              {t('setup.check.openEndpoint', { name: check.resourceName || t('setup.kind.Model') })}
            </Link>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

SetupCheckCard.propTypes = {
  check: PropTypes.object.isRequired,
  canConfigure: PropTypes.bool,
  onVerify: PropTypes.func,
  verifying: PropTypes.bool
};
