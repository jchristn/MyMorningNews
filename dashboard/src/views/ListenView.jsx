import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { BookOpen, Braces, Play, RefreshCw, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useInterval from '../hooks/useInterval.js';
import { pagedQuery } from '../utils/api.js';
import { ACTIVE_MEDIA_STATUSES, MEDIA_KINDS, MEDIA_STATUSES } from '../utils/enums.js';
import AudioPlayer from '../components/AudioPlayer.jsx';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyableId from '../components/CopyableId.jsx';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import ErrorState from '../components/ErrorState.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import LoadingState from '../components/LoadingState.jsx';
import Modal from '../components/Modal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';
import { MediaStatusBadge, useMakeAudio } from './media/mediaParts.jsx';

const POLL_MS = 4000;

// Plays one episode in a modal: loads its chapters, then the audio.
function PlayModal({ episode, onClose }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const episodeId = episode ? episode.id : null;

  useEffect(() => {
    if (!episodeId) return undefined;
    let cancelled = false;
    setDetail(null);
    setError(null);
    apiClient
      .getMedia(episodeId)
      .then((d) => !cancelled && setDetail(d))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, episodeId]);

  const media = detail ? detail.media : null;
  return (
    <Modal open={Boolean(episode)} title={episode ? episode.title || t('listen.untitled') : ''} onClose={onClose} size="large">
      <ErrorState error={error} inline />
      {!detail && !error ? <LoadingState /> : null}
      {media && !media.artifactId ? <p className="muted">{t('audio.missingFile')}</p> : null}
      {media && media.artifactId ? (
        <AudioPlayer artifactId={media.artifactId} chapters={detail.chapters || []} durationSeconds={Number(media.durationSeconds) || 0} bytes={Number(media.bytes) || 0} title={media.title || ''} />
      ) : null}
    </Modal>
  );
}

PlayModal.propTypes = { episode: PropTypes.object, onClose: PropTypes.func.isRequired };

// Listen: every audio episode, with status, duration, and size. Play opens the player with chapters; Remake queues
// the briefing's audio again with the same voice; Delete removes the episode and its files. Polls while any episode
// on the page is still being made.
export default function ListenView() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const makeAudio = useMakeAudio();
  const [refresh, setRefresh] = useState(0);
  const [hasActive, setHasActive] = useState(false);
  const [playing, setPlaying] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [json, setJson] = useState(null);
  const bump = useCallback(() => setRefresh((n) => n + 1), []);

  const fetchPage = useCallback((q) => apiClient.listMedia(pagedQuery(q)), [apiClient]);
  const onLoaded = useCallback((data) => setHasActive(Boolean(data && Array.isArray(data.items) && data.items.some((m) => ACTIVE_MEDIA_STATUSES.includes(m.status)))), []);
  useInterval(bump, hasActive ? POLL_MS : null);

  async function remake(m) {
    const queued = await makeAudio(m.briefingId, m.voiceProfileId || null);
    if (queued) bump();
  }

  const columns = [
    {
      key: 'title',
      header: t('listen.columns.title'),
      pinned: true,
      render: (m) => (
        <span className="date-cell">
          <strong className="truncate">{m.title || t('listen.untitled')}</strong>
          {m.status === 'Failed' && m.errorMessage ? <span className="small text-danger truncate">{m.errorMessage}</span> : null}
        </span>
      )
    },
    {
      key: 'status',
      header: t('listen.columns.status'),
      filter: { type: 'select', param: 'status', options: MEDIA_STATUSES.map((s) => ({ value: s, label: t('enums.mediaStatus.' + s) })) },
      render: (m) => <MediaStatusBadge status={m.status} />
    },
    {
      key: 'kind',
      header: t('listen.columns.kind'),
      filter: { type: 'select', param: 'kind', options: MEDIA_KINDS.map((k) => ({ value: k, label: t('enums.mediaKind.' + k) })) },
      render: (m) => <StatusBadge tone={m.kind === 'PodcastEpisode' ? 'accent' : 'info'}>{t('enums.mediaKind.' + m.kind)}</StatusBadge>
    },
    { key: 'durationSeconds', header: t('listen.columns.duration'), render: (m) => (m.durationSeconds > 0 ? f.seconds(m.durationSeconds) : <span className="muted">{t('listen.noValue')}</span>) },
    { key: 'bytes', header: t('listen.columns.size'), render: (m) => (m.bytes > 0 ? f.bytes(m.bytes) : <span className="muted">{t('listen.noValue')}</span>) },
    { key: 'segmentCount', header: t('listen.columns.segments'), defaultHidden: true, render: (m) => f.number(m.segmentCount || 0) },
    { key: 'script', header: t('listen.columns.script'), defaultHidden: true, render: (m) => (m.scriptFromModel ? t('listen.scriptModel') : t('listen.scriptText')) },
    { key: 'createdUtc', header: t('listen.columns.created'), sortKey: 'createdUtc', render: (m) => <DateCell value={m.createdUtc} /> },
    { key: 'completedUtc', header: t('listen.columns.completed'), sortKey: 'completedUtc', defaultHidden: true, render: (m) => <DateCell value={m.completedUtc} /> },
    { key: 'id', header: t('listen.columns.id'), defaultHidden: true, className: 'cell-id', render: (m) => <CopyableId value={m.id} /> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.listen')} summary={t('routes.listen.summary')} />
      <section className="panel">
        <DataTable
          tableKey="listen"
          columns={columns}
          fetchPage={fetchPage}
          refreshSignal={refresh}
          onLoaded={onLoaded}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          onRowClick={(m) => m.status === 'Ready' && setPlaying(m)}
          rowActions={(m) => [
            { key: 'play', label: t('listen.actions.play'), icon: Play, disabled: m.status !== 'Ready', onClick: () => setPlaying(m) },
            { key: 'briefing', label: t('listen.actions.briefing'), icon: BookOpen, onClick: () => navigate('/dashboard/briefings/' + encodeURIComponent(m.briefingId)) },
            { key: 'remake', label: t('listen.actions.remake'), icon: RefreshCw, disabled: ACTIVE_MEDIA_STATUSES.includes(m.status), onClick: () => remake(m) },
            { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(m) },
            { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, onClick: () => setDeleting(m) }
          ]}
          emptyTitle={t('listen.empty.title')}
          emptyMessage={t('listen.empty.message')}
        />
      </section>

      <PlayModal episode={playing} onClose={() => setPlaying(null)} />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('listen.delete.title')}
        message={deleting ? t('listen.delete.message', { title: deleting.title || t('listen.untitled') }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteMedia(deleting.id);
          addNotification(t('listen.delete.done'), 'success');
          bump();
        }}
      />
    </div>
  );
}
