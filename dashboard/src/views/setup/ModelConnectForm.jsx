import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { CircleAlert, LoaderCircle, PlugZap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useSetup } from '../../context/SetupContext.jsx';
import { isAgentCli, isCliDefaultModel, needsSecret } from '../../utils/modelAuth.js';
import CommandChip from '../../components/modelSignIn/CommandChip.jsx';
import SecretInput from '../../components/modelSignIn/SecretInput.jsx';
import SignInMethodPicker from '../../components/modelSignIn/SignInMethodPicker.jsx';
import SignInSteps from '../../components/modelSignIn/SignInSteps.jsx';
import {
  COMPATIBLE_URL_EXAMPLE,
  OLLAMA_DOCKER_URL,
  errorField,
  failureHint,
  providerName
} from '../../components/modelSignIn/signInCatalog.js';
import ErrorState from '../../components/ErrorState.jsx';
import ProviderPicker from './ProviderPicker.jsx';
import { connectedCheck, isHttpUrl, modelChecks } from './setupState.js';

const FIELD_IDS = { secret: 'setup-secret', model: 'setup-model', baseUrl: 'setup-base-url' };

function recommendedMethod(option) {
  const methods = option && Array.isArray(option.methods) ? option.methods : [];
  const pick = methods.find((m) => m.recommended) || methods[0];
  return pick ? pick.method : null;
}

// The form state for a provider: its recommended method (or the preset's), its default model, and its default URL.
function formFor(option, preset) {
  const same = Boolean(preset && option && preset.provider === option.provider);
  const methods = option ? option.methods.map((m) => m.method) : [];
  const method = same && preset.method && methods.includes(preset.method) ? preset.method : recommendedMethod(option);
  const defaultModel = option && option.defaultModel && !isCliDefaultModel(option.defaultModel) ? option.defaultModel : '';
  const presetModel = same && preset.model && !(isAgentCli(option.provider) && isCliDefaultModel(preset.model)) ? preset.model : '';
  return {
    provider: option ? option.provider : null,
    method,
    secret: '',
    model: presetModel || defaultModel,
    baseUrl: (same && preset.baseUrl) || (option && option.defaultBaseUrl) || '',
    endpointId: same ? preset.endpointId || null : null
  };
}

// The connect flow: provider, sign-in method, instructions, the secret, model, and URL, then Connect and test. The
// server stores the secret, points every chat task at the endpoint, and sends one test prompt; the result shows
// under the button and takes focus. preset reopens the flow for an endpoint that is already set up.
export default function ModelConnectForm({ providers, container, preset, onConnected, onCancel }) {
  const { t } = useTranslation();
  const setup = useSetup();
  const presetOption = preset ? providers.find((o) => o.provider === preset.provider && o.available !== false) : null;
  const [form, setForm] = useState(() => formFor(presetOption || null, preset));
  const [busy, setBusy] = useState(false);
  const [errors, setErrors] = useState({});
  const [failure, setFailure] = useState(null);
  const [requestError, setRequestError] = useState(null);
  const resultRef = useRef(null);
  const headingRef = useRef(null);
  const option = providers.find((o) => o.provider === form.provider) || null;
  const methods = option ? option.methods.map((m) => m.method) : [];
  const methodInput = option ? (option.methods.find((m) => m.method === form.method) || {}).input : null;
  const secretNeeded = Boolean(form.method && (methodInput ? methodInput !== 'None' : needsSecret(form.method)));
  // Reconnecting the same endpoint with the same method: an empty secret keeps the one already stored.
  const keepsStored = Boolean(
    preset && preset.hasSecret && form.provider === preset.provider && form.method === preset.method && form.endpointId
  );
  const modelRequired = Boolean(option && !option.defaultModel);
  const cliDefault = Boolean(option && isAgentCli(option.provider));

  // Opening the flow for a configured endpoint moves focus into it: the chosen provider's card, so arrow keys move
  // from it, or the heading when none is chosen. The preset is fixed for the life of the form (the parent remounts it
  // with a key), so this runs once.
  useEffect(() => {
    if (!preset) return;
    const checked = document.querySelector('input[name="setup-provider"]:checked');
    if (checked) checked.focus();
    else if (headingRef.current) headingRef.current.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function pickProvider(provider) {
    const next = providers.find((o) => o.provider === provider);
    setForm(formFor(next, preset));
    setErrors({});
    setFailure(null);
    setRequestError(null);
  }

  function pickMethod(method) {
    setForm((f) => ({ ...f, method, secret: '' }));
    setErrors({});
    setFailure(null);
    setRequestError(null);
  }

  const set = (patch) => {
    setForm((f) => ({ ...f, ...patch }));
    const field = Object.keys(patch)[0];
    if (errors[field]) setErrors((e) => ({ ...e, [field]: null }));
  };

  function validate() {
    const found = {};
    if (secretNeeded && !form.secret.trim() && !keepsStored)
      found.secret = t(
        'setup.field.required.' + (form.method === 'AuthFile' ? 'AuthFile' : form.method === 'AccessToken' ? 'AccessToken' : 'ApiKey')
      );
    if (option && option.needsBaseUrl && !isHttpUrl(form.baseUrl)) found.baseUrl = t('setup.field.baseUrlInvalid');
    if (modelRequired && !form.model.trim()) found.model = t('setup.field.modelRequired');
    return found;
  }

  function focusField(field) {
    window.setTimeout(() => {
      const el = document.getElementById(FIELD_IDS[field]);
      if (el) el.focus();
    }, 0);
  }

  function focusResult() {
    window.setTimeout(() => {
      if (resultRef.current) resultRef.current.focus();
    }, 0);
  }

  async function submit(event) {
    event.preventDefault();
    if (!option || busy) return;
    setFailure(null);
    setRequestError(null);
    const found = validate();
    setErrors(found);
    const first = ['secret', 'baseUrl', 'model'].find((k) => found[k]);
    if (first) {
      focusField(first);
      return;
    }
    const body = { provider: form.provider, authMethod: form.method };
    if (secretNeeded && form.secret.trim()) body.secret = form.secret.trim();
    if (form.model.trim()) body.model = form.model.trim();
    if (option.needsBaseUrl) body.baseUrl = form.baseUrl.trim();
    if (form.endpointId) body.modelEndpointId = form.endpointId;
    setBusy(true);
    try {
      const next = await setup.connect(body);
      const connection = next.connection;
      const checks = modelChecks(next);
      const check = connectedCheck(checks, form.provider);
      // The server's own verdict on the test; older servers send none, so the model check stands in.
      const succeeded = connection ? Boolean(connection.succeeded) : Boolean(check && check.state === 'Ready');
      if (succeeded) {
        setBusy(false);
        onConnected(form.provider);
        return;
      }
      // A change that failed its test leaves the previous model in place, still Ready.
      const kept = connection && connection.keptPrevious ? checks.find((c) => c.state === 'Ready') || null : null;
      setFailure({
        message: (connection && connection.message) || (check && check.message) || t('setup.result.noAnswer'),
        kept: kept ? providerName(t, kept.provider) || kept.resourceName || '' : null,
        hint: failureHint(form.provider, form.method, container)
      });
      focusResult();
    } catch (err) {
      const field = err && err.status === 400 ? errorField(err) : null;
      if (field && (field !== 'baseUrl' || option.needsBaseUrl) && (field !== 'secret' || secretNeeded)) {
        setErrors({ [field]: err.message });
        if (field === 'model' && !modelRequired) {
          const details = document.getElementById('setup-advanced');
          if (details) details.open = true;
        }
        focusField(field);
      } else {
        setRequestError(err);
        focusResult();
      }
    }
    setBusy(false);
  }

  const modelField = (
    <div className={'form-field' + (errors.model ? ' has-error' : '')}>
      <label htmlFor={FIELD_IDS.model} className="field-label">
        {t('setup.field.model')}
      </label>
      <input
        id={FIELD_IDS.model}
        className="mono"
        value={form.model}
        onChange={(e) => set({ model: e.target.value })}
        autoComplete="off"
        spellCheck={false}
        placeholder={cliDefault ? t('setup.field.cliDefault') : undefined}
        aria-describedby={FIELD_IDS.model + '-message'}
      />
      {errors.model ? (
        <span id={FIELD_IDS.model + '-message'} className="field-error" role="alert">
          {errors.model}
        </span>
      ) : (
        <span id={FIELD_IDS.model + '-message'} className="field-help muted">
          {cliDefault
            ? t('setup.field.modelHelpCli')
            : modelRequired
              ? t('setup.field.modelHelpRequired', { provider: providerName(t, form.provider) })
              : t('setup.field.modelHelpDefault')}
        </span>
      )}
    </div>
  );

  const hint = failure ? failure.hint : null;

  return (
    <form className="setup-connect" onSubmit={submit} noValidate aria-labelledby="setup-connect-heading">
      <h3 id="setup-connect-heading" ref={headingRef} tabIndex={-1} className="sr-only">
        {t('setup.model.providerLegend')}
      </h3>
      <fieldset className="setup-connect-fields" disabled={busy}>
        <ProviderPicker options={providers} value={form.provider} onChange={pickProvider} />

        {option ? (
          <div className="setup-connect-detail stack">
            {methods.length > 1 ? (
              <SignInMethodPicker
                name="setup-method"
                provider={option.provider}
                methods={methods}
                value={form.method}
                onChange={pickMethod}
              />
            ) : null}

            <section className="setup-instructions" aria-labelledby="setup-steps-heading">
              <h4 id="setup-steps-heading">{t('setup.model.stepsTitle', { provider: providerName(t, option.provider) })}</h4>
              <SignInSteps provider={option.provider} method={form.method} container={container} headingId="setup-steps-heading" />
            </section>

            <div className="setup-inputs stack">
              {option.needsBaseUrl ? (
                <div className={'form-field' + (errors.baseUrl ? ' has-error' : '')}>
                  <label htmlFor={FIELD_IDS.baseUrl} className="field-label">
                    {t('setup.field.baseUrl')}
                  </label>
                  <input
                    id={FIELD_IDS.baseUrl}
                    type="url"
                    className="mono"
                    value={form.baseUrl}
                    onChange={(e) => set({ baseUrl: e.target.value })}
                    autoComplete="off"
                    spellCheck={false}
                    placeholder={option.provider === 'Ollama' ? OLLAMA_DOCKER_URL : COMPATIBLE_URL_EXAMPLE}
                    aria-describedby={FIELD_IDS.baseUrl + '-message'}
                  />
                  {errors.baseUrl ? (
                    <span id={FIELD_IDS.baseUrl + '-message'} className="field-error" role="alert">
                      {errors.baseUrl}
                    </span>
                  ) : (
                    <span id={FIELD_IDS.baseUrl + '-message'} className="field-help muted">
                      {option.provider === 'Ollama'
                        ? t('setup.field.baseUrlHelpOllama', { url: OLLAMA_DOCKER_URL })
                        : t('setup.field.baseUrlHelp')}
                    </span>
                  )}
                </div>
              ) : null}

              {secretNeeded ? (
                <SecretInput
                  key={option.provider + ':' + form.method}
                  id={FIELD_IDS.secret}
                  provider={option.provider}
                  method={form.method}
                  value={form.secret}
                  onChange={(v) => set({ secret: v })}
                  disabled={busy}
                  error={errors.secret || null}
                  help={keepsStored ? t('setup.field.keepStored') : null}
                />
              ) : null}

              {modelRequired ? (
                modelField
              ) : (
                <details className="setup-advanced" id="setup-advanced">
                  <summary>
                    {t('setup.field.advanced')}{' '}
                    <span className="muted small">
                      {t('setup.field.advancedSummary', { model: form.model.trim() || t('setup.field.cliDefault') })}
                    </span>
                  </summary>
                  {modelField}
                </details>
              )}
            </div>

            <div className="setup-connect-actions">
              <button type="submit" className="button primary">
                {busy ? <LoaderCircle size={14} className="spin" aria-hidden="true" /> : <PlugZap size={14} aria-hidden="true" />}{' '}
                {busy ? t('setup.model.connecting') : t('setup.model.connect')}
              </button>
              {onCancel ? (
                <button type="button" className="button secondary" onClick={onCancel}>
                  {t('common.cancel')}
                </button>
              ) : null}
            </div>
          </div>
        ) : null}
      </fieldset>

      <div className="setup-live" aria-live="polite">
        {busy ? (
          <p className="setup-testing">
            <LoaderCircle size={14} className="spin" aria-hidden="true" /> {t('setup.model.testing')}
          </p>
        ) : null}
      </div>

      {failure || requestError ? (
        <div className="setup-result setup-result-failed" ref={resultRef} tabIndex={-1} role="alert" data-testid="setup-result">
          {failure ? (
            <>
              <p className="setup-result-title">
                <CircleAlert size={16} aria-hidden="true" />{' '}
                <strong>{failure.kept !== null ? t('setup.result.keptPrevious', { provider: failure.kept }) : t('setup.result.failed')}</strong>
              </p>
              <p className="setup-result-message" dir="auto">
                {failure.message}
              </p>
              {hint ? (
                <p className="setup-result-hint small">
                  <span>{t(hint.key, hint.values)}</span> {hint.command ? <CommandChip command={hint.command} /> : null}
                </p>
              ) : null}
            </>
          ) : (
            <ErrorState error={requestError} inline onDismiss={() => setRequestError(null)} />
          )}
        </div>
      ) : null}
    </form>
  );
}

ModelConnectForm.propTypes = {
  providers: PropTypes.arrayOf(PropTypes.object).isRequired,
  container: PropTypes.string,
  preset: PropTypes.shape({
    provider: PropTypes.string,
    method: PropTypes.string,
    model: PropTypes.string,
    baseUrl: PropTypes.string,
    endpointId: PropTypes.string,
    hasSecret: PropTypes.bool
  }),
  onConnected: PropTypes.func.isRequired,
  onCancel: PropTypes.func
};
