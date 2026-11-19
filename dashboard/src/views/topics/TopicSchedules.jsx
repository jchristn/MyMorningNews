import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { CalendarPlus, Pencil, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { useApp } from '../../context/AppContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { defaultScheduleForm, formFromSchedule, scheduleBody } from '../../utils/schedule.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import ScheduleEditor, { describeSchedule } from '../../components/ScheduleEditor.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// The research schedules of a topic: a list with enable or disable, edit, and delete, plus an editor with the live
// next-runs preview. single shows just one editor (the first schedule, or a new one), for the wizard.
export default function TopicSchedules({ topicId, single = false, onSaved, disabled = false, controlRef }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [schedules, setSchedules] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(defaultScheduleForm());
  const [preview, setPreview] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [deleting, setDeleting] = useState(null);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const result = await apiClient.listSchedules({ ownerType: 'Topic', ownerId: topicId, pageSize: 50, sortBy: 'createdUtc', sortDirection: 'Ascending' });
      const items = (result && result.items) || [];
      setSchedules(items);
      if (single) {
        setEditing(items[0] ? { mode: 'edit', id: items[0].id } : { mode: 'create' });
        setForm(items[0] ? formFromSchedule(items[0]) : defaultScheduleForm());
      }
    } catch (err) {
      setLoadError(err);
    }
  }, [apiClient, topicId, single]);

  useEffect(() => {
    load();
  }, [load]);

  function open(schedule) {
    setError(null);
    setEditing(schedule ? { mode: 'edit', id: schedule.id } : { mode: 'create' });
    setForm(schedule ? formFromSchedule(schedule) : defaultScheduleForm());
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      const body = scheduleBody(form);
      const saved = editing.mode === 'create' ? await apiClient.createSchedule({ ...body, ownerType: 'Topic', ownerId: topicId }) : await apiClient.updateSchedule(editing.id, body);
      setSchedules((list) => [...(list || []).filter((s) => s.id !== saved.id), saved]);
      addNotification(t('schedule.saved'), 'success');
      if (single) setEditing({ mode: 'edit', id: saved.id });
      else setEditing(null);
      if (onSaved) onSaved(saved);
      return saved;
    } catch (err) {
      setError(err);
      return null;
    } finally {
      setSaving(false);
    }
  }

  // The wizard saves the single schedule when the reader moves on from the Delivery step.
  useEffect(() => {
    if (!controlRef) return undefined;
    controlRef.current = { save: () => (editing ? save() : Promise.resolve(null)) };
    return () => {
      controlRef.current = null;
    };
  });

  async function toggle(schedule) {
    setError(null);
    try {
      const saved = await apiClient.updateSchedule(schedule.id, { enabled: !schedule.enabled });
      setSchedules((list) => list.map((s) => (s.id === saved.id ? saved : s)));
    } catch (err) {
      setError(err);
    }
  }

  if (loadError) return <ErrorState error={loadError} onRetry={load} />;
  if (!schedules) return <LoadingState />;

  const editor = editing ? (
    <div className="schedule-edit-panel" data-testid="schedule-edit-panel">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <ScheduleEditor value={form} onChange={setForm} idPrefix={'schedule-' + (editing.id || 'new')} disabled={disabled} onPreview={setPreview} />
      <div className="form-actions">
        {!single ? (
          <button type="button" className="button secondary" onClick={() => setEditing(null)}>
            {t('common.cancel')}
          </button>
        ) : null}
        <button type="button" className="button primary" disabled={disabled || saving || (preview && !preview.valid)} onClick={save}>
          {saving ? t('common.saving') : editing.mode === 'create' ? t('schedule.create') : t('schedule.save')}
        </button>
      </div>
    </div>
  ) : null;

  if (single) return editor;

  return (
    <div className="stack">
      {!editing ? <ErrorState error={error} inline onDismiss={() => setError(null)} /> : null}
      {schedules.length === 0 && !editing ? (
        <EmptyState
          title={t('schedule.emptyTitle')}
          message={t('schedule.emptyMessage')}
          action={
            <button type="button" className="button primary small" disabled={disabled} onClick={() => open(null)}>
              <CalendarPlus size={14} aria-hidden="true" /> {t('schedule.add')}
            </button>
          }
        />
      ) : null}
      {schedules.length > 0 ? (
        <ul className="schedule-list">
          {schedules.map((s) => (
            <li key={s.id} className={'schedule-item' + (editing && editing.id === s.id ? ' selected' : '')}>
              <div className="schedule-item-main">
                <strong>{s.name || describeSchedule(s, t, f)}</strong>
                {s.name ? <span className="muted small">{describeSchedule(s, t, f)}</span> : null}
                <span className="muted small">
                  {s.timeZone} {' · '} {t('enums.missedRunPolicy.' + s.missedRunPolicy)}
                </span>
              </div>
              <div className="schedule-item-meta">
                <span className="small">{t('schedule.nextRun')}</span>
                {s.enabled && s.nextRunUtc ? <DateCell value={s.nextRunUtc} /> : <span className="muted small">{t('schedule.noNextRun')}</span>}
              </div>
              <div className="schedule-item-meta">
                <span className="small">{t('schedule.lastRun')}</span>
                <DateCell value={s.lastRunUtc} />
              </div>
              <div className="row-actions">
                <label className="checkbox-row small">
                  <input type="checkbox" checked={s.enabled} disabled={disabled} onChange={() => toggle(s)} aria-label={t('schedule.enableFor', { name: s.name || describeSchedule(s, t, f) })} />
                  <StatusBadge tone={s.enabled ? 'success' : 'neutral'}>{s.enabled ? t('schedule.enabledOn') : t('schedule.enabledOff')}</StatusBadge>
                </label>
                <button type="button" className="icon-button" disabled={disabled} aria-label={t('schedule.edit')} title={t('schedule.edit')} onClick={() => open(s)}>
                  <Pencil size={14} aria-hidden="true" />
                </button>
                <button type="button" className="icon-button" disabled={disabled} aria-label={t('schedule.delete')} title={t('schedule.delete')} onClick={() => setDeleting(s)}>
                  <Trash2 size={14} aria-hidden="true" />
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
      {schedules.length > 0 && !editing ? (
        <div>
          <button type="button" className="button secondary small" disabled={disabled} onClick={() => open(null)}>
            <CalendarPlus size={14} aria-hidden="true" /> {t('schedule.add')}
          </button>
        </div>
      ) : null}
      {editor}
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('schedule.deleteTitle')}
        message={deleting ? t('schedule.deleteMessage', { name: deleting.name || describeSchedule(deleting, t, f) }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteSchedule(deleting.id);
          setSchedules((list) => list.filter((s) => s.id !== deleting.id));
          if (editing && editing.id === deleting.id) setEditing(null);
        }}
      />
    </div>
  );
}

TopicSchedules.propTypes = {
  topicId: PropTypes.string.isRequired,
  single: PropTypes.bool,
  onSaved: PropTypes.func,
  disabled: PropTypes.bool,
  controlRef: PropTypes.object
};
