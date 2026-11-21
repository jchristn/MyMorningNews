import { useCallback, useEffect, useRef, useState } from 'react';
import { CircleCheck, RefreshCw, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { orderedChecks, useSetup } from '../context/SetupContext.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import SetupCheckCard from './setup/SetupCheckCard.jsx';

// True when a required check has what it needs (a key, or none is needed) but has not been checked with a live call.
export function needsAutoVerify(checks) {
  return (Array.isArray(checks) ? checks : []).some((c) => c.required && c.state === 'Unverified' && (c.hasApiKey || !c.needsApiKey));
}

// Setup comes first: the checks that must pass (a verified model key, a working search provider) before anyone can
// use the product, required ones first, with a key form and live verification for administrators.
export default function SetupView() {
  const { t } = useTranslation();
  const setup = useSetup();
  const status = setup ? setup.status : null;
  const [verifying, setVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState(null);
  const autoVerified = useRef(false);
  const canConfigure = Boolean(status && status.canConfigure);
  const verifyAll = setup ? setup.verify : null;

  const verify = useCallback(async () => {
    if (!verifyAll) return;
    setVerifying(true);
    setVerifyError(null);
    try {
      await verifyAll();
    } catch (err) {
      setVerifyError(err);
    } finally {
      setVerifying(false);
    }
  }, [verifyAll]);

  // Once when the screen opens: a key is there but nobody has checked it yet, so check it now.
  useEffect(() => {
    if (autoVerified.current || !status) return;
    autoVerified.current = true;
    if (canConfigure && !status.ready && needsAutoVerify(status.checks)) verify();
  }, [status, canConfigure, verify]);

  if (!status) return <LoadingState />;
  const { required, optional } = orderedChecks(status.checks);
  const card = (check) => <SetupCheckCard key={check.key} check={check} canConfigure={canConfigure} onVerify={verify} verifying={verifying} />;

  return (
    <div className="page setup-page">
      <PageHeader
        title={t('nav.items.setup')}
        summary={t('setup.summary')}
        actions={
          canConfigure ? (
            <button type="button" className="button secondary" onClick={verify} disabled={verifying}>
              <RefreshCw size={14} className={verifying ? 'spin' : undefined} aria-hidden="true" /> {t('setup.verifyAll')}
            </button>
          ) : null
        }
      />

      {status.ready ? (
        <section className="panel setup-ready" role="status" data-testid="setup-ready">
          <CircleCheck size={28} aria-hidden="true" />
          <div className="stack-tight">
            <h2>{t('setup.ready.title')}</h2>
            <p>{t('setup.ready.message')}</p>
            <div className="setup-check-actions">
              <Link to="/dashboard/topics/new" className="button primary">
                {t('setup.ready.newTopic')}
              </Link>
              <Link to="/dashboard/topics" className="button secondary">
                {t('setup.ready.topics')}
              </Link>
            </div>
          </div>
        </section>
      ) : (
        <div className="notice notice-warning setup-intro" data-testid="setup-intro">
          <strong>{t('setup.intro.title')}</strong> {t('setup.intro.message')}
        </div>
      )}

      {!status.ready && setup.blockedMessage !== null ? (
        <div className="notice notice-danger setup-blocked-notice" role="alert">
          <span>
            <strong>{t('setup.blockedNotice')}</strong> {setup.blockedMessage}
          </span>
          <button type="button" className="icon-button" onClick={setup.dismissBlocked} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      ) : null}

      {verifying ? <LoadingState compact label={t('setup.verifying')} /> : null}
      <ErrorState error={verifyError} inline onDismiss={() => setVerifyError(null)} />

      <section className="stack setup-section" aria-labelledby="setup-required-heading">
        <h2 id="setup-required-heading">{t('setup.required.heading')}</h2>
        {required.length > 0 ? required.map(card) : <p className="muted">{t('setup.required.none')}</p>}
      </section>

      {optional.length > 0 ? (
        <section className="stack setup-section" aria-labelledby="setup-optional-heading">
          <h2 id="setup-optional-heading">{t('setup.optional.heading')}</h2>
          <p className="muted small">{t('setup.optional.note')}</p>
          {optional.map(card)}
        </section>
      ) : null}

      <section className="panel setup-help stack-tight">
        <h2>{t('setup.help.title')}</h2>
        <p className="small">{t('setup.help.env')}</p>
        <p className="small">
          <Link to="/dashboard/models">{t('setup.help.models')}</Link>
        </p>
      </section>
    </div>
  );
}
