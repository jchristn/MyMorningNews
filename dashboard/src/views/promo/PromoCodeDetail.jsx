import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Pencil, Power, PowerOff, Trash2, Undo2, UserPlus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { redemptionsByDay } from '../../utils/promo.js';
import ChartCard from '../../charts/ChartCard.jsx';
import TimeChart from '../../charts/TimeChart.jsx';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyButton from '../../components/CopyButton.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import DeletePromoModal from './DeletePromoModal.jsx';
import PromoCodeFormModal from './PromoCodeFormModal.jsx';
import UserPicker from './UserPicker.jsx';
import usePlans from './usePlans.js';
import { PromoStatusBadge, UsageBar, durationText, friendlyPromoError, remainingText } from './promoParts.jsx';

// Redeem a code for a chosen user (channel Admin). The same limits apply as for a user redeeming it.
function ApplyModal({ stats, onClose, onApplied }) {
  const { t } = useTranslation();
  const { apiClient, identity } = useAuth();
  const [target, setTarget] = useState({ tenantId: identity ? identity.tenantId : 'default', userId: '', email: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function apply() {
    setBusy(true);
    setError(null);
    try {
      const r = await apiClient.applyPromoCode(stats.code.id, target.tenantId, target.userId);
      onApplied(r, target);
    } catch (err) {
      setError(friendlyPromoError(err, t));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      title={t('promo.apply.title', { code: stats.code.code })}
      onClose={() => !busy && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={apply} disabled={busy || !target.userId}>
            {busy ? t('common.working') : t('promo.apply.confirm')}
          </button>
        </>
      }
    >
      <p className="muted">{t('promo.apply.help', { plan: stats.planName || '', duration: durationText(t, stats.code.durationDays) })}</p>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <UserPicker value={target} onChange={setTarget} idPrefix="apply" />
    </Modal>
  );
}

ApplyModal.propTypes = { stats: PropTypes.object.isRequired, onClose: PropTypes.func.isRequired, onApplied: PropTypes.func.isRequired };

export default function PromoCodeDetail() {
  const { t } = useTranslation();
  const f = useFormat();
  const { id } = useParams();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const { plans } = usePlans();
  const [stats, setStats] = useState(null);
  const [redemptions, setRedemptions] = useState(null);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(false);
  const [applying, setApplying] = useState(false);
  const [revoking, setRevoking] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [s, r] = await Promise.all([apiClient.getPromoCode(id), apiClient.listPromoRedemptions(id)]);
      setStats(s);
      setRedemptions(Array.isArray(r) ? r : []);
      setRefresh((n) => n + 1);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, id]);

  useEffect(() => {
    load();
  }, [load]);

  const buckets = useMemo(() => redemptionsByDay(redemptions || []), [redemptions]);
  const sorted = useMemo(() => [...(redemptions || [])].sort((a, b) => String(b.createdUtc).localeCompare(String(a.createdUtc))), [redemptions]);

  const fetchRedemptions = useCallback(
    async (q) => {
      const needle = String((q.filters && q.filters.search) || '')
        .trim()
        .toLowerCase();
      const status = q.filters && q.filters.status;
      const rows = sorted.filter((r) => {
        if (needle && !String(r.email || '').toLowerCase().includes(needle) && !String(r.userId || '').toLowerCase().includes(needle)) return false;
        if (status === 'active' && r.revokedUtc) return false;
        if (status === 'revoked' && !r.revokedUtc) return false;
        return true;
      });
      const start = (q.pageNumber - 1) * q.pageSize;
      return { items: rows.slice(start, start + q.pageSize), pageNumber: q.pageNumber, pageSize: q.pageSize, totalCount: rows.length, totalPages: Math.max(1, Math.ceil(rows.length / q.pageSize)) };
    },
    [sorted]
  );

  async function toggle() {
    try {
      setStats(await apiClient.updatePromoCode(stats.code.id, { enabled: !stats.code.enabled }));
      addNotification(stats.code.enabled ? t('promo.notify.disabled', { code: stats.code.code }) : t('promo.notify.enabled', { code: stats.code.code }), 'success');
    } catch (err) {
      addNotification(err.message, 'error', 8000);
    }
  }

  if (error && !stats) return <ErrorState error={error} onRetry={load} />;
  if (!stats) return <LoadingState />;
  const code = stats.code;

  const columns = [
    { key: 'email', header: t('promo.redemptions.user'), pinned: true, filter: { type: 'text', param: 'search' }, render: (r) => <strong>{r.email || r.userId}</strong> },
    { key: 'tenant', header: t('promo.redemptions.tenant'), render: (r) => <span className="mono small">{r.tenantId}</span> },
    { key: 'channel', header: t('promo.redemptions.channel'), render: (r) => t('promo.channels.' + r.channel, { defaultValue: r.channel }) },
    { key: 'date', header: t('promo.redemptions.date'), render: (r) => <DateCell value={r.createdUtc} /> },
    {
      key: 'status',
      header: t('promo.redemptions.status'),
      filter: { type: 'select', param: 'status', options: [{ value: 'active', label: t('promo.redemptions.active') }, { value: 'revoked', label: t('promo.redemptions.revoked') }] },
      render: (r) =>
        r.revokedUtc ? (
          <StatusBadge tone="neutral" title={new Date(r.revokedUtc).toISOString()}>
            {t('promo.redemptions.revokedOn', { date: f.date(r.revokedUtc) })}
          </StatusBadge>
        ) : (
          <StatusBadge tone="success">{t('promo.redemptions.active')}</StatusBadge>
        )
    },
    { key: 'userId', header: t('promo.redemptions.userId'), defaultHidden: true, className: 'cell-id', render: (r) => <span className="mono small">{r.userId}</span> }
  ];

  const plan = plans.find((p) => p.plan.id === code.planId);
  const details = [
    { key: 'description', label: t('promo.fields.description'), value: code.description },
    { key: 'benefit', label: t('promo.offer.kind'), value: stats.code.benefitKind === 'PaidOffer' ? t('promo.offer.paid') : t('promo.offer.complimentary') },
    ...(stats.code.benefitKind === 'PaidOffer' ? [{ key: 'discount', label: t('promo.offer.percent'), value: String(stats.code.discountPercent) + '%' }, { key: 'billingPlan', label: t('promo.offer.billingPlan'), value: stats.code.billingPlanKey }] : []),
    { key: 'plan', label: t('promo.fields.plan'), value: plan ? <Link to="/dashboard/admin/plans">{plan.plan.name}</Link> : stats.planName },
    { key: 'duration', label: t('promo.fields.duration'), value: durationText(t, code.durationDays) },
    { key: 'max', label: t('promo.fields.maxRedemptions'), value: code.maxRedemptions ? f.number(code.maxRedemptions) : t('promo.usage.unlimited') },
    { key: 'perUser', label: t('promo.fields.perUserLimit'), value: f.number(code.perUserLimit || 1) },
    { key: 'starts', label: t('promo.fields.starts'), value: code.startsUtc ? <DateCell value={code.startsUtc} /> : t('promo.immediately') },
    { key: 'expires', label: t('promo.fields.expires'), value: code.expiresUtc ? <DateCell value={code.expiresUtc} /> : t('promo.never') },
    { key: 'newOnly', label: t('promo.fields.newAccountsOnly'), value: code.newAccountsOnly ? t('common.yes') : t('common.no') },
    { key: 'batch', label: t('promo.columns.batch'), value: code.batchId, mono: true },
    { key: 'createdBy', label: t('promo.createdBy'), value: code.createdByUserId, mono: true },
    { key: 'created', label: t('promo.columns.created'), value: <DateCell value={code.createdUtc} /> }
  ];

  return (
    <div className="page">
      <PageHeader
        title={code.code}
        summary={t('promo.detail.summary')}
        meta={
          <span className="row-actions">
            <PromoStatusBadge code={code} />
            <CopyButton value={code.code} labelKey="promo.actions.copyCode" />
          </span>
        }
        actions={
          <div className="row-actions">
            <Link to="/dashboard/admin/promo-codes" className="button secondary small">
              <ArrowLeft size={14} aria-hidden="true" /> {t('promo.detail.back')}
            </Link>
            <button type="button" className="button secondary small" disabled={stats.code.benefitKind === 'PaidOffer'} onClick={() => setApplying(true)}>
              <UserPlus size={14} aria-hidden="true" /> {t('promo.apply.open')}
            </button>
            <button type="button" className="button secondary small" onClick={toggle}>
              {code.enabled ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />} {code.enabled ? t('promo.actions.disable') : t('promo.actions.enable')}
            </button>
            <button type="button" className="button secondary small" onClick={() => setEditing(true)}>
              <Pencil size={14} aria-hidden="true" /> {t('common.edit')}
            </button>
            <button type="button" className="button danger small" onClick={() => setDeleting(stats)}>
              <Trash2 size={14} aria-hidden="true" /> {t('common.delete')}
            </button>
          </div>
        }
      />
      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
      <div className="kpi-grid">
        <KpiTile label={t('promo.kpi.redemptions')} value={<UsageBar code={code} />} />
        <KpiTile label={t('promo.kpi.remaining')} value={remainingText(t, f, stats)} />
        <KpiTile label={t('promo.kpi.activeSubscriptions')} value={f.number(stats.activeAssignments || 0)} tone="success" />
        <KpiTile label={t('promo.kpi.revoked')} value={f.number(stats.revoked || 0)} tone={stats.revoked ? 'warning' : 'neutral'} />
        <KpiTile label={t('promo.kpi.lastRedeemed')} value={stats.lastRedeemedUtc ? f.relative(stats.lastRedeemedUtc) : t('promo.kpi.notYet')} detail={stats.lastRedeemedUtc ? f.dateTime(stats.lastRedeemedUtc) : null} />
      </div>
      <div className="chart-grid">
        <ChartCard title={t('promo.chart.title')} subtitle={t('promo.chart.subtitle')} className="wide" onRefresh={load}>
          {(redemptions || []).length === 0 ? (
            <div className="chart-empty">{t('promo.chart.empty')}</div>
          ) : (
            <TimeChart
              ariaLabel={t('promo.chart.title')}
              bucketMinutes={1440}
              buckets={buckets}
              series={[
                { key: 'active', label: t('promo.redemptions.active'), color: 'var(--chart-series-2)', values: buckets.map((b) => b.active) },
                { key: 'revoked', label: t('promo.redemptions.revoked'), color: 'var(--chart-series-5)', values: buckets.map((b) => b.revoked) }
              ]}
            />
          )}
        </ChartCard>
        <section className="panel">
          <h2>{t('promo.detail.settings')}</h2>
          <KeyValueGrid rows={details} />
        </section>
      </div>
      <section className="panel">
        <h2>{t('promo.redemptions.title')}</h2>
        <DataTable
          tableKey="promo-redemptions"
          columns={columns}
          fetchPage={fetchRedemptions}
          refreshSignal={refresh}
          rowActions={(r) => [{ key: 'revoke', label: t('promo.redemptions.revoke'), icon: Undo2, danger: true, disabled: Boolean(r.revokedUtc), onClick: () => setRevoking(r) }]}
          emptyTitle={t('promo.redemptions.empty')}
          emptyMessage={t('promo.redemptions.emptyMessage')}
        />
      </section>
      {editing ? (
        <PromoCodeFormModal
          mode="edit"
          plans={plans}
          stats={stats}
          onClose={() => setEditing(false)}
          onSaved={(s) => {
            setStats(s);
            setEditing(false);
            addNotification(t('admin.saved'), 'success');
          }}
        />
      ) : null}
      {applying ? (
        <ApplyModal
          stats={stats}
          onClose={() => setApplying(false)}
          onApplied={(r, target) => {
            setApplying(false);
            addNotification(t('promo.apply.done', { code: code.code, email: target.email || r.email || r.userId }), 'success');
            load();
          }}
        />
      ) : null}
      <ConfirmModal
        open={Boolean(revoking)}
        title={t('promo.redemptions.revokeTitle')}
        message={revoking ? t('promo.redemptions.revokeMessage', { email: revoking.email || revoking.userId, code: code.code }) : ''}
        confirmLabel={t('promo.redemptions.revoke')}
        danger
        onClose={() => setRevoking(null)}
        onConfirm={async () => {
          await apiClient.revokePromoRedemption(code.id, revoking.id);
          addNotification(t('promo.redemptions.revokedNotice'), 'success');
          load();
        }}
      />
      <DeletePromoModal
        stats={deleting}
        onClose={() => setDeleting(null)}
        onDone={(what) => {
          if (what === 'deleted') {
            addNotification(t('admin.deleted'), 'success');
            navigate('/dashboard/admin/promo-codes');
          } else {
            addNotification(t('promo.notify.disabled', { code: code.code }), 'success');
            load();
          }
        }}
      />
    </div>
  );
}
