import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { BadgeCheck, ExternalLink } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// The surrounding text split around the quote, so the quote can be highlighted in place. Returns null when the
// context does not contain the quote.
export function splitContext(context, quote) {
  if (!context || !quote) return null;
  const at = context.indexOf(quote);
  if (at < 0) return null;
  return { before: context.slice(0, at), quote, after: context.slice(at + quote.length) };
}

// One piece of evidence read fresh from GET /v1.0/api/evidence/{id}: the quote in its surrounding text, whether it
// was verified against the source, the source, and the quote's position in the source text.
export default function EvidenceModal({ evidenceId, role = null, onClose }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [view, setView] = useState(null);
  const [error, setError] = useState(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!evidenceId) return undefined;
    let cancelled = false;
    setView(null);
    setError(null);
    apiClient
      .getEvidence(evidenceId)
      .then((v) => !cancelled && setView(v))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, evidenceId, attempt]);

  const e = (view && view.evidence) || {};
  const parts = splitContext(e.contextText, e.quote);
  const label = view ? view.sourceTitle || view.sourceDomain || view.sourceUrl || t('findings.evidence.unknownSource') : '';
  return (
    <Modal open={Boolean(evidenceId)} title={t('findings.evidence.detailTitle')} subtitle={evidenceId ? <CopyableId value={evidenceId} /> : null} onClose={onClose} size="medium">
      <ErrorState error={error} inline onRetry={() => setAttempt((n) => n + 1)} onDismiss={() => setError(null)} />
      {!view && !error ? <LoadingState /> : null}
      {view ? (
        <div className="stack" data-testid="evidence-detail">
          <div className="result-grid">
            {role ? <StatusBadge>{t('enums.evidenceRole.' + role, { defaultValue: role })}</StatusBadge> : null}
            {e.verified ? (
              <StatusBadge tone="success">
                <BadgeCheck size={12} aria-hidden="true" /> {t('findings.evidence.verified')}
              </StatusBadge>
            ) : (
              <StatusBadge tone="warning">{t('findings.evidence.unverified')}</StatusBadge>
            )}
          </div>
          <section className="detail-section">
            <div className="detail-section-header">
              <strong>{parts ? t('findings.evidence.inContext') : t('findings.evidence.quote')}</strong>
            </div>
            <div className="detail-section-body">
              {parts ? (
                <p className="evidence-context">
                  {parts.before}
                  <mark>{parts.quote}</mark>
                  {parts.after}
                </p>
              ) : (
                <blockquote className="evidence-quote">{e.quote}</blockquote>
              )}
              {e.contextText && !parts ? <p className="evidence-context muted">{e.contextText}</p> : null}
              {!e.verified ? <p className="muted small">{t('findings.evidence.unverifiedHelp')}</p> : null}
            </div>
          </section>
          <KeyValueGrid
            rows={[
              {
                key: 'source',
                label: t('findings.evidence.source'),
                value: view.sourceUrl ? (
                  <a href={view.sourceUrl} target="_blank" rel="noopener noreferrer">
                    {label} <ExternalLink size={12} aria-hidden="true" />
                  </a>
                ) : (
                  label
                )
              },
              { key: 'domain', label: t('findings.evidence.domain'), value: view.sourceDomain, hidden: !view.sourceDomain },
              { key: 'published', label: t('findings.columns.published'), value: <DateCell value={view.sourcePublishedUtc} relative={false} />, hidden: !view.sourcePublishedUtc },
              {
                key: 'offsets',
                label: t('findings.evidence.position'),
                value: t('findings.evidence.positionValue', { start: f.number(e.startOffset || 0), end: f.number(e.endOffset || 0) }),
                hidden: !(e.endOffset > 0)
              },
              { key: 'run', label: t('findings.detail.run'), value: e.runId ? <CopyableId value={e.runId} /> : null, hidden: !e.runId },
              { key: 'sourceId', label: t('findings.evidence.sourceId'), value: e.sourceItemId ? <CopyableId value={e.sourceItemId} /> : null, hidden: !e.sourceItemId },
              { key: 'created', label: t('common.created'), value: <DateCell value={e.createdUtc} /> }
            ]}
          />
        </div>
      ) : null}
    </Modal>
  );
}

EvidenceModal.propTypes = {
  evidenceId: PropTypes.string,
  role: PropTypes.string,
  onClose: PropTypes.func.isRequired
};
