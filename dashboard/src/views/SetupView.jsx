import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { CircleCheck, RefreshCw, X } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { pendingChecks, useSetup } from '../context/SetupContext.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import SetupStateBadge, { checkTitle } from '../components/SetupStateBadge.jsx';
import ModelStep from './setup/ModelStep.jsx';
import OptionalExtras from './setup/OptionalExtras.jsx';
import SearchStep from './setup/SearchStep.jsx';
import { StepMarker } from './setup/SetupStep.jsx';
import { modelChecks, modelStepState, needsAutoVerify, optionalChecks, progressState, searchCheck } from './setup/setupState.js';

// Where the administrator is: one line per required step, done, to do, or needing attention.
function SetupProgress({ steps }) {
  const { t } = useTranslation();
  return (
    <ol className="setup-progress" aria-label={t('setup.progress.label')} data-testid="setup-progress">
      {steps.map((step, index) => (
        <li key={step.key} className={'setup-progress-item ' + step.state}>
          <StepMarker number={index + 1} state={step.state} />
          <a href={'#' + step.anchor} className="setup-progress-link">
            {step.label}
          </a>
          <span className={'setup-step-state ' + step.state}>{t('setup.progress.' + step.state)}</span>
        </li>
      ))}
    </ol>
  );
}

SetupProgress.propTypes = {
  steps: PropTypes.arrayOf(
    PropTypes.shape({ key: PropTypes.string, anchor: PropTypes.string, label: PropTypes.string, state: PropTypes.string })
  ).isRequired
};

// Leaving setup unfinished: exactly what is still missing, what will not work without it, and the choice to stay.
function SkipSetupModal({ open, pending, onStay, onSkip }) {
  const { t } = useTranslation();
  const needsModel = pending.some((c) => c.kind === 'Model');
  const needsSearch = pending.some((c) => c.kind === 'Search');
  return (
    <Modal
      open={open}
      title={t('setup.skip.title')}
      onClose={onStay}
      size="small"
      footer={
        <>
          <button type="button" className="button primary" onClick={onStay}>
            {t('setup.skip.stay')}
          </button>
          <button type="button" className="button secondary" onClick={onSkip} data-testid="setup-skip-confirm">
            {t('setup.skip.button')}
          </button>
        </>
      }
    >
      <div className="stack-tight" data-testid="setup-skip-modal">
        <p>{t('setup.skip.intro')}</p>
        <ul className="setup-skip-list">
          {pending.map((check) => (
            <li key={check.key}>
              <div className="setup-extras-head">
                <strong>{checkTitle(check, t)}</strong>
                <SetupStateBadge state={check.state} />
              </div>
              {check.message ? (
                <p className="muted small" dir="auto">
                  {check.message}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
        <p>{t('setup.skip.untilThen')}</p>
        <ul className="setup-skip-effects">
          {needsModel ? <li>{t('setup.skip.needsModel')}</li> : null}
          {needsSearch ? <li>{t('setup.skip.needsSearch')}</li> : null}
        </ul>
        <p className="muted small">{t('setup.skip.later')}</p>
      </div>
    </Modal>
  );
}

SkipSetupModal.propTypes = {
  open: PropTypes.bool.isRequired,
  pending: PropTypes.arrayOf(PropTypes.object).isRequired,
  onStay: PropTypes.func.isRequired,
  onSkip: PropTypes.func.isRequired
};

// The Setup page. Until the workspace has a working AI model and web search it is a guided flow: connect the model (a
// subscription CLI, an API key, or a model you run), test it with one prompt, and check search. Nobody has to finish
// it: Skip for now says what is missing and opens the rest of the app. Once everything required works, the same page
// is a plain status page (what is connected, with Change and Test again), not a wizard.
export default function SetupView() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const setup = useSetup();
  const status = setup ? setup.status : null;
  const canConfigure = Boolean(status && status.canConfigure);
  const verifyAll = setup ? setup.verify : null;
  const [verifying, setVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState(null);
  const [options, setOptions] = useState(null);
  const [optionsError, setOptionsError] = useState(null);
  const [skipOpen, setSkipOpen] = useState(false);
  const autoVerified = useRef(false);

  const verify = useCallback(async () => {
    if (!verifyAll) return;
    setVerifying(true);
    setVerifyError(null);
    try {
      await verifyAll(undefined);
    } catch (err) {
      setVerifyError(err);
    } finally {
      setVerifying(false);
    }
  }, [verifyAll]);

  const loadOptions = useCallback(() => {
    setOptionsError(null);
    Promise.resolve()
      .then(() => apiClient.getSetupOptions())
      .then((r) => setOptions(r && Array.isArray(r.providers) ? r : { providers: [] }))
      .catch((err) => setOptionsError(err));
  }, [apiClient]);

  useEffect(() => {
    if (canConfigure) loadOptions();
  }, [canConfigure, loadOptions]);

  // Once when the screen opens: a sign-in is stored but nobody has tested it yet, so test it now.
  useEffect(() => {
    if (autoVerified.current || !status) return;
    autoVerified.current = true;
    if (canConfigure && !status.ready && needsAutoVerify(status.checks)) verify();
  }, [status, canConfigure, verify]);

  if (!status) return <LoadingState />;
  const models = modelChecks(status);
  const search = searchCheck(status);
  const extras = optionalChecks(status);
  const steps = [
    { key: 'model', anchor: 'setup-step-model', label: t('setup.progress.model'), state: progressState(modelStepState(models)) },
    {
      key: 'search',
      anchor: 'setup-step-search',
      label: t('setup.progress.search'),
      state: progressState(search ? search.state : 'Missing')
    }
  ];

  const ready = status.ready;
  const pending = pendingChecks(status);

  function skipSetup() {
    setSkipOpen(false);
    setup.skip();
    navigate('/dashboard/home');
  }

  return (
    <div className="page setup-page">
      <PageHeader
        title={ready ? t('setup.status.title') : t('setup.title')}
        summary={ready ? t('setup.status.summary') : t('setup.summary')}
        actions={
          <>
            {canConfigure ? (
              <button type="button" className="button secondary" onClick={() => verify()} disabled={verifying}>
                <RefreshCw size={14} className={verifying ? 'spin' : undefined} aria-hidden="true" /> {t('setup.verifyAll')}
              </button>
            ) : null}
            {!ready && setup.gated ? (
              <button type="button" className="button secondary" onClick={() => setSkipOpen(true)} data-testid="setup-skip">
                {t('setup.skip.button')}
              </button>
            ) : null}
          </>
        }
      />

      {status?.checks?.some(c => c.key === 'personal:chat') ? <Link className="button primary" to="/dashboard/account/providers">{t('personalProviders.title')}</Link> : null}
      {!ready ? <SetupProgress steps={steps} /> : null}

      {!ready && !setup.gated ? (
        <p className="muted small setup-skipped-note" data-testid="setup-skipped-note">
          {t('setup.skip.skippedNote')}{' '}
          <button type="button" className="link-button small" onClick={setup.resume}>
            {t('setup.skip.resume')}
          </button>
        </p>
      ) : null}

      {ready && setup.justCompleted ? (
        <div className="notice notice-success setup-complete" role="status" data-testid="setup-complete">
          <CircleCheck size={16} aria-hidden="true" />
          <span className="setup-warning-text">
            {t('setup.complete.message')} <Link to="/dashboard/topics">{t('setup.complete.topics')}</Link>
          </span>
          <button type="button" className="icon-button" onClick={setup.dismissCompleted} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      ) : null}

      {!status.ready && setup.blockedMessage !== null ? (
        <div className="notice notice-danger setup-blocked-notice" role="alert">
          <span>
            <strong>{t('setup.blockedNotice')}</strong> {setup.blockedMessage}
          </span>
          <button
            type="button"
            className="icon-button"
            onClick={setup.dismissBlocked}
            aria-label={t('common.dismiss')}
            title={t('common.dismiss')}
          >
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      ) : null}

      <ErrorState error={verifyError} inline onDismiss={() => setVerifyError(null)} />

      <ModelStep
        checks={models}
        canConfigure={canConfigure}
        options={options}
        optionsError={optionsError}
        onRetryOptions={loadOptions}
        verifying={verifying}
        onVerify={() => verify()}
        numbered={!ready}
      />
      <SearchStep check={search} canConfigure={canConfigure} verifying={verifying} onVerify={() => verify()} numbered={!ready} />
      <OptionalExtras key={ready ? 'status' : 'guide'} checks={extras} defaultOpen={ready} onTest={canConfigure && verifyAll ? verifyAll : undefined} />
      <SkipSetupModal open={skipOpen} pending={pending} onStay={() => setSkipOpen(false)} onSkip={skipSetup} />
    </div>
  );
}
