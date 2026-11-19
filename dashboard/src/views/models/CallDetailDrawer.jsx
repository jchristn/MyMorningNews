import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import CopyButton from '../../components/CopyButton.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

function Section({ title, children, extra = null }) {
  return (
    <section className="detail-section">
      <div className="detail-section-header">
        <strong>{title}</strong>
        {extra}
      </div>
      <div className="detail-section-body">{children}</div>
    </section>
  );
}

Section.propTypes = { title: PropTypes.string.isRequired, children: PropTypes.node, extra: PropTypes.node };

// Pretty-print JSON payloads; leave anything else as is.
export function formatPayload(text) {
  if (!text) return '';
  const trimmed = String(text).trim();
  if (!(trimmed.startsWith('{') || trimmed.startsWith('['))) return String(text);
  try {
    return JSON.stringify(JSON.parse(trimmed), null, 2);
  } catch {
    return String(text);
  }
}

// Every accounting field of one model call, plus the prompt and response payloads (cleared after the retention window).
export default function CallDetailDrawer({ callId, onClose, endpointLabel, userLabel }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [call, setCall] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!callId) return undefined;
    let cancelled = false;
    setCall(null);
    setError(null);
    apiClient
      .getLlmCall(callId)
      .then((c) => !cancelled && setCall(c))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, callId]);

  const id = (v) => (v ? <CopyableId value={v} /> : null);
  const n = (v) => (v === null || v === undefined ? null : f.number(v));
  const ms = (v) => (v === null || v === undefined ? null : f.duration(v));
  const usd = (v) => (v === null || v === undefined ? null : f.usd(v));

  return (
    <Modal open={Boolean(callId)} drawer title={t('models.calls.detailTitle')} subtitle={callId ? <CopyableId value={callId} /> : null} onClose={onClose}>
      <ErrorState error={error} />
      {!call && !error ? <LoadingState /> : null}
      {call ? (
        <div className="stack" data-testid="call-detail">
          <div className="result-grid">
            <span>{call.success ? <StatusBadge tone="success">{t('models.calls.succeeded')}</StatusBadge> : <StatusBadge tone="danger">{t('models.calls.failed')}</StatusBadge>}</span>
            {call.isDryRun ? <StatusBadge tone="info">{t('models.calls.dryRun')}</StatusBadge> : null}
            <span>{t('enums.task.' + call.task)}</span>
            <span className="mono">{call.model || ''}</span>
            <span>{f.dateTime(call.createdUtc)}</span>
          </div>
          {call.errorMessage ? <div className="notice notice-danger">{call.errorMessage}</div> : null}
          <Section title={t('models.calls.sections.attribution')}>
            <KeyValueGrid
              rows={[
                { key: 'provider', label: t('models.calls.fields.provider'), value: call.provider },
                { key: 'endpoint', label: t('models.calls.fields.endpoint'), value: call.modelEndpointId ? endpointLabel(call.modelEndpointId) : null },
                { key: 'user', label: t('models.calls.fields.user'), value: call.userId ? userLabel(call.userId) : t('models.calls.system') },
                { key: 'topic', label: t('models.calls.fields.topic'), value: id(call.topicId) },
                { key: 'run', label: t('models.calls.fields.run'), value: id(call.runId) },
                { key: 'session', label: t('models.calls.fields.refinementSession'), value: id(call.refinementSessionId) },
                { key: 'briefing', label: t('models.calls.fields.briefing'), value: id(call.briefingId) },
                { key: 'promptKey', label: t('models.calls.fields.promptKey'), value: call.promptKey, mono: true },
                { key: 'promptVersion', label: t('models.calls.fields.promptVersion'), value: id(call.promptTemplateVersionId) }
              ]}
            />
          </Section>
          <Section title={t('models.calls.sections.tokens')}>
            <KeyValueGrid
              rows={[
                { key: 'input', label: t('models.calls.fields.inputTokens'), value: n(call.inputTokens) },
                { key: 'uncached', label: t('models.calls.fields.uncachedInputTokens'), value: n(Math.max(0, (call.inputTokens || 0) - (call.cachedInputTokens || 0))) },
                { key: 'cached', label: t('models.calls.fields.cachedInputTokens'), value: n(call.cachedInputTokens) },
                { key: 'cacheWrite', label: t('models.calls.fields.cacheWriteTokens'), value: n(call.cacheWriteTokens) },
                { key: 'output', label: t('models.calls.fields.outputTokens'), value: n(call.outputTokens) },
                { key: 'reasoning', label: t('models.calls.fields.reasoningTokens'), value: n(call.reasoningTokens) },
                { key: 'total', label: t('models.calls.fields.totalTokens'), value: n(call.totalTokens) }
              ]}
            />
          </Section>
          <Section title={t('models.calls.sections.timing')}>
            <KeyValueGrid
              rows={[
                { key: 'duration', label: t('models.calls.fields.duration'), value: ms(call.durationMs) },
                { key: 'ttft', label: t('models.calls.fields.timeToFirstToken'), value: ms(call.timeToFirstTokenMs) },
                { key: 'streaming', label: t('models.calls.fields.streamingDuration'), value: ms(call.streamingDurationMs) },
                { key: 'chunks', label: t('models.calls.fields.chunks'), value: n(call.chunkCount) },
                { key: 'tps', label: t('models.calls.fields.tokensPerSecond'), value: call.outputTokensPerSecond === null || call.outputTokensPerSecond === undefined ? null : f.number(call.outputTokensPerSecond, { maximumFractionDigits: 1 }) },
                { key: 'streamed', label: t('models.calls.fields.streamed'), value: call.streamed ? t('common.yes') : t('common.no') },
                { key: 'attempts', label: t('models.calls.fields.attempts'), value: n(call.attempts) }
              ]}
            />
          </Section>
          <Section title={t('models.calls.sections.outcome')}>
            <KeyValueGrid
              rows={[
                { key: 'status', label: t('models.calls.fields.statusCode'), value: call.statusCode ? String(call.statusCode) : null },
                { key: 'finish', label: t('models.calls.fields.finishReason'), value: call.finishReason, mono: true },
                { key: 'repairs', label: t('models.calls.fields.repairs'), value: n(call.structuredOutputRepairs) },
                { key: 'tools', label: t('models.calls.fields.toolCalls'), value: n(call.toolCallCount) },
                { key: 'toolNames', label: t('models.calls.fields.toolNames'), value: call.toolNames && call.toolNames.length > 0 ? call.toolNames.join(', ') : null, mono: true },
                { key: 'dry', label: t('models.calls.fields.dryRun'), value: call.isDryRun ? t('common.yes') : t('common.no') }
              ]}
            />
          </Section>
          <Section title={t('models.calls.sections.cost')}>
            <KeyValueGrid
              rows={[
                { key: 'in', label: t('models.calls.fields.inputCost'), value: usd(call.inputCostUsd) },
                { key: 'out', label: t('models.calls.fields.outputCost'), value: usd(call.outputCostUsd) },
                { key: 'total', label: t('models.calls.fields.estimatedCost'), value: usd(call.estimatedCostUsd) }
              ]}
            />
          </Section>
          <Section title={t('models.calls.sections.prompt')} extra={call.promptText ? <CopyButton value={call.promptText} /> : null}>
            {call.promptText ? <pre className="code-block">{formatPayload(call.promptText)}</pre> : <p className="muted small">{t('models.calls.payloadCleared')}</p>}
          </Section>
          <Section title={t('models.calls.sections.response')} extra={call.responseText ? <CopyButton value={call.responseText} /> : null}>
            {call.responseText ? <pre className="code-block">{formatPayload(call.responseText)}</pre> : <p className="muted small">{t('models.calls.payloadCleared')}</p>}
          </Section>
          {call.promptKey ? (
            <Link to={'/dashboard/prompts?key=' + encodeURIComponent(call.promptKey)} onClick={onClose}>
              {t('models.calls.openPrompt', { key: call.promptKey })}
            </Link>
          ) : null}
        </div>
      ) : null}
    </Modal>
  );
}

CallDetailDrawer.propTypes = {
  callId: PropTypes.string,
  onClose: PropTypes.func.isRequired,
  endpointLabel: PropTypes.func.isRequired,
  userLabel: PropTypes.func.isRequired
};
