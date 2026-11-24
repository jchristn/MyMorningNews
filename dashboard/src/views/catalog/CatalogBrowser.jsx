import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import TablePagination from '../../components/TablePagination.jsx';

export default function CatalogBrowser() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [page, setPage] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(12);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(null);
  const load = useCallback(async () => {
    try {
      setPage(await apiClient.listCatalog({ pageNumber, pageSize }));
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, pageNumber, pageSize]);
  useEffect(() => {
    load();
  }, [load]);
  const toggle = async (entry, preferences = null) => {
    setBusy(entry.id);
    setError(null);
    try {
      if (preferences) await apiClient.subscribeCatalog(entry.id, { emailEnabled: entry.emailEnabled ?? true, pushEnabled: entry.pushEnabled ?? true, ...preferences });
      else if (entry.subscribed) await apiClient.unsubscribeCatalog(entry.id);
      else await apiClient.subscribeCatalog(entry.id);
      await load();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(null);
    }
  };
  return (
    <div className="page">
      <PageHeader
        title={t('catalog.title')}
        summary={t('catalog.summary')}
        actions={
          <Link className="button secondary" to="/dashboard/catalog/issues">
            {t('catalog.issues')}
          </Link>
        }
      />
      <ErrorState error={error} onRetry={load} />
      {!page && !error ? <LoadingState /> : null}
      {page?.items.length === 0 ? (
        <section className="panel">
          <p className="muted">{t('catalog.empty')}</p>
        </section>
      ) : null}
      <div className="plan-grid">
        {page?.items.map((entry) => (
          <section className="panel stack" key={entry.id}>
            <div className="section-title">
              <h2 dir="auto">{entry.title}</h2>
              <StatusBadge tone={entry.eligible ? 'success' : 'neutral'}>
                {t(entry.eligible && !entry.suspended ? 'catalog.included' : 'catalog.restricted')}
              </StatusBadge>
            </div>
            <p className="muted" dir="auto">
              {entry.description}
            </p>
            <p className="small muted">
              {entry.category} · {entry.language}
            </p>
            {entry.subscribed && !entry.suspended ? <div className="stack">
              <label className="checkbox-row"><input type="checkbox" checked={entry.emailEnabled ?? true} disabled={Boolean(busy)} onChange={e => toggle(entry, { emailEnabled: e.target.checked })} /> {t('notifications.channels.Email')}</label>
              <label className="checkbox-row"><input type="checkbox" checked={entry.pushEnabled ?? true} disabled={Boolean(busy)} onChange={e => toggle(entry, { pushEnabled: e.target.checked })} /> {t('notifications.channels.Push')}</label>
            </div> : null}
            <div className="row-actions">
              <button
                type="button"
                className={'button ' + (entry.subscribed ? 'secondary' : 'primary')}
                disabled={Boolean(busy) || (!entry.eligible && !entry.subscribed)}
                onClick={() => toggle(entry)}
              >
                {t(entry.subscribed ? 'catalog.unsubscribe' : 'catalog.subscribe')}
              </button>
              {entry.subscribed && !entry.suspended ? (
                <Link className="button secondary" to={'/dashboard/catalog/' + entry.id + '/issues'}>
                  {t('catalog.read')}
                </Link>
              ) : null}
            </div>
          </section>
        ))}
      </div>
      {page ? (
        <TablePagination
          pageNumber={pageNumber}
          totalPages={page.totalPages}
          pageSize={pageSize}
          pageSizes={[12, 24, 48]}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPageNumber(1);
          }}
          disabled={Boolean(busy)}
        />
      ) : null}
    </div>
  );
}
