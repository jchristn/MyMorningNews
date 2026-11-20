import { useCallback, useState } from 'react';
import { Route, Routes, useNavigate } from 'react-router-dom';
import { Copy, ExternalLink, Layers, Plus, Power, PowerOff, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import { pagedQuery } from '../utils/api.js';
import { copyText } from '../components/CopyButton.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import PageHeader from '../components/PageHeader.jsx';
import DeletePromoModal from './promo/DeletePromoModal.jsx';
import PromoCodeDetail from './promo/PromoCodeDetail.jsx';
import PromoCodeFormModal from './promo/PromoCodeFormModal.jsx';
import usePlans from './promo/usePlans.js';
import { PromoStatusBadge, UsageBar, durationText, remainingText } from './promo/promoParts.jsx';

function PromoCodesList() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const { plans } = usePlans();
  const [dialog, setDialog] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const reload = () => setRefresh((n) => n + 1);
  const open = (s) => navigate('/dashboard/admin/promo-codes/' + encodeURIComponent(s.code.id));

  const fetchPage = useCallback((q) => apiClient.listPromoCodes(pagedQuery(q)), [apiClient]);

  async function toggle(s) {
    try {
      await apiClient.updatePromoCode(s.code.id, { enabled: !s.code.enabled });
      addNotification(s.code.enabled ? t('promo.notify.disabled', { code: s.code.code }) : t('promo.notify.enabled', { code: s.code.code }), 'success');
      reload();
    } catch (err) {
      addNotification(err.message, 'error', 8000);
    }
  }

  async function copy(s) {
    if (await copyText(s.code.code)) addNotification(t('promo.notify.copied', { code: s.code.code }), 'success');
  }

  const planOptions = plans.map((p) => ({ value: p.plan.id, label: p.plan.name }));
  const columns = [
    { key: 'code', header: t('promo.columns.code'), pinned: true, filter: { type: 'text', param: 'search', placeholder: t('promo.filters.search') }, render: (s) => <strong className="mono">{s.code.code}</strong> },
    { key: 'description', header: t('promo.columns.description'), render: (s) => <span className="truncate" title={s.code.description || ''}>{s.code.description || ''}</span> },
    { key: 'plan', header: t('promo.columns.plan'), filter: { type: 'select', param: 'planId', options: planOptions }, render: (s) => s.planName || s.code.planId },
    { key: 'duration', header: t('promo.columns.duration'), render: (s) => durationText(t, s.code.durationDays) },
    { key: 'usage', header: t('promo.columns.usage'), sortKey: 'redemptionCount', render: (s) => <UsageBar code={s.code} /> },
    { key: 'remaining', header: t('promo.columns.remaining'), align: 'right', render: (s) => remainingText(t, f, s) },
    { key: 'active', header: t('promo.columns.activeSubscriptions'), align: 'right', render: (s) => f.number(s.activeAssignments || 0) },
    {
      key: 'status',
      header: t('promo.columns.status'),
      filter: { type: 'select', param: 'enabled', options: [{ value: 'true', label: t('promo.filters.enabledOnly') }, { value: 'false', label: t('promo.filters.disabledOnly') }] },
      render: (s) => <PromoStatusBadge code={s.code} />
    },
    { key: 'batch', header: t('promo.columns.batch'), filter: { type: 'text', param: 'batchId' }, render: (s) => (s.code.batchId ? <span className="mono small">{s.code.batchId}</span> : '') },
    { key: 'expires', header: t('promo.columns.expires'), sortKey: 'expiresUtc', render: (s) => (s.code.expiresUtc ? <DateCell value={s.code.expiresUtc} /> : <span className="muted">{t('promo.never')}</span>) },
    { key: 'lastRedeemed', header: t('promo.columns.lastRedeemed'), defaultHidden: true, render: (s) => <DateCell value={s.lastRedeemedUtc} /> },
    { key: 'created', header: t('promo.columns.created'), sortKey: 'createdUtc', defaultHidden: true, render: (s) => <DateCell value={s.code.createdUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.promoCodes')} summary={t('routes.promoCodes.summary')} />
      <section className="panel">
        <DataTable
          tableKey="promo-codes"
          columns={columns}
          fetchPage={fetchPage}
          rowKey={(s) => s.code.id}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          refreshSignal={refresh}
          onRowClick={open}
          toolbar={
            <>
              <button type="button" className="button secondary small" onClick={() => setDialog('batch')}>
                <Layers size={14} aria-hidden="true" /> {t('promo.batch.open')}
              </button>
              <button type="button" className="button primary small" onClick={() => setDialog('create')}>
                <Plus size={14} aria-hidden="true" /> {t('promo.create.open')}
              </button>
            </>
          }
          rowActions={(s) => [
            { key: 'open', label: t('common.view'), icon: ExternalLink, onClick: () => open(s) },
            s.code.enabled
              ? { key: 'disable', label: t('promo.actions.disable'), icon: PowerOff, onClick: () => toggle(s) }
              : { key: 'enable', label: t('promo.actions.enable'), icon: Power, onClick: () => toggle(s) },
            { key: 'copy', label: t('promo.actions.copyCode'), icon: Copy, onClick: () => copy(s) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(s) }
          ]}
          emptyTitle={t('promo.empty.title')}
          emptyMessage={t('promo.empty.message')}
        />
      </section>
      {dialog ? (
        <PromoCodeFormModal
          mode={dialog}
          plans={plans}
          onClose={() => setDialog(null)}
          onSaved={(result, options) => {
            reload();
            if (options && options.keepOpen) {
              addNotification(t('promo.notify.generated', { count: Array.isArray(result) ? result.length : 0 }), 'success');
              return;
            }
            setDialog(null);
            addNotification(t('promo.notify.created', { code: result && result.code ? result.code.code : '' }), 'success');
          }}
        />
      ) : null}
      <DeletePromoModal
        stats={deleting}
        onClose={() => setDeleting(null)}
        onDone={(what) => {
          addNotification(what === 'deleted' ? t('admin.deleted') : t('promo.notify.disabled', { code: deleting.code.code }), 'success');
          reload();
        }}
      />
    </div>
  );
}

export default function PromoCodesView() {
  return (
    <Routes>
      <Route index element={<PromoCodesList />} />
      <Route path=":id" element={<PromoCodeDetail />} />
    </Routes>
  );
}
