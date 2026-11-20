import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, ExternalLink } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useLocalStorage from '../../hooks/useLocalStorage.js';
import CopyButton from '../../components/CopyButton.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge, { statusTone } from '../../components/StatusBadge.jsx';

// One fetched source in a side drawer: where it came from, when, its size and type, the run that first saw it, and
// its full extracted text (scrollable, with an optional monospace view and a copy control).
export default function SourceDetailDrawer({ sourceId, onClose }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [source, setSource] = useState(null);
  const [error, setError] = useState(null);
  const [attempt, setAttempt] = useState(0);
  const [json, setJson] = useState(false);
  const [mono, setMono] = useLocalStorage('mymorningnews.sources.mono', false);

  useEffect(() => {
    if (!sourceId) return undefined;
    let cancelled = false;
    setSource(null);
    setError(null);
    apiClient
      .getSource(sourceId)
      .then((s) => !cancelled && setSource(s))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, sourceId, attempt]);

  const text = source && source.contentText ? source.contentText : '';
  return (
    <Modal
      open={Boolean(sourceId)}
      drawer
      title={t('sources.detail.title')}
      subtitle={sourceId ? <CopyableId value={sourceId} /> : null}
      onClose={onClose}
      headerExtra={
        <button type="button" className="icon-button" onClick={() => setJson(true)} disabled={!source} aria-label={t('common.viewJson')} title={t('common.viewJson')}>
          <Braces size={16} aria-hidden="true" />
        </button>
      }
    >
      <ErrorState error={error} inline onRetry={() => setAttempt((n) => n + 1)} onDismiss={() => setError(null)} />
      {!source && !error ? <LoadingState /> : null}
      {source ? (
        <div className="stack" data-testid="source-drawer">
          <h3 className="finding-title">{source.title || source.canonicalUrl}</h3>
          <div className="row-actions">
            <a href={source.canonicalUrl} target="_blank" rel="noopener noreferrer" className="button secondary small">
              <ExternalLink size={14} aria-hidden="true" /> {t('sources.detail.open')}
            </a>
            <CopyButton value={source.canonicalUrl} labelKey="common.copyUrl" />
          </div>
          <span className="muted small mono truncate">{source.canonicalUrl}</span>
          <KeyValueGrid
            rows={[
              { key: 'domain', label: t('runs.source.domain'), value: source.domain },
              { key: 'http', label: t('runs.source.httpStatus'), value: source.httpStatus ? <StatusBadge tone={statusTone(source.httpStatus)} mono>{String(source.httpStatus)}</StatusBadge> : null },
              { key: 'type', label: t('runs.source.contentType'), value: source.contentType, mono: true },
              { key: 'size', label: t('runs.source.size'), value: f.bytes(source.contentBytes || 0) },
              { key: 'published', label: t('runs.source.published'), value: <DateCell value={source.publishedUtc} /> },
              { key: 'fetched', label: t('runs.source.fetched'), value: <DateCell value={source.fetchedUtc} /> },
              { key: 'seen', label: t('runs.source.timesSeen'), value: f.number(source.timesSeen || 0) },
              {
                key: 'run',
                label: t('sources.columns.firstRun'),
                value: source.firstSeenRunId ? <Link to={'/dashboard/runs?run=' + encodeURIComponent(source.firstSeenRunId)}>{source.firstSeenRunId}</Link> : null,
                mono: true
              }
            ]}
          />
          <section className="detail-section">
            <div className="detail-section-header">
              <strong>{t('runs.source.text')}</strong>
              {text ? (
                <span className="row-actions">
                  <label className="checkbox-row small">
                    <input type="checkbox" checked={Boolean(mono)} onChange={(e) => setMono(e.target.checked)} />
                    {t('sources.detail.monospace')}
                  </label>
                  <CopyButton value={text} labelKey="sources.detail.copyText" />
                </span>
              ) : null}
            </div>
            <div className="detail-section-body stack">
              {text ? (
                <>
                  <span className="muted small">{t('sources.detail.characters', { count: text.length, formatted: f.number(text.length) })}</span>
                  <pre className={'code-block source-full-text' + (mono ? ' mono' : '')} data-testid="source-text" tabIndex={0}>
                    {text}
                  </pre>
                </>
              ) : (
                <p className="muted">{t('runs.source.noText')}</p>
              )}
            </div>
          </section>
        </div>
      ) : null}
      <JsonViewerModal open={json && Boolean(source)} value={source} onClose={() => setJson(false)} />
    </Modal>
  );
}

SourceDetailDrawer.propTypes = {
  sourceId: PropTypes.string,
  onClose: PropTypes.func.isRequired
};
