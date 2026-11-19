import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Send } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { pagedQuery } from '../../utils/api.js';
import { REQUEST_STATUSES, REQUEST_STATUS_TONES } from '../../utils/enums.js';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import useRequestActions from './useRequestActions.jsx';

export default function RequestsList() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const [refresh, setRefresh] = useState(0);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);
  const { actionsFor, modals } = useRequestActions({ onChanged: bump });
  const fetchPage = useCallback((q) => apiClient.listResearchRequests(pagedQuery(q)), [apiClient]);
  const create = () => navigate('/dashboard/one-time/new');

  const columns = [
    {
      key: 'title',
      header: t('oneTime.columns.title'),
      sortKey: 'title',
      pinned: true,
      filter: { type: 'text', param: 'search', placeholder: t('oneTime.searchPlaceholder') },
      render: (r) => (
        <span className="date-cell">
          <strong>{r.title || r.requestText}</strong>
          {r.title && r.title !== r.requestText ? <span className="muted small truncate topic-description">{r.requestText}</span> : null}
        </span>
      )
    },
    {
      key: 'status',
      header: t('oneTime.columns.status'),
      sortKey: 'status',
      filter: { type: 'select', param: 'status', options: REQUEST_STATUSES.map((s) => ({ value: s, label: t('enums.requestStatus.' + s) })) },
      render: (r) => <StatusBadge tone={REQUEST_STATUS_TONES[r.status]}>{t('enums.requestStatus.' + r.status)}</StatusBadge>
    },
    {
      key: 'deliverables',
      header: t('oneTime.columns.deliverables'),
      render: (r) => (
        <span className="chip-list">
          {(r.deliverables || []).map((d) => (
            <StatusBadge key={d}>{t('enums.deliverable.' + d, { defaultValue: d })}</StatusBadge>
          ))}
        </span>
      )
    },
    { key: 'length', header: t('oneTime.columns.length'), defaultHidden: true, render: (r) => t('enums.deliverableLength.' + r.length) },
    { key: 'runAtUtc', header: t('oneTime.columns.runAt'), sortKey: 'runAtUtc', render: (r) => (r.runAtUtc ? <DateCell value={r.runAtUtc} /> : <span className="muted small">{t('oneTime.whenConfirmed')}</span>) },
    { key: 'createdUtc', header: t('oneTime.columns.created'), sortKey: 'createdUtc', render: (r) => <DateCell value={r.createdUtc} /> },
    { key: 'id', header: t('oneTime.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const addButton = (
    <button type="button" className="button primary small" onClick={create}>
      <Plus size={14} aria-hidden="true" /> {t('oneTime.new')}
    </button>
  );

  return (
    <div className="page">
      <PageHeader title={t('nav.items.oneTimeRequests')} summary={t('routes.oneTimeRequests.summary')} />
      <section className="panel">
        <DataTable
          tableKey="one-time-requests"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          onRowClick={(r) => navigate('/dashboard/one-time/' + encodeURIComponent(r.id))}
          toolbar={addButton}
          rowActions={actionsFor}
          emptyTitle={t('oneTime.empty.title')}
          emptyMessage={t('oneTime.empty.message')}
          emptyAction={
            <button type="button" className="button primary" onClick={create}>
              <Send size={14} aria-hidden="true" /> {t('oneTime.empty.action')}
            </button>
          }
        />
      </section>
      {modals}
    </div>
  );
}
