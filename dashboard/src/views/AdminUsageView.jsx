import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import ChartCard from '../charts/ChartCard.jsx';
import HorizontalBarChart from '../charts/HorizontalBarChart.jsx';
import RankedBarTable from '../charts/RankedBarTable.jsx';
import StackedColumnChart from '../charts/StackedColumnChart.jsx';
import { computeUsageRange, splitRange, USAGE_RANGES } from '../charts/ranges.js';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import { CostTrend, ExportCsvButton, RangeControl, UsageKpis, UsageTable } from './usage/UsageParts.jsx';
import { cleanQuery, rowLabel, topRows } from './usage/usageData.js';

// The per-tenant trend is built from one tenant-grouped report per window, because the time series has no grouping.
export const TREND_WINDOWS = 12;
export const TREND_TENANTS = 6;
const SERIES_COLORS = ['var(--chart-series-1)', 'var(--chart-series-2)', 'var(--chart-series-3)', 'var(--chart-series-5)', 'var(--chart-series-6)', 'var(--chart-series-7)', 'var(--chart-series-8)'];

// Stack the top tenants (by total cost) per window, with everyone else folded into "other".
export function tenantTrend(windows, topKeys) {
  return windows.map((w) => {
    const values = {};
    let other = 0;
    for (const row of (w.report && w.report.rows) || []) {
      const cost = Number(row.costUsd) || 0;
      if (topKeys.includes(row.key)) values[row.key] = (values[row.key] || 0) + cost;
      else other += cost;
    }
    return { from: w.from, to: w.to, values, other };
  });
}

export default function AdminUsageView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [rangeId, setRangeId] = useLocalStorage('mymorningnews.adminUsage.range', 'month');
  const [includeDryRun, setIncludeDryRun] = useState(false);
  const [byTenant, setByTenant] = useState(null);
  const [byModel, setByModel] = useState(null);
  const [series, setSeries] = useState(null);
  const [windows, setWindows] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const requestId = useRef(0);
  const range = USAGE_RANGES[rangeId] ? rangeId : 'month';

  const load = useCallback(async () => {
    const id = ++requestId.current;
    const r = computeUsageRange(range);
    const base = cleanQuery({ from: r.from, to: r.to, allTenants: true, includeDryRun });
    setLoading(true);
    setError(null);
    try {
      const parts = splitRange(r, TREND_WINDOWS);
      const [tenants, models, timeseries, ...trend] = await Promise.all([
        apiClient.getUsageReport({ ...base, groupBy: 'Tenant' }),
        apiClient.getUsageReport({ ...base, groupBy: 'Model' }),
        apiClient.getUsageTimeseries({ ...base, bucketMinutes: r.bucketMinutes }),
        ...parts.map((p) => apiClient.getUsageReport({ ...base, from: p.from, to: p.to, groupBy: 'Tenant', top: 1000 }))
      ]);
      if (id !== requestId.current) return;
      setByTenant(tenants);
      setByModel(models);
      setSeries(timeseries);
      setWindows(parts.map((p, i) => ({ ...p, report: trend[i] })));
    } catch (err) {
      if (id === requestId.current) setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [apiClient, range, includeDryRun]);

  useEffect(() => {
    load();
  }, [load]);

  const tenantRows = topRows(byTenant, 50).map((r) => ({ key: r.key, label: rowLabel(r, 'Tenant', t), sublabel: r.label && r.label !== r.key ? r.key : undefined, value: Number(r.costUsd) || 0, share: r.costShare, calls: r.calls, tokens: r.totalTokens }));
  const topKeys = topRows(byTenant, TREND_TENANTS).map((r) => r.key);
  const trend = windows ? tenantTrend(windows, topKeys) : [];
  const hasOther = trend.some((w) => w.other > 0);
  const trendSeries = [
    ...topKeys.map((key, i) => ({ key, label: rowLabel((byTenant.rows || []).find((r) => r.key === key) || { key }, 'Tenant', t), color: SERIES_COLORS[i % SERIES_COLORS.length], values: trend.map((w) => w.values[key] || 0) })),
    ...(hasOther ? [{ key: '__other', label: t('usage.admin.otherTenants'), color: 'var(--chart-series-8)', values: trend.map((w) => w.other) }] : [])
  ];
  const windowLabel = (w) => {
    const days = (new Date(w.to).getTime() - new Date(w.from).getTime()) / 86400000;
    return days < 1 ? f.dateTime(w.from, { month: 'short', day: 'numeric', hour: 'numeric' }) : f.date(w.from, { month: 'short', day: 'numeric' });
  };

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.adminUsage')}
        summary={t('routes.adminUsage.summary')}
        actions={
          <div className="row-actions">
            <RangeControl rangeId={range} onChange={setRangeId} />
            <button type="button" className="icon-button" onClick={load} aria-label={t('common.refresh')} title={t('common.refresh')}>
              <RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />
            </button>
          </div>
        }
      />
      <div className="toolbar-row">
        <label className="checkbox-row">
          <input type="checkbox" checked={includeDryRun} onChange={(e) => setIncludeDryRun(e.target.checked)} />
          <span>{t('usage.filters.includeDryRun')}</span>
        </label>
        <ExportCsvButton report={byTenant} prefix="platform-usage" />
      </div>
      <ErrorState error={error} onRetry={load} />
      <UsageKpis totals={byTenant ? byTenant.totals : null} />
      <div className="chart-grid">
        <ChartCard title={t('usage.admin.costByTenant')} subtitle={t('usage.admin.costByTenantHelp')} className="wide">
          {!byTenant && !error ? <LoadingState /> : null}
          {byTenant && tenantRows.length === 0 ? <div className="chart-empty">{t('usage.noData')}</div> : null}
          {tenantRows.length > 0 ? (
            <RankedBarTable
              ariaLabel={t('usage.admin.costByTenant')}
              labelHeader={t('usage.groups.Tenant')}
              valueHeader={t('usage.columns.cost')}
              formatValue={(v) => f.usd(v)}
              rows={tenantRows}
              extraColumns={[
                { key: 'calls', header: t('usage.columns.calls'), text: (r) => f.number(r.calls) },
                { key: 'tokens', header: t('usage.columns.tokens'), className: 'hide-narrow', text: (r) => f.compact(r.tokens) }
              ]}
            />
          ) : null}
        </ChartCard>
        <ChartCard title={t('usage.admin.tenantTrend')} subtitle={t('usage.admin.tenantTrendHelp', { count: TREND_WINDOWS })} className="wide">
          {!windows && !error ? <LoadingState /> : null}
          {windows && trendSeries.length === 0 ? <div className="chart-empty">{t('usage.noData')}</div> : null}
          {windows && trendSeries.length > 0 ? (
            <StackedColumnChart
              ariaLabel={t('usage.admin.tenantTrend')}
              formatValue={(v) => f.usd(v)}
              categories={trend.map((w) => ({ key: w.from, label: windowLabel(w) }))}
              series={trendSeries}
            />
          ) : null}
        </ChartCard>
        <ChartCard title={t('usage.admin.platformTrend')}>
          {!series && !error ? <LoadingState /> : null}
          <CostTrend series={series} ariaLabel={t('usage.admin.platformTrend')} />
        </ChartCard>
        <ChartCard title={t('usage.admin.costByModel')}>
          {!byModel && !error ? <LoadingState /> : null}
          {byModel && byModel.rows.length === 0 ? <div className="chart-empty">{t('usage.noData')}</div> : null}
          {byModel ? <HorizontalBarChart ariaLabel={t('usage.admin.costByModel')} formatValue={(v) => f.usd(v)} items={topRows(byModel, 10).map((r) => ({ key: r.key, label: rowLabel(r, 'Model', t), value: Number(r.costUsd) || 0 }))} /> : null}
        </ChartCard>
        <ChartCard title={t('usage.breakdown.title')} subtitle={t('usage.admin.breakdownHelp')} className="wide">
          {byTenant ? <UsageTable report={byTenant} /> : <LoadingState />}
        </ChartCard>
      </div>
    </div>
  );
}
