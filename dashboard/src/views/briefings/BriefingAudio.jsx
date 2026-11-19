import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Headphones, Mic } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useInterval from '../../hooks/useInterval.js';
import { ACTIVE_MEDIA_STATUSES } from '../../utils/enums.js';
import AudioPlayer from '../../components/AudioPlayer.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { MediaStatusBadge, VoicePicker, useMakeAudio, useVoices } from '../media/mediaParts.jsx';

export const AUDIO_POLL_MS = 3000;
const NEWEST = { pageSize: 5, sortBy: 'createdUtc', sortDirection: 'Descending' };
const NO_CHAPTERS = [];

// The episode to play: the briefing's own (mediaAssetId) when it is ready, else the newest ready one.
export function playableEpisode(episodes, briefing) {
  if (!Array.isArray(episodes)) return null;
  const ready = episodes.filter((e) => e.status === 'Ready');
  return ready.find((e) => briefing && e.id === briefing.mediaAssetId) || ready[0] || null;
}

function MakeAudio({ briefingId, compact, label, onQueued }) {
  const { t } = useTranslation();
  const voices = useVoices();
  const makeAudio = useMakeAudio();
  const [voiceId, setVoiceId] = useState('');
  const [busy, setBusy] = useState(false);

  async function make() {
    setBusy(true);
    try {
      const queued = await makeAudio(briefingId, voiceId);
      await onQueued(queued);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className={'audio-make' + (compact ? ' compact' : '')}>
      <VoicePicker id={'audio-voice-' + briefingId} value={voiceId} onChange={setVoiceId} voices={voices} disabled={busy} />
      <button type="button" className={'button primary' + (compact ? ' small' : '')} onClick={make} disabled={busy}>
        <Mic size={14} aria-hidden="true" /> {busy ? t('common.working') : label || t('audio.make.action')}
      </button>
    </div>
  );
}

MakeAudio.propTypes = { briefingId: PropTypes.string.isRequired, compact: PropTypes.bool, label: PropTypes.string, onQueued: PropTypes.func.isRequired };

// Audio for a briefing: the player when an episode is ready, live status while one is queued, scripting, or
// synthesizing (polled), the error with Make audio when the newest failed, and Make audio with a voice picker when
// there is none. onSectionChange and apiRef pass through to the player; onChaptersChange receives the chapters of
// the episode shown. compact is the Today variant (no chapter list).
export default function BriefingAudio({ briefing, compact = false, onSectionChange = null, onChaptersChange = null, apiRef = null }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [episodes, setEpisodes] = useState(null);
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const [remaking, setRemaking] = useState(false);
  const briefingId = briefing.id;
  const briefingReady = briefing.status === 'Ready';

  const loadEpisodes = useCallback(async () => {
    try {
      const page = await apiClient.listMedia({ briefingId, ...NEWEST });
      setEpisodes((page && page.items) || []);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, briefingId]);

  useEffect(() => {
    if (briefingReady) loadEpisodes();
  }, [briefingReady, loadEpisodes]);

  const latest = episodes && episodes.length > 0 ? episodes[0] : null;
  const active = Boolean(latest && ACTIVE_MEDIA_STATUSES.includes(latest.status));
  const playable = useMemo(() => playableEpisode(episodes, briefing), [episodes, briefing]);
  const playableId = playable ? playable.id : null;
  useInterval(loadEpisodes, active ? AUDIO_POLL_MS : null);

  useEffect(() => {
    if (!playableId) return undefined;
    let cancelled = false;
    apiClient
      .getMedia(playableId)
      .then((d) => !cancelled && setDetail(d))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [apiClient, playableId]);

  const shown = detail && playable && detail.media && detail.media.id === playable.id ? detail : null;
  const chapters = shown && Array.isArray(shown.chapters) ? shown.chapters : NO_CHAPTERS;
  useEffect(() => {
    if (onChaptersChange) onChaptersChange(chapters);
  }, [chapters, onChaptersChange]);

  if (!briefingReady) return null;

  async function onQueued(queued) {
    setRemaking(false);
    if (queued && queued.id) setEpisodes((list) => [queued, ...(list || []).filter((e) => e.id !== queued.id)]);
    await loadEpisodes();
  }

  const failed = Boolean(latest && latest.status === 'Failed');
  const media = shown ? shown.media : null;
  const missingFile = Boolean(shown && !media.artifactId);
  const showMake = episodes !== null && !active && (!playable || failed || missingFile || remaking);

  return (
    <section className={'panel audio-panel' + (compact ? ' compact' : '')} data-slot="audio-player" aria-label={t('audio.panel.label')}>
      <div className="audio-panel-head">
        <span className="audio-panel-title">
          <Headphones size={16} aria-hidden="true" />
          <strong>{t('audio.panel.title')}</strong>
          {media && media.durationSeconds ? <span className="muted small">{f.seconds(media.durationSeconds)}</span> : null}
          {media && !media.scriptFromModel ? <span className="muted small">{t('audio.panel.fromText')}</span> : null}
        </span>
        {playable && !active && !failed && !missingFile ? (
          <button type="button" className="link-button small" onClick={() => setRemaking((v) => !v)} aria-expanded={remaking}>
            {remaking ? t('common.cancel') : t('audio.make.remake')}
          </button>
        ) : null}
      </div>

      <ErrorState error={error} inline onRetry={loadEpisodes} onDismiss={() => setError(null)} />
      {episodes === null && !error ? <LoadingState compact label={t('audio.panel.checking')} /> : null}

      {active ? (
        <div className="notice notice-info audio-status" role="status" data-testid="audio-status">
          <MediaStatusBadge status={latest.status} />
          <span>{t('audio.progress.' + latest.status)}</span>
        </div>
      ) : null}
      {failed ? (
        <div className="notice notice-danger" role="alert" data-testid="audio-failed">
          <strong>{t('audio.failed')}</strong> {latest.errorMessage || ''}
        </div>
      ) : null}
      {missingFile ? <div className="notice notice-warning">{t('audio.missingFile')}</div> : null}

      {playable && !shown && !error ? <LoadingState compact /> : null}
      {media && media.artifactId ? (
        <AudioPlayer
          key={media.artifactId}
          artifactId={media.artifactId}
          chapters={chapters}
          durationSeconds={Number(media.durationSeconds) || 0}
          bytes={Number(media.bytes) || 0}
          title={media.title || briefing.title || ''}
          compact={compact}
          onSectionChange={onSectionChange}
          apiRef={apiRef}
        />
      ) : null}

      {episodes !== null && episodes.length === 0 ? <p className="muted small">{t('audio.none')}</p> : null}
      {showMake ? <MakeAudio briefingId={briefingId} compact={compact} label={playable || failed ? t('audio.make.again') : null} onQueued={onQueued} /> : null}
    </section>
  );
}

BriefingAudio.propTypes = {
  briefing: PropTypes.shape({ id: PropTypes.string.isRequired, status: PropTypes.string, title: PropTypes.string, mediaAssetId: PropTypes.string }).isRequired,
  compact: PropTypes.bool,
  onSectionChange: PropTypes.func,
  onChaptersChange: PropTypes.func,
  apiRef: PropTypes.oneOfType([PropTypes.func, PropTypes.shape({ current: PropTypes.any })])
};
