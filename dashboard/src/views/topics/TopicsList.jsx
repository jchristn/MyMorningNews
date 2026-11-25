import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Pause, Plus, Sparkles, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import TopicReviewBanner from '../../components/TopicReviewBanner.jsx';
import { pagedQuery } from '../../utils/api.js';
import { TOPIC_STATUSES, TOPIC_STATUS_TONES } from '../../utils/enums.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import useTopicActions from './useTopicActions.jsx';

export default function TopicsList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [refresh, setRefresh] = useState(0);
  const [plan, setPlan] = useState(null);
  useEffect(() => { let active = true; if (apiClient.getMyPlan) apiClient.getMyPlan().then(value => { if (active) setPlan(value); }).catch(() => {}); return () => { active = false; }; }, [apiClient]);
  const customAllowed = plan?.features?.['customResearch.enabled'] !== 'false';
  const [bulkDelete, setBulkDelete] = useState(null);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);
  const { actionsFor, modals } = useTopicActions({ onChanged: bump });

  const fetchPage = useCallback((q) => apiClient.listTopics(pagedQuery(q)), [apiClient]);
  const open = (r) => navigate('/dashboard/topics/' + encodeURIComponent(r.id));
  const newTopic = () => navigate('/dashboard/topics/new');

  async function pauseMany(rows, clear) {
    const active = rows.filter((r) => r.status === 'Active');
    const results = await Promise.allSettled(active.map((r) => apiClient.pauseTopic(r.id)));
    const failed = results.filter((r) => r.status === 'rejected').length;
    addNotification(t('topics.bulk.paused', { count: active.length - failed }), failed ? 'warning' : 'success');
    clear();
    bump();
  }

  const columns = [
    {
      key: 'name',
      header: t('topics.columns.name'),
      sortKey: 'name',
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('topics.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell">
          <strong>{r.name}</strong>
          {r.description ? <span className="muted small truncate topic-description">{r.description}</span> : null}
        </span>
      )
    },
    {
      key: 'status',
      header: t('topics.columns.status'),
      sortKey: 'status',
      filter: { type: 'select', param: 'status', options: TOPIC_STATUSES.map((s) => ({ value: s, label: t('enums.topicStatus.' + s) })) },
      render: (r) => <StatusBadge tone={TOPIC_STATUS_TONES[r.status]}>{t('enums.topicStatus.' + r.status)}</StatusBadge>
    },
    {
      key: 'isOneTime',
      header: t('topics.columns.oneTime'),
      filter: { type: 'boolean', param: 'isOneTime' },
      render: (r) => (r.isOneTime ? <StatusBadge tone="accent">{t('topics.oneTime')}</StatusBadge> : <span className="muted small">{t('topics.recurring')}</span>)
    },
    { key: 'lastRunUtc', header: t('topics.columns.lastRun'), sortKey: 'lastRunUtc', render: (r) => <DateCell value={r.lastRunUtc} /> },
    { key: 'lastRefinedUtc', header: t('topics.columns.lastRefined'), render: (r) => <DateCell value={r.lastRefinedUtc} /> },
    { key: 'createdUtc', header: t('topics.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'audienceLevel', header: t('topics.columns.audience'), defaultHidden: true, render: (r) => t('enums.audienceLevel.' + r.audienceLevel) },
    { key: 'id', header: t('topics.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const addButton = (
    <button type="button" className="button primary small" onClick={newTopic} disabled={!customAllowed}>
      <Plus size={14} aria-hidden="true" /> {t('topics.new')}
    </button>
  );

  return (
    <div className="page">
      <PageHeader title={t('nav.items.topics')} summary={t('routes.topics.summary')} />
      <TopicReviewBanner onChanged={bump} />
      {!customAllowed ? <section className="panel stack"><p>{t('catalog.summary')}</p><div className="row-actions"><button className="button primary" onClick={() => navigate('/dashboard/catalog')}>{t('catalog.title')}</button><button className="button secondary" onClick={() => navigate('/dashboard/account/billing')}>{t('billing.title')}</button></div></section> : null}
      <section className="panel">
        <DataTable
          tableKey="topics"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          onRowClick={open}
          toolbar={addButton}
          selectable
          bulkActions={(rows, clear) => (
            <>
              <button type="button" className="button secondary small" onClick={() => pauseMany(rows, clear)} disabled={!rows.some((r) => r.status === 'Active')}>
                <Pause size={14} aria-hidden="true" /> {t('topics.bulk.pause')}
              </button>
              <button type="button" className="button danger small" onClick={() => setBulkDelete({ rows, clear })}>
                <Trash2 size={14} aria-hidden="true" /> {t('topics.bulk.delete')}
              </button>
            </>
          )}
          rowActions={actionsFor}
          emptyTitle={t('topics.empty.title')}
          emptyMessage={t('topics.empty.message')}
          emptyAction={
            <button type="button" className="button primary" onClick={newTopic}>
              <Sparkles size={14} aria-hidden="true" /> {t('topics.empty.action')}
            </button>
          }
        />
      </section>
      {modals}
      <ConfirmModal
        open={Boolean(bulkDelete)}
        title={t('topics.bulk.deleteTitle')}
        message={bulkDelete ? t('topics.bulk.deleteMessage', { count: bulkDelete.rows.length }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setBulkDelete(null)}
        onConfirm={async () => {
          const results = await Promise.allSettled(bulkDelete.rows.map((r) => apiClient.deleteTopic(r.id)));
          const failed = results.filter((r) => r.status === 'rejected').length;
          addNotification(t('topics.bulk.deleted', { count: bulkDelete.rows.length - failed }), failed ? 'warning' : 'success');
          bulkDelete.clear();
          bump();
        }}
      />
    </div>
  );
}
