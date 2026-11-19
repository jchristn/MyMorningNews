import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { ExternalLink } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { statusTone } from '../../components/StatusBadge.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

const PREVIEW = 4000;

// A fetched source: where it came from, when, how big, and the start of its extracted text.
export default function SourceModal({ sourceId, onClose }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [source, setSource] = useState(null);
  const [error, setError] = useState(null);

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
  }, [apiClient, sourceId]);

  const text = source && source.contentText ? source.contentText : '';
  return (
    <Modal open={Boolean(sourceId)} title={t('runs.source.title')} onClose={onClose} size="large">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {!source && !error ? <LoadingState /> : null}
      {source ? (
        <div className="stack" data-testid="source-detail">
          <strong>{source.title || source.canonicalUrl}</strong>
          <a href={source.canonicalUrl} target="_blank" rel="noopener noreferrer" className="evidence-source small">
            {source.canonicalUrl} <ExternalLink size={12} aria-hidden="true" />
          </a>
          <KeyValueGrid
            rows={[
              { key: 'domain', label: t('runs.source.domain'), value: source.domain },
              { key: 'http', label: t('runs.source.httpStatus'), value: source.httpStatus ? <StatusBadge tone={statusTone(source.httpStatus)} mono>{String(source.httpStatus)}</StatusBadge> : null },
              { key: 'type', label: t('runs.source.contentType'), value: source.contentType, mono: true },
              { key: 'size', label: t('runs.source.size'), value: f.bytes(source.contentBytes || 0) },
              { key: 'published', label: t('runs.source.published'), value: <DateCell value={source.publishedUtc} /> },
              { key: 'fetched', label: t('runs.source.fetched'), value: <DateCell value={source.fetchedUtc} /> },
              { key: 'seen', label: t('runs.source.timesSeen'), value: f.number(source.timesSeen || 0) }
            ]}
          />
          {text ? (
            <>
              <strong className="small">{t('runs.source.text')}</strong>
              <pre className="code-block source-text">{text.length > PREVIEW ? text.slice(0, PREVIEW) + '\n...' : text}</pre>
              {text.length > PREVIEW ? <span className="muted small">{t('runs.source.truncated', { shown: f.number(PREVIEW), total: f.number(text.length) })}</span> : null}
            </>
          ) : (
            <p className="muted">{t('runs.source.noText')}</p>
          )}
        </div>
      ) : null}
    </Modal>
  );
}

SourceModal.propTypes = {
  sourceId: PropTypes.string,
  onClose: PropTypes.func.isRequired
};
