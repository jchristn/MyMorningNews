import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { BookOpen, FileCode, FileText, Newspaper, Play, Send, Square } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import useInterval from '../hooks/useInterval.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import { ApiError } from '../utils/api.js';
import { ACTIVE_BRIEFING_STATUSES } from '../utils/enums.js';
import { describeError } from '../utils/errors.js';
import DateCell from '../components/DateCell.jsx';
import EmptyState from '../components/EmptyState.jsx';
import ErrorState from '../components/ErrorState.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import AudioPlayer from '../components/AudioPlayer.jsx';
import BriefingAudio, { playableEpisode } from './briefings/BriefingAudio.jsx';
import BriefingMarkdown from './briefings/BriefingMarkdown.jsx';
import useBriefingActions, { BriefingStatusBadge } from './briefings/briefingParts.jsx';

const POLL_MS = 3000;
const NEWEST = { sortBy: 'createdUtc', sortDirection: 'Descending' };

// Ready audio for each briefing in a list (the briefing's own episode, else its newest ready one), keyed by briefing.
function useListAudio(items) {
  const { apiClient } = useAuth();
  const [audio, setAudio] = useState({});
  const ids = (items || []).map((b) => b.id).join(',');
  useEffect(() => {
    if (!items || items.length === 0) return undefined;
    let cancelled = false;
    Promise.all(
      items.map((b) =>
        apiClient
          .listMedia({ briefingId: b.id, status: 'Ready', pageSize: 5, ...NEWEST })
          .then((page) => [b.id, playableEpisode((page && page.items) || [], b)])
          .catch(() => [b.id, null])
      )
    ).then((pairs) => {
      if (!cancelled) setAudio(Object.fromEntries(pairs.filter((p) => p[1])));
    });
    return () => {
      cancelled = true;
    };
    // The ids string stands for the items.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiClient, ids]);
  return audio;
}

// A list of recent briefings. Each one with ready audio has a play button; pressing it opens a compact player under
// the row and starts it. Only one plays at a time across the page (playingId lives in the page).
function RecentList({ title, items, emptyText, moreHref, playingId, onPlay, testId }) {
  const { t } = useTranslation();
  const audio = useListAudio(items);
  return (
    <section className="panel" data-testid={testId}>
      <div className="section-title">
        <h2>{title}</h2>
        <Link to={moreHref} className="small">
          {t('today.seeAll')}
        </Link>
      </div>
      {items === null ? <LoadingState /> : null}
      {items && items.length === 0 ? <p className="muted small">{emptyText}</p> : null}
      {items && items.length > 0 ? (
        <ul className="event-list recent-briefings">
          {items.map((b) => {
            const episode = audio[b.id] || null;
            const playing = playingId === b.id;
            const name = b.title || t('briefings.untitled');
            return (
              <li key={b.id} className={playing ? 'playing' : ''}>
                <div className="recent-briefing-row">
                  {episode ? (
                    <button
                      type="button"
                      className={'icon-button play-toggle' + (playing ? ' active' : '')}
                      aria-pressed={playing}
                      aria-label={playing ? t('today.audio.hide', { title: name }) : t('today.audio.play', { title: name })}
                      title={playing ? t('today.audio.hide', { title: name }) : t('today.audio.play', { title: name })}
                      onClick={() => onPlay(playing ? null : b.id)}
                    >
                      {playing ? <Square size={14} aria-hidden="true" /> : <Play size={14} aria-hidden="true" />}
                    </button>
                  ) : (
                    <span className="play-toggle-placeholder" title={t('today.audio.none')} aria-hidden="true" />
                  )}
                  <Link to={'/dashboard/briefings/' + encodeURIComponent(b.id)} className="truncate">
                    {name}
                  </Link>
                  <BriefingStatusBadge status={b.status} />
                  <DateCell value={b.composedUtc || b.createdUtc} />
                </div>
                {playing && episode ? (
                  <AudioPlayer artifactId={episode.artifactId} durationSeconds={episode.durationSeconds} bytes={episode.bytes} title={name} compact autoPlay />
                ) : null}
              </li>
            );
          })}
        </ul>
      ) : null}
    </section>
  );
}

RecentList.propTypes = {
  title: PropTypes.string.isRequired,
  items: PropTypes.array,
  emptyText: PropTypes.string.isRequired,
  moreHref: PropTypes.string.isRequired,
  playingId: PropTypes.string,
  onPlay: PropTypes.func.isRequired,
  testId: PropTypes.string
};

// Today: the latest ready edition briefing rendered inline, Compose now with live status while it is queued or
// composing, then the most recent run briefings and, beneath them, one-time reports, each full width with a play
// button for briefings that have audio.
export default function TodayView() {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const actions = useBriefingActions();
  const [editions, setEditions] = useState(null);
  const [chosenEdition, setChosenEdition] = useLocalStorage('mymorningnews.today.edition', '');
  const [latest, setLatest] = useState(undefined);
  const [pending, setPending] = useState([]);
  const [runs, setRuns] = useState(null);
  const [playingId, setPlayingId] = useState(null);
  const [oneTime, setOneTime] = useState(null);
  const [error, setError] = useState(null);
  const [composing, setComposing] = useState(false);
  const [speakingSectionId, setSpeakingSectionId] = useState(null);
  const pendingRef = useRef([]);

  const loadLatest = useCallback(async () => {
    const page = await apiClient.listBriefings({ kind: 'Edition', status: 'Ready', pageSize: 1, ...NEWEST });
    const top = page && page.items && page.items[0];
    setLatest(top ? await apiClient.getBriefing(top.id) : null);
  }, [apiClient]);

  // Edition briefings still on the way. When one finishes, the latest briefing reloads.
  const loadPending = useCallback(async () => {
    const page = await apiClient.listBriefings({ kind: 'Edition', pageSize: 10, ...NEWEST });
    const items = (page && page.items) || [];
    const next = items.filter((b) => ACTIVE_BRIEFING_STATUSES.includes(b.status));
    const finished = pendingRef.current.filter((p) => !next.some((n) => n.id === p.id));
    pendingRef.current = next;
    setPending(next);
    for (const done of finished) {
      const now = items.find((b) => b.id === done.id);
      if (now && now.status === 'Failed') addNotification(t('today.composeFailed', { message: now.errorMessage || '' }), 'error', 8000);
      else if (now && now.status === 'Ready') addNotification(t('today.composeReady'), 'success');
    }
    return finished.length > 0;
  }, [apiClient, addNotification, t]);

  const load = useCallback(async () => {
    setError(null);
    try {
      await Promise.all([
        apiClient.listEditions({ pageSize: 100, sortBy: 'name', sortDirection: 'Ascending', mine: true }).then((r) => setEditions((r && r.items) || [])),
        loadLatest(),
        loadPending(),
        apiClient.listBriefings({ kind: 'Run', pageSize: 5, ...NEWEST }).then((r) => setRuns((r && r.items) || [])),
        apiClient.listBriefings({ kind: 'OneTime', pageSize: 5, ...NEWEST }).then((r) => setOneTime((r && r.items) || []))
      ]);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, loadLatest, loadPending]);

  useEffect(() => {
    load();
  }, [load]);

  useInterval(
    async () => {
      try {
        if (await loadPending()) await loadLatest();
      } catch {
        // The next tick retries.
      }
    },
    pending.length > 0 ? POLL_MS : null
  );

  const editionId = useMemo(() => {
    if (!editions || editions.length === 0) return '';
    if (chosenEdition && editions.some((e) => e.id === chosenEdition)) return chosenEdition;
    const fromLatest = latest && latest.briefing && latest.briefing.editionId;
    return fromLatest && editions.some((e) => e.id === fromLatest) ? fromLatest : editions[0].id;
  }, [editions, chosenEdition, latest]);
  const pendingForEdition = pending.find((p) => p.editionId === editionId);

  async function compose() {
    if (!editionId) return;
    setComposing(true);
    try {
      const queued = await apiClient.composeEdition(editionId);
      addNotification(t('today.composeQueued'), 'success');
      if (queued && queued.id) {
        pendingRef.current = [queued, ...pendingRef.current.filter((p) => p.id !== queued.id)];
        setPending(pendingRef.current);
      }
      if (await loadPending()) await loadLatest();
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        addNotification(t('today.composeConflict'), 'warning', 6000);
        await loadPending().catch(() => {});
      } else addNotification(describeError(err, t).message, 'error', 8000);
    } finally {
      setComposing(false);
    }
  }

  const briefing = latest ? latest.briefing : null;

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.today')}
        summary={t('routes.today.summary')}
        actions={
          <div className="row-actions">
            {editions && editions.length > 1 ? (
              <select value={editionId} onChange={(e) => setChosenEdition(e.target.value)} aria-label={t('today.edition')}>
                {editions.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.name}
                  </option>
                ))}
              </select>
            ) : null}
            <button type="button" className="button primary" disabled={!editionId || composing || Boolean(pendingForEdition)} onClick={compose}>
              <Send size={14} aria-hidden="true" /> {composing ? t('common.working') : t('today.composeNow')}
            </button>
            <button type="button" className="button secondary" onClick={load}>
              {t('common.refresh')}
            </button>
          </div>
        }
      />

      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />

      {pending.map((p) => (
        <div key={p.id} className="notice notice-info compose-progress" role="status" data-testid="compose-progress">
          <BriefingStatusBadge status={p.status} />
          <span>
            <strong>{p.title}</strong>{' '}
            {p.status === 'Queued' && p.researchDeadlineUtc ? t('briefings.progress.researching', { time: f.time(p.researchDeadlineUtc) }) : t('briefings.progress.' + p.status)}
          </span>
          <Link to={'/dashboard/briefings/' + encodeURIComponent(p.id)} className="small">
            {t('today.follow')}
          </Link>
        </div>
      ))}

      {editions && editions.length === 0 ? (
        <div className="notice notice-warning">
          {t('today.noEditions')} <Link to="/dashboard/editions/new">{t('today.createEdition')}</Link>
        </div>
      ) : null}

      {latest === undefined ? <LoadingState /> : null}
      {latest === null ? (
        <section className="panel">
          <EmptyState
            icon={Newspaper}
            title={t('today.empty.title')}
            message={t('today.empty.message')}
            action={
              editionId ? (
                <button type="button" className="button primary small" disabled={composing || Boolean(pendingForEdition)} onClick={compose}>
                  <Send size={14} aria-hidden="true" /> {t('today.composeNow')}
                </button>
              ) : null
            }
          />
        </section>
      ) : null}
      {briefing ? (
        <section className="panel today-briefing" data-testid="today-briefing">
          <div className="section-title">
            <div className="date-cell">
              <h2>{briefing.title}</h2>
              <span className="muted small">
                {[latest.editionName, briefing.composedUtc ? f.dateTime(briefing.composedUtc) : null, t('briefings.reader.readMinutes', { minutes: briefing.readMinutes || 0 })].filter(Boolean).join(' · ')}
              </span>
            </div>
            <div className="row-actions">
              <Link to={'/dashboard/briefings/' + encodeURIComponent(briefing.id)} className="button secondary small">
                <BookOpen size={14} aria-hidden="true" /> {t('today.openReader')}
              </Link>
              <button type="button" className="button secondary small" disabled={latest.filesExpired} onClick={() => actions.download(briefing, 'md')}>
                <FileText size={14} aria-hidden="true" /> {t('briefings.download.md')}
              </button>
              <button type="button" className="button secondary small" disabled={latest.filesExpired} onClick={() => actions.download(briefing, 'html')}>
                <FileCode size={14} aria-hidden="true" /> {t('briefings.download.html')}
              </button>
            </div>
          </div>
          <BriefingAudio key={briefing.id} briefing={briefing} compact onSectionChange={setSpeakingSectionId} />
          <BriefingMarkdown markdown={briefing.markdown} sections={latest.sections || []} currentSectionId={speakingSectionId} />
        </section>
      ) : null}

      <RecentList title={t('today.runs')} items={runs} emptyText={t('today.noRuns')} moreHref="/dashboard/briefings?kind=Run" playingId={playingId} onPlay={setPlayingId} testId="today-runs" />
      <RecentList title={t('today.oneTime')} items={oneTime} emptyText={t('today.noOneTime')} moreHref="/dashboard/briefings?kind=OneTime" playingId={playingId} onPlay={setPlayingId} testId="today-one-time" />
    </div>
  );
}
