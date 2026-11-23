import PropTypes from 'prop-types';
import { Clock, Coins, Download, Hash, Layers, TriangleAlert, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import LineChart from '../../charts/LineChart.jsx';
import StackedColumnChart from '../../charts/StackedColumnChart.jsx';
import { USAGE_RANGE_IDS } from '../../charts/ranges.js';
import KpiTile from '../../components/KpiTile.jsx';
import { downloadText, usageCsvFilename, usageReportCsv } from '../../utils/csv.js';
import { failureRate, measureLabel, measureValue, rowLabel, TREND_MEASURES, trendSeries } from './usageData.js';

export function UsageKpis({ totals }) {
  const { t } = useTranslation();
  const f = useFormat();
  const v = (fn) => (totals ? fn(totals) : '...');
  const rate = totals ? failureRate(totals) : 0;
  return (
    <div className="kpi-grid kpi-grid-6">
      <KpiTile icon={Coins} label={t('usage.kpi.cost')} value={v((x) => f.usd(x.costUsd))} detail={totals && totals.calls > 0 ? t('usage.kpi.perCall', { value: f.usd(totals.costUsd / totals.calls) }) : null} tone="accent" />
      <KpiTile icon={Hash} label={t('usage.kpi.calls')} value={v((x) => f.number(x.calls))} />
      <KpiTile icon={Layers} label={t('usage.kpi.tokens')} value={v((x) => f.compact(x.totalTokens))} detail={totals ? t('usage.kpi.tokensDetail', { input: f.compact(totals.inputTokens), output: f.compact(totals.outputTokens) }) : null} />
      <KpiTile icon={TriangleAlert} label={t('usage.kpi.failures')} value={v((x) => f.number(x.failures))} detail={totals && totals.calls > 0 ? t('usage.kpi.failureRate', { value: f.percent(rate) }) : null} tone={totals && totals.failures > 0 ? 'danger' : 'neutral'} />
      <KpiTile icon={Zap} label={t('usage.kpi.avgTtft')} value={totals ? (totals.averageTimeToFirstTokenMs !== null && totals.averageTimeToFirstTokenMs !== undefined ? f.duration(totals.averageTimeToFirstTokenMs) : '-') : '...'} />
      <KpiTile icon={Clock} label={t('usage.kpi.p95Ttft')} value={totals ? (totals.p95TimeToFirstTokenMs !== null && totals.p95TimeToFirstTokenMs !== undefined ? f.duration(totals.p95TimeToFirstTokenMs) : '-') : '...'} />
    </div>
  );
}

UsageKpis.propTypes = { totals: PropTypes.object };

export function RangeControl({ rangeId, onChange }) {
  const { t } = useTranslation();
  return (
    <div className="segmented" role="group" aria-label={t('charts.range')}>
      {USAGE_RANGE_IDS.map((id) => (
        <button type="button" key={id} className={rangeId === id ? 'active' : ''} aria-pressed={rangeId === id} onClick={() => onChange(id)}>
          {t('charts.ranges.' + id)}
        </button>
      ))}
    </div>
  );
}

RangeControl.propTypes = { rangeId: PropTypes.string.isRequired, onChange: PropTypes.func.isRequired };

export function ExportCsvButton({ report, prefix }) {
  const { t } = useTranslation();
  return (
    <button
      type="button"
      className="button secondary small"
      disabled={!report}
      onClick={() => downloadText(usageCsvFilename(prefix, report.groupBy, report.fromUtc, report.toUtc), usageReportCsv(report))}
    >
      <Download size={14} aria-hidden="true" /> {t('usage.exportCsv')}
    </button>
  );
}

ExportCsvButton.propTypes = { report: PropTypes.object, prefix: PropTypes.string.isRequired };

// Pickers for the trend chart: what to plot and, when groups are offered, how to split it.
export function TrendControls({ id, measure, onMeasure, groupBy, onGroupBy, groups }) {
  const { t } = useTranslation();
  return (
    <>
      {groups && onGroupBy ? (
        <label className="inline-field" htmlFor={id + '-group'}>
          <span>{t('usage.charts.groupBy')}</span>
          <select id={id + '-group'} value={groupBy || ''} onChange={(e) => onGroupBy(e.target.value)}>
            {groups.map((g) => (
              <option key={g || 'none'} value={g}>
                {g ? t('usage.groups.' + g) : t('usage.trend.none')}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      {onMeasure ? (
        <label className="inline-field" htmlFor={id + '-measure'}>
          <span>{t('usage.trend.measure')}</span>
          <select id={id + '-measure'} value={measure} onChange={(e) => onMeasure(e.target.value)}>
            {TREND_MEASURES.map((m) => (
              <option key={m} value={m}>
                {measureLabel(m, t)}
              </option>
            ))}
          </select>
        </label>
      ) : null}
    </>
  );
}

TrendControls.propTypes = {
  id: PropTypes.string.isRequired,
  measure: PropTypes.string,
  onMeasure: PropTypes.func,
  groupBy: PropTypes.string,
  onGroupBy: PropTypes.func,
  groups: PropTypes.arrayOf(PropTypes.string)
};

// One measure over time from the gap-filled time series: a line per group when the series is grouped (or stacked
// columns with stacked), otherwise a single line. The tooltip always carries the bucket's other measures.
export function UsageTrend({ series, ariaLabel, measure = 'cost', stacked = false }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (!series) return null;
  const source = series.buckets || [];
  if (source.length === 0) return <div className="chart-empty">{t('usage.noData')}</div>;
  const ms = (v) => (v === null || v === undefined ? '-' : f.duration(v));
  const formatValue = measure === 'cost' ? (v) => f.usd(v) : measure === 'ttft' ? (v) => f.duration(v) : measure === 'calls' || measure === 'toolCalls' ? (v) => f.number(v) : (v) => f.compact(v);
  const extra = (b) =>
    [
      { key: 'cost', label: t('usage.columns.cost'), value: f.usd(b.costUsd || 0) },
      { key: 'calls', label: t('usage.columns.calls'), value: f.number(b.calls || 0) },
      { key: 'failures', label: t('usage.columns.failures'), value: f.number(b.failures || 0) },
      { key: 'tokens', label: t('usage.columns.tokens'), value: f.compact(measureValue(b, 'tokens')) },
      { key: 'cacheWriteTokens', label: t('usage.columns.cacheWriteTokens'), value: f.compact(b.cacheWriteTokens || 0) },
      { key: 'reasoningTokens', label: t('usage.columns.reasoningTokens'), value: f.compact(b.reasoningTokens || 0) },
      { key: 'toolCalls', label: t('usage.columns.toolCalls'), value: f.number(b.toolCalls || 0) },
      { key: 'ttft', label: t('usage.columns.p95Ttft'), value: ms(b.p95TimeToFirstTokenMs) }
    ]
      // The plotted measure is already in the tooltip as a series line, unless the series is split into groups.
      .filter((line) => line.key !== measure || Boolean(series.groupBy))
      .map((line) => ({ label: line.label, value: line.value }));
  const lines = trendSeries(series, measure, t);
  if (stacked) {
    const label = (b) => (series.bucketMinutes >= 1440 ? f.date(b.bucketStartUtc, { month: 'short', day: 'numeric' }) : f.dateTime(b.bucketStartUtc, { month: 'short', day: 'numeric', hour: 'numeric' }));
    return <StackedColumnChart ariaLabel={ariaLabel} formatValue={formatValue} categories={source.map((b) => ({ key: b.bucketStartUtc, label: label(b), extra: extra(b) }))} series={lines} />;
  }
  return (
    <LineChart
      ariaLabel={ariaLabel}
      bucketMinutes={series.bucketMinutes}
      buckets={source.map((b) => ({ startUtc: b.bucketStartUtc, endUtc: b.bucketEndUtc, extra: extra(b) }))}
      formatValue={formatValue}
      area={lines.length === 1}
      series={lines}
    />
  );
}

UsageTrend.propTypes = { series: PropTypes.object, ariaLabel: PropTypes.string.isRequired, measure: PropTypes.string, stacked: PropTypes.bool };

// The full report as a table: every token type, tool calls, cost, share, and timing.
export function UsageTable({ report, onRowClick }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (!report) return null;
  const rows = report.rows || [];
  if (rows.length === 0) return <div className="chart-empty">{t('usage.noData')}</div>;
  const ms = (v) => (v === null || v === undefined ? '-' : f.duration(v));
  const totals = report.totals || {};
  return (
    <div className="table-scroll">
      <table className="ranked-table" aria-label={t('usage.breakdown.title')}>
        <thead>
          <tr>
            <th scope="col">{t('usage.groups.' + report.groupBy)}</th>
            <th scope="col" className="align-right">
              {t('usage.columns.cost')}
            </th>
            <th scope="col" className="align-right">
              {t('charts.share')}
            </th>
            <th scope="col" className="align-right">
              {t('usage.columns.calls')}
            </th>
            <th scope="col" className="align-right">
              {t('usage.columns.failures')}
            </th>
            <th scope="col" className="align-right">
              {t('usage.columns.inputTokens')}
            </th>
            <th scope="col" className="align-right hide-narrow">
              {t('usage.columns.cachedInputTokens')}
            </th>
            <th scope="col" className="align-right hide-narrow">
              {t('usage.columns.cacheWriteTokens')}
            </th>
            <th scope="col" className="align-right">
              {t('usage.columns.outputTokens')}
            </th>
            <th scope="col" className="align-right hide-narrow">
              {t('usage.columns.reasoningTokens')}
            </th>
            <th scope="col" className="align-right hide-narrow">
              {t('usage.columns.toolCalls')}
            </th>
            <th scope="col" className="align-right hide-narrow">
              {t('usage.columns.avgDuration')}
            </th>
            <th scope="col" className="align-right">
              {t('usage.columns.avgTtft')}
            </th>
            <th scope="col" className="align-right hide-narrow">
              {t('usage.columns.p95Ttft')}
            </th>
          </tr>
        </thead>
        <tbody>
          {[...rows, { ...totals, key: '__total' }].map((r) => {
            const isTotal = r.key === '__total';
            return (
              <tr key={r.key} className={onRowClick && !isTotal ? 'clickable' : ''} onClick={() => onRowClick && !isTotal && onRowClick(r)}>
                <td className="ranked-label">
                  <span className="truncate" title={isTotal ? undefined : r.key}>
                    {isTotal ? <strong>{t('usage.total')}</strong> : rowLabel(r, report.groupBy, t)}
                  </span>
                </td>
                <td className="align-right">{f.usd(r.costUsd || 0)}</td>
                <td className="align-right">{isTotal ? '' : f.percent(r.costShare || 0)}</td>
                <td className="align-right">{f.number(r.calls || 0)}</td>
                <td className="align-right">{f.number(r.failures || 0)}</td>
                <td className="align-right">{f.number(r.inputTokens || 0)}</td>
                <td className="align-right hide-narrow">{f.number(r.cachedInputTokens || 0)}</td>
                <td className="align-right hide-narrow">{f.number(r.cacheWriteTokens || 0)}</td>
                <td className="align-right">{f.number(r.outputTokens || 0)}</td>
                <td className="align-right hide-narrow">{f.number(r.reasoningTokens || 0)}</td>
                <td className="align-right hide-narrow">{f.number(r.toolCalls || 0)}</td>
                <td className="align-right hide-narrow">{ms(r.averageDurationMs)}</td>
                <td className="align-right">{ms(r.averageTimeToFirstTokenMs)}</td>
                <td className="align-right hide-narrow">{ms(r.p95TimeToFirstTokenMs)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

UsageTable.propTypes = { report: PropTypes.object, onRowClick: PropTypes.func };
