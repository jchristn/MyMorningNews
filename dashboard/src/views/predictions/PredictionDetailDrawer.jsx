import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Braces, Check, Gavel, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { PREDICTION_STATUS_TONES } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import ResolveModal from './ResolveModal.jsx';

export function PredictionStatusBadge({ status }) {
  const { t } = useTranslation();
  return <StatusBadge tone={PREDICTION_STATUS_TONES[status] || 'neutral'}>{t('enums.predictionStatus.' + status)}</StatusBadge>;
}

PredictionStatusBadge.propTypes = { status: PropTypes.string.isRequired };

export function isOverdue(prediction, now = Date.now()) {
  return Boolean(prediction && (prediction.status === 'Open' || prediction.status === 'ProposedResolved') && prediction.horizonUtc && new Date(prediction.horizonUtc).getTime() < now);
}

function ReviewItem({ review, busy, onDecide }) {
  const { t } = useTranslation();
  const f = useFormat();
  const decided = review.acceptedByUser === true || review.acceptedByUser === false;
  return (
    <li className="review-item" data-testid={'review-' + review.id}>
      <div className="review-header">
        <span className="row-actions">
          <span className="muted small">{t('predictions.reviews.proposes')}</span>
          <PredictionStatusBadge status={review.proposedStatus} />
          <span className="muted small">{t('predictions.reviews.confidence', { confidence: f.percent(review.confidence, 0) })}</span>
        </span>
        <DateCell value={review.createdUtc} />
      </div>
      {review.rationale ? <p className="finding-text">{review.rationale}</p> : null}
      {review.findingIds && review.findingIds.length > 0 ? (
        <span className="chip-list">
          {review.findingIds.map((id) => (
            <Link key={id} className="badge badge-info" to={'/dashboard/findings/' + encodeURIComponent(id)}>
              {t('predictions.reviews.finding', { id })}
            </Link>
          ))}
        </span>
      ) : null}
      <div className="review-item-actions">
        {decided ? (
          <StatusBadge tone={review.acceptedByUser ? 'success' : 'neutral'}>{review.acceptedByUser ? t('predictions.reviews.accepted') : t('predictions.reviews.rejected')}</StatusBadge>
        ) : (
          <>
            <button type="button" className="button primary small" disabled={busy} onClick={() => onDecide(review, true)}>
              <Check size={14} aria-hidden="true" /> {t('predictions.reviews.accept')}
            </button>
            <button type="button" className="button secondary small" disabled={busy} onClick={() => onDecide(review, false)}>
              <X size={14} aria-hidden="true" /> {t('predictions.reviews.reject')}
            </button>
          </>
        )}
      </div>
    </li>
  );
}

ReviewItem.propTypes = { review: PropTypes.object.isRequired, busy: PropTypes.bool, onDecide: PropTypes.func.isRequired };

// One prediction with its reviews. Accept or reject a proposed resolution, or resolve it by hand.
export default function PredictionDetailDrawer({ predictionId, topicLabel, onClose, onChanged }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [resolving, setResolving] = useState(false);
  const [json, setJson] = useState(false);

  const load = useCallback(async () => {
    if (!predictionId) return;
    setError(null);
    try {
      setDetail(await apiClient.getPrediction(predictionId));
    } catch (err) {
      setError(err);
    }
  }, [apiClient, predictionId]);

  useEffect(() => {
    setDetail(null);
    load();
  }, [load]);

  async function decide(review, accept) {
    setBusy(true);
    try {
      if (accept) await apiClient.acceptPredictionReview(review.id);
      else await apiClient.rejectPredictionReview(review.id);
      addNotification(accept ? t('predictions.reviews.acceptedToast') : t('predictions.reviews.rejectedToast'), 'success');
      await load();
      if (onChanged) onChanged();
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    } finally {
      setBusy(false);
    }
  }

  const p = detail ? detail.prediction : null;
  const reviews = detail ? detail.reviews || [] : [];
  return (
    <Modal
      open={Boolean(predictionId)}
      drawer
      title={t('predictions.detail.title')}
      subtitle={predictionId ? <CopyableId value={predictionId} /> : null}
      onClose={onClose}
      headerExtra={
        p ? (
          <div className="row-actions">
            <button type="button" className="button secondary small" onClick={() => setResolving(true)}>
              <Gavel size={14} aria-hidden="true" /> {p.resolvedUtc ? t('predictions.actions.reresolve') : t('predictions.actions.resolve')}
            </button>
            <button type="button" className="icon-button" onClick={() => setJson(true)} aria-label={t('common.viewJson')} title={t('common.viewJson')}>
              <Braces size={16} aria-hidden="true" />
            </button>
          </div>
        ) : null
      }
    >
      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
      {!detail && !error ? <LoadingState /> : null}
      {p ? (
        <div className="stack" data-testid="prediction-detail">
          <div className="result-grid">
            <PredictionStatusBadge status={p.status} />
            {isOverdue(p) ? <StatusBadge tone="warning">{t('predictions.overdue')}</StatusBadge> : null}
            {p.isDryRun ? <StatusBadge tone="info">{t('runs.dryRun')}</StatusBadge> : null}
          </div>
          <h3 className="finding-title">{p.statement}</h3>
          {p.rationale ? <p className="finding-text">{p.rationale}</p> : null}
          <KeyValueGrid
            rows={[
              { key: 'confidence', label: t('predictions.columns.confidence'), value: f.percent(p.confidence, 0) },
              { key: 'horizon', label: t('predictions.columns.horizon'), value: <DateCell value={p.horizonUtc} /> },
              {
                key: 'brier',
                label: t('predictions.columns.brier'),
                value:
                  p.brierScore === null || p.brierScore === undefined ? null : (
                    <span className="date-cell">
                      <strong>{f.number(p.brierScore, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}</strong>
                      <span className="muted small">{t('predictions.brierHelp')}</span>
                    </span>
                  )
              },
              { key: 'resolved', label: t('predictions.detail.resolved'), value: p.resolvedUtc ? <DateCell value={p.resolvedUtc} /> : null },
              { key: 'resolvedBy', label: t('predictions.detail.resolvedBy'), value: p.resolvedBy ? t('predictions.resolvedBy.' + p.resolvedBy, { defaultValue: p.resolvedBy }) : null },
              { key: 'note', label: t('predictions.detail.note'), value: p.resolutionNote },
              { key: 'topic', label: t('predictions.columns.topic'), value: <Link to={'/dashboard/topics/' + encodeURIComponent(p.topicId) + '?tab=predictions'}>{(topicLabel && topicLabel(p.topicId)) || p.topicId}</Link> },
              { key: 'run', label: t('findings.detail.run'), value: <Link to={'/dashboard/runs?run=' + encodeURIComponent(p.runId)}>{p.runId}</Link>, mono: true },
              { key: 'created', label: t('predictions.columns.created'), value: <DateCell value={p.createdUtc} /> }
            ]}
          />
          <section className="detail-section">
            <div className="detail-section-header">
              <strong>{t('predictions.reviews.title', { count: reviews.length })}</strong>
            </div>
            <div className="detail-section-body">
              {reviews.length > 0 ? (
                <ol className="review-list">
                  {reviews.map((r) => (
                    <ReviewItem key={r.id} review={r} busy={busy} onDecide={decide} />
                  ))}
                </ol>
              ) : (
                <p className="muted">{t('predictions.reviews.empty')}</p>
              )}
            </div>
          </section>
        </div>
      ) : null}
      <ResolveModal
        prediction={resolving ? p : null}
        onClose={() => setResolving(false)}
        onResolved={(updated) => {
          setDetail((d) => ({ ...d, prediction: updated }));
          addNotification(t('predictions.resolve.done'), 'success');
          if (onChanged) onChanged();
        }}
      />
      <JsonViewerModal open={json && Boolean(detail)} value={detail} onClose={() => setJson(false)} />
    </Modal>
  );
}

PredictionDetailDrawer.propTypes = {
  predictionId: PropTypes.string,
  topicLabel: PropTypes.func,
  onClose: PropTypes.func.isRequired,
  onChanged: PropTypes.func
};
