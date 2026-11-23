import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { ListTree } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { MODEL_PROVIDERS, PROVIDER_URL_EXAMPLES, REASONING_EFFORTS, VAULT_KIND_FOR_METHOD } from '../../utils/enums.js';
import { authMethodsFor, effectiveAuthMethod, isAgentCli, needsSecret } from '../../utils/modelAuth.js';
import SecretInput from '../../components/modelSignIn/SecretInput.jsx';
import SignInMethodPicker from '../../components/modelSignIn/SignInMethodPicker.jsx';
import SignInSteps from '../../components/modelSignIn/SignInSteps.jsx';
import { DEFAULT_SERVER_CONTAINER } from '../../components/modelSignIn/signInCatalog.js';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';

const NUMBER_FIELDS = ['maxTokens', 'contextLength', 'timeoutMs', 'temperature', 'inputCostPerMTok', 'outputCostPerMTok', 'cachedInputCostPerMTok', 'cacheWriteCostPerMTok'];

function str(v) {
  return v === null || v === undefined ? '' : String(v);
}

export function formFromEndpoint(endpoint, copyName) {
  const e = endpoint || {};
  return {
    name: copyName || e.name || '',
    provider: e.provider || 'Anthropic',
    authMethod: endpoint ? effectiveAuthMethod(e) : 'ApiKey',
    endpoint: e.endpoint || '',
    keyMode: e.credentialId ? 'vault' : endpoint ? 'none' : 'new',
    credentialId: e.credentialId || '',
    apiKey: '',
    model: e.model || '',
    embeddingModel: e.embeddingModel || '',
    supportsChat: endpoint ? Boolean(e.supportsChat) : true,
    supportsEmbeddings: Boolean(e.supportsEmbeddings),
    maxTokens: str(endpoint ? e.maxTokens : 16000),
    reasoningEffort: e.reasoningEffort || '',
    temperature: str(e.temperature),
    contextLength: str(e.contextLength),
    timeoutMs: str(endpoint ? e.timeoutMs : 300000),
    inputCostPerMTok: str(e.inputCostPerMTok),
    outputCostPerMTok: str(e.outputCostPerMTok),
    cachedInputCostPerMTok: str(e.cachedInputCostPerMTok),
    cacheWriteCostPerMTok: str(e.cacheWriteCostPerMTok),
    active: endpoint ? Boolean(e.active) : true
  };
}

// The form after choosing a provider: its recommended sign-in method, and for an agent CLI chat only, with no URL.
export function withProvider(form, provider) {
  const methods = authMethodsFor(provider);
  const next = { ...form, provider, authMethod: methods[0], apiKey: '', temperature: provider === 'Anthropic' || isAgentCli(provider) ? '' : form.temperature };
  if (isAgentCli(provider)) return { ...next, endpoint: '', supportsChat: true, supportsEmbeddings: false, embeddingModel: '' };
  return next;
}

// Client-side checks mirroring the server's rules, keyed by field.
export function validateEndpointForm(form, t) {
  const errors = {};
  const cli = isAgentCli(form.provider);
  if (!form.name.trim()) errors.name = t('models.form.errors.name');
  if (form.provider === 'OpenAiCompatible' && !form.endpoint.trim()) errors.endpoint = t('models.form.errors.endpointRequired');
  if (!cli && form.endpoint.trim() && !/^[a-z][a-z0-9+.-]*:\/\/\S+$/i.test(form.endpoint.trim())) errors.endpoint = t('models.form.errors.endpointUrl');
  if (form.supportsChat && !cli && !form.model.trim()) errors.model = t('models.form.errors.model');
  if (form.supportsEmbeddings && !cli && !form.embeddingModel.trim()) errors.embeddingModel = t('models.form.errors.embeddingModel');
  if (!form.supportsChat && !form.supportsEmbeddings) errors.supportsChat = t('models.form.errors.capability');
  if (needsSecret(form.authMethod) && form.keyMode === 'vault' && !form.credentialId) errors.credentialId = t('models.form.errors.credential');
  for (const key of NUMBER_FIELDS) {
    if (form[key] !== '' && (Number.isNaN(Number(form[key])) || Number(form[key]) < 0)) errors[key] = t('models.form.errors.number');
  }
  return errors;
}

// Optional values the server clears to null only when named in clearFields (an omitted value means "unchanged").
const CLEARABLE_FIELDS = ['temperature', 'reasoningEffort', 'contextLength', 'inputCostPerMTok', 'outputCostPerMTok', 'cachedInputCostPerMTok', 'cacheWriteCostPerMTok'];

function hasValue(v) {
  return v !== null && v !== undefined && v !== '';
}

// The request body for create and update. Empty optional fields are omitted so the server keeps its value; on an
// update, an optional field the endpoint had a value for and the form now leaves empty is named in clearFields so the
// server sets it back to null. An agent CLI endpoint has no URL, no embeddings, and no temperature; apiKey carries
// whatever secret the sign-in method needs (an API key, a subscription token, or a sign-in file's contents).
export function endpointBody(form, original) {
  const cli = isAgentCli(form.provider);
  const body = {
    name: form.name.trim(),
    provider: form.provider,
    authMethod: form.authMethod,
    endpoint: cli ? '' : form.endpoint.trim(),
    model: form.model.trim(),
    embeddingModel: cli ? '' : form.embeddingModel.trim(),
    supportsChat: cli ? true : form.supportsChat,
    supportsEmbeddings: cli ? false : form.supportsEmbeddings,
    active: form.active
  };
  if (form.reasoningEffort) body.reasoningEffort = form.reasoningEffort;
  for (const key of NUMBER_FIELDS) {
    if (key === 'temperature' && (form.provider === 'Anthropic' || cli)) continue;
    if (form[key] !== '') body[key] = Number(form[key]);
  }
  if (!needsSecret(form.authMethod)) body.credentialId = '';
  else if (form.keyMode === 'new' && form.apiKey.trim()) body.apiKey = form.apiKey.trim();
  else if (form.keyMode === 'vault') body.credentialId = form.credentialId;
  else if (form.keyMode === 'none') body.credentialId = '';
  if (original) {
    const clearFields = CLEARABLE_FIELDS.filter((key) => hasValue(original[key]) && !hasValue(body[key]) && form[key] === '');
    if (clearFields.length > 0) body.clearFields = clearFields;
  }
  return body;
}

export default function EndpointFormModal({ state, onClose, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [form, setForm] = useState(() => formFromEndpoint(null));
  const [touched, setTouched] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [credentials, setCredentials] = useState([]);
  const [modelOptions, setModelOptions] = useState(null);
  const [modelsError, setModelsError] = useState(null);
  const [loadingModels, setLoadingModels] = useState(false);
  const [setupOptions, setSetupOptions] = useState(null);
  const open = Boolean(state);
  const isEdit = Boolean(state && state.mode === 'edit');

  useEffect(() => {
    if (!state) return;
    setForm(formFromEndpoint(state.endpoint, state.mode === 'duplicate' ? t('admin.copyOf', { name: state.endpoint.name }) : null));
    setTouched(false);
    setSaveError(null);
    setModelOptions(null);
    setModelsError(null);
    apiClient
      .listVaultCredentials({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => setCredentials(r.items || []))
      .catch(() => setCredentials([]));
    // Which CLIs the server can run, and its container name for the sign-in commands. Optional: the editor works
    // without it.
    Promise.resolve()
      .then(() => apiClient.getSetupOptions())
      .then((r) => setSetupOptions(r && Array.isArray(r.providers) ? r : null))
      .catch(() => setSetupOptions(null));
  }, [state, apiClient, t]);

  const errors = touched ? validateEndpointForm(form, t) : {};
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const cli = isAgentCli(form.provider);
  const noTemperature = form.provider === 'Anthropic' || cli;
  const methods = authMethodsFor(form.provider);
  const secretMethod = needsSecret(form.authMethod);
  // Stored credentials of the kind the method stores (an API key, a bearer token, or a sign-in file).
  const wantedKind = VAULT_KIND_FOR_METHOD[form.authMethod];
  const usableCredentials = credentials.filter((c) => !wantedKind || !c.kind || c.kind === wantedKind || c.id === form.credentialId);
  const providerOption = setupOptions ? setupOptions.providers.find((o) => o.provider === form.provider) : null;
  const unavailable =
    providerOption && providerOption.available === false
      ? providerOption.unavailableReason || (providerOption.turnedOff ? t('setup.model.turnedOff') : t('setup.model.notInstalled'))
      : null;
  const container = setupOptions && setupOptions.serverContainer ? setupOptions.serverContainer : DEFAULT_SERVER_CONTAINER;

  async function browseModels() {
    if (!state || !state.endpoint) return;
    setLoadingModels(true);
    setModelsError(null);
    try {
      const list = await apiClient.listEndpointModels(state.endpoint.id);
      setModelOptions(Array.isArray(list) ? list : []);
    } catch (err) {
      setModelsError(err);
    } finally {
      setLoadingModels(false);
    }
  }

  async function save() {
    setTouched(true);
    if (Object.keys(validateEndpointForm(form, t)).length > 0) return;
    setSaving(true);
    setSaveError(null);
    try {
      const body = endpointBody(form, isEdit ? state.endpoint : null);
      const saved = isEdit ? await apiClient.updateModelEndpoint(state.endpoint.id, body) : await apiClient.createModelEndpoint(body);
      onSaved(saved, isEdit);
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  const number = (key, label, help, step = 'any') => (
    <FormField id={'ep-' + key} label={label} help={help} error={errors[key]}>
      <input id={'ep-' + key} type="number" min={0} step={step} value={form[key]} onChange={(e) => set({ [key]: e.target.value })} />
    </FormField>
  );

  return (
    <Modal
      open={open}
      size="large"
      title={isEdit ? t('models.form.editTitle') : state && state.mode === 'duplicate' ? t('models.form.duplicateTitle') : t('models.form.createTitle')}
      subtitle={isEdit ? <CopyableId value={state.endpoint.id} /> : null}
      onClose={() => !saving && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={save} disabled={saving}>
            {saving ? t('common.saving') : t('common.save')}
          </button>
        </>
      }
    >
      <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
      <div className="stack">
        <div className="form-grid">
          <FormField id="ep-name" label={t('models.columns.name')} error={errors.name}>
            <input id="ep-name" value={form.name} onChange={(e) => set({ name: e.target.value })} autoComplete="off" required />
          </FormField>
          <FormField id="ep-provider" label={t('models.columns.provider')}>
            <select id="ep-provider" value={form.provider} onChange={(e) => setForm((f) => withProvider(f, e.target.value))}>
              {MODEL_PROVIDERS.map((p) => (
                <option key={p} value={p}>
                  {t('enums.provider.' + p)}
                </option>
              ))}
            </select>
          </FormField>
          {cli ? null : (
            <FormField
              id="ep-endpoint"
              label={form.provider === 'OpenAiCompatible' ? t('models.form.endpointRequired') : t('models.form.endpoint')}
              help={t('models.form.endpointHelp.' + form.provider)}
              error={errors.endpoint}
              wide
            >
              <input id="ep-endpoint" type="url" className="mono" value={form.endpoint} onChange={(e) => set({ endpoint: e.target.value })} placeholder={PROVIDER_URL_EXAMPLES[form.provider]} autoComplete="off" />
            </FormField>
          )}
        </div>
        {cli ? <div className="notice notice-info small">{t('models.form.cliNotice')}</div> : null}
        {unavailable ? (
          <div className="notice notice-warning small" role="alert">
            {unavailable}
          </div>
        ) : null}

        <fieldset className="form-field wide endpoint-signin">
          <legend className="field-label">{t('models.form.signIn')}</legend>
          {methods.length > 1 ? <SignInMethodPicker name="ep-method" provider={form.provider} methods={methods} value={form.authMethod} onChange={(m) => set({ authMethod: m, apiKey: '' })} /> : null}
          {secretMethod ? (
            <div className="segmented" role="radiogroup" aria-label={t('models.form.secretSource')}>
              {['new', 'vault', 'none'].map((mode) => (
                <button type="button" role="radio" key={mode} aria-checked={form.keyMode === mode} className={form.keyMode === mode ? 'active' : ''} onClick={() => set({ keyMode: mode })}>
                  {t('models.form.keyModes.' + mode)}
                </button>
              ))}
            </div>
          ) : null}
          {secretMethod && form.keyMode === 'new' ? (
            <SecretInput key={form.provider + ':' + form.authMethod} id="ep-apikey" provider={form.provider} method={form.authMethod} value={form.apiKey} onChange={(v) => set({ apiKey: v })} help={t('models.form.newKeyHelp')} />
          ) : null}
          {secretMethod && form.keyMode === 'vault' ? (
            <FormField id="ep-credential" label={t('models.form.storedKey')} help={t('models.form.storedKeyHelp')} error={errors.credentialId}>
              <select id="ep-credential" value={form.credentialId} onChange={(e) => set({ credentialId: e.target.value })}>
                <option value="">{t('models.form.pickCredential')}</option>
                {usableCredentials.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name + ' (' + c.secretPreview + ')'}
                  </option>
                ))}
                {form.credentialId && !credentials.some((c) => c.id === form.credentialId) ? <option value={form.credentialId}>{form.credentialId}</option> : null}
              </select>
            </FormField>
          ) : null}
          {secretMethod && form.keyMode === 'none' ? <span className="field-help muted">{t('models.form.noKeyWarning')}</span> : null}
          {form.authMethod !== 'None' ? (
            <details className="endpoint-signin-help">
              <summary>{t('models.form.howTo')}</summary>
              <SignInSteps provider={form.provider} method={form.authMethod} container={container} />
            </details>
          ) : null}
        </fieldset>

        {cli ? null : (
          <fieldset className="form-field wide">
            <legend className="field-label">{t('models.form.capabilities')}</legend>
            <div className="checkbox-group">
              <label className="checkbox-row">
                <input type="checkbox" checked={form.supportsChat} onChange={(e) => set({ supportsChat: e.target.checked })} />
                <span>{t('models.capabilities.chat')}</span>
              </label>
              <label className="checkbox-row">
                <input type="checkbox" checked={form.supportsEmbeddings} onChange={(e) => set({ supportsEmbeddings: e.target.checked })} />
                <span>{t('models.capabilities.embeddings')}</span>
              </label>
            </div>
            {errors.supportsChat ? (
              <span className="field-error" role="alert">
                {errors.supportsChat}
              </span>
            ) : (
              <span className="field-help muted">{t('models.form.toolsDetected')}</span>
            )}
          </fieldset>
        )}

        <div className="form-grid">
          {form.supportsChat ? (
            <FormField id="ep-model" label={t('models.columns.model')} error={errors.model} help={cli ? t('setup.field.modelHelpCli') : undefined}>
              <div className="input-with-button">
                <input id="ep-model" className="mono" list="ep-model-options" value={form.model} onChange={(e) => set({ model: e.target.value })} autoComplete="off" />
                {isEdit ? (
                  <button type="button" className="icon-button" onClick={browseModels} aria-label={t('models.actions.listModels')} title={t('models.actions.listModels')} disabled={loadingModels}>
                    <ListTree size={16} aria-hidden="true" />
                  </button>
                ) : null}
              </div>
              <datalist id="ep-model-options">
                {(modelOptions || []).map((m) => (
                  <option key={m.name} value={m.name}>
                    {m.displayName || m.name}
                  </option>
                ))}
              </datalist>
            </FormField>
          ) : null}
          {form.supportsEmbeddings ? (
            <FormField id="ep-embedding" label={t('models.columns.embeddingModel')} error={errors.embeddingModel}>
              <input id="ep-embedding" className="mono" value={form.embeddingModel} onChange={(e) => set({ embeddingModel: e.target.value })} autoComplete="off" />
            </FormField>
          ) : null}
          {form.supportsEmbeddings ? (
            <FormField id="ep-dimensions" label={t('models.form.embeddingDimensions')} help={t('models.form.embeddingDimensionsHelp')}>
              <input id="ep-dimensions" value={state && state.endpoint && state.endpoint.embeddingDimensions ? String(state.endpoint.embeddingDimensions) : ''} readOnly placeholder={t('models.form.notDetected')} />
            </FormField>
          ) : null}
          {number('maxTokens', t('models.form.maxTokens'), t('models.form.maxTokensHelp'), 1)}
          <FormField id="ep-effort" label={t('models.form.reasoningEffort')} help={t('models.form.reasoningEffortHelp')}>
            <select id="ep-effort" value={form.reasoningEffort} onChange={(e) => set({ reasoningEffort: e.target.value })}>
              <option value="">{t('models.form.providerDefault')}</option>
              {REASONING_EFFORTS.map((r) => (
                <option key={r} value={r}>
                  {t('enums.reasoningEffort.' + r)}
                </option>
              ))}
            </select>
          </FormField>
          {cli ? null : (
            <FormField id="ep-temperature" label={t('models.form.temperature')} help={noTemperature ? t('models.form.temperatureAnthropic') : t('models.form.temperatureHelp')} error={errors.temperature}>
              <input id="ep-temperature" type="number" min={0} max={2} step={0.1} value={noTemperature ? '' : form.temperature} disabled={noTemperature} onChange={(e) => set({ temperature: e.target.value })} />
            </FormField>
          )}
          {number('contextLength', t('models.form.contextLength'), null, 1)}
          {number('timeoutMs', t('models.form.timeoutMs'), t('models.form.timeoutHelp'), 1000)}
        </div>
        {modelsError ? <ErrorState error={modelsError} inline onDismiss={() => setModelsError(null)} /> : null}
        {modelOptions ? <span className="muted small">{t('models.form.modelsLoaded', { count: modelOptions.length })}</span> : null}

        <fieldset className="form-field wide">
          <legend className="field-label">{t('models.form.pricing')}</legend>
          <span className="field-help muted">{t('models.form.pricingHelp')}</span>
          {isEdit ? <span className="field-help muted">{t('models.form.clearHint')}</span> : null}
          <div className="form-grid compact">
            {number('inputCostPerMTok', t('models.form.inputCost'))}
            {number('outputCostPerMTok', t('models.form.outputCost'))}
            {number('cachedInputCostPerMTok', t('models.form.cachedInputCost'), t('models.form.cachedInputCostHelp'))}
            {number('cacheWriteCostPerMTok', t('models.form.cacheWriteCost'), t('models.form.cacheWriteCostHelp'))}
          </div>
        </fieldset>

        <FormField id="ep-active" label={t('common.active')}>
          <input id="ep-active" type="checkbox" checked={form.active} onChange={(e) => set({ active: e.target.checked })} />
        </FormField>
      </div>
    </Modal>
  );
}

EndpointFormModal.propTypes = {
  state: PropTypes.shape({ mode: PropTypes.oneOf(['create', 'edit', 'duplicate']).isRequired, endpoint: PropTypes.object }),
  onClose: PropTypes.func.isRequired,
  onSaved: PropTypes.func.isRequired
};
