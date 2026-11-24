import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useCanReadCalls from '../hooks/useCanReadCalls.js';
import useDebounce from '../hooks/useDebounce.js';
import useFormat from '../hooks/useFormat.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import ChartCard from '../charts/ChartCard.jsx';
import HorizontalBarChart from '../charts/HorizontalBarChart.jsx';
import RankedBarTable from '../charts/RankedBarTable.jsx';
import StackedColumnChart from '../charts/StackedColumnChart.jsx';
import { computeUsageRange, USAGE_RANGES } from '../charts/ranges.js';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import { MODEL_TASKS, USAGE_GROUPS } from '../utils/enums.js';
import useModelEndpoints from './models/useModelEndpoints.js';
import { ExportCsvButton, RangeControl, TrendControls, UsageKpis, UsageTable, UsageTrend } from './usage/UsageParts.jsx';
import { cleanQuery, rowLabel, TOKEN_TYPES, tokenMix, topRows, TREND_GROUPS, TREND_MEASURES, TREND_TOP } from './usage/usageData.js';

const BASE_GROUPS = ['User', 'Model', 'Task', 'Topic'];
const MIX_GROUPS = ['Model', 'Task', 'User'];
const EMPTY_FILTERS = { userId: '', topicId: '', modelEndpointId: '', task: '', includeDryRun: false };

function isForbidden(err) {
  return Boolean(err && err.status === 403);
}

// The caller's own usage, for members who cannot read tenant-wide usage.
function MyUsage({ rangeId, includeDryRun, refreshSignal = 0 }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [report, setReport] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    const range = computeUsageRange(rangeId);
    setLoading(true);
    setError(null);
    try {
      setReport(await apiClient.getMyUsage(cleanQuery({ from: range.from, to: range.to, groupBy: 'Task', includeDryRun })));
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
    // refreshSignal re-runs the load when the page header refresh is pressed.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiClient, rangeId, includeDryRun, refreshSignal]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <>
      <div className="notice notice-info">{t('usage.member.notice')}</div>
      <ErrorState error={error} onRetry={load} />
      <UsageKpis totals={report ? report.totals : null} />
      <div className="chart-grid">
        <ChartCard title={t('usage.member.byTask')} onRefresh={load} loading={loading} controls={<ExportCsvButton report={report} prefix="my-usage" />} className="wide">
          {!report && !error ? <LoadingState /> : null}
          {report && report.rows.length === 0 ? <div className="chart-empty">{t('usage.noData')}</div> : null}
          {report && report.rows.length > 0 ? (
            <HorizontalBarChart ariaLabel={t('usage.member.byTask')} formatValue={(v) => f.usd(v)} items={report.rows.map((r) => ({ key: r.key, label: rowLabel(r, 'Task', t), value: Number(r.costUsd) || 0 }))} />
          ) : null}
        </ChartCard>
        <section className="panel wide">
          <UsageTable report={report} />
        </section>
      </div>
    </>
  );
}

MyUsage.propTypes = { rangeId: PropTypes.string.isRequired, includeDryRun: PropTypes.bool, refreshSignal: PropTypes.number };

export default function UsageView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient, isTenantAdmin, identity } = useAuth();
  // Tenant-wide usage needs the accounting permission; members see their own usage without probing for it first.
  const canReadUsage = useCanReadCalls();
  const { endpoints } = useModelEndpoints();
  const [rangeId, setRangeId] = useLocalStorage('mymorningnews.usage.range', 'month');
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [topicInput, setTopicInput] = useState('');
  const debouncedTopic = useDebounce(topicInput, 400);
  const [breakdownGroup, setBreakdownGroup] = useLocalStorage('mymorningnews.usage.breakdown', 'User');
  const [mixGroup, setMixGroup] = useLocalStorage('mymorningnews.usage.mix', 'Model');
  const [storedTrendGroup, setTrendGroup] = useLocalStorage('mymorningnews.usage.trendGroup', '');
  const [storedTrendMeasure, setTrendMeasure] = useLocalStorage('mymorningnews.usage.trendMeasure', 'cost');
  const trendGroup = TREND_GROUPS.includes(storedTrendGroup) ? storedTrendGroup : '';
  const trendMeasure = TREND_MEASURES.includes(storedTrendMeasure) ? storedTrendMeasure : 'cost';
  const [reports, setReports] = useState({});
  const [series, setSeries] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [memberMode, setMemberMode] = useState(!canReadUsage);
  const [users, setUsers] = useState([]);
  const requestId = useRef(0);
  const inFlight = useRef(new Set());
  const memberRef = useRef(!canReadUsage);
  const [memberTick, setMemberTick] = useState(0);
  const range =USAGE_RANGES[rangeId] ? rangeId : 'month';

  useEffect(() => {
    const next = debouncedTopic.trim();
    setFilters((current) => (current.topicId === next ? current : { ...current, topicId: next }));
  }, [debouncedTopic]);

  useEffect(() => {
    if (!isTenantAdmin) return;
    apiClient
      .listUsers(identity ? identity.tenantId : 'default', { pageSize: 1000, sortBy: 'email', sortDirection: 'Ascending' })
      .then((r) => setUsers((r && r.objects) || []))
      .catch(() => setUsers([]));
  }, [apiClient, isTenantAdmin, identity]);

  const query = useMemo(() => {
    const r = computeUsageRange(range);
    return { range: r, base: cleanQuery({ from: r.from, to: r.to, ...filters }) };
    // A refresh recomputes the window; filters and range drive the rest.
  }, [range, filters]);

  const fetchGroup = useCallback((groupBy, base) => apiClient.getUsageReport({ ...base, groupBy }), [apiClient]);
  // The trend: one line for everything, or the largest groups of the chosen grouping with the rest combined.
  const fetchSeries = useCallback(
    (base, bucketMinutes, groupBy) => apiClient.getUsageTimeseries(cleanQuery({ ...base, bucketMinutes, groupBy, top: groupBy ? TREND_TOP : '' })),
    [apiClient]
  );
  const trendGroupRef = useRef(trendGroup);
  const loadedTrendGroup = useRef(trendGroup);

  const load = useCallback(async () => {
    // Once the server has said the caller cannot read tenant usage, only the caller's own usage is loaded.
    if (memberRef.current) return;
    const id = ++requestId.current;
    const r = computeUsageRange(range);
    const base = cleanQuery({ from: r.from, to: r.to, ...filters });
    setLoading(true);
    setError(null);
    const groups = Array.from(new Set([...BASE_GROUPS, breakdownGroup, mixGroup])).filter((g) => g !== 'Model');
    try {
      // One request first, so a caller the role flags misjudge sees a single 403 rather than one per grouping.
      const model = await fetchGroup('Model', base);
      const group = trendGroupRef.current;
      const [timeseries, ...results] = await Promise.all([fetchSeries(base, r.bucketMinutes, group), ...groups.map((g) => fetchGroup(g, base))]);
      if (id !== requestId.current) return;
      loadedTrendGroup.current = group;
      const next = { Model: model };
      groups.forEach((g, i) => (next[g] = results[i]));
      setSeries(timeseries);
      setReports(next);
    } catch (err) {
      if (id !== requestId.current) return;
      if (isForbidden(err)) {
        memberRef.current = true;
        setMemberMode(true);
      } else setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
    // breakdownGroup and mixGroup are fetched lazily below when they change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiClient, range, filters, fetchGroup, fetchSeries]);

  useEffect(() => {
    load();
  }, [load]);

  // A newly chosen trend grouping reloads only the time series.
  useEffect(() => {
    trendGroupRef.current = trendGroup;
    if (memberMode || loading || loadedTrendGroup.current === trendGroup) return;
    const id = requestId.current;
    loadedTrendGroup.current = trendGroup;
    fetchSeries(query.base, query.range.bucketMinutes, trendGroup)
      .then((timeseries) => {
        if (id === requestId.current && trendGroupRef.current === trendGroup) setSeries(timeseries);
      })
      .catch(setError);
  }, [trendGroup, memberMode, loading, fetchSeries, query]);

  // Fetch a newly selected breakdown or mix grouping without reloading everything.
  useEffect(() => {
    if (memberMode || loading) return;
    for (const g of [breakdownGroup, mixGroup]) {
      if (reports[g] || inFlight.current.has(g)) continue;
      inFlight.current.add(g);
      fetchGroup(g, query.base)
        .then((report) => setReports((current) => ({ ...current, [g]: report })))
        .catch(setError)
        .finally(() => inFlight.current.delete(g));
    }
  }, [breakdownGroup, mixGroup, reports, memberMode, loading, fetchGroup, query.base]);

  const set = (key, value) => setFilters((current) => ({ ...current, [key]: value }));
  const totals = reports.Model ? reports.Model.totals : null;
  const anyFilter = Object.entries(filters).some(([, v]) => v !== '' && v !== false);

  const header = (
    <PageHeader
      title={t('nav.items.usage')}
      summary={t('routes.usage.summary')}
      actions={
        <div className="row-actions">
          <RangeControl rangeId={range} onChange={setRangeId} />
          <button type="button" className="icon-button" onClick={() => (memberMode ? setMemberTick((n) => n + 1) : load())} aria-label={t('common.refresh')} title={t('common.refresh')}>
            <RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />
          </button>
        </div>
      }
    />
  );

  if (memberMode) {
    return (
      <div className="page">
        {header}
        <label className="checkbox-row">
          <input type="checkbox" checked={filters.includeDryRun} onChange={(e) => set('includeDryRun', e.target.checked)} />
          <span>{t('usage.filters.includeDryRun')}</span>
        </label>
        <MyUsage rangeId={range} includeDryRun={filters.includeDryRun} refreshSignal={memberTick} />
      </div>
    );
  }

  const userRows = topRows(reports.User, 20).map((r) => ({ key: r.key, label: rowLabel(r, 'User', t), value: Number(r.costUsd) || 0, share: r.costShare, calls: r.calls, tokens: r.totalTokens }));
  const bars = (groupBy, limit = 10) =>
    topRows(reports[groupBy], limit).map((r) => ({ key: r.key, label: rowLabel(r, groupBy, t), value: Number(r.costUsd) || 0, endpointId: r.modelEndpointId || '' }));
  const mixRows = topRows(reports[mixGroup], 8);
  const ttftRows = topRows(reports.Model, 10).filter((r) => r.averageTimeToFirstTokenMs !== null && r.averageTimeToFirstTokenMs !== undefined);
  const toolRows = topRows(reports.Task, 11).some((r) => Number(r.toolCalls) > 0) ? topRows(reports.Task, 11) : [];
  const empty = (list) => (list.length === 0 ? <div className="chart-empty">{t('usage.noData')}</div> : null);

  return (
    <div className="page">
      {header}
      <section className="panel">
        <div className="filter-bar" role="search">
          <label>
            <span className="field-label">{t('usage.filters.task')}</span>
            <select value={filters.task} onChange={(e) => set('task', e.target.value)}>
              <option value="">{t('table.any')}</option>
              {MODEL_TASKS.map((task) => (
                <option key={task} value={task}>
                  {t('enums.task.' + task)}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="field-label">{t('usage.filters.endpoint')}</span>
            <select value={filters.modelEndpointId} onChange={(e) => set('modelEndpointId', e.target.value)}>
              <option value="">{t('table.any')}</option>
              {endpoints.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="field-label">{t('usage.filters.user')}</span>
            {users.length > 0 ? (
              <select value={filters.userId} onChange={(e) => set('userId', e.target.value)}>
                <option value="">{t('table.any')}</option>
                {users.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.email}
                  </option>
                ))}
              </select>
            ) : (
              <input className="mono" value={filters.userId} onChange={(e) => set('userId', e.target.value)} placeholder={t('usage.filters.userPlaceholder')} />
            )}
          </label>
          <label>
            <span className="field-label">{t('usage.filters.topic')}</span>
            <input className="mono" value={topicInput} onChange={(e) => setTopicInput(e.target.value)} placeholder={t('usage.filters.topicPlaceholder')} />
          </label>
          <label className="checkbox-row">
            <input type="checkbox" checked={filters.includeDryRun} onChange={(e) => set('includeDryRun', e.target.checked)} />
            <span>{t('usage.filters.includeDryRun')}</span>
          </label>
          <div className="filter-actions">
            <button
              type="button"
              className="button secondary small"
              disabled={!anyFilter}
              onClick={() => {
                setFilters(EMPTY_FILTERS);
                setTopicInput('');
              }}
            >
              {t('common.clearFilters')}
            </button>
          </div>
        </div>
        <p className="muted small">{t('usage.window', { from: f.dateTime(query.range.from, { dateStyle: 'medium', timeStyle: 'short' }), to: f.dateTime(query.range.to, { dateStyle: 'medium', timeStyle: 'short' }) })}</p>
      </section>
      <ErrorState error={error} onRetry={load} />
      <UsageKpis totals={totals} />
      <div className="chart-grid">
        <ChartCard
          title={t('usage.trend.title')}
          subtitle={t('usage.trend.help')}
          className="wide"
          controls={<TrendControls id="usage-trend" measure={trendMeasure} onMeasure={setTrendMeasure} groupBy={trendGroup} onGroupBy={setTrendGroup} groups={TREND_GROUPS} />}
        >
          {!series && !error ? <LoadingState /> : null}
          <UsageTrend series={series} ariaLabel={t('usage.trend.title')} measure={trendMeasure} />
        </ChartCard>
        <ChartCard title={t('usage.charts.costByUser')} subtitle={t('usage.charts.costByUserHelp')} className="wide">
          {reports.User ? empty(userRows) : <LoadingState />}
          {userRows.length > 0 ? (
            <RankedBarTable
              ariaLabel={t('usage.charts.costByUser')}
              labelHeader={t('usage.groups.User')}
              valueHeader={t('usage.columns.cost')}
              formatValue={(v) => f.usd(v)}
              rows={userRows}
              onRowClick={(row) => row.key !== '(system)' && set('userId', row.key)}
              extraColumns={[
                { key: 'calls', header: t('usage.columns.calls'), text: (r) => f.number(r.calls) },
                { key: 'tokens', header: t('usage.columns.tokens'), className: 'hide-narrow', text: (r) => f.compact(r.tokens) }
              ]}
            />
          ) : null}
        </ChartCard>
        <ChartCard title={t('usage.charts.costByModel')}>
          {reports.Model ? empty(bars('Model')) : <LoadingState />}
          <HorizontalBarChart ariaLabel={t('usage.charts.costByModel')} formatValue={(v) => f.usd(v)} items={bars('Model')} color="var(--chart-series-1)" onItemClick={(item) => item.endpointId && set('modelEndpointId', item.endpointId)} />
        </ChartCard>
        <ChartCard title={t('usage.charts.costByTask')}>
          {reports.Task ? empty(bars('Task', 11)) : <LoadingState />}
          <HorizontalBarChart ariaLabel={t('usage.charts.costByTask')} formatValue={(v) => f.usd(v)} items={bars('Task', 11)} color="var(--chart-series-5)" onItemClick={(item) => set('task', item.key)} />
        </ChartCard>
        <ChartCard title={t('usage.charts.costByTopic')}>
          {reports.Topic ? empty(bars('Topic')) : <LoadingState />}
          <HorizontalBarChart
            ariaLabel={t('usage.charts.costByTopic')}
            formatValue={(v) => f.usd(v)}
            items={bars('Topic')}
            color="var(--chart-series-2)"
            onItemClick={(item) => {
              if (item.key !== '(none)') {
                setTopicInput(item.key);
                set('topicId', item.key);
              }
            }}
          />
        </ChartCard>
        <ChartCard title={t('usage.charts.ttftByModel')} subtitle={t('usage.charts.ttftHelp')}>
          {reports.Model ? empty(ttftRows) : <LoadingState />}
          {ttftRows.length > 0 ? (
            <StackedColumnChart
              mode="grouped"
              ariaLabel={t('usage.charts.ttftByModel')}
              formatValue={(v) => f.duration(v)}
              categories={ttftRows.map((r) => ({ key: r.key, label: rowLabel(r, 'Model', t) }))}
              series={[
                { key: 'avg', label: t('usage.series.average'), color: 'var(--chart-series-1)', values: ttftRows.map((r) => Number(r.averageTimeToFirstTokenMs) || 0) },
                { key: 'p95', label: t('usage.series.p95'), color: 'var(--chart-series-3)', values: ttftRows.map((r) => Number(r.p95TimeToFirstTokenMs) || 0) }
              ]}
            />
          ) : null}
        </ChartCard>
        <ChartCard
          title={t('usage.charts.tokenMix')}
          subtitle={t('usage.charts.tokenMixHelp')}
          className="wide"
          controls={
            <div className="segmented" role="group" aria-label={t('usage.charts.groupBy')}>
              {MIX_GROUPS.map((g) => (
                <button type="button" key={g} className={mixGroup === g ? 'active' : ''} aria-pressed={mixGroup === g} onClick={() => setMixGroup(g)}>
                  {t('usage.groups.' + g)}
                </button>
              ))}
            </div>
          }
        >
          {reports[mixGroup] ? empty(mixRows) : <LoadingState />}
          {mixRows.length > 0 ? (
            <StackedColumnChart
              ariaLabel={t('usage.charts.tokenMix')}
              formatValue={(v) => f.compact(v)}
              categories={mixRows.map((r) => ({ key: r.key, label: rowLabel(r, mixGroup, t) }))}
              series={TOKEN_TYPES.map((type) => ({ key: type.key, label: t('usage.tokens.' + type.key), color: type.color, values: mixRows.map((r) => tokenMix(r)[type.key]) }))}
            />
          ) : null}
        </ChartCard>
        <ChartCard title={t('usage.charts.toolCalls')} subtitle={t('usage.charts.toolCallsHelp')}>
          {reports.Task ? (toolRows.length === 0 ? <div className="chart-empty">{t('usage.noToolCalls')}</div> : null) : <LoadingState />}
          {toolRows.length > 0 ? (
            <StackedColumnChart
              ariaLabel={t('usage.charts.toolCalls')}
              formatValue={(v) => f.number(v, { maximumFractionDigits: 2 })}
              categories={toolRows.map((r) => ({ key: r.key, label: rowLabel(r, 'Task', t), extra: [{ label: t('usage.columns.toolCalls'), value: f.number(r.toolCalls) }] }))}
              series={[{ key: 'perCall', label: t('usage.series.toolCallsPerCall'), color: 'var(--chart-series-6)', values: toolRows.map((r) => (r.calls > 0 ? (Number(r.toolCalls) || 0) / r.calls : 0)) }]}
              onCategoryClick={(c) => set('task', c.key)}
            />
          ) : null}
        </ChartCard>
        <ChartCard
          title={t('usage.breakdown.title')}
          subtitle={t('usage.breakdown.help')}
          className="wide"
          controls={
            <>
              <label className="inline-field">
                <span>{t('usage.charts.groupBy')}</span>
                <select value={breakdownGroup} onChange={(e) => setBreakdownGroup(e.target.value)}>
                  {USAGE_GROUPS.map((g) => (
                    <option key={g} value={g}>
                      {t('usage.groups.' + g)}
                    </option>
                  ))}
                </select>
              </label>
              <ExportCsvButton report={reports[breakdownGroup]} prefix="usage" />
            </>
          }
        >
          {reports[breakdownGroup] ? <UsageTable report={reports[breakdownGroup]} /> : <LoadingState />}
        </ChartCard>
      </div>
    </div>
  );
}
