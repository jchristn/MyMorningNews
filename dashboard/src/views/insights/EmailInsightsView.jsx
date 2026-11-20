import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { EMAIL_STATUS_COLORS, EMAIL_STATUSES } from '../../utils/email.js';
import ChartCard from '../../charts/ChartCard.jsx';
import HorizontalBarChart from '../../charts/HorizontalBarChart.jsx';
import TimeChart from '../../charts/TimeChart.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import { dayBuckets, rateBars } from './insightsData.js';
import { RateChart, ScopeBar, pctOrDash, useInsightsScope, useReport, useTopics } from './insightsParts.jsx';

// Email delivery for the chosen range: totals, the delivery rate, mean time to send, messages per day (sent and not
// sent), delivery by template, messages by status, and how readers use the feedback and unsubscribe links.
export default function EmailInsightsView() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const topics = useTopics();
  const scope = useInsightsScope();
  const email = useReport('email', scope);
  const r = email.data;
  const n = (v) => (r ? f.number(v || 0) : '-');
  const days = dayBuckets(r && r.byDay);
  const templateLabel = (row) => t('email.templates.' + row.key, { defaultValue: row.label || row.key });
  const byTemplate = rateBars(r && r.byTemplate, templateLabel);
  const order = (key) => (EMAIL_STATUSES.includes(key) ? EMAIL_STATUSES.indexOf(key) : EMAIL_STATUSES.length);
  const byStatus = [...((r && r.byStatus) || [])]
    .filter((row) => (row.count || 0) > 0)
    .sort((a, b) => order(a.key) - order(b.key))
    .map((row) => ({ key: String(row.key), label: t('email.statuses.' + row.key, { defaultValue: row.label || row.key }), value: row.count || 0, color: EMAIL_STATUS_COLORS[row.key] }));
  const submitRate = r && r.feedbackLinkOpens > 0 ? r.feedbackLinkSubmits / r.feedbackLinkOpens : null;

  return (
    <div className="page">
      <PageHeader title={t('nav.items.emailInsights')} summary={t('routes.emailInsights.summary')} />
      <ScopeBar scope={scope} topics={topics} onRefresh={email.reload} loading={email.loading} />
      <ErrorState error={email.error} onRetry={email.reload} />
      <div className="kpi-grid" data-testid="email-kpis">
        <KpiTile label={t('insights.email.total')} value={n(r && r.total)} detail={r ? t('insights.email.pendingSkipped', { pending: f.number(r.pending || 0), skipped: f.number(r.skipped || 0) }) : null} onClick={() => navigate('/dashboard/email')} />
        <KpiTile label={t('insights.email.sent')} value={n(r && r.sent)} tone="success" />
        <KpiTile label={t('insights.email.failed')} value={n(r && r.failed)} tone={r && r.failed > 0 ? 'danger' : 'neutral'} />
        <KpiTile label={t('insights.email.deliveryRate')} value={r ? pctOrDash(f, r.deliveryRate, 1) : '-'} detail={t('insights.email.deliveryRateHelp')} tone="info" />
        <KpiTile
          label={t('insights.email.meanTimeToSend')}
          value={r && r.meanSecondsToSend !== null && r.meanSecondsToSend !== undefined ? f.duration(Number(r.meanSecondsToSend) * 1000) : '-'}
          detail={t('insights.email.meanTimeToSendHelp')}
        />
        <KpiTile label={t('insights.email.linkOpens')} value={n(r && r.feedbackLinkOpens)} tone="info" />
        <KpiTile label={t('insights.email.linkSubmits')} value={n(r && r.feedbackLinkSubmits)} detail={submitRate !== null ? t('insights.feedback.submitRate', { rate: pctOrDash(f, submitRate) }) : null} tone="success" />
        <KpiTile label={t('insights.email.unsubscribes')} value={n(r && r.unsubscribes)} tone={r && r.unsubscribes > 0 ? 'warning' : 'neutral'} />
      </div>
      <div className="chart-grid">
        <ChartCard title={t('insights.email.byDay')} subtitle={t('insights.email.byDayHelp')} className="wide">
          {days.length === 0 ? (
            <div className="chart-empty">{t('insights.noData')}</div>
          ) : (
            <TimeChart
              ariaLabel={t('insights.email.byDay')}
              bucketMinutes={1440}
              buckets={days.map((b) => ({
                startUtc: b.startUtc,
                endUtc: b.endUtc,
                extra: b.row.rate !== null && b.row.rate !== undefined ? [{ label: t('insights.email.deliveryRate'), value: pctOrDash(f, b.row.rate) }] : []
              }))}
              series={[
                { key: 'sent', label: t('insights.email.sent'), color: EMAIL_STATUS_COLORS.Sent, values: days.map((b) => b.row.positive || 0) },
                { key: 'notSent', label: t('insights.email.notSent'), color: EMAIL_STATUS_COLORS.Failed, values: days.map((b) => Math.max(0, (b.row.count || 0) - (b.row.positive || 0))) }
              ]}
            />
          )}
        </ChartCard>
        <ChartCard title={t('insights.email.byTemplate')} subtitle={t('insights.email.byTemplateHelp')}>
          <RateChart items={byTemplate} ariaLabel={t('insights.email.byTemplate')} loading={email.loading} color="var(--chart-series-2)" />
        </ChartCard>
        <ChartCard title={t('insights.email.byStatus')}>
          {byStatus.length > 0 ? <HorizontalBarChart ariaLabel={t('insights.email.byStatus')} items={byStatus} color="var(--chart-series-1)" /> : <div className="chart-empty">{t('insights.noData')}</div>}
        </ChartCard>
      </div>
    </div>
  );
}
