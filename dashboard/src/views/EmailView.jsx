import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, Eye, SendHorizontal, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import { EMAIL_STATUSES, EMAIL_STATUS_TONES, EMAIL_TEMPLATES, emailSeries, emailsByDay } from '../utils/email.js';
import ChartCard from '../charts/ChartCard.jsx';
import TimeChart from '../charts/TimeChart.jsx';
import ConfirmModal from '../components/ConfirmModal.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import ErrorState from '../components/ErrorState.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import EmailPreviewModal from './email/EmailPreviewModal.jsx';
import SendTestEmailButton from './email/SendTestEmailButton.jsx';

export const CHART_DAYS = 30;
const CHART_PAGE = 1000;

function EmailChart({ refreshSignal }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [messages, setMessages] = useState(null);
  const [error, setError] = useState(null);
  const load = useCallback(async () => {
    setError(null);
    try {
      const from = new Date(Date.now() - CHART_DAYS * 86400000).toISOString();
      const r = await apiClient.listEmailMessages({ pageSize: CHART_PAGE, fromUtc: from, sortBy: 'createdUtc', sortDirection: 'Descending' });
      setMessages((r && r.objects) || []);
    } catch (err) {
      setError(err);
    }
  }, [apiClient]);
  useEffect(() => {
    load();
  }, [load, refreshSignal]);
  const buckets = useMemo(() => emailsByDay(messages || [], { days: CHART_DAYS }), [messages]);
  const series = emailSeries(buckets, (s) => t('email.statuses.' + s));
  return (
    <ChartCard title={t('email.chart.title')} subtitle={t('email.chart.subtitle', { days: CHART_DAYS })} onRefresh={load} className="wide">
      <ErrorState error={error} inline onRetry={load} />
      {!messages && !error ? <LoadingState /> : null}
      {messages && series.length === 0 ? <div className="chart-empty">{t('email.chart.empty')}</div> : null}
      {messages && series.length > 0 ? <TimeChart ariaLabel={t('email.chart.title')} bucketMinutes={1440} buckets={buckets} series={series} /> : null}
    </ChartCard>
  );
}

EmailChart.propTypes = { refreshSignal: PropTypes.number };

export default function EmailView() {
  const { t } = useTranslation();
  const { apiClient, isTenantAdmin } = useAuth();
  const { addNotification } = useApp();
  const [viewing, setViewing] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const reload = () => setRefresh((n) => n + 1);

  const fetchPage = useCallback((q) => apiClient.listEmailMessages(pagedQuery(q)), [apiClient]);
  const loadViewing = useCallback(() => apiClient.getEmailMessage(viewing.id), [apiClient, viewing]);

  async function resend(m) {
    try {
      await apiClient.resendEmailMessage(m.id);
      addNotification(t('email.resent', { recipient: m.recipient }), 'success');
      reload();
    } catch (err) {
      addNotification(err.message, 'error', 8000);
    }
  }

  const columns = [
    { key: 'createdUtc', header: t('email.columns.time'), sortKey: 'createdUtc', render: (m) => <DateCell value={m.createdUtc} /> },
    {
      key: 'template',
      header: t('email.columns.template'),
      filter: { type: 'select', param: 'template', options: EMAIL_TEMPLATES.map((x) => ({ value: x, label: t('email.templates.' + x) })) },
      render: (m) => t('email.templates.' + m.template, { defaultValue: m.template })
    },
    {
      key: 'recipient',
      header: t('email.columns.recipient'),
      pinned: true,
      filter: isTenantAdmin ? { type: 'text', param: 'recipient' } : undefined,
      render: (m) => <strong>{m.recipient}</strong>
    },
    { key: 'subject', header: t('email.columns.subject'), render: (m) => <span className="truncate" title={m.subject}>{m.subject}</span> },
    {
      key: 'status',
      header: t('email.columns.status'),
      filter: { type: 'select', param: 'status', options: EMAIL_STATUSES.map((x) => ({ value: x, label: t('email.statuses.' + x) })) },
      render: (m) => (
        <StatusBadge tone={EMAIL_STATUS_TONES[m.status] || 'neutral'} title={m.lastError || undefined}>
          {t('email.statuses.' + m.status, { defaultValue: m.status })}
        </StatusBadge>
      )
    },
    { key: 'attempts', header: t('email.columns.attempts'), align: 'right', render: (m) => m.attempts ?? 0 },
    { key: 'sentUtc', header: t('email.columns.sent'), defaultHidden: true, render: (m) => <DateCell value={m.sentUtc} /> },
    { key: 'topic', header: t('email.columns.topic'), defaultHidden: true, render: (m) => (m.topicId ? <span className="mono small">{m.topicId}</span> : '') },
    { key: 'provider', header: t('email.columns.providerId'), defaultHidden: true, render: (m) => (m.providerMessageId ? <span className="mono small">{m.providerMessageId}</span> : '') }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.email')} summary={t('routes.email.summary')} actions={<SendTestEmailButton />} />
      {!isTenantAdmin ? <p className="notice notice-info">{t('email.memberNotice')}</p> : null}
      <div className="chart-grid">
        <EmailChart refreshSignal={refresh} />
      </div>
      <section className="panel">
        <DataTable
          tableKey="email-outbox"
          columns={columns}
          fetchPage={fetchPage}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          refreshSignal={refresh}
          onRowClick={(m) => setViewing(m)}
          rowActions={(m) => [
            { key: 'view', label: t('common.view'), icon: Eye, onClick: () => setViewing(m) },
            { key: 'resend', label: t('email.resend'), icon: SendHorizontal, onClick: () => resend(m) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(m) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(m) }
          ]}
          emptyTitle={t('email.empty.title')}
          emptyMessage={t('email.empty.message')}
        />
      </section>
      {viewing ? <EmailPreviewModal title={t('email.preview.title')} load={loadViewing} onClose={() => setViewing(null)} /> : null}
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('email.delete.title')}
        message={deleting ? t('email.delete.message', { subject: deleting.subject, recipient: deleting.recipient }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteEmailMessage(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          reload();
        }}
      />
    </div>
  );
}
