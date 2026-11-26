import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { ArrowDown, ArrowUp, Eraser, Save, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { TOPIC_STATUS_TONES } from '../../utils/enums.js';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { moveItem } from '../editions/EditionTopicPicker.jsx';

export const TOPIC_SLOTS_ANCHOR = 'topic-slots';

// True when a TopicSlotsResponse should be shown: the account has custom topics and a finite limit.
export function slotsRelevant(slots) {
  return Boolean(slots) && Array.isArray(slots.topics) && slots.topics.length > 0 && slots.limit !== null && slots.limit !== undefined;
}

// Whether the topic at index keeps running under limit (null means unlimited).
export function withinLimit(index, limit) {
  return limit === null || limit === undefined || index < Number(limit);
}

// "Topics that keep running" (plan section 8.4): the account's custom topics in keep order against the plan's limit.
// Topics past the limit keep their history but do not run, and creating, duplicating, or restoring a topic stays
// blocked until enough are archived or the plan grows. Reordering uses Move up and Move down buttons (no drag), and
// the markers follow the order as edited. Renders nothing for unlimited plans or accounts without custom topics.
export default function TopicSlotsPanel() {
  const { t } = useTranslation();
  const f = useFormat();
  const location = useLocation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [slots, setSlots] = useState(null);
  const [order, setOrder] = useState([]);
  const [error, setError] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [loaded, setLoaded] = useState(false);
  const [saving, setSaving] = useState(false);
  const panelRef = useRef(null);

  const apply = useCallback((value) => {
    const next = value && Array.isArray(value.topics) ? value : null;
    setSlots(next);
    setOrder(
      next
        ? next.topics
            .slice()
            .sort((a, b) => (a.position ?? 0) - (b.position ?? 0))
            .map((x) => x.topicId)
        : []
    );
  }, []);

  const load = useCallback(async () => {
    setError(null);
    try {
      apply(await apiClient.getTopicSlots());
    } catch (err) {
      setError(err);
    } finally {
      setLoaded(true);
    }
  }, [apiClient, apply]);

  useEffect(() => {
    load();
  }, [load]);

  const relevant = slotsRelevant(slots);
  const over = relevant ? Number(slots.overLimit) || 0 : 0;

  // Opened from a link to #topic-slots (the Topics list banner): bring the panel into view once it renders.
  useEffect(() => {
    if (relevant && location.hash === '#' + TOPIC_SLOTS_ANCHOR && panelRef.current && panelRef.current.scrollIntoView) {
      panelRef.current.scrollIntoView({ block: 'start' });
    }
  }, [relevant, location.hash]);

  const byId = useMemo(() => new Map(((slots && slots.topics) || []).map((x) => [x.topicId, x])), [slots]);
  const saved = useMemo(
    () =>
      ((slots && slots.topics) || [])
        .slice()
        .sort((a, b) => (a.position ?? 0) - (b.position ?? 0))
        .map((x) => x.topicId),
    [slots]
  );
  const dirty = order.length !== saved.length || order.some((id, i) => id !== saved[i]);

  async function submit(ids, message) {
    setSaving(true);
    setSaveError(null);
    try {
      apply(await apiClient.saveTopicSlots(ids));
      addNotification(message, 'success');
    } catch (err) {
      setSaveError(err);
    } finally {
      setSaving(false);
    }
  }

  if (!loaded) return <LoadingState />;
  // A server without topic slots answers 404; that is not worth an error on the billing page.
  if (error) return error.status === 404 ? null : <ErrorState error={error} inline onRetry={load} />;
  if (!relevant) return null;

  const limit = Number(slots.limit);

  return (
    <section
      ref={panelRef}
      id={TOPIC_SLOTS_ANCHOR}
      className={'panel stack topic-slots' + (over > 0 ? ' topic-slots-over' : '')}
      aria-labelledby="topic-slots-title"
      tabIndex={-1}
      data-testid="topic-slots"
    >
      <div className="section-title">
        <h2 id="topic-slots-title">{t('topicSlots.title')}</h2>
        <StatusBadge tone={over > 0 ? 'warning' : 'success'}>
          {t('topicSlots.count', { counted: f.number(slots.counted ?? order.length), limit: f.number(limit) })}
        </StatusBadge>
      </div>
      {over > 0 ? (
        <div className="notice notice-warning" role="status">
          <strong>{t('topicSlots.overTitle', { count: over })}</strong> {t('topicSlots.overHelp')}
        </div>
      ) : (
        <p className="muted small">{limit === 0 ? t('topicSlots.noneAllowed') : t('topicSlots.help', { limit })}</p>
      )}
      <p className="muted small">{t('topicSlots.orderHelp')}</p>
      <ol className="ordered-picker topic-slot-list" aria-label={t('topicSlots.listLabel')}>
        {order.map((id, i) => {
          const topic = byId.get(id);
          const name = topic ? topic.name : id;
          const within = withinLimit(i, limit);
          return (
            <li key={id} className={within ? undefined : 'past-limit'}>
              <span className="ordered-index muted small">{f.number(i + 1)}</span>
              <span className="topic-slot-name">
                <Link to={'/dashboard/topics/' + encodeURIComponent(id)} className="truncate" dir="auto">
                  {name}
                </Link>
                <span className="muted small">
                  {topic && topic.status ? t('enums.topicStatus.' + topic.status) : null}
                  {topic && topic.lastRunUtc ? ' · ' + t('topicSlots.lastRun', { date: f.date(topic.lastRunUtc) }) : ''}
                </span>
              </span>
              <StatusBadge tone={within ? TOPIC_STATUS_TONES.Active : 'warning'}>
                {within ? t('topicSlots.within') : t('topicSlots.past')}
              </StatusBadge>
              <span className="row-actions">
                <button
                  type="button"
                  className="icon-button touch-target"
                  disabled={saving || i === 0}
                  onClick={() => setOrder((l) => moveItem(l, i, -1))}
                  aria-label={t('editions.topics.moveUp', { name })}
                  title={t('editions.topics.moveUp', { name })}
                >
                  <ArrowUp size={14} aria-hidden="true" />
                </button>
                <button
                  type="button"
                  className="icon-button touch-target"
                  disabled={saving || i === order.length - 1}
                  onClick={() => setOrder((l) => moveItem(l, i, 1))}
                  aria-label={t('editions.topics.moveDown', { name })}
                  title={t('editions.topics.moveDown', { name })}
                >
                  <ArrowDown size={14} aria-hidden="true" />
                </button>
              </span>
            </li>
          );
        })}
      </ol>
      {dirty ? (
        <p className="small muted" role="status">
          {t('topicSlots.unsaved')}
        </p>
      ) : null}
      <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
      <div className="form-actions">
        {dirty ? (
          <button type="button" className="button secondary" disabled={saving} onClick={() => setOrder(saved)}>
            <Undo2 size={14} aria-hidden="true" /> {t('editions.discard')}
          </button>
        ) : null}
        <button
          type="button"
          className="button secondary"
          disabled={saving || !slots.customized}
          onClick={() => submit([], t('topicSlots.cleared'))}
        >
          <Eraser size={14} aria-hidden="true" /> {t('topicSlots.clear')}
        </button>
        <button type="button" className="button primary" disabled={saving || !dirty} onClick={() => submit(order, t('topicSlots.saved'))}>
          <Save size={14} aria-hidden="true" /> {saving ? t('common.saving') : t('topicSlots.save')}
        </button>
      </div>
      <p className="muted small">{slots.customized ? t('topicSlots.customized') : t('topicSlots.defaultOrder')}</p>
    </section>
  );
}
