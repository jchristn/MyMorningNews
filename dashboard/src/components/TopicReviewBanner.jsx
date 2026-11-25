import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { CalendarClock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import ConfirmModal from './ConfirmModal.jsx';

// Keep-or-delete prompts for the signed-in user's topics that found nothing new for a while
// (GET /v1.0/api/topic-reviews). Pass topicId to show only that topic's prompt. Renders nothing when there are none.
export default function TopicReviewBanner({ topicId = null, onDeleted, onChanged }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [reviews, setReviews] = useState([]);
  const [busy, setBusy] = useState(null);
  const [deleting, setDeleting] = useState(null);

  const load = useCallback(async () => {
    try {
      const page = await apiClient.listTopicReviews({ pageSize: 50 });
      const list = page && Array.isArray(page.objects) ? page.objects : [];
      setReviews(topicId ? list.filter((r) => r.topicId === topicId) : list);
    } catch {
      // A server without topic reviews, or a transient failure, simply shows no prompt.
      setReviews([]);
    }
  }, [apiClient, topicId]);

  useEffect(() => {
    load();
  }, [load]);

  async function keep(review) {
    setBusy(review.id);
    try {
      await apiClient.keepTopicReview(review.id);
      addNotification(t('topicReviews.kept', { name: review.topicName || t('topicReviews.untitled') }), 'success');
      await load();
      if (onChanged) onChanged();
    } catch (err) {
      addNotification(err && err.message ? err.message : t('topicReviews.failed'), 'error');
    } finally {
      setBusy(null);
    }
  }

  if (reviews.length === 0) return null;
  return (
    <>
      <div className="stack topic-reviews" role="region" aria-label={t('topicReviews.region')}>
        {reviews.map((r) => {
          const name = r.topicName || t('topicReviews.untitled');
          return (
            <div className="notice notice-warning topic-review" key={r.id} data-testid="topic-review">
              <CalendarClock size={18} aria-hidden="true" />
              <div className="topic-review-text">
                <strong>{t('topicReviews.title', { name })}</strong>
                <span>
                  {r.lastNewFindingUtc
                    ? t('topicReviews.since', { date: f.date(r.lastNewFindingUtc) })
                    : t('topicReviews.noneIn', { days: r.staleDays })}{' '}
                  {t('topicReviews.deadline', { date: f.date(r.deleteAfterUtc) })}
                </span>
              </div>
              <div className="row-actions">
                <button type="button" className="button primary small" disabled={busy === r.id} onClick={() => keep(r)}>
                  {t('topicReviews.keep')}
                </button>
                <button type="button" className="button danger small" disabled={busy === r.id} onClick={() => setDeleting(r)}>
                  {t('topicReviews.delete')}
                </button>
                {topicId ? (
                  <Link className="button secondary small" to={'/dashboard/topics/' + encodeURIComponent(r.topicId) + '/refine?start=1'}>
                    {t('topicReviews.refine')}
                  </Link>
                ) : (
                  <Link className="button secondary small" to={'/dashboard/topics/' + encodeURIComponent(r.topicId)}>
                    {t('topicReviews.open')}
                  </Link>
                )}
              </div>
            </div>
          );
        })}
      </div>
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('topicReviews.deleteTitle')}
        message={deleting ? t('topicReviews.deleteMessage', { name: deleting.topicName || t('topicReviews.untitled') }) : ''}
        confirmLabel={t('topicReviews.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          const target = deleting;
          await apiClient.deleteTopicReview(target.id);
          addNotification(t('topicReviews.deleted', { name: target.topicName || t('topicReviews.untitled') }), 'success');
          await load();
          if (onDeleted) onDeleted(target);
          else if (onChanged) onChanged();
        }}
      />
    </>
  );
}

TopicReviewBanner.propTypes = { topicId: PropTypes.string, onDeleted: PropTypes.func, onChanged: PropTypes.func };
