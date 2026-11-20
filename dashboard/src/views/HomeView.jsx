import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Activity, Building2, CircleAlert, Gauge, History, Lightbulb, Send, Users } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import RequestActivityChart from '../charts/RequestActivityChart.jsx';
import CopyButton from '../components/CopyButton.jsx';
import DateCell from '../components/DateCell.jsx';
import EmptyState from '../components/EmptyState.jsx';
import ErrorState from '../components/ErrorState.jsx';
import KpiTile from '../components/KpiTile.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge, { methodTone, statusTone } from '../components/StatusBadge.jsx';
import OverviewTiles from './insights/OverviewTiles.jsx';
import { insightsQuery } from './insights/insightsData.js';
import RequestForm from './requests/RequestForm.jsx';
import RunCharts from './runs/RunCharts.jsx';

// Headline numbers Home shows from the Insights overview, for the last OVERVIEW_DAYS days.
const OVERVIEW_KEYS = ['runs', 'findings', 'precision', 'briefings', 'feedback', 'cost'];
const OVERVIEW_DAYS = 30;

// Command center: what happened, what needs attention, and where to go next.
export default function HomeView() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient, identity, isTenantAdmin } = useAuth();
  const [rangeId, setRangeId] = useLocalStorage('mymorningnews.home.range', 'day');
  const [health, setHealth] = useState(null);
  const [info, setInfo] = useState(null);
  const [counts, setCounts] = useState({});
  const [topicCount, setTopicCount] = useState(null);
  const [failures, setFailures] = useState(null);
  const [summary, setSummary] = useState(null);
  const [overview, setOverview] = useState(null);
  const [errors, setErrors] = useState({});
  const [loadedAt, setLoadedAt] = useState(null);

  const load = useCallback(async () => {
    const nextErrors = {};
    const tenantId = identity ? identity.tenantId : 'default';
    await Promise.all([
      apiClient.healthCheck().then(setHealth).catch((e) => (nextErrors.health = e)),
      apiClient.getServerInfo().then(setInfo).catch((e) => (nextErrors.info = e)),
      apiClient
        .getAnalytics('overview', insightsQuery({ days: OVERVIEW_DAYS }))
        .then((r) => setOverview(r || false))
        .catch(() => setOverview(false)),
      apiClient
        .listTopics({ isOneTime: false, pageSize: 1 })
        .then((r) => setTopicCount(r && typeof r.totalCount === 'number' ? r.totalCount : 0))
        .catch(() => setTopicCount(null)),
      isTenantAdmin
        ? Promise.all([apiClient.listUsers(tenantId, { pageSize: 1 }), apiClient.listTenants({ pageSize: 1 })])
            .then(([u, ten]) => setCounts({ users: u.totalCount, tenants: ten.totalCount }))
            .catch((e) => (nextErrors.counts = e))
        : Promise.resolve(),
      isTenantAdmin
        ? apiClient
            .getRequestHistory({ minStatusCode: 400, pageSize: 6, sortBy: 'createdUtc', sortDirection: 'Descending' })
            .then((r) => setFailures(r.items))
            .catch((e) => (nextErrors.failures = e))
        : Promise.resolve()
    ]);
    setErrors(nextErrors);
    setLoadedAt(new Date());
  }, [apiClient, identity, isTenantAdmin]);

  useEffect(() => {
    load();
  }, [load]);

  const failureRate = summary && summary.totalCount > 0 ? summary.totalFailure / summary.totalCount : 0;

  return (
    <div className="page">
      <PageHeader
        title={t('home.title', { name: identity && identity.email ? identity.email.split('@')[0] : '' })}
        summary={t('home.summary')}
        meta={loadedAt ? <span className="muted small">{t('common.lastLoaded', { time: f.time(loadedAt) })}</span> : null}
        actions={
          <button type="button" className="button secondary" onClick={load}>
            {t('common.refresh')}
          </button>
        }
      />

      <div className="kpi-grid">
        <KpiTile
          icon={Gauge}
          label={t('home.kpi.health')}
          value={health ? t('health.states.' + (health.status === 'Healthy' ? 'healthy' : 'degraded')) : errors.health ? t('health.states.unreachable') : '...'}
          tone={health && health.status === 'Healthy' ? 'success' : 'danger'}
          detail={info ? t('home.kpi.uptime', { value: f.seconds(info.uptimeSeconds) }) : null}
          onClick={() => navigate('/dashboard/health')}
        />
        {isTenantAdmin ? (
          <>
            <KpiTile icon={History} label={t('home.kpi.requests')} value={summary ? f.number(summary.totalCount) : '...'} detail={t('charts.ranges.' + rangeId)} onClick={() => navigate('/dashboard/requests')} />
            <KpiTile
              icon={CircleAlert}
              label={t('home.kpi.failureRate')}
              value={summary ? f.percent(failureRate) : '...'}
              tone={failureRate > 0.05 ? 'danger' : failureRate > 0 ? 'warning' : 'success'}
              detail={summary ? t('home.kpi.failures', { count: f.number(summary.totalFailure) }) : null}
              onClick={() => navigate('/dashboard/requests')}
            />
            <KpiTile icon={Activity} label={t('home.kpi.latency')} value={summary ? f.duration(summary.averageDurationMs) : '...'} detail={t('home.kpi.average')} />
            <KpiTile icon={Users} label={t('home.kpi.users')} value={counts.users !== undefined ? f.number(counts.users) : '...'} onClick={() => navigate('/dashboard/admin/users')} />
            <KpiTile icon={Building2} label={t('home.kpi.tenants')} value={counts.tenants !== undefined ? f.number(counts.tenants) : '...'} onClick={() => navigate('/dashboard/admin/tenants')} />
          </>
        ) : null}
        <KpiTile icon={Lightbulb} label={t('home.kpi.topics')} value={topicCount === null ? '...' : f.number(topicCount)} detail={t('home.kpi.recurring')} onClick={() => navigate('/dashboard/topics')} />
      </div>

      {overview !== false ? (
        <section className="stack home-overview" data-testid="home-overview">
          <div className="section-heading">
            <h2>{t('home.overview.title', { count: OVERVIEW_DAYS })}</h2>
            <Link to="/dashboard/insights/overview" className="link-button small">
              {t('home.overview.more')}
            </Link>
          </div>
          <OverviewTiles report={overview} keys={OVERVIEW_KEYS} testId="home-overview-tiles" />
        </section>
      ) : null}

      <RunCharts charts={['status', 'activity']} rangeStorage="mymorningnews.home.runCharts" />

      <section className="panel home-request" data-testid="home-request">
        <h2>
          <Send size={16} aria-hidden="true" /> {t('home.request.title')}
        </h2>
        <p className="muted small">{t('home.request.summary')}</p>
        <RequestForm compact />
      </section>

      {isTenantAdmin ? (
        <RequestActivityChart
          rangeId={rangeId}
          onRangeChange={setRangeId}
          onSummary={setSummary}
          onBucketClick={({ fromUtc, toUtc }) => navigate('/dashboard/requests?fromUtc=' + encodeURIComponent(fromUtc) + '&toUtc=' + encodeURIComponent(toUtc))}
        />
      ) : null}

      <div className="two-column">
        <section className="panel">
          <h2>{t('home.next.title')}</h2>
          <ul className="cta-list">
            <li>
              <button type="button" className="link-button" onClick={() => navigate(topicCount ? '/dashboard/topics' : '/dashboard/topics/new')}>
                {topicCount ? t('home.next.topics') : t('home.next.topic')}
              </button>
            </li>
            <li>
              <button type="button" className="link-button" onClick={() => navigate('/dashboard/models')}>
                {t('home.next.models')}
              </button>
              <span className="muted small">{t('nav.phase', { phase: 3 })}</span>
            </li>
            <li>
              <button type="button" className="link-button" onClick={() => navigate('/dashboard/profile')}>
                {t('home.next.password')}
              </button>
            </li>
            <li>
              <button type="button" className="link-button" onClick={() => navigate('/dashboard/explorer')}>
                {t('home.next.explorer')}
              </button>
            </li>
          </ul>
        </section>
        {isTenantAdmin ? (
          <section className="panel">
            <h2>{t('home.failures.title')}</h2>
            {errors.failures ? <ErrorState error={errors.failures} inline onRetry={load} /> : null}
            {failures && failures.length === 0 ? <EmptyState title={t('home.failures.none')} message={t('home.failures.noneMessage')} /> : null}
            {failures && failures.length > 0 ? (
              <ul className="event-list">
                {failures.map((r) => (
                  <li key={r.id}>
                    <button type="button" className="link-button" onClick={() => navigate('/dashboard/requests?pathContains=' + encodeURIComponent(r.path))}>
                      <StatusBadge tone={methodTone(r.method)} mono>
                        {r.method}
                      </StatusBadge>{' '}
                      <span className="mono truncate">{r.path}</span>
                    </button>
                    <StatusBadge tone={statusTone(r.statusCode)}>{String(r.statusCode)}</StatusBadge>
                    <DateCell value={r.createdUtc} />
                  </li>
                ))}
              </ul>
            ) : null}
          </section>
        ) : null}
      </div>

      <section className="panel external-services">
        <h2>{t('home.services.title')}</h2>
        <p className="muted small">{t('home.services.summary')}</p>
        {errors.info ? <ErrorState error={errors.info} inline onRetry={load} /> : null}
        {info && info.externalServices && info.externalServices.length > 0 ? (
          <div className="service-grid">
            {info.externalServices.map((s) => (
              <div className="service-item" key={s.name}>
                <strong>{s.name}</strong>
                {s.description ? <span className="muted small">{s.description}</span> : null}
                <span className="service-url">
                  <a href={s.url} target="_blank" rel="noreferrer" className="mono truncate">
                    {s.url}
                  </a>
                  <CopyButton value={s.url} labelKey="common.copyUrl" />
                </span>
                {s.credentials ? (
                  <span className="small">
                    {t('home.services.credentials')} <code>{s.credentials}</code>
                  </span>
                ) : null}
              </div>
            ))}
          </div>
        ) : info ? (
          <EmptyState title={t('home.services.none')} />
        ) : null}
      </section>
    </div>
  );
}
