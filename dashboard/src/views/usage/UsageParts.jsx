import PropTypes from 'prop-types';
import { Clock, Coins, Download, Hash, Layers, TriangleAlert, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import LineChart from '../../charts/LineChart.jsx';
import { USAGE_RANGE_IDS } from '../../charts/ranges.js';
import KpiTile from '../../components/KpiTile.jsx';
import { downloadText, usageCsvFilename, usageReportCsv } from '../../utils/csv.js';
import { failureRate, rowLabel } from './usageData.js';

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

// Cost (and calls) over time from the gap-filled time series.
export function CostTrend({ series, ariaLabel }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (!series) return null;
  const buckets = (series.buckets || []).map((b) => ({
    startUtc: b.bucketStartUtc,
    endUtc: b.bucketEndUtc,
    extra: [
      { label: t('usage.columns.calls'), value: f.number(b.calls) },
      { label: t('usage.columns.failures'), value: f.number(b.failures) },
      { label: t('usage.columns.tokens'), value: f.compact((b.inputTokens || 0) + (b.outputTokens || 0)) }
    ]
  }));
  if (buckets.length === 0) return <div className="chart-empty">{t('usage.noData')}</div>;
  return (
    <LineChart
      ariaLabel={ariaLabel}
      bucketMinutes={series.bucketMinutes}
      buckets={buckets}
      formatValue={(v) => f.usd(v)}
      series={[{ key: 'cost', label: t('usage.kpi.cost'), color: 'var(--chart-series-1)', values: (series.buckets || []).map((b) => Number(b.costUsd) || 0) }]}
    />
  );
}

CostTrend.propTypes = { series: PropTypes.object, ariaLabel: PropTypes.string.isRequired };

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
