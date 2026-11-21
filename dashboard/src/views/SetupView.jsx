import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { CircleCheck, RefreshCw, X } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { useSetup } from '../context/SetupContext.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
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

// Setup comes first: before anyone can create topics, the workspace needs a working AI model and web search. A guided
// flow connects the model (a subscription CLI, an API key, or a model you run) and tests it with one prompt.
export default function SetupView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const setup = useSetup();
  const status = setup ? setup.status : null;
  const canConfigure = Boolean(status && status.canConfigure);
  const verifyAll = setup ? setup.verify : null;
  const [verifying, setVerifying] = useState(false);
  const [verifyError, setVerifyError] = useState(null);
  const [options, setOptions] = useState(null);
  const [optionsError, setOptionsError] = useState(null);
  const autoVerified = useRef(false);

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

  return (
    <div className="page setup-page">
      <PageHeader
        title={t('setup.title')}
        summary={t('setup.summary')}
        actions={
          canConfigure ? (
            <button type="button" className="button secondary" onClick={verify} disabled={verifying}>
              <RefreshCw size={14} className={verifying ? 'spin' : undefined} aria-hidden="true" /> {t('setup.verifyAll')}
            </button>
          ) : null
        }
      />

      <SetupProgress steps={steps} />

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
              <Link to="/dashboard/home" className="button secondary">
                {t('setup.ready.home')}
              </Link>
            </div>
          </div>
        </section>
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
        onVerify={verify}
      />
      <SearchStep check={search} canConfigure={canConfigure} verifying={verifying} onVerify={verify} />
      <OptionalExtras checks={extras} />
    </div>
  );
}
