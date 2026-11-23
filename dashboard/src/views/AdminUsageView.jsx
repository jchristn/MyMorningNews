import { useCallback, useEffect, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import ChartCard from '../charts/ChartCard.jsx';
import HorizontalBarChart from '../charts/HorizontalBarChart.jsx';
import RankedBarTable from '../charts/RankedBarTable.jsx';
import { coarseBucketMinutes, computeUsageRange, USAGE_RANGES } from '../charts/ranges.js';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import { ExportCsvButton, RangeControl, TrendControls, UsageKpis, UsageTable, UsageTrend } from './usage/UsageParts.jsx';
import { cleanQuery, rowLabel, topRows, TREND_MEASURES, TREND_TOP } from './usage/usageData.js';

// The per-tenant trend is a time series grouped by tenant: the top tenants stacked in at most TREND_COLUMNS columns,
// with everyone else combined by the server.
export const TREND_COLUMNS = 12;
export const TREND_TENANTS = 6;
// Groupings the platform trend offers; an empty value is a single line for the whole platform.
export const PLATFORM_TREND_GROUPS = ['', 'Model', 'Provider', 'Task'];

export default function AdminUsageView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [rangeId, setRangeId] = useLocalStorage('mymorningnews.adminUsage.range', 'month');
  const [includeDryRun, setIncludeDryRun] = useState(false);
  const [byTenant, setByTenant] = useState(null);
  const [byModel, setByModel] = useState(null);
  const [series, setSeries] = useState(null);
  const [tenantSeries, setTenantSeries] = useState(null);
  const [storedGroup, setTrendGroup] = useLocalStorage('mymorningnews.adminUsage.trendGroup', '');
  const [storedMeasure, setTrendMeasure] = useLocalStorage('mymorningnews.adminUsage.trendMeasure', 'cost');
  const trendGroup = PLATFORM_TREND_GROUPS.includes(storedGroup) ? storedGroup : '';
  const trendMeasure = TREND_MEASURES.includes(storedMeasure) ? storedMeasure : 'cost';
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
      const [tenants, models, timeseries, byTenantOverTime] = await Promise.all([
        apiClient.getUsageReport({ ...base, groupBy: 'Tenant' }),
        apiClient.getUsageReport({ ...base, groupBy: 'Model' }),
        apiClient.getUsageTimeseries(cleanQuery({ ...base, bucketMinutes: r.bucketMinutes, groupBy: trendGroup, top: trendGroup ? TREND_TOP : '' })),
        apiClient.getUsageTimeseries({ ...base, bucketMinutes: coarseBucketMinutes(r, TREND_COLUMNS), groupBy: 'Tenant', top: TREND_TENANTS })
      ]);
      if (id !== requestId.current) return;
      setByTenant(tenants);
      setByModel(models);
      setSeries(timeseries);
      setTenantSeries(byTenantOverTime);
    } catch (err) {
      if (id === requestId.current) setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [apiClient, range, includeDryRun, trendGroup]);

  useEffect(() => {
    load();
  }, [load]);

  const tenantRows = topRows(byTenant, 50).map((r) => ({ key: r.key, label: rowLabel(r, 'Tenant', t), sublabel: r.label && r.label !== r.key ? r.key : undefined, value: Number(r.costUsd) || 0, share: r.costShare, calls: r.calls, tokens: r.totalTokens }));
  const tenantGroups = (tenantSeries && tenantSeries.groups) || [];

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
        <ChartCard title={t('usage.admin.tenantTrend')} subtitle={t('usage.admin.tenantTrendHelp')} className="wide">
          {!tenantSeries && !error ? <LoadingState /> : null}
          {tenantSeries && tenantGroups.length === 0 ? <div className="chart-empty">{t('usage.noData')}</div> : null}
          {tenantSeries && tenantGroups.length > 0 ? <UsageTrend series={tenantSeries} ariaLabel={t('usage.admin.tenantTrend')} measure="cost" stacked /> : null}
        </ChartCard>
        <ChartCard
          title={t('usage.admin.platformTrend')}
          controls={<TrendControls id="platform-trend" measure={trendMeasure} onMeasure={setTrendMeasure} groupBy={trendGroup} onGroupBy={setTrendGroup} groups={PLATFORM_TREND_GROUPS} />}
        >
          {!series && !error ? <LoadingState /> : null}
          <UsageTrend series={series} ariaLabel={t('usage.admin.platformTrend')} measure={trendMeasure} />
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
