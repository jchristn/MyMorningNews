import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { DEFAULT_SERVER_CONTAINER } from '../../components/modelSignIn/signInCatalog.js';
import ModelConnectForm from './ModelConnectForm.jsx';
import ModelSummary from './ModelSummary.jsx';
import SetupStep from './SetupStep.jsx';
import { modelStepState } from './setupState.js';

// What reopening the flow for a configured endpoint preselects: its provider and method, its model, and the endpoint
// itself, so an empty secret keeps the stored one.
function presetFrom(check) {
  if (!check || !check.provider) return null;
  return {
    provider: check.provider,
    method: check.authMethod || null,
    model: check.model || '',
    baseUrl: check.baseUrl || '',
    endpointId: check.resourceId && !check.usesPlatformKey ? check.resourceId : null,
    hasSecret: Boolean(check.hasApiKey && check.needsApiKey)
  };
}

// Step 1, Connect an AI model. Open with the provider choice while nothing works; a summary with Change once a model
// passed its test; the problem with Fix when a configured endpoint fails.
export default function ModelStep({ checks, canConfigure, options, optionsError, onRetryOptions, verifying, onVerify, numbered = true }) {
  const { t } = useTranslation();
  const stepState = modelStepState(checks);
  const [editing, setEditing] = useState(null);
  const [preset, setPreset] = useState(null);
  const [formKey, setFormKey] = useState(0);
  const [justConnected, setJustConnected] = useState(null);
  const resultRef = useRef(null);
  const open = canConfigure && (editing === null ? stepState === 'empty' : editing);
  const container = options && options.serverContainer ? options.serverContainer : DEFAULT_SERVER_CONTAINER;

  useEffect(() => {
    if (justConnected && resultRef.current) resultRef.current.focus();
  }, [justConnected]);

  // The flow opened because nothing is connected: keep it open when a failed test turns the step into "broken", so
  // the result and the choices stay on screen.
  useEffect(() => {
    if (editing === null && canConfigure && stepState === 'empty') setEditing(true);
  }, [editing, canConfigure, stepState]);

  function startEdit(check) {
    setPreset(presetFrom(check));
    setJustConnected(null);
    setFormKey((k) => k + 1);
    setEditing(true);
  }

  function connected(provider) {
    setEditing(false);
    setPreset(null);
    setJustConnected(provider);
  }

  let body;
  if (open && !options && !optionsError) body = <LoadingState compact />;
  else if (open && optionsError) {
    body = (
      <div className="stack-tight">
        <ErrorState error={optionsError} inline />
        <div>
          <button type="button" className="button secondary" onClick={onRetryOptions}>
            {t('common.retry')}
          </button>
        </div>
      </div>
    );
  } else if (open) {
    // While a working model is being changed, its summary stays on top: it keeps serving research until a new
    // sign-in passes its test.
    body = (
      <div className="stack">
        {stepState === 'ready' ? <ModelSummary checks={checks} stepState={stepState} canConfigure={false} container={container} /> : null}
        <ModelConnectForm
          key={formKey}
          providers={options.providers || []}
          container={container}
          preset={preset}
          onConnected={connected}
          onCancel={stepState === 'empty' ? null : () => setEditing(false)}
        />
      </div>
    );
  } else {
    body = (
      <ModelSummary
        checks={checks}
        stepState={stepState}
        canConfigure={canConfigure}
        container={container}
        justConnected={justConnected}
        resultRef={resultRef}
        verifying={verifying}
        onChange={startEdit}
        onVerify={onVerify}
      />
    );
  }

  return (
    <SetupStep
      number={1}
      id="setup-step-model"
      title={t('setup.model.heading')}
      state={stepState === 'ready' ? 'done' : stepState === 'broken' ? 'attention' : 'todo'}
      intro={open ? t('setup.model.intro') : null}
      numbered={numbered}
    >
      {body}
    </SetupStep>
  );
}

ModelStep.propTypes = {
  checks: PropTypes.arrayOf(PropTypes.object).isRequired,
  canConfigure: PropTypes.bool,
  options: PropTypes.object,
  optionsError: PropTypes.object,
  onRetryOptions: PropTypes.func,
  verifying: PropTypes.bool,
  onVerify: PropTypes.func,
  numbered: PropTypes.bool
};
