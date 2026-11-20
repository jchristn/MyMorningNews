import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { ACTION_TONES, actionOf, targetPath } from '../../utils/feedback.js';
import { fieldErrors } from '../../utils/errors.js';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

export const PREFERENCE_STATUSES = ['Proposed', 'Active', 'Rejected', 'Disabled'];
export const PREFERENCE_ORIGINS = ['Distilled', 'User'];
export const PREFERENCE_KINDS = ['Prefer', 'Avoid', 'PreferSource', 'AvoidSource', 'StyleNote'];
export const PREFERENCE_STATUS_TONES = { Proposed: 'warning', Active: 'success', Rejected: 'neutral', Disabled: 'neutral' };

// The status actions that apply to a preference: accept or reject a proposal, disable an active one, and enable a
// disabled or rejected one.
export function preferenceActions(p) {
  if (!p) return [];
  if (p.status === 'Proposed') return ['accept', 'reject'];
  if (p.status === 'Active') return ['disable'];
  return ['enable'];
}

export function PreferenceStatusBadge({ status }) {
  const { t } = useTranslation();
  return <StatusBadge tone={PREFERENCE_STATUS_TONES[status] || 'neutral'}>{t('insights.preferences.statuses.' + status, { defaultValue: status })}</StatusBadge>;
}

PreferenceStatusBadge.propTypes = { status: PropTypes.string };

// Write your own preference (active at once) or edit one. preference is null to create.
export function PreferenceFormModal({ preference = null, topics, defaultTopicId = '', onClose, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [form, setForm] = useState({ statement: preference ? preference.statement : '', kind: preference ? preference.kind : 'Prefer', topicId: preference ? preference.topicId || '' : defaultTopicId });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const errors = fieldErrors(error);

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const body = { statement: form.statement.trim(), kind: form.kind, topicId: form.topicId || null };
      onSaved(preference ? await apiClient.updatePreference(preference.id, body) : await apiClient.createPreference(body));
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      title={preference ? t('insights.preferences.editTitle') : t('insights.preferences.createTitle')}
      onClose={() => !busy && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={save} disabled={busy || form.statement.trim().length < 3}>
            {busy ? t('common.saving') : t('common.save')}
          </button>
        </>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {!preference ? <p className="muted small">{t('insights.preferences.createHelp')}</p> : null}
      <div className="form-grid">
        <FormField id="pref-statement" label={t('insights.preferences.statement')} help={t('insights.preferences.statementHelp')} error={errors.statement} wide>
          <textarea id="pref-statement" rows={3} maxLength={1000} value={form.statement} onChange={(e) => setForm({ ...form, statement: e.target.value })} />
        </FormField>
        <FormField id="pref-kind" label={t('insights.preferences.kind')}>
          <select id="pref-kind" value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>
            {PREFERENCE_KINDS.map((k) => (
              <option key={k} value={k}>
                {t('insights.preferences.kinds.' + k)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="pref-topic" label={t('insights.scope.topic')} help={t('insights.preferences.topicHelp')}>
          <select id="pref-topic" value={form.topicId} onChange={(e) => setForm({ ...form, topicId: e.target.value })}>
            <option value="">{t('insights.preferences.allTopics')}</option>
            {topics.map((x) => (
              <option key={x.id} value={x.id}>
                {x.name}
              </option>
            ))}
          </select>
        </FormField>
      </div>
    </Modal>
  );
}

PreferenceFormModal.propTypes = { preference: PropTypes.object, topics: PropTypes.array.isRequired, defaultTopicId: PropTypes.string, onClose: PropTypes.func.isRequired, onSaved: PropTypes.func.isRequired };

// A preference with the feedback that supports it, and its status actions.
export function PreferenceDetailModal({ preferenceId, topics, refreshSignal = 0, onClose, onAction, onEdit }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    let live = true;
    setError(null);
    apiClient
      .getPreference(preferenceId)
      .then((d) => live && setDetail(d))
      .catch((err) => live && setError(err));
    return () => {
      live = false;
    };
  }, [apiClient, preferenceId, refreshSignal]);

  const p = detail ? detail.preference : null;
  const support = (detail && detail.support) || [];
  const topicName = p && p.topicId ? (topics.find((x) => x.id === p.topicId) || {}).name || p.topicId : t('insights.preferences.allTopics');

  return (
    <Modal
      open
      drawer
      title={t('insights.preferences.detailTitle')}
      onClose={onClose}
      footer={
        p ? (
          <div className="row-actions">
            {preferenceActions(p).map((a) => (
              <button type="button" key={a} className={a === 'accept' || a === 'enable' ? 'button primary small' : 'button secondary small'} onClick={() => onAction(p, a)}>
                {t('insights.preferences.actions.' + a)}
              </button>
            ))}
            <button type="button" className="button secondary small" onClick={() => onEdit(p)}>
              {t('common.edit')}
            </button>
          </div>
        ) : null
      }
    >
      <ErrorState error={error} inline />
      {!detail && !error ? <LoadingState /> : null}
      {p ? (
        <div className="stack">
          <h3 className="finding-title">{p.statement}</h3>
          <span className="row-actions">
            <PreferenceStatusBadge status={p.status} />
            <StatusBadge>{t('insights.preferences.kinds.' + p.kind, { defaultValue: p.kind })}</StatusBadge>
            <StatusBadge tone="info">{t('insights.preferences.origins.' + p.origin, { defaultValue: p.origin })}</StatusBadge>
          </span>
          <KeyValueGrid
            rows={[
              { key: 'topic', label: t('insights.scope.topic'), value: p.topicId ? <Link to={'/dashboard/topics/' + encodeURIComponent(p.topicId) + '?tab=preferences'}>{topicName}</Link> : topicName },
              { key: 'confidence', label: t('insights.preferences.confidence'), value: f.percent(p.confidence || 0, 0) },
              { key: 'support', label: t('insights.preferences.supportCount'), value: f.number(p.supportCount || 0) },
              { key: 'decided', label: t('insights.preferences.decided'), value: p.decidedUtc ? <DateCell value={p.decidedUtc} /> : null },
              { key: 'created', label: t('common.created'), value: <DateCell value={p.createdUtc} /> }
            ]}
          />
          <h4>{t('insights.preferences.supportTitle', { count: support.length })}</h4>
          {support.length === 0 ? (
            <p className="muted small">{t('insights.preferences.noSupport')}</p>
          ) : (
            <ul className="preference-support">
              {support.map((fb) => {
                const action = actionOf(fb);
                const path = targetPath(fb.targetType, fb.targetId);
                return (
                  <li key={fb.id}>
                    <span className="row-actions">
                      {action ? <StatusBadge tone={ACTION_TONES[action]}>{t('feedbackControl.actions.' + action)}</StatusBadge> : null}
                      {path ? <Link to={path}>{t('insights.feedback.targetTypes.' + fb.targetType, { defaultValue: fb.targetType })}</Link> : <span>{fb.targetType}</span>}
                      <DateCell value={fb.createdUtc} relative={false} />
                    </span>
                    {fb.reason ? <span className="feedback-reason">{fb.reason}</span> : <span className="muted small">{t('insights.feedback.noReason')}</span>}
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      ) : null}
    </Modal>
  );
}

PreferenceDetailModal.propTypes = { preferenceId: PropTypes.string.isRequired, refreshSignal: PropTypes.number, topics: PropTypes.array.isRequired, onClose: PropTypes.func.isRequired, onAction: PropTypes.func.isRequired, onEdit: PropTypes.func.isRequired };
