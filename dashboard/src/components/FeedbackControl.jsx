import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { MessageSquare } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { actionOf, actionsFor, feedbackByTarget } from '../utils/feedback.js';
import ErrorState from './ErrorState.jsx';
import FormField from './FormField.jsx';
import Modal from './Modal.jsx';

// Edit the "why" of a rating, or retract it. feedback is the current row.
export function FeedbackReasonModal({ feedback, onClose, onSaved, onRetracted }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [reason, setReason] = useState(feedback.reason || '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function run(fn) {
    setBusy(true);
    setError(null);
    try {
      await fn();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      size="small"
      title={t('feedbackControl.whyTitle')}
      onClose={() => !busy && onClose()}
      footer={
        <>
          {onRetracted ? (
            <button
              type="button"
              className="button danger"
              disabled={busy}
              onClick={() =>
                run(async () => {
                  await apiClient.retractFeedback(feedback.id);
                  onRetracted(feedback);
                })
              }
            >
              {t('feedbackControl.retract')}
            </button>
          ) : null}
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" disabled={busy} onClick={() => run(async () => onSaved(await apiClient.editFeedback(feedback.id, reason.trim())))}>
            {busy ? t('common.saving') : t('common.save')}
          </button>
        </>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <p className="muted small">{t('feedbackControl.ratedAs', { rating: t('feedbackControl.actions.' + (actionOf(feedback) || 'none')) })}</p>
      <FormField id={'why-' + feedback.id} label={t('feedbackControl.why')} help={t('feedbackControl.whyHelp')}>
        <textarea id={'why-' + feedback.id} rows={4} value={reason} maxLength={2000} onChange={(e) => setReason(e.target.value)} />
      </FormField>
    </Modal>
  );
}

FeedbackReasonModal.propTypes = { feedback: PropTypes.object.isRequired, onClose: PropTypes.func.isRequired, onSaved: PropTypes.func.isRequired, onRetracted: PropTypes.func };

// Segmented rating plus a "why" box (plan section 25.5). Rating posts at once; Why opens the reason editor for the
// current rating. Pass current (a feedback row or null) when the caller already loaded ratings, or leave it
// undefined and the control reads the current rating itself.
export default function FeedbackControl({ targetType, targetId, actions = null, current, onChange, compact = false, label = null }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [mine, setMine] = useState(current === undefined ? undefined : current);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(false);
  const list = actions || actionsFor(targetType);

  useEffect(() => {
    if (current !== undefined) setMine(current);
  }, [current]);

  useEffect(() => {
    if (current !== undefined || !targetId) return undefined;
    let live = true;
    apiClient
      .listFeedback({ targetType, targetId, pageSize: 1 })
      .then((r) => live && setMine((r && r.objects && r.objects[0]) || null))
      .catch(() => live && setMine(null));
    return () => {
      live = false;
    };
  }, [apiClient, targetType, targetId, current]);

  const changed = useCallback(
    (next) => {
      setMine(next);
      if (onChange) onChange(next, targetId);
    },
    [onChange, targetId]
  );

  async function rate(action) {
    setBusy(true);
    setError(null);
    try {
      changed(await apiClient.giveFeedback({ targetType, targetId, action, reason: mine && actionOf(mine) === action ? mine.reason : null }));
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const active = actionOf(mine);
  const groupLabel = label || t('feedbackControl.rate');
  return (
    <span className={'feedback-control' + (compact ? ' compact' : '')} data-ignore-row-click="true">
      <span className="segmented" role="group" aria-label={groupLabel}>
        {list.map((a) => (
          <button type="button" key={a} className={active === a ? 'active tone-' + a : ''} aria-pressed={active === a} disabled={busy || mine === undefined} onClick={() => rate(a)}>
            {t('feedbackControl.actions.' + a)}
          </button>
        ))}
      </span>
      {mine ? (
        <button
          type="button"
          className={'icon-button feedback-why' + (mine.reason ? ' has-reason' : '')}
          onClick={() => setEditing(true)}
          aria-label={mine.reason ? t('feedbackControl.whyEdit') : t('feedbackControl.whyAdd')}
          title={mine.reason ? mine.reason : t('feedbackControl.whyAdd')}
        >
          <MessageSquare size={14} aria-hidden="true" />
          {!compact ? <span>{mine.reason ? t('feedbackControl.whyEdit') : t('feedbackControl.whyAdd')}</span> : null}
        </button>
      ) : null}
      {error ? (
        <span className="field-error small" role="alert">
          {error.message || t('errors.generic')}
        </span>
      ) : null}
      {editing && mine ? (
        <FeedbackReasonModal
          feedback={mine}
          onClose={() => setEditing(false)}
          onSaved={(saved) => {
            setEditing(false);
            changed(saved);
          }}
          onRetracted={() => {
            setEditing(false);
            changed(null);
          }}
        />
      ) : null}
    </span>
  );
}

FeedbackControl.propTypes = {
  targetType: PropTypes.string.isRequired,
  targetId: PropTypes.string.isRequired,
  actions: PropTypes.arrayOf(PropTypes.string),
  current: PropTypes.object,
  onChange: PropTypes.func,
  compact: PropTypes.bool,
  label: PropTypes.string
};

// Current ratings for every row of one target type, loaded once per table. Returns [map | null, update].
export function useFeedbackMap(targetType, topicId = null) {
  const { apiClient } = useAuth();
  const [map, setMap] = useState(null);

  useEffect(() => {
    let live = true;
    apiClient
      .listFeedback({ targetType, topicId: topicId || undefined, pageSize: 1000 })
      .then((r) => {
        if (live) setMap(feedbackByTarget(r && r.objects));
      })
      .catch(() => live && setMap(new Map()));
    return () => {
      live = false;
    };
  }, [apiClient, targetType, topicId]);

  const update = useCallback((feedback, targetId) => {
    setMap((m) => {
      const next = new Map(m || []);
      if (feedback) next.set(targetId, feedback);
      else next.delete(targetId);
      return next;
    });
  }, []);

  return [map, update];
}
