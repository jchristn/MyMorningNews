import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Check, Eye, Pencil, Plus, Power, PowerOff, Sparkles, Trash2, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { pagedQuery } from '../../utils/api.js';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import {
  PREFERENCE_ORIGINS,
  PREFERENCE_STATUSES,
  PreferenceDetailModal,
  PreferenceFormModal,
  PreferenceStatusBadge,
  preferenceActions
} from './PreferenceModals.jsx';

const ACTION_ICONS = { accept: Check, reject: X, disable: PowerOff, enable: Power };

// Run a learning pass for a topic now: tags reasons and distills new proposals from recent feedback.
export function LearnNowButton({ topicId, onLearned }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [busy, setBusy] = useState(false);

  async function learn() {
    setBusy(true);
    try {
      const r = await apiClient.learnTopic(topicId);
      addNotification(t('insights.preferences.learned', { count: (r && r.proposed) || 0 }), 'success', 8000);
      if (onLearned) onLearned(r);
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    } finally {
      setBusy(false);
    }
  }

  return (
    <button type="button" className="button secondary small" onClick={learn} disabled={busy || !topicId}>
      <Sparkles size={14} aria-hidden="true" /> {busy ? t('insights.preferences.learning') : t('insights.preferences.learnNow')}
    </button>
  );
}

LearnNowButton.propTypes = { topicId: PropTypes.string, onLearned: PropTypes.func };

// Learned preferences with status, origin, and topic filters; accept, reject, disable, enable, edit, and delete;
// write your own; and a detail drawer with the supporting feedback (initialPreferenceId opens one).
export default function PreferencesTable({ topicId = null, topics = [], tableKey = 'preferences', initialPreferenceId = null, onPreferenceClosed }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [openId, setOpenId] = useState(initialPreferenceId);
  const [editing, setEditing] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);
  const topicNames = useMemo(() => new Map(topics.map((x) => [x.id, x.name])), [topics]);
  const extraFilters = useMemo(() => ({ topicId: topicId || undefined }), [topicId]);
  const fetchPage = useCallback((q) => apiClient.listPreferences(pagedQuery(q)), [apiClient]);

  useEffect(() => {
    if (initialPreferenceId) setOpenId(initialPreferenceId);
  }, [initialPreferenceId]);

  async function act(p, action) {
    try {
      await apiClient.setPreferenceStatus(p.id, action);
      addNotification(t('insights.preferences.changed.' + action), 'success');
      bump();
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    }
  }

  function close() {
    setOpenId(null);
    if (onPreferenceClosed) onPreferenceClosed();
  }

  const columns = [
    {
      key: 'statement',
      header: t('insights.preferences.statement'),
      pinned: true,
      filter: { type: 'text', param: 'search' },
      render: (p) => <strong className="preference-statement">{p.statement}</strong>
    },
    { key: 'kind', header: t('insights.preferences.kind'), render: (p) => t('insights.preferences.kinds.' + p.kind, { defaultValue: p.kind }) },
    {
      key: 'status',
      header: t('common.status'),
      filter: { type: 'select', param: 'status', options: PREFERENCE_STATUSES.map((s) => ({ value: s, label: t('insights.preferences.statuses.' + s) })) },
      render: (p) => <PreferenceStatusBadge status={p.status} />
    },
    {
      key: 'origin',
      header: t('insights.preferences.origin'),
      filter: { type: 'select', param: 'origin', options: PREFERENCE_ORIGINS.map((s) => ({ value: s, label: t('insights.preferences.origins.' + s) })) },
      render: (p) => <StatusBadge tone={p.origin === 'User' ? 'accent' : 'info'}>{t('insights.preferences.origins.' + p.origin, { defaultValue: p.origin })}</StatusBadge>
    },
    ...(topicId
      ? []
      : [
          {
            key: 'topicId',
            header: t('insights.scope.topic'),
            filter: { type: 'select', param: 'topicId', options: topics.map((x) => ({ value: x.id, label: x.name })) },
            render: (p) => (p.topicId ? <span className="truncate">{topicNames.get(p.topicId) || p.topicId}</span> : <span className="muted">{t('insights.preferences.allTopics')}</span>)
          }
        ]),
    { key: 'supportCount', header: t('insights.preferences.supportCount'), align: 'right', render: (p) => f.number(p.supportCount || 0) },
    { key: 'confidence', header: t('insights.preferences.confidence'), align: 'right', render: (p) => f.percent(p.confidence || 0, 0) },
    { key: 'createdUtc', header: t('common.created'), sortKey: 'createdUtc', render: (p) => <DateCell value={p.createdUtc} /> }
  ];

  return (
    <>
      <DataTable
        tableKey={tableKey}
        columns={columns}
        fetchPage={fetchPage}
        extraFilters={extraFilters}
        refreshSignal={refresh}
        defaultSort={{ key: 'createdUtc', direction: 'desc' }}
        onRowClick={(p) => setOpenId(p.id)}
        toolbar={
          <>
            {topicId ? <LearnNowButton topicId={topicId} onLearned={bump} /> : null}
            <button type="button" className="button primary small" onClick={() => setEditing({ mode: 'create' })}>
              <Plus size={14} aria-hidden="true" /> {t('insights.preferences.writeOwn')}
            </button>
          </>
        }
        rowActions={(p) => [
          { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setOpenId(p.id) },
          ...preferenceActions(p).map((a) => ({ key: a, label: t('insights.preferences.actions.' + a), icon: ACTION_ICONS[a], onClick: () => act(p, a) })),
          { key: 'edit', label: t('common.edit'), icon: Pencil, onClick: () => setEditing({ mode: 'edit', preference: p }) },
          { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(p) }
        ]}
        emptyTitle={t('insights.preferences.emptyTitle')}
        emptyMessage={t('insights.preferences.emptyMessage')}
      />
      {openId ? (
        <PreferenceDetailModal
          preferenceId={openId}
          refreshSignal={refresh}
          topics={topics}
          onClose={close}
          onAction={act}
          onEdit={(p) => {
            close();
            setEditing({ mode: 'edit', preference: p });
          }}
        />
      ) : null}
      {editing ? (
        <PreferenceFormModal
          preference={editing.mode === 'edit' ? editing.preference : null}
          topics={topics}
          defaultTopicId={topicId || ''}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            addNotification(t('admin.saved'), 'success');
            bump();
          }}
        />
      ) : null}
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('insights.preferences.deleteTitle')}
        message={deleting ? t('insights.preferences.deleteMessage', { statement: deleting.statement }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deletePreference(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          bump();
        }}
      />
    </>
  );
}

PreferencesTable.propTypes = {
  topicId: PropTypes.string,
  topics: PropTypes.array,
  tableKey: PropTypes.string,
  initialPreferenceId: PropTypes.string,
  onPreferenceClosed: PropTypes.func
};
