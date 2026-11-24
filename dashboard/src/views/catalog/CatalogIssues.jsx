import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import AudioPlayer from '../../components/AudioPlayer.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import MarkdownView from '../../components/MarkdownView.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import TablePagination from '../../components/TablePagination.jsx';

export function CatalogIssues({ publicationId = null, compact = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [page, setPage] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(compact ? 5 : 25);
  const [error, setError] = useState(null);
  const [playingId, setPlayingId] = useState(null);
  const load = useCallback(async () => {
    try {
      const query = { pageNumber, pageSize };
      setPage(publicationId ? await apiClient.listPublicationIssues(publicationId, query) : await apiClient.listCatalogIssues(query));
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, publicationId, pageNumber, pageSize]);
  useEffect(() => {
    load();
  }, [load]);
  return (
    <section className="panel stack">
      <div className="section-title">
        <h2>{t('catalog.issues')}</h2>
        <Link to="/dashboard/catalog">{t('catalog.title')}</Link>
      </div>
      <ErrorState error={error} onRetry={load} />
      {!page && !error ? <LoadingState /> : null}
      {page?.items.length === 0 ? <p className="muted">{t('catalog.noIssues')}</p> : null}
      <ul className="event-list recent-briefings">
        {page?.items.map((issue) => (
          <li key={issue.id}>
            <div className="recent-briefing-row">
              <Link to={'/dashboard/catalog/' + issue.publicationId + '/issues/' + issue.id}>{issue.title}</Link>
              <DateCell value={issue.createdUtc} />
              {issue.audioPath ? (
                <button
                  type="button"
                  className="button secondary small"
                  aria-pressed={playingId === issue.id}
                  onClick={() => setPlayingId(playingId === issue.id ? null : issue.id)}
                >
                  {t('audio.player.play')}
                </button>
              ) : null}
            </div>
            {playingId === issue.id ? (
              <AudioPlayer
                key={issue.id}
                catalogPublicationId={issue.publicationId}
                catalogIssueId={issue.id}
                title={issue.title}
                durationSeconds={issue.audioSeconds || 0}
                compact
                autoPlay
              />
            ) : null}
          </li>
        ))}
      </ul>
      {page && !compact ? (
        <TablePagination
          pageNumber={pageNumber}
          totalPages={page.totalPages}
          pageSize={pageSize}
          pageSizes={[25, 50, 100]}
          onPageChange={setPageNumber}
          onPageSizeChange={(size) => {
            setPageSize(size);
            setPageNumber(1);
          }}
        />
      ) : null}
      {compact ? <Link to="/dashboard/catalog/issues">{t('catalog.read')}</Link> : null}
    </section>
  );
}
CatalogIssues.propTypes = { publicationId: PropTypes.string, compact: PropTypes.bool };

export function CatalogIssuesPage() {
  const { t } = useTranslation();
  return (
    <div className="page">
      <PageHeader title={t('catalog.title')} />
      <CatalogIssues />
    </div>
  );
}
export function PublicationIssuesPage() {
  const { publicationId } = useParams();
  const { t } = useTranslation();
  return (
    <div className="page">
      <PageHeader title={t('catalog.title')} />
      <CatalogIssues key={publicationId} publicationId={publicationId} />
    </div>
  );
}
export function CatalogIssuePage() {
  const { publicationId, issueId } = useParams();
  return <CatalogIssueReader key={publicationId + issueId} publicationId={publicationId} issueId={issueId} />;
}
function CatalogIssueReader({ publicationId, issueId }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const lastSaved = useRef(-1);
  const pendingSave = useRef(Promise.resolve());
  const load = useCallback(async () => {
    try {
      const [issue, progress] = await Promise.all([
        apiClient.getCatalogIssue(publicationId, issueId),
        apiClient.getCatalogProgress(publicationId, issueId)
      ]);
      setData({ issue, progress });
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, publicationId, issueId]);
  useEffect(() => {
    load();
  }, [load]);
  const progress = (positionSeconds, completed) => {
    if (!completed && Math.abs(positionSeconds - lastSaved.current) < 5) return;
    lastSaved.current = positionSeconds;
    pendingSave.current = pendingSave.current
      .then(() =>
        apiClient.saveCatalogProgress(publicationId, issueId, { positionSeconds, completed, durationSeconds: data?.issue.audioSeconds })
      )
      .catch(setError);
  };
  return (
    <div className="page">
      <PageHeader
        title={data?.issue.title || t('catalog.title')}
        actions={<Link to={'/dashboard/catalog/' + publicationId + '/issues'}>{t('catalog.read')}</Link>}
      />
      <ErrorState error={error} onRetry={load} />
      {!data && !error ? <LoadingState /> : null}
      {data ? (
        <section className="panel stack">
          <DateCell value={data.issue.createdUtc} />
          {data.issue.audioPath ? (
            <AudioPlayer
              catalogPublicationId={publicationId}
              catalogIssueId={issueId}
              title={data.issue.title}
              durationSeconds={data.issue.audioSeconds || 0}
              initialPosition={data.progress.completed ? 0 : data.progress.positionSeconds}
              onPositionChange={progress}
            />
          ) : null}
          <MarkdownView source={data.issue.markdown || ''} />
        </section>
      ) : null}
    </div>
  );
}
CatalogIssueReader.propTypes = { publicationId: PropTypes.string.isRequired, issueId: PropTypes.string.isRequired };
