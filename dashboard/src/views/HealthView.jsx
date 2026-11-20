import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useInterval from '../hooks/useInterval.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import ErrorState from '../components/ErrorState.jsx';
import KpiTile from '../components/KpiTile.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import RunCharts from './runs/RunCharts.jsx';

const INTERVALS = [0, 5000, 15000, 60000];

function tone(status) {
  if (status === 'Healthy' || status === 'Enabled') return 'success';
  if (status === 'Disabled') return 'neutral';
  return 'danger';
}

export default function HealthView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [health, setHealth] = useState(null);
  const [error, setError] = useState(null);
  const [checkedAt, setCheckedAt] = useState(null);
  const [interval, setIntervalMs] = useLocalStorage('mymorningnews.health.interval', 15000);

  const load = useCallback(() => {
    apiClient
      .healthCheck()
      .then((h) => {
        setHealth(h);
        setError(null);
      })
      .catch(setError)
      .finally(() => setCheckedAt(new Date()));
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);
  useInterval(load, interval);

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.health')}
        summary={t('routes.health.summary')}
        meta={checkedAt ? <span className="muted small">{t('health.checkedAt', { time: f.time(checkedAt) })}</span> : null}
        actions={
          <>
            <label className="inline-field">
              <span>{t('health.autoRefresh')}</span>
              <select value={interval} onChange={(e) => setIntervalMs(Number(e.target.value))}>
                {INTERVALS.map((ms) => (
                  <option key={ms} value={ms}>
                    {ms === 0 ? t('health.off') : f.duration(ms)}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" className="button secondary" onClick={load}>
              {t('common.refresh')}
            </button>
          </>
        }
      />
      <ErrorState error={error} onRetry={load} />
      {!health && !error ? <LoadingState /> : null}
      {health ? (
        <>
          <div className="kpi-grid">
            <KpiTile label={t('health.overall')} value={t('health.status.' + health.status, { defaultValue: health.status })} tone={tone(health.status)} />
            <KpiTile label={t('health.version')} value={health.version} />
            <KpiTile label={t('health.uptime')} value={f.seconds(health.uptimeSeconds)} detail={f.dateTime(health.startedUtc)} />
          </div>
          <section className="panel">
            <h2>{t('health.components')}</h2>
            <table className="kv-table">
              <tbody>
                {Object.entries(health.components || {}).map(([name, status]) => (
                  <tr key={name}>
                    <th scope="row">{t('health.componentNames.' + name, { defaultValue: name })}</th>
                    <td>
                      <StatusBadge tone={tone(status)}>{t('health.status.' + status, { defaultValue: status })}</StatusBadge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </>
      ) : null}
      <RunCharts charts={['stages']} tenantWide rangeStorage="mymorningnews.health.runCharts" />
    </div>
  );
}
