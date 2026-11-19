import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { FlaskConical, Play } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import CopyButton from '../../components/CopyButton.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

function Block({ title, text }) {
  const { t } = useTranslation();
  return (
    <div>
      <div className="section-title">
        <span className="field-label">{title}</span>
        {text ? <CopyButton value={text} /> : null}
      </div>
      {text ? <pre className="code-block output-block">{text}</pre> : <p className="muted small">{t('prompts.lab.emptySection')}</p>}
    </div>
  );
}

Block.propTypes = { title: PropTypes.string.isRequired, text: PropTypes.string };

// Dry-run a prompt (saved or draft) against any chat endpoint. The server logs the call as a dry run.
export default function PromptLab({ template, variables, draft, dirty, endpoints, canReadCalls }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [endpointId, setEndpointId] = useState('');
  const [model, setModel] = useState('');
  const [values, setValues] = useState({});
  const [useDraft, setUseDraft] = useState(dirty);
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState(null);
  const [cost, setCost] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    setUseDraft(dirty);
  }, [dirty]);

  const chatEndpoints = endpoints.filter((e) => e.supportsChat);

  async function run() {
    setRunning(true);
    setError(null);
    setResult(null);
    setCost(null);
    try {
      const vars = {};
      for (const name of variables) if (values[name] !== undefined && values[name] !== '') vars[name] = values[name];
      const r = await apiClient.runPromptLab({
        key: template.key,
        content: useDraft && dirty ? draft : undefined,
        variables: vars,
        modelEndpointId: endpointId || undefined,
        model: model.trim() || undefined,
        topicId: template.topicId || undefined
      });
      setResult(r);
      if (r && r.llmCallId && canReadCalls) {
        apiClient
          .getLlmCall(r.llmCallId)
          .then(setCost)
          .catch(() => setCost(null));
      }
    } catch (err) {
      setError(err);
    } finally {
      setRunning(false);
    }
  }

  return (
    <div className="stack" data-testid="prompt-lab">
      <p className="muted small">
        <FlaskConical size={14} aria-hidden="true" /> {t('prompts.lab.intro')}
      </p>
      <div className="form-grid">
        <FormField id="lab-endpoint" label={t('prompts.lab.endpoint')}>
          <select id="lab-endpoint" value={endpointId} onChange={(e) => setEndpointId(e.target.value)}>
            <option value="">{t('prompts.lab.assigned', { role: t('enums.task.' + template.role) })}</option>
            {chatEndpoints.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name + (e.model ? ' (' + e.model + ')' : '')}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="lab-model" label={t('prompts.lab.model')} help={t('prompts.lab.modelHelp')}>
          <input id="lab-model" className="mono" value={model} onChange={(e) => setModel(e.target.value)} autoComplete="off" />
        </FormField>
      </div>
      {variables.length > 0 ? (
        <fieldset className="form-field wide">
          <legend className="field-label">{t('prompts.lab.variables')}</legend>
          <div className="stack">
            {variables.map((name) => (
              <FormField key={name} id={'lab-var-' + name} label={name}>
                <textarea id={'lab-var-' + name} rows={2} value={values[name] || ''} onChange={(e) => setValues((v) => ({ ...v, [name]: e.target.value }))} />
              </FormField>
            ))}
          </div>
        </fieldset>
      ) : (
        <p className="muted small">{t('prompts.lab.noVariables')}</p>
      )}
      {dirty ? (
        <label className="checkbox-row">
          <input type="checkbox" checked={useDraft} onChange={(e) => setUseDraft(e.target.checked)} />
          <span>{t('prompts.lab.useDraft')}</span>
        </label>
      ) : null}
      <div className="form-actions">
        <button type="button" className="button primary" onClick={run} disabled={running}>
          <Play size={14} aria-hidden="true" /> {running ? t('common.working') : t('prompts.lab.run')}
        </button>
      </div>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {running ? <LoadingState label={t('prompts.lab.running')} /> : null}
      {result ? (
        <div className="stack" data-testid="lab-result">
          <div className="result-grid">
            <span>{result.success ? <StatusBadge tone="success">{t('models.test.succeeded')}</StatusBadge> : <StatusBadge tone="danger">{t('models.test.failed')}</StatusBadge>}</span>
            {result.model ? <span className="mono">{result.model}</span> : null}
            <span>{t('models.test.tokens', { input: f.number(result.inputTokens), output: f.number(result.outputTokens) })}</span>
            <span>{t('models.test.duration', { value: f.duration(result.durationMs) })}</span>
            <span>{t('prompts.lab.cost', { value: cost && cost.estimatedCostUsd !== null && cost.estimatedCostUsd !== undefined ? f.usd(cost.estimatedCostUsd) : t('prompts.lab.costUnknown') })}</span>
          </div>
          {result.unknownVariables && result.unknownVariables.length > 0 ? <div className="notice notice-warning">{t('prompts.lab.unknownVariables', { names: result.unknownVariables.join(', ') })}</div> : null}
          {result.error ? <div className="notice notice-danger">{result.error}</div> : null}
          <p className="muted small">{t('prompts.lab.guardrailNote')}</p>
          <Block title={t('prompts.lab.renderedSystem')} text={result.renderedSystem} />
          <Block title={t('prompts.lab.renderedUser')} text={result.renderedUser} />
          {result.success ? <Block title={t('prompts.lab.output')} text={result.text} /> : null}
          {result.llmCallId ? (
            <span className="row-actions">
              <span className="muted small">{t('prompts.lab.loggedAs')}</span>
              <CopyableId value={result.llmCallId} />
              {canReadCalls ? <Link to={'/dashboard/models?tab=calls&call=' + encodeURIComponent(result.llmCallId)}>{t('prompts.lab.openCall')}</Link> : null}
            </span>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

PromptLab.propTypes = {
  template: PropTypes.object.isRequired,
  variables: PropTypes.arrayOf(PropTypes.string).isRequired,
  draft: PropTypes.string,
  dirty: PropTypes.bool,
  endpoints: PropTypes.array.isRequired,
  canReadCalls: PropTypes.bool
};
