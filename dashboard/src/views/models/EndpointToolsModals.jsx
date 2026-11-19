import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { CircleCheck, CircleX, Play } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import CopyButton from '../../components/CopyButton.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

export const DEFAULT_TEST_PROMPT = 'Reply with the single word: ready';

function yesNo(value, t) {
  if (value === null || value === undefined) return <span className="muted">{t('models.unknown')}</span>;
  return value ? <StatusBadge tone="success">{t('common.yes')}</StatusBadge> : <StatusBadge tone="warning">{t('common.no')}</StatusBadge>;
}

// Sends a sample prompt and shows the reply, tokens, duration, or the provider error. The call is logged as a dry run.
export function TestModal({ endpoint, onClose }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [prompt, setPrompt] = useState(DEFAULT_TEST_PROMPT);
  const [model, setModel] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [running, setRunning] = useState(false);

  useEffect(() => {
    setPrompt(DEFAULT_TEST_PROMPT);
    setModel('');
    setResult(null);
    setError(null);
  }, [endpoint]);

  async function run() {
    setRunning(true);
    setError(null);
    setResult(null);
    try {
      setResult(await apiClient.testModelEndpoint(endpoint.id, { prompt, model: model.trim() || undefined }));
    } catch (err) {
      setError(err);
    } finally {
      setRunning(false);
    }
  }

  return (
    <Modal
      open={Boolean(endpoint)}
      title={t('models.test.title')}
      subtitle={endpoint ? endpoint.name : null}
      onClose={onClose}
      size="medium"
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose}>
            {t('common.close')}
          </button>
          <button type="button" className="button primary" onClick={run} disabled={running || !prompt.trim()}>
            <Play size={14} aria-hidden="true" /> {running ? t('common.working') : t('models.test.run')}
          </button>
        </>
      }
    >
      <div className="stack">
        <FormField id="test-prompt" label={t('models.test.prompt')}>
          <textarea id="test-prompt" rows={4} value={prompt} onChange={(e) => setPrompt(e.target.value)} />
        </FormField>
        <FormField id="test-model" label={t('models.test.model')} help={t('models.test.modelHelp')}>
          <input id="test-model" className="mono" value={model} onChange={(e) => setModel(e.target.value)} placeholder={endpoint ? endpoint.model || '' : ''} />
        </FormField>
        <ErrorState error={error} inline onDismiss={() => setError(null)} />
        {running ? <LoadingState label={t('models.test.running')} /> : null}
        {result ? (
          <div className="stack" data-testid="test-result">
            <div className="result-grid">
              <span>{result.success ? <StatusBadge tone="success">{t('models.test.succeeded')}</StatusBadge> : <StatusBadge tone="danger">{t('models.test.failed')}</StatusBadge>}</span>
              {result.model ? <span className="mono">{result.model}</span> : null}
              <span>{t('models.test.tokens', { input: f.number(result.inputTokens), output: f.number(result.outputTokens) })}</span>
              <span>{t('models.test.duration', { value: f.duration(result.durationMs) })}</span>
              {result.supportsTools !== null && result.supportsTools !== undefined ? <span>{t('models.test.tools', { value: result.supportsTools ? t('common.yes') : t('common.no') })}</span> : null}
            </div>
            {result.error ? <div className="notice notice-danger">{result.error}</div> : null}
            {result.text ? (
              <div>
                <div className="section-title">
                  <span className="field-label">{t('models.test.output')}</span>
                  <CopyButton value={result.text} />
                </div>
                <pre className="code-block output-block">{result.text}</pre>
              </div>
            ) : null}
          </div>
        ) : null}
      </div>
    </Modal>
  );
}

TestModal.propTypes = { endpoint: PropTypes.object, onClose: PropTypes.func.isRequired };

// Runs the connectivity, tool support, and embedding probes as soon as it opens.
export function ValidateModal({ endpoint, onClose, onDone }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const done = useRef(onDone);
  done.current = onDone;

  useEffect(() => {
    if (!endpoint) return undefined;
    let cancelled = false;
    setResult(null);
    setError(null);
    apiClient
      .validateModelEndpoint(endpoint.id)
      .then((r) => {
        if (cancelled) return;
        setResult(r);
        if (done.current) done.current(r);
      })
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, endpoint]);

  return (
    <Modal open={Boolean(endpoint)} title={t('models.validate.title')} subtitle={endpoint ? endpoint.name : null} onClose={onClose} size="medium">
      <ErrorState error={error} />
      {!result && !error ? <LoadingState label={t('models.validate.running')} /> : null}
      {result ? (
        <div className="stack" data-testid="validate-result">
          <KeyValueGrid
            rows={[
              { key: 'reachable', label: t('models.validate.reachable'), value: yesNo(result.reachable, t) },
              { key: 'modelFound', label: t('models.validate.modelFound'), value: yesNo(result.modelFound, t) },
              { key: 'tools', label: t('models.validate.supportsTools'), value: yesNo(result.supportsTools, t) },
              { key: 'dims', label: t('models.validate.embeddingDimensions'), value: result.embeddingDimensions ? f.number(result.embeddingDimensions) : null },
              { key: 'when', label: t('models.validate.validatedUtc'), value: f.dateTime(result.validatedUtc) }
            ]}
          />
          {result.messages && result.messages.length > 0 ? (
            <ul className="event-list">
              {result.messages.map((m, i) => (
                <li key={i}>{m}</li>
              ))}
            </ul>
          ) : null}
        </div>
      ) : null}
    </Modal>
  );
}

ValidateModal.propTypes = { endpoint: PropTypes.object, onClose: PropTypes.func.isRequired, onDone: PropTypes.func };

// The models the provider offers, with a one-click "use this model" for editors.
export function ModelsListModal({ endpoint, onClose, canWrite, onUse }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [models, setModels] = useState(null);
  const [error, setError] = useState(null);
  const [filter, setFilter] = useState('');
  const [busy, setBusy] = useState(null);
  const endpointId = endpoint ? endpoint.id : null;

  useEffect(() => {
    if (!endpointId) return undefined;
    let cancelled = false;
    setModels(null);
    setError(null);
    setFilter('');
    apiClient
      .listEndpointModels(endpointId)
      .then((r) => !cancelled && setModels(Array.isArray(r) ? r : []))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, endpointId]);

  const shown = (models || []).filter((m) => !filter || (m.name + ' ' + (m.displayName || '')).toLowerCase().includes(filter.toLowerCase()));

  async function use(name) {
    setBusy(name);
    try {
      await onUse(endpoint, name);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(null);
    }
  }

  return (
    <Modal open={Boolean(endpoint)} title={t('models.list.title')} subtitle={endpoint ? endpoint.name : null} onClose={onClose} size="large">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {!models && !error ? <LoadingState /> : null}
      {models ? (
        <div className="stack">
          <input type="search" value={filter} onChange={(e) => setFilter(e.target.value)} placeholder={t('models.list.filter')} aria-label={t('models.list.filter')} />
          {shown.length === 0 ? <p className="muted">{t('models.list.none')}</p> : null}
          {shown.length > 0 ? (
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th scope="col">{t('models.list.name')}</th>
                    <th scope="col" className="hide-narrow">
                      {t('models.list.inputLimit')}
                    </th>
                    <th scope="col" className="hide-narrow">
                      {t('models.list.outputLimit')}
                    </th>
                    <th scope="col">{t('models.list.tools')}</th>
                    <th scope="col">
                      <span className="sr-only">{t('table.actions')}</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {shown.map((m) => (
                    <tr key={m.name}>
                      <td>
                        <span className="date-cell">
                          <span className="mono">{m.name}</span>
                          {m.displayName && m.displayName !== m.name ? <span className="muted small">{m.displayName}</span> : null}
                        </span>
                      </td>
                      <td className="hide-narrow">{m.inputTokenLimit ? f.number(m.inputTokenLimit) : ''}</td>
                      <td className="hide-narrow">{m.outputTokenLimit ? f.number(m.outputTokenLimit) : ''}</td>
                      <td>{m.supportsTools === null || m.supportsTools === undefined ? '' : m.supportsTools ? <CircleCheck size={14} aria-label={t('common.yes')} /> : <CircleX size={14} aria-label={t('common.no')} />}</td>
                      <td className="align-right">
                        {endpoint.model === m.name ? (
                          <StatusBadge tone="accent">{t('models.list.current')}</StatusBadge>
                        ) : canWrite ? (
                          <button type="button" className="button secondary small" onClick={() => use(m.name)} disabled={Boolean(busy)}>
                            {busy === m.name ? t('common.working') : t('models.list.use')}
                          </button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      ) : null}
    </Modal>
  );
}

ModelsListModal.propTypes = { endpoint: PropTypes.object, onClose: PropTypes.func.isRequired, canWrite: PropTypes.bool, onUse: PropTypes.func.isRequired };
