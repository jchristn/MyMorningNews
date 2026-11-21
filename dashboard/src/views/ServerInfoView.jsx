import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import CopyButton from '../components/CopyButton.jsx';
import ErrorState from '../components/ErrorState.jsx';
import KeyValueGrid from '../components/KeyValueGrid.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';

// The server rows a viewer sees. Runtime, operating system, database, schema, and pending-restart details are for
// global administrators only (the server stops sending them to others); a row whose value is missing is left out.
export function serverRows(info, { isAdmin, t, f }) {
  const rows = [
    { key: 'product', label: t('serverInfo.product'), value: info.product },
    { key: 'version', label: t('serverInfo.version'), value: info.version, mono: true },
    { key: 'started', label: t('serverInfo.started'), value: info.startedUtc ? f.dateTime(info.startedUtc) : null },
    { key: 'uptime', label: t('serverInfo.uptime'), value: typeof info.uptimeSeconds === 'number' ? f.seconds(info.uptimeSeconds) : null }
  ];
  if (!isAdmin) return rows;
  return rows.concat(
    [
      { key: 'db', label: t('serverInfo.database'), value: info.databaseType ? info.databaseType + (info.schemaVersion !== undefined && info.schemaVersion !== null ? ' / ' + t('serverInfo.schemaVersion', { version: info.schemaVersion }) : '') : null },
      { key: 'vector', label: t('serverInfo.vector'), value: typeof info.vectorAvailable === 'boolean' ? (info.vectorAvailable ? t('common.yes') : t('common.no')) : null },
      { key: 'runtime', label: t('serverInfo.runtime'), value: info.runtimeVersion, mono: true },
      { key: 'os', label: t('serverInfo.os'), value: info.operatingSystem },
      { key: 'pending', label: t('serverInfo.pendingRestart'), value: info.pendingRestart && info.pendingRestart.length > 0 ? f.list(info.pendingRestart) : t('common.none') }
    ].filter((r) => r.value !== undefined && r.value !== null && r.value !== '')
  );
}

export default function ServerInfoView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient, serverUrl, identity, isAdmin } = useAuth();
  const [info, setInfo] = useState(null);
  const [error, setError] = useState(null);

  const load = useCallback(() => {
    setError(null);
    apiClient.getServerInfo().then(setInfo).catch(setError);
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  const urls = [
    { key: 'api', label: t('serverInfo.apiBase'), value: serverUrl },
    { key: 'openapi', label: t('serverInfo.openapi'), value: serverUrl + '/openapi.json' },
    { key: 'swagger', label: t('serverInfo.swagger'), value: serverUrl + '/swagger' },
    { key: 'health', label: t('serverInfo.health'), value: serverUrl + '/v1.0/api/health' }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.serverInfo')} summary={t('routes.serverInfo.summary')} actions={<button type="button" className="button secondary" onClick={load}>{t('common.refresh')}</button>} />
      <ErrorState error={error} onRetry={load} />
      {!info && !error ? <LoadingState /> : null}
      {info ? (
        <div className="two-column">
          <section className="panel">
            <h2>{t('serverInfo.server')}</h2>
            <KeyValueGrid rows={serverRows(info, { isAdmin, t, f })} />
          </section>
          <section className="panel">
            <h2>{t('serverInfo.session')}</h2>
            <KeyValueGrid
              rows={[
                { key: 'tenant', label: t('topbar.tenant'), value: identity ? identity.tenantId : '', mono: true },
                { key: 'user', label: t('serverInfo.principal'), value: identity ? identity.email || identity.displayName : '' },
                { key: 'type', label: t('serverInfo.principalType'), value: identity ? identity.principalType : '' },
                { key: 'expires', label: t('serverInfo.expires'), value: identity ? f.dateTime(identity.expiresUtc) : '' }
              ]}
            />
            <h2>{t('serverInfo.urls')}</h2>
            <ul className="url-list">
              {urls.map((u) => (
                <li key={u.key}>
                  <span className="field-label">{u.label}</span>
                  <span className="service-url">
                    <code className="mono truncate">{u.value}</code>
                    <CopyButton value={u.value} labelKey="common.copyUrl" />
                  </span>
                </li>
              ))}
            </ul>
            <a href={info.repositoryUrl} target="_blank" rel="noreferrer">
              {t('serverInfo.repository')}
            </a>
          </section>
        </div>
      ) : null}
    </div>
  );
}
